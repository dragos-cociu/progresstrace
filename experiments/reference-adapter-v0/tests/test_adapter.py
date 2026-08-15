import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
from adapter import adapt, load_manifest, write_outputs
from run_dogfood import clean_retained_evidence


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.cases = sorted((HERE / "cases").glob("*.json"))

    def test_exactly_five_redacted_cases(self):
        self.assertEqual(5, len(self.cases))
        self.assertEqual(5, len({load_manifest(path)["caseId"] for path in self.cases}))

    def test_generation_is_deterministic_and_exactly_four_inputs(self):
        for path in self.cases:
            manifest = load_manifest(path)
            self.assertEqual(adapt(manifest), adapt(manifest))
            with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
                write_outputs(path, first)
                write_outputs(path, second)
                names = sorted(p.name for p in Path(first).iterdir())
                self.assertEqual(["baseline.json", "declaration.json", "ledger.json", "trace.json"], names)
                for name in names:
                    self.assertEqual((Path(first) / name).read_bytes(), (Path(second) / name).read_bytes())

    def test_redaction_and_path_safety(self):
        forbidden = ("/home/", "/srv/", "\\Users\\", "rawPrompt", "userChat", "credential", "oauth")
        for path in self.cases:
            text = path.read_text(encoding="utf-8")
            self.assertIn("redacted-projection-of-real-run", text)
            self.assertFalse(any(token in text for token in forbidden))
            generated = json.dumps(adapt(load_manifest(path)))
            self.assertFalse(any(token in generated for token in forbidden))

    def test_cleanup_removes_only_previous_retained_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            experiment = Path(temporary) / "experiment"
            stale_case = experiment / "outputs" / "old-case"
            stale_case.mkdir(parents=True)
            (stale_case / "evaluate.stderr.txt").write_text("stale", encoding="utf-8")
            (experiment / "outputs" / "summary.json").write_text("stale", encoding="utf-8")
            (experiment / "FRICTION.md").write_text("stale", encoding="utf-8")
            unrelated = experiment / "cases" / "keep.json"
            unrelated.parent.mkdir()
            unrelated.write_text("keep", encoding="utf-8")

            output_root = clean_retained_evidence(experiment)

            self.assertEqual(experiment / "outputs", output_root)
            self.assertEqual([], list(output_root.iterdir()))
            self.assertFalse((experiment / "FRICTION.md").exists())
            self.assertEqual("keep", unrelated.read_text(encoding="utf-8"))

    def test_retained_summary_records_real_cli_success(self):
        summary = json.loads((HERE / "outputs" / "summary.json").read_text(encoding="utf-8"))
        self.assertEqual((5, 5, 15, 15), (summary["caseCount"], summary["successfulCaseCount"],
                                         summary["stageCount"], summary["successfulStageCount"]))
        self.assertTrue(all(stage["exitCode"] == 0 and stage["success"] for stage in summary["stages"]))


if __name__ == "__main__":
    unittest.main()
