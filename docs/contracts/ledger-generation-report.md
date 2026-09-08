# LedgerGenerationReport 1.0 normative contract

`LedgerGenerationReport` is the sidecar output of the `generate-ledger` CLI
verb defined by `docs/architecture/ADR-0007-task-contract-ledger-generation.md`.
It carries provenance and coverage for one generated `ObligationLedger` 1.0
document without adding any field to `ObligationLedger` itself
(`contracts/obligation-ledger.schema.json` and
`docs/contracts/obligation-ledger.md` are unmodified by this contract). The
normative machine-readable schema is
`contracts/ledger-generation-report.schema.json`. `LedgerGenerationReport`
versions independently of `ObligationLedger`: a change to one never forces a
change to the other.

## Purpose

A generated ledger's obligations are mechanically derived from a source
task contract's `deliverables` and `required_contract_decisions`/
`required_invariants` arrays (ADR-0007 Decision 2). The report answers,
for a given generated ledger, three questions the ledger itself cannot
answer: which exact source document and byte content produced it, which
source array entry produced each obligation, and which source-field
category, if any, contributed nothing. It is written and read exactly like
every other artifact in this repository: as untrusted structured data, not
as an instruction of any kind.

## Encoding

UTF-8 JSON, the same size limit
(`TraceValidator.MaximumInputSizeBytes`, 16 MiB), the same rejection of
malformed JSON before any semantic rule, and the same closed-object,
duplicate-property-name rejection (`PT004` precedent) as every other
contract in this repository.

## Required fields

- `schemaVersion`: exactly `"1.0"`.
- `reportType`: exactly `"LedgerGenerationReport"`.
- `generator`: object `{name: "progresstrace-ledger-generator", version}`,
  mirroring `VerificationReport.runner`'s existing `{name, version}` shape.
- `generatedAt`: the canonical ISO-8601 timestamp
  `1970-01-01T00:00:00.0000000Z`. It is a deterministic generation sentinel,
  not a wall-clock claim; this fixed value preserves the required byte-identical
  regeneration without adding a fifth CLI input or reading environment state.
- `taskContractId`: the source task contract's own `id` property, verbatim.
- `taskContractPath`: the repository-relative path the generator read.
- `taskContractDigest`: lowercase SHA-256 of the exact source task-contract
  file bytes the generator read, matching `GateOutcome.sourceDigest`'s
  existing digest convention.
- `traceId`: the value bound by the caller's explicit `<trace-id>` argument
  (ADR-0007 Decision 6); must equal the paired ledger's `traceId`.
- `ledgerDigest`: lowercase SHA-256 of the exact generated ledger's
  canonical bytes, per `docs/contracts/obligation-ledger.md`'s existing
  canonical serialization (fixed property order, UTF-8, no BOM, one
  trailing newline).
- `obligations`: non-empty array, one entry per obligation in the paired
  ledger, in the same order as the ledger's `obligations` array.
  - `obligationId`: must equal one `obligations[].id` in the paired ledger.
  - `coverageStatus`: `derived-automatic` or `manual` (ADR-0007 Decision 8).
    The generator itself emits only `derived-automatic`; `manual` is
    reserved for a human hand-editing a generated ledger/report pair after
    the fact to add an obligation the generator did not derive.
  - `sourceField`: one of `deliverables`, `required_contract_decisions`,
    `required_invariants`. Required when `coverageStatus` is
    `derived-automatic`; the generator never sets it for `manual` entries,
    since a manually added obligation has no source array entry.
  - `sourcePointer`: an RFC 6901 JSON Pointer into the source task contract
    identifying the exact array entry that produced this obligation, for
    example `/deliverables/2` or `/required_contract_decisions/5`. Required
    when `coverageStatus` is `derived-automatic`; absent for `manual`
    entries for the same reason as `sourceField`.
- `unsupportedSourceFields`: array, possibly empty, of
  `deliverables`/`required_contract_decisions`/`required_invariants`
  values, each recorded once, for a source-field category that was absent
  or empty in the source task contract and therefore contributed zero
  obligations (ADR-0007 Decision 8). Recording an entry here never blocks
  generation; generation is blocked only when every qualifying category is
  empty (ADR-0007 Decision 4), which itself produces no report at all,
  since no ledger exists to describe.

## Stable obligation identity

`obligationId` is `"<taskContractId>:<sourceField>:<index>"`, where `index`
is the zero-based position of the source entry within that field's array in
the source document (ADR-0007 Decision 5). This id is a pure function of
`taskContractId`, `sourceField`, and array position; regenerating from
byte-identical input, verified via `taskContractDigest`, always reproduces
the same ids. A reader must never treat a changed id, after a source edit,
as evidence that the underlying obligation's meaning changed; `sourcePointer`
combined with `taskContractDigest` is the only reliable way to compare two
generations of the same conceptual obligation across a source edit.

## Excluded sources

`objective`, `acceptance_commands`, and every task-contract property other
than `deliverables`, `required_contract_decisions`, and
`required_invariants` are never read by the generator, never appear as a
`sourceField` value, and are never represented anywhere in this report
(ADR-0007 Decision 3). This exclusion list is closed; a `sourceField` value
outside the three named above never occurs in a valid report.

## Fail-closed conditions and diagnostic registry

