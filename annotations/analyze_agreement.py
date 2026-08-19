#!/usr/bin/env python3
"""Compare two completed blind annotation CSV files.

This tool intentionally does not infer an oracle. It reports inter-rater
agreement only; adjudication and model/baseline comparisons remain separate
steps.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import sys
from collections import Counter
from pathlib import Path

REQUIRED_COLUMNS = (
    "caseId",
    "traceLabel",
    "obligationLabels",
    "maxTurnsHalt",
    "maxTurnsRank",
    "exactRepeatHalt",
    "exactRepeatRank",
    "fuzzyRepeatCycleHalt",
    "fuzzyRepeatCycleRank",
    "confidence",
    "ambiguityNote",
)
LABEL_COLUMNS = {
    "traceLabel": "trace label",
    "maxTurnsHalt": "max-turns halt",
    "exactRepeatHalt": "exact-repeat halt",
    "fuzzyRepeatCycleHalt": "fuzzy-repeat/cycle halt",
}
VALID_TRACE_LABELS = {
    "progress",
    "stagnation",
    "regression",
    "insufficient-evidence",
}
VALID_CONFIDENCE = {"high", "medium", "low"}
VALID_BOOLEAN = {"true", "false"}
RANK_COLUMNS = {
    "maxTurnsRank",
    "exactRepeatRank",
    "fuzzyRepeatCycleRank",
}


def load_rows(path: Path) -> dict[str, dict[str, str]]:
    with path.open(newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        columns = tuple(reader.fieldnames or ())
        missing = [column for column in REQUIRED_COLUMNS if column not in columns]
        if missing:
            raise ValueError(f"{path}: missing columns: {', '.join(missing)}")
        rows: dict[str, dict[str, str]] = {}
        for number, raw in enumerate(reader, start=2):
            case_id = (raw.get("caseId") or "").strip()
            if not case_id:
                raise ValueError(f"{path}:{number}: empty caseId")
            if case_id in rows:
                raise ValueError(f"{path}:{number}: duplicate caseId {case_id}")
            row = {column: (raw.get(column) or "").strip() for column in REQUIRED_COLUMNS}
            validate_row(path, number, row)
            rows[case_id] = row
        if not rows:
            raise ValueError(f"{path}: no annotation rows")
        return rows


def validate_row(path: Path, number: int, row: dict[str, str]) -> None:
    for column in LABEL_COLUMNS:
        value = row[column]
        if not value:
            raise ValueError(f"{path}:{number}: {column} is empty")
        if column == "traceLabel" and value not in VALID_TRACE_LABELS:
            raise ValueError(f"{path}:{number}: invalid traceLabel {value!r}")
        if column != "traceLabel" and value not in VALID_BOOLEAN:
            raise ValueError(f"{path}:{number}: {column} must be true or false")
    for column in RANK_COLUMNS:
        value = row[column]
        if value and (not value.isdigit() or int(value) < 0):
            raise ValueError(f"{path}:{number}: {column} must be a non-negative integer or empty")
    confidence = row["confidence"]
    if confidence not in VALID_CONFIDENCE:
        raise ValueError(f"{path}:{number}: confidence must be high, medium, or low")
    if not row["ambiguityNote"] and confidence == "low":
        raise ValueError(f"{path}:{number}: low confidence requires ambiguityNote")


def cohen_kappa(left: list[str], right: list[str]) -> float | None:
    if not left or len(left) != len(right):
        raise ValueError("kappa requires equally sized non-empty label lists")
    observed = sum(a == b for a, b in zip(left, right)) / len(left)
    left_counts = Counter(left)
    right_counts = Counter(right)
    categories = set(left_counts) | set(right_counts)
    expected = sum(
        (left_counts[category] / len(left)) * (right_counts[category] / len(left))
        for category in categories
    )
    if math.isclose(1.0 - expected, 0.0):
        return None
    return round((observed - expected) / (1.0 - expected), 6)


def compare(left: dict[str, dict[str, str]], right: dict[str, dict[str, str]]) -> dict:
    if set(left) != set(right):
        missing_left = sorted(set(right) - set(left))
        missing_right = sorted(set(left) - set(right))
        raise ValueError(
            f"case sets differ; missing from evaluator 1: {missing_left}; "
            f"missing from evaluator 2: {missing_right}"
        )
    case_ids = sorted(left)
    fields = tuple(LABEL_COLUMNS)
    agreement: dict[str, dict] = {}
    for field in fields:
        left_values = [left[case_id][field] for case_id in case_ids]
        right_values = [right[case_id][field] for case_id in case_ids]
        matches = sum(a == b for a, b in zip(left_values, right_values))
        agreement[field] = {
            "matches": matches,
            "total": len(case_ids),
            "agreement": round(matches / len(case_ids), 6),
            "cohenKappa": cohen_kappa(left_values, right_values),
        }
    disagreements = [
        {
            "caseId": case_id,
            "fields": [field for field in fields if left[case_id][field] != right[case_id][field]],
        }
        for case_id in case_ids
    ]
    disagreements = [item for item in disagreements if item["fields"]]
    low_confidence = sum(
        left[case_id]["confidence"] == "low" or right[case_id]["confidence"] == "low"
        for case_id in case_ids
    )
    return {
        "format": "progresstrace-annotation-agreement-1",
        "caseCount": len(case_ids),
        "oracleStatus": "not-adjudicated",
        "agreement": agreement,
        "lowConfidenceCaseCount": low_confidence,
        "disagreementCount": len(disagreements),
        "disagreements": disagreements,
        "notes": [
            "This report measures inter-rater agreement only.",
            "It does not establish an adjudicated oracle or model accuracy.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--evaluator-1", type=Path, required=True)
    parser.add_argument("--evaluator-2", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = compare(load_rows(args.evaluator_1), load_rows(args.evaluator_2))
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    except (OSError, ValueError) as error:
        print(f"annotation analysis failed: {error}", file=sys.stderr)
        return 1
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
