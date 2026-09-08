using System.Runtime.CompilerServices;
using System.Text;
using ProgressTrace.Core.Generation;
using ProgressTrace.Core.Models;

static class CoverageReportConformance
{
    private const string Task = "task-coverage";
    private const string Trace = "trace-coverage";
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [ModuleInitializer]
    public static void Assert()
    {
        var ledger = new ObligationLedger("1.0", Trace,
            [new(Task + ":deliverables:0", "full"), new(Task + ":deliverables:1", "partial"), new(Task + ":deliverables:2", "zero"), new(Task + ":deliverables:3", "manual")], []);
        var manifest = Manifest(
            Binding("gate-b", 0), Binding("gate-a", 0), Binding("gate-partial", 1), Binding("gate-zero", 2));
        var outcomes = new GateOutcome?[] { Outcome("outcome-2", 0), Outcome("outcome-1", 0), Outcome("outcome-3", 1) };
        var first = CoverageReportGenerator.Generate(manifest, ledger, outcomes);
        var second = CoverageReportGenerator.Generate(manifest, ledger, outcomes);
        Require(first.IsValid && first.Report is not null && first.ReportBytes is not null, "valid report failed");
        var reportBytes = first.ReportBytes!;
        Require(reportBytes.AsSpan().SequenceEqual(second.ReportBytes), "report bytes were not deterministic");
        Require(reportBytes[^1] == (byte)'\n', "report was not newline-terminated");
        Require(Encoding.UTF8.GetString(reportBytes) == "{\"schemaVersion\":\"pt-coverage-report-1.0\",\"taskContractId\":\"task-coverage\",\"traceId\":\"trace-coverage\",\"obligations\":[{\"obligationId\":\"task-coverage:deliverables:0\",\"coverageStatus\":\"automatic\",\"gateEvidenceCount\":2,\"gateKeys\":[\"gate-b\",\"gate-a\"]},{\"obligationId\":\"task-coverage:deliverables:1\",\"coverageStatus\":\"automatic\",\"gateEvidenceCount\":1,\"gateKeys\":[\"gate-partial\"]},{\"obligationId\":\"task-coverage:deliverables:2\",\"coverageStatus\":\"insufficient-evidence\",\"gateEvidenceCount\":0,\"gateKeys\":[\"gate-zero\"]},{\"obligationId\":\"task-coverage:deliverables:3\",\"coverageStatus\":\"manual\",\"gateEvidenceCount\":0,\"gateKeys\":[]}]}\n", "report shape or ledger order differed");

        Failure(null, ledger, outcomes, "null manifest");
        Failure(manifest, null, outcomes, "null ledger");
        Failure(manifest, ledger, null, "null outcomes");
        Failure(manifest with { TraceId = "other" }, ledger, outcomes, "trace mismatch");
        Failure(manifest with { TaskContractId = "other" }, ledger, outcomes, "task mismatch");
        Failure(Manifest(Binding("same", 0), Binding("same", 1)), ledger, [], "duplicate gate key");
        Failure(Manifest(new CorrelationGateBinding("dangling", Task + ":deliverables:9", "ignored", new("ignored", "ignored"))), ledger, [], "dangling binding");
        Failure(manifest, ledger, [Outcome("same", 0), Outcome("same", 0)], "duplicate outcome");
        Failure(manifest, ledger, [Outcome("unknown", 9)], "unknown outcome obligation");

        var unbound = CoverageReportGenerator.Generate(manifest, ledger, [Outcome("unbound", 3), Outcome("none", null)]);
        Require(unbound.IsValid && unbound.Diagnostics.Count == 2 && unbound.Report!.Obligations[3].CoverageStatus == "manual", "unbound outcomes were not diagnosed and ignored");
        var misleading = Outcome("misleading", 2) with { Command = "exit 1; mentions gate-a", ExitCode = 42, Verdict = "fail" };
        Require(CoverageReportGenerator.Generate(manifest, ledger, [misleading]).Report!.Obligations[2].CoverageStatus == "automatic", "non-identity fields influenced coverage");
    }

    private static CorrelationManifest Manifest(params CorrelationGateBinding[] bindings) => new("pt-shadow-correlation-1.0", Task, Trace, bindings);
    private static CorrelationGateBinding Binding(string key, int obligation) => new(key, Task + ":deliverables:" + obligation, "ignored", new("ignored", "ignored"));
    private static GateOutcome Outcome(string id, int? obligation) => new("1.0", id, "session", "invocation-" + id, 1, "ignored", 0, DateTimeOffset.UnixEpoch, obligation is null ? null : Task + ":deliverables:" + obligation, "pass", Digest);

    private static void Failure(CorrelationManifest? manifest, ObligationLedger? ledger, IReadOnlyList<GateOutcome?>? outcomes, string name)
    {
        var result = CoverageReportGenerator.Generate(manifest, ledger, outcomes);
        Require(!result.IsValid && result.Report is null && result.ReportBytes is null && result.Diagnostics.Count == 1, name + " did not fail closed");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Coverage report conformance: " + message);
    }
}