The generator writes neither the ledger nor the report unless generation
succeeds completely; there is no partial or best-effort output pair.
Diagnostics reuse the existing `Diagnostic{Code, Pointer, Message}` shape.
`Pointer` targets the source task-contract document unless noted otherwise.
No message echoes an untrusted task-contract value, matching the `PT004`
redaction precedent.

This contract allocates a new, non-colliding `PT6xx` block:

- `PT600` unreadable task-contract path. Pointer `""`. Message: `"Task
  contract file could not be read."`
- `PT601` malformed task-contract JSON. Pointer `""`. Message: `"Task
  contract is not valid JSON."` Short-circuits; no other `PT6xx` diagnostic
  accompanies it.
- `PT602` missing or non-string task-contract `id`. Pointer `/id`. Message:
  `"Task contract id is missing or not a string."`
- `PT603` zero candidate obligations: `deliverables` and both
  `required_contract_decisions` and `required_invariants` are absent or
  empty (ADR-0007 Decision 4). Pointer `""`. Message: `"Task contract has
  no deliverables and no required_contract_decisions or
  required_invariants."`
- `PT604` non-string or empty/whitespace-only entry inside a qualifying
  source array. Pointer: that entry's own pointer, for example
  `/deliverables/3`. Message: `"Source array entry must be a non-empty
  string."`
- `PT605` missing or empty `<trace-id>` argument (ADR-0007 Decision 6).
  Pointer `""`. Message: `"traceId argument is missing or empty."`
- `PT606` duplicate stable obligation id across candidates in the same
  generation run. Pointer: the later (duplicate) candidate's own source
  pointer. Message: `"Generated obligation id is not unique."`

A candidate obligation that fails `PT604` never reaches id generation. If
Phase A structural validation under the unchanged `ObligationLedgerValidator`
(`docs/contracts/obligation-ledger.md`) subsequently fails against the
assembled ledger, the generator reports the existing `PT1xx`/`PT2xx`
diagnostic unchanged, at the assembled ledger's own pointer; this contract
defines no new code for that case, since the ledger's own registry already
covers it.

## CLI `generate-ledger` behavior

Usage: `generate-ledger <task-contract-path> <trace-id> <ledger-output-path>
<report-output-path> [--manifest-output-path <path>]`. The original four
positional arguments remain unchanged (ADR-0007 Decision 11). Without the
optional flag, ledger and report bytes are unchanged. With the flag, ledger,
report, and the derived CorrelationManifest are written as one atomic set.
The output files are written if and only if generation fully
succeeds: the task contract reads and parses; at least one candidate
obligation exists; every candidate entry is a non-empty string; `traceId`
is non-empty; every stable id is unique within the run; and the assembled
ledger passes `ObligationLedgerValidator` Phase A. On any failure, neither
output file is written or partially written. Manifest output additionally
requires at least one valid explicit binding. Legacy argv arrays remain valid
but are not represented in the manifest. Bound entries contain only a
non-empty string `command` array, a unique non-blank `gateKey`, and an
`obligationRef` whose closed `sourceField` and integer `index` identify a
generated obligation. Binding errors use `PT607` and fail closed.

Exit codes reuse the existing convention unchanged: `0` only when both
files are written; `1` for `PT603`, `PT604`, `PT606`, or an assembled-ledger
Phase A diagnostic; `2` for bad usage, `PT600`, `PT601`, `PT602`, or
`PT605`. `generate-ledger` introduces no network, external process,
database, or filesystem access beyond reading the task-contract path and
writing the two named output paths.

## Referential scope

The generator validates only what exists at generation time: task-contract
structure, candidate-array entry shape, stable-id uniqueness, and the
assembled ledger's own Phase A structural rules. It never runs
`ObligationLedgerValidator` Phase B (cross-document referential checks
against a trace envelope), because the generator always emits `signals: []`
and no trace envelope is paired at generation time (ADR-0007 Decision 10).
A generated ledger is not fully validated until it is later paired with a
real trace envelope through the existing, unchanged `evaluate` verb.

## Version policy

Same additive-only-versus-breaking-change policy as every other contract in
this repository, applied independently to `LedgerGenerationReport`:
changing the closed `coverageStatus` enum, the stable-id algorithm, the
excluded-sources list, or any `PT6xx` diagnostic's meaning or pointer rule
is a breaking change requiring a new major version and Dragos's approval. A
new optional property is additive and may land in a later minor version.

## Synthetic example

Illustrative of document shape only; every normative rule is fixed above.
Correlates to a task contract with `id: "phase-x-example"`, one
`deliverables` entry, and no `required_contract_decisions`/
`required_invariants`.

```json
{
  "schemaVersion": "1.0",
  "reportType": "LedgerGenerationReport",
  "generator": { "name": "progresstrace-ledger-generator", "version": "1.0.0" },
  "generatedAt": "2026-08-20T00:00:00.0000000Z",
  "taskContractId": "phase-x-example",
  "taskContractPath": "tasks/phase-x-example.json",
  "taskContractDigest": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "traceId": "trace-example-1",
  "ledgerDigest": "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
  "obligations": [
    {
      "obligationId": "phase-x-example:deliverables:0",
      "coverageStatus": "derived-automatic",
      "sourceField": "deliverables",
      "sourcePointer": "/deliverables/0"
    }
  ],
  "unsupportedSourceFields": ["required_contract_decisions", "required_invariants"]
}
```
