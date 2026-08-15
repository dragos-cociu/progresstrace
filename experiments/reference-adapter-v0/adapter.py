#!/usr/bin/env python3
"""Internal v0 redacted projection adapter; not a general ingestion API."""
import json
import re
from pathlib import Path

ADAPTER = "reference-adapter-v0"
SHA256 = re.compile(r"^[0-9a-f]{64}$")
SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9-]*$")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def load_manifest(path):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    _require(data.get("manifestVersion") == "0", "manifestVersion must be 0")
    _require(data.get("classification") == "redacted-projection-of-real-run", "missing redaction classification")
    _require(SAFE_ID.fullmatch(data.get("caseId", "")), "unsafe caseId")
    observed = data.get("observedMetadata", {})
    for field in ("sourceDigest", "authorityDigest"):
        _require(SHA256.fullmatch(observed.get(field, "")), f"invalid {field}")
    _require(not any(Path(data.get(k, "")).is_absolute() for k in ("caseId",)), "absolute path forbidden")
    _require(data.get("projection", {}).get("annotationKind") == "operator-authored-projection", "projection provenance required")
    return data


def adapt(data):
    case_id = data["caseId"]
    trace_id = f"redacted-{case_id}"
    projection = data["projection"]
    obligations = projection["obligations"]
    events = projection["events"]
    _require(events and events[-1].get("terminal") is True, "last event must be terminal")
    obligation_ids = {o["id"] for o in obligations}
    _require(len(obligation_ids) == len(obligations), "duplicate obligation id")
    _require(all(SAFE_ID.fullmatch(x) for x in obligation_ids), "unsafe obligation id")

    trace_events, signals = [], []
    for index, event in enumerate(events):
        event_id = f"event-{index + 1:03d}"
        trace_events.append({
            "id": event_id, "sequence": index,
            "timestamp": f"2026-01-{index + 1:02d}T00:00:00Z",
            "type": "redacted-projection-terminal" if event.get("terminal") else "redacted-projection",
            "actor": "system",
            "payload": {
                "annotationKind": "operator-authored-projection",
                "label": event["label"],
                **({"observedMetadata": data["observedMetadata"]} if index == 0 else {})
            },
            "provenance": {"sourceEventId": f"synthetic-{case_id}-{index + 1:03d}"}
        })
        for signal in event.get("signals", []):
            _require(signal["obligationId"] in obligation_ids, "unknown obligation")
            signals.append({"obligationId": signal["obligationId"], "eventId": event_id, "status": signal["status"]})

    source = {"producerType": "adapter", "producerName": ADAPTER, "producerVersion": "0", "evidenceBasis": "adapter-inference"}
    declaration_source = source
    if projection["terminationKind"] == "harness-declared-stop":
        # Canonical coherence requires this pairing; the manifest explicitly projects a harness stop.
        declaration_source = {"producerType": "harness", "producerName": "redacted-projected-harness",
                              "producerVersion": "0", "evidenceBasis": "harness-lifecycle"}
    return {
        "trace.json": {"schemaVersion": "1.0", "traceId": trace_id,
                       "source": {"name": "experimental-redacted-projection-adapter", "version": "0"},
                       "createdAt": "2026-01-01T00:00:00Z", "events": trace_events},
        "ledger.json": {"schemaVersion": "1.0", "traceId": trace_id,
                        "obligations": [{"id": o["id"], "description": o["description"]} for o in obligations],
                        "signals": signals},
        "declaration.json": {"schemaVersion": "1.0", "traceId": trace_id,
                             "terminationEventId": trace_events[-1]["id"],
                             "terminationKind": projection["terminationKind"], "declarationSource": declaration_source},
        "baseline.json": {"schemaVersion": "1.0", "traceId": trace_id,
                          "baselineSource": source,
                          "obligationBudgets": [{"obligationId": o["id"], "eventBudget": o["eventBudget"]} for o in obligations]}
    }


def write_outputs(manifest_path, output_dir):
    result = adapt(load_manifest(manifest_path))
    target = Path(output_dir)
    target.mkdir(parents=True, exist_ok=True)
    for name, value in result.items():
        (target / name).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return result
