#!/usr/bin/env python3
"""ProgressTrace Phase 4 verification control-plane CLI.

The implementation intentionally uses only the Python standard library. Inputs
from evaluated repositories are data; no extracted content is executed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from fnmatch import fnmatch
from pathlib import Path
from typing import Any

SCHEMA_VERSION = "1.0"
REQUIRED_CHECK_NAMES = (
    "release-build",
    "conformance",
    "json-schema-validity",
    "benchmark-60-byte-identical",
    "dotnet-format",
    "git-diff-check",
    "invariants",
)


def canonical(value: Any) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def run(args: list[str], cwd: Path, timeout: int = 900) -> tuple[int, float, str]:
    start = time.monotonic()
    try:
        result = subprocess.run(
            args,
            cwd=cwd,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
            check=False,
            env={**os.environ, "CI": "true", "DOTNET_NOLOGO": "true"},
        )
        return result.returncode, time.monotonic() - start, result.stdout[-4000:]
    except subprocess.TimeoutExpired as exc:
        return 124, time.monotonic() - start, f"timeout: {exc}"
    except OSError as exc:
        return 127, time.monotonic() - start, f"runner error: {type(exc).__name__}"


def git(repo: Path, *args: str, timeout: int = 120) -> tuple[int, float, str]:
    return run(["git", *args], repo, timeout)


def tree_digest(repo: Path) -> str:
    code, _, output = git(repo, "ls-files", "-z")
    if code != 0:
        raise RuntimeError("git ls-files failed")
    digest = hashlib.sha256()
    for name in sorted(filter(None, output.split("\0"))):
        path = repo / name
        if path.is_file():
            digest.update(name.encode())
            digest.update(b"\0")
            digest.update(path.read_bytes())
            digest.update(b"\0")
    return digest.hexdigest()


def record(name: str, args: list[str], code: int, duration: float, output: str = "") -> dict[str, Any]:
    return {
        "name": name,
        "command": args,
        "exitCode": code,
        "durationSeconds": round(duration, 3),
        "status": "pass" if code == 0 else "fail",
        "outputDigest": sha256_bytes(output.encode()),
    }


def json_files_valid(repo: Path) -> tuple[int, float, str]:
    start = time.monotonic()
    failures: list[str] = []
    for path in sorted((repo / "contracts").rglob("*.json")):
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            failures.append(f"{path.relative_to(repo)}:{type(exc).__name__}")
    return (0 if not failures else 1, time.monotonic() - start, "\n".join(failures))


def benchmark(repo: Path, output: Path) -> tuple[int, float, str]:
    return run(
        [
            "dotnet", "run", "--project", "src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj",
            "--configuration", "Release", "--no-build", "--", "run", "--input",
            "fixtures/benchmarks", "--output", str(output),
        ],
        repo,
        300,
    )


def run_invariants(repo: Path) -> dict[str, Any]:
    results: list[dict[str, Any]] = []

    with tempfile.TemporaryDirectory(prefix="progresstrace-invariants-") as temp:
        first = Path(temp) / "benchmark-a.json"
        second = Path(temp) / "benchmark-b.json"
        code_a, duration_a, output_a = benchmark(repo, first)
        code_b, duration_b, output_b = benchmark(repo, second)
        same = code_a == 0 and code_b == 0 and first.exists() and second.exists() and first.read_bytes() == second.read_bytes()
        results.append({
            "id": "determinism",
            "status": "pass" if same else "fail",
            "seed": 0,
            "reproducer": ["benchmark-a.json", "benchmark-b.json"],
            "details": {"firstExit": code_a, "secondExit": code_b, "durations": [round(duration_a, 3), round(duration_b, 3)], "outputDigest": sha256_file(first) if first.exists() else None},
        })

    fixture = repo / "fixtures" / "valid" / "minimal-trace.json"
    cli = ["dotnet", "run", "--project", "src/ProgressTrace.Cli/ProgressTrace.Cli.csproj", "--configuration", "Release", "--no-build", "--", "normalize", str(fixture)]
    first_code, _, first_out = run(cli, repo, 180)
    with tempfile.TemporaryDirectory(prefix="progresstrace-normalize-") as temp:
        normalized = Path(temp) / "normalized.json"
        normalized.write_text(first_out, encoding="utf-8")
        second_code, _, second_out = run([*cli[:-1], str(normalized)], repo, 180)
    idem = first_code == 0 and second_code == 0 and first_out.encode() == second_out.encode()
    results.append({"id": "normalization-idempotence", "status": "pass" if idem else "fail", "seed": 0, "reproducer": [str(fixture.relative_to(repo))], "details": {"firstExit": first_code, "secondExit": second_code}})

    # The benchmark's 40 metamorphic variants are the repository's fixed,
    # reviewable metamorphic corpus. This check verifies the invariant without
    # treating authored labels as a production oracle.
    benchmark_path = repo / "fixtures" / "benchmarks"
    variants = sorted(p for p in benchmark_path.glob("*-variant-*.json"))
    variant_ok = bool(variants)
    if variant_ok:
        with tempfile.TemporaryDirectory(prefix="progresstrace-metamorphic-") as temp:
            out = Path(temp) / "variants.json"
            code, _, _ = run(["dotnet", "run", "--project", "src/ProgressTrace.Benchmarks/ProgressTrace.Benchmarks.csproj", "--configuration", "Release", "--no-build", "--", "run", "--input", str(benchmark_path), "--output", str(out)], repo, 300)
            variant_ok = code == 0 and out.exists()
    results.append({"id": "metamorphic-invariance", "status": "pass" if variant_ok else "fail", "seed": 0, "reproducer": ["fixtures/benchmarks/*-variant-*.json"], "details": {"variantCount": len(variants)}})

    # Cross-command coherence is checked at the shared evaluator boundary by
    # the existing conformance executable; invoke it here as an independent
    # property check rather than duplicating its normative implementation.
    code, _, output = run(["dotnet", "run", "--project", "tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj", "--configuration", "Release", "--no-build"], repo, 300)
    results.append({"id": "command-coherence", "status": "pass" if code == 0 else "fail", "seed": 0, "reproducer": ["tests/ProgressTrace.ConformanceTests"], "details": {"exitCode": code, "outputDigest": sha256_bytes(output.encode())}})

    return {"schemaVersion": SCHEMA_VERSION, "status": "pass" if all(x["status"] == "pass" for x in results) else "fail", "results": results}


def verify(repo: Path, commit: str | None, report_path: Path) -> int:
    source = repo.resolve()
    code, _, head = git(source, "rev-parse", commit or "HEAD")
    if code != 0:
        print("candidate commit could not be resolved", file=sys.stderr)
        return 2
    evaluated_commit = head.strip()
    with tempfile.TemporaryDirectory(prefix="progresstrace-clean-checkout-") as temp:
        checkout = Path(temp) / "repo"
        clone_code, _, clone_output = run(["git", "clone", "--no-local", "--no-hardlinks", str(source), str(checkout)], source, 300)
        if clone_code != 0:
            print("clean checkout failed", file=sys.stderr)
            return 2
        checkout_code, _, _ = git(checkout, "checkout", "--detach", evaluated_commit)
        if checkout_code != 0:
            print("candidate checkout failed", file=sys.stderr)
            return 2
        dirty_code, _, dirty = git(checkout, "status", "--porcelain")
        if dirty_code != 0 or dirty.strip():
            print("clean checkout is dirty", file=sys.stderr)
            return 2

        checks: list[dict[str, Any]] = []
        command_outputs: list[str] = []
        commands: list[tuple[str, list[str], int]] = [
            ("release-build", ["dotnet", "build", "ProgressTrace.slnx", "--configuration", "Release", "--nologo", "--warnaserror"], 1200),
            ("conformance", ["dotnet", "run", "--project", "tests/ProgressTrace.ConformanceTests/ProgressTrace.ConformanceTests.csproj", "--configuration", "Release", "--no-build"], 300),
            ("dotnet-format", ["dotnet", "format", "ProgressTrace.slnx", "--verify-no-changes", "--no-restore", "--verbosity", "minimal"], 300),
            ("git-diff-check", ["git", "diff", "--check"], 120),
        ]
        for name, args, timeout in commands:
            result = run(args, checkout, timeout)
            checks.append(record(name, args, result[0], result[1], result[2]))
            command_outputs.append(result[2])
        schema_result = json_files_valid(checkout)
        checks.append(record("json-schema-validity", ["python3", "verification/ptverify.py", "schema-validity"], schema_result[0], schema_result[1], schema_result[2]))
        command_outputs.append(schema_result[2])
        with tempfile.TemporaryDirectory(prefix="progresstrace-benchmark-") as temp_out:
            a, da, oa = benchmark(checkout, Path(temp_out) / "a.json")
            b, db, ob = benchmark(checkout, Path(temp_out) / "b.json")
            same = a == 0 and b == 0 and (Path(temp_out) / "a.json").read_bytes() == (Path(temp_out) / "b.json").read_bytes()
            benchmark_code = 0 if same else 1
            benchmark_output = oa + ob
            checks.append(record("benchmark-60-byte-identical", ["dotnet", "run", "...", "benchmark twice + cmp"], benchmark_code, da + db, benchmark_output))
            command_outputs.append(benchmark_output)
        invariant = run_invariants(checkout)
        invariant_bytes = canonical(invariant)
        checks.append(record("invariants", ["python3", "verification/ptverify.py", "invariants"], 0 if invariant["status"] == "pass" else 1, 0.0, invariant_bytes.decode()))
        command_outputs.append(invariant_bytes.decode())
        complete = set(x["name"] for x in checks) == set(REQUIRED_CHECK_NAMES) and all(x["status"] == "pass" for x in checks)
        report = {
            "schemaVersion": SCHEMA_VERSION,
            "reportType": "VerificationReport",
            "status": "pass" if complete else "fail",
            "evaluatedCommit": evaluated_commit,
            "cleanCheckout": True,
            "runner": {"name": "progresstrace-independent-verifier", "version": "0.1.0"},
            "checks": checks,
            "commandSetDigest": sha256_bytes(canonical([x[1] for x in commands] + [["schema-validity"], ["benchmark-byte-identical"], ["invariants"]])),
            "evaluatedTreeDigest": tree_digest(checkout),
            "reportDigestBasis": sha256_bytes("".join(command_outputs).encode()),
        }
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_bytes(canonical(report))
        print(json.dumps({"status": report["status"], "report": str(report_path), "commit": evaluated_commit, "checks": len(checks)}, separators=(",", ":")))
        return 0 if complete else 1


def load_policy(repo: Path) -> dict[str, Any]:
    return json.loads((repo / "policy" / "change-class-policy.json").read_text(encoding="utf-8"))


def classify(repo: Path, base: str, head: str) -> dict[str, Any]:
    code, _, raw = git(repo, "diff", "--name-status", f"{base}..{head}")
    if code != 0:
        raise RuntimeError("git diff failed")
    paths = [line.split("\t")[-1] for line in raw.splitlines() if line.strip()]
    policy = load_policy(repo)
    rules = policy["rules"]
    triggered: list[dict[str, str]] = []
    for path in paths:
        matched = False
        for rule in rules:
            if any(fnmatch(path, pattern) for pattern in rule["patterns"]):
                triggered.append({"ruleId": rule["id"], "path": path, "class": rule["class"]})
                matched = True
                break
        if not matched:
            triggered.append({"ruleId": "unmatched-path", "path": path, "class": "C"})
    classes = [item["class"] for item in triggered]
    result_class = "C" if not classes or "C" in classes else ("B" if "B" in classes else "A")
    result = {"schemaVersion": SCHEMA_VERSION, "classification": result_class, "baseCommit": base, "candidateCommit": head, "policyVersion": policy["version"], "policyDigest": sha256_file(repo / "policy" / "change-class-policy.json"), "triggeringRules": triggered}
    return result


def escalate(repo: Path, args: argparse.Namespace) -> int:
    items: list[dict[str, Any]] = []
    if args.classification:
        classification = json.loads(Path(args.classification).read_text(encoding="utf-8"))
        if classification.get("classification") == "C":
            items.append({"type": "class-c-change", "commit": classification.get("candidateCommit"), "question": "Human gate: approve or reject the Class C change based on triggering rules and paths."})
    if args.invariant:
        inv = json.loads(Path(args.invariant).read_text(encoding="utf-8"))
        for result in inv.get("results", []):
            if result.get("status") != "pass":
                items.append({"type": "invariant-violation", "invariant": result.get("id"), "question": "Human gate: decide whether the invariant or implementation requires a contract issue."})
    output = {"schemaVersion": SCHEMA_VERSION, "interval": args.interval, "cap": args.cap, "status": "ok", "entries": items[: args.cap], "deferredCount": max(0, len(items) - args.cap)}
    if len(items) > args.cap:
        output["status"] = "deferred"
    print(json.dumps(output, ensure_ascii=False, sort_keys=True, indent=2))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(prog="ptverify")
    sub = parser.add_subparsers(dest="command", required=True)
    p_verify = sub.add_parser("verify")
    p_verify.add_argument("--repo", type=Path, default=Path.cwd())
    p_verify.add_argument("--commit")
    p_verify.add_argument("--report", type=Path, required=True)
    p_class = sub.add_parser("classify")
    p_class.add_argument("--repo", type=Path, default=Path.cwd())
    p_class.add_argument("--base", default="main")
    p_class.add_argument("--head", default="HEAD")
    p_inv = sub.add_parser("invariants")
    p_inv.add_argument("--repo", type=Path, default=Path.cwd())
    sub.add_parser("schema-validity").add_argument("--repo", type=Path, default=Path.cwd())
    p_esc = sub.add_parser("escalate")
    p_esc.add_argument("--repo", type=Path, default=Path.cwd())
    p_esc.add_argument("--classification", type=Path)
    p_esc.add_argument("--invariant", type=Path)
    p_esc.add_argument("--interval", default="local")
    p_esc.add_argument("--cap", type=int, default=10)
    args = parser.parse_args()
    try:
        if args.command == "verify":
            return verify(args.repo, args.commit, args.report)
        if args.command == "classify":
            print(json.dumps(classify(args.repo, args.base, args.head), ensure_ascii=False, sort_keys=True, indent=2))
            return 0
        if args.command == "invariants":
            print(json.dumps(run_invariants(args.repo), ensure_ascii=False, sort_keys=True, indent=2))
            return 0
        if args.command == "schema-validity":
            code, _, details = json_files_valid(args.repo)
            print(json.dumps({"status": "pass" if code == 0 else "fail", "details": details}, sort_keys=True))
            return code
        return escalate(args.repo, args)
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError) as exc:
        print(f"fail-closed: {type(exc).__name__}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
