#!/usr/bin/env python3
import json
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from adapter import write_outputs  # noqa: E402


def clean_retained_evidence(experiment_dir):
    output_root = experiment_dir / "outputs"
    shutil.rmtree(output_root, ignore_errors=True)
    (experiment_dir / "FRICTION.md").unlink(missing_ok=True)
    output_root.mkdir()
    return output_root


def main():
    output_root = clean_retained_evidence(HERE)
    cases = sorted((HERE / "cases").glob("*.json"))
    if len(cases) != 5:
        raise SystemExit(f"expected exactly five cases, found {len(cases)}")
    cli_project = ROOT / "src/ProgressTrace.Cli/ProgressTrace.Cli.csproj"
    build = subprocess.run(["dotnet", "build", str(cli_project), "--configuration", "Release", "--nologo"],
                           cwd=ROOT, text=True, capture_output=True, check=False)
    if build.returncode != 0:
        raise SystemExit(f"CLI build failed ({build.returncode}): {build.stderr.strip()}\n{build.stdout.strip()}")
    stages = []
    for manifest in cases:
        case_id = json.loads(manifest.read_text(encoding="utf-8"))["caseId"]
        target = output_root / case_id
        write_outputs(manifest, target)
        args = {
            "evaluate": ["trace.json", "ledger.json"],
            "assess": ["trace.json", "ledger.json", "declaration.json"],
            "compare": ["trace.json", "ledger.json", "declaration.json", "baseline.json"]
        }
        for verb, names in args.items():
            command = ["dotnet", "run", "--project", str(cli_project),
                       "--configuration", "Release", "--no-build", "--", verb, *[str(target / n) for n in names]]
            completed = subprocess.run(command, cwd=ROOT, text=True, capture_output=True, check=False)
            (target / f"{verb}.stdout.json").write_text(completed.stdout, encoding="utf-8")
            if completed.stderr:
                (target / f"{verb}.stderr.txt").write_text(completed.stderr, encoding="utf-8")
            stages.append({"caseId": case_id, "stage": verb, "exitCode": completed.returncode,
                           "success": completed.returncode == 0})
            if completed.returncode != 0:
                raise SystemExit(f"{case_id} {verb} failed ({completed.returncode}): {completed.stderr.strip()}")
    summary = {"manifestVersion": "0", "caseCount": len(cases), "successfulCaseCount": len(cases),
               "stageCount": len(stages), "successfulStageCount": sum(s["success"] for s in stages), "stages": stages}
    (output_root / "summary.json").write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    (HERE / "FRICTION.md").write_text(
        "# Dogfood friction\n\nAll five projections passed all three CLI stages. The main friction was manual: "
        "obligations and signals cannot be derived from invocation metadata, so each needed an explicitly authored projection. "
        "Observed closeout metadata and inferred termination provenance also required careful separation. The canonical coherence "
        "rule required the explicitly projected harness stop to use harness-lifecycle provenance; other declarations use adapter-inference. "
        "The runner performs one Release CLI build, then uses `--no-build` so each retained invocation stays focused on evaluation.\n",
        encoding="utf-8")
    print(json.dumps({"cases": "5/5", "cliStages": "15/15", "summary": str(output_root.relative_to(ROOT) / "summary.json")}))


if __name__ == "__main__":
    main()
