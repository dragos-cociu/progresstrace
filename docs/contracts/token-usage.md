# TokenUsage 1.0 normative contract

`TokenUsage` is a new, additive sidecar that records per-invocation token
totals and their provenance for an existing `AgentSession`. It is raw
supplied evidence, like `AgentSession` and `GateOutcome`, not a generated
report: it carries no `generatedAt` or `sourceDigest` of its own. It never
modifies `AgentSession` — `contracts/agent-session.schema.json` and
`docs/contracts/agent-session.md` are unchanged by this contract. See
[ADR-0009](../architecture/ADR-0009-observed-budget.md) for the approved
decision record.

## Scope

`TokenUsage` pairs with exactly one `AgentSession`, identified by
`sessionId` and `taskContractId`. It carries zero or more `records[]`
entries, at most one per invocation, reporting that invocation's observed
token total and how it was obtained. `TokenUsage` never reads conversation
text, calls a model, executes a command, or performs an online action.

## Required fields

`schemaVersion` is `1.0`. `sessionId` and `taskContractId` must equal the
paired `AgentSession`'s own `sessionId` and `taskContractId`. `records` is
required and may be empty — an empty array is a legitimate "no token
evidence captured yet" state, not a validation failure.

## Record fields

Each `records[]` entry is a JSON object with `additionalProperties: false`.
All eight properties are required:

- `sessionId` (string, non-empty): must equal the document's own
  `sessionId`.
- `invocationId` (string, non-empty): must name an invocation declared in
  the paired `AgentSession.invocations[]`.
- `sequence` (integer, `>= 0`): must equal that invocation's own
  `sequence`.
- `tokensTotal` (integer, `0..2147483647` inclusive): the observed token
  count for this invocation. `0` is a present, valid value — "zero tokens
  observed" — and is never conflated with the absence of a record for that
  invocation, which asserts nothing about the invocation's token count.
- `producerType` (string): exactly one of the closed four-value enum
  `agent`, `harness`, `operator`, `adapter` — the same vocabulary already
  used by `producerType` in `baseline-definition.schema.json`,
  `termination-declaration.schema.json`, and
  `stop-assessment-result.schema.json`.
- `producerName` (string, non-empty): identifies the producing agent,
  harness, operator, or adapter by name.
- `producerVersion` (string non-empty, or JSON `null`): the producer's
  version, or `null` when no meaningful version applies (for example an
  `agent` producer with no separately versioned identity). A present but
  empty `producerVersion` string is invalid; `null` is valid.
- `evidenceBasis` (string): exactly one of the closed four-value enum
  `provider-usage-field` (read from a model/provider API's own usage
  field), `harness-metered` (counted by the harness itself, for example
  via a local tokenizer), `agent-self-reported` (claimed by the agent's
  own output), `adapter-inference` (inferred by an adapter from other
  captured data). This vocabulary is new to `TokenUsage` and distinct from
  the estimate-oriented `evidenceBasis` vocabularies already defined for
  `baseline-definition.schema.json`, `termination-declaration.schema.json`,
  and `stop-assessment-result.schema.json`, because a token count is a
  measurement claim, not an estimate. `TokenUsage` defines no coherence
  rule coupling `producerType` to `evidenceBasis`; every combination of the
  two is structurally valid.

## Record admissibility

A `records[]` entry is admissible only when its `sessionId` equals the
document's own `sessionId`, its `invocationId` names an invocation declared
in the paired `AgentSession`, and its `sequence` equals that invocation's
own `sequence` — the same admissibility rule already established for
`GateOutcome` in `docs/contracts/agent-session.md`'s "Cross-contract
boundary" section, reused unchanged for this new record type. At most one
admissible record may target a given `invocationId`; a second record
naming the same `invocationId` is a duplicate, never treated as an update,
a correction, or an additional constituent to accumulate.

## Missing token records

`records[]` need not contain an entry for every invocation in the paired
`AgentSession`. An invocation with no corresponding record is a missing
token record. `TokenUsage` itself does not fail validation because of a
missing record — it is structurally valid with an empty `records` array,
or with any subset of a session's invocations covered. The consequence of
a missing record is realized downstream, when `TokenUsage` is consumed by
`ObservedBudget` (`docs/contracts/observed-budget.md`): one informational
`PT806` diagnostic per missing invocation, and `null` for any
`observedTokensTotal` whose sum would otherwise include that invocation.

## Fail-closed behavior

An inadmissible record (`PT801`), a duplicate `invocationId` across
`records[]` (`PT802`), or a `sessionId`/`taskContractId` mismatch against
the paired `AgentSession` (`PT800`, raised when `TokenUsage` is consumed
alongside that session) fails closed and produces no trusted
`ObservedBudget`. `TokenUsage`'s own Phase A structural validation
(malformed JSON, wrong type, missing/unknown property, duplicate property
name, oversized input, unsupported `schemaVersion`, an out-of-range or
empty field value) reuses the existing generic codes `PT000`-`PT005` and
`PT100`-`PT101` unmodified, exactly as `ObligationLedger`'s own Phase A
does (`docs/contracts/obligation-ledger.md`). Diagnostic messages do not
echo source values.

Normalization writes a stable property order and a trailing newline;
parse/normalize is byte-idempotent.

## CLI

`TokenUsage` has no CLI verb of its own; it is consumed as one of three
required inputs to the `budget` verb defined in
`docs/contracts/observed-budget.md`:

```
progresstrace budget \
  --session-path <path> \
  --token-usage-path <path> \
  --ledger-path <path> \
  [--out <path>]
```

## Non-goals

Pricing or cost-in-dollars fields, network access, model calls, and
persistence beyond the single invocation that consumes a `TokenUsage`
document are explicitly out of scope. See
[ADR-0009](../architecture/ADR-0009-observed-budget.md)'s "Non-goals" for
the complete list.

## Security

Read-only over local files: no command execution, no network access, no
credential handling.

### Provenance and security caveat

Reported token counts are structurally validated — schema-conformant and
admissible against the paired `AgentSession` — but they are not
independently truth-verified. `TokenUsage` records what a producer
(`agent`, `harness`, `operator`, or `adapter`) claims, tagged with
`evidenceBasis` describing the claim's origin. It performs no independent
audit of that claim against, for example, a provider's own billing record.
A caller that needs a stronger provenance guarantee must supply records
whose `evidenceBasis` and `producerType` already reflect it (for example
`provider-usage-field` from a trusted provider integration).

## Ownership

Owned by the ProgressTrace core contract maintainers, the same owners as
`agent-session.schema.json`. Changes to required or normative fields
require a superseding ADR and a new human gate.

## Compatibility

Schema version `1.0`. Only new optional fields may be added within `1.0`;
any change to a required field's shape or vocabulary requires a new
`schemaVersion`. `TokenUsage` versions independently of `AgentSession`,
`ObligationLedger`, and `ObservedBudget`.

## Synthetic example

Illustrative of document shape only; every normative rule is fixed above.
Correlates to a hypothetical `AgentSession` with `sessionId: "session-example-1"`
and two invocations, `inv-1` (`sequence` 0) and `inv-2` (`sequence` 1).

```json
{
  "schemaVersion": "1.0",
  "sessionId": "session-example-1",
  "taskContractId": "phase-x-example",
  "records": [
    {
      "sessionId": "session-example-1",
      "invocationId": "inv-1",
      "sequence": 0,
      "tokensTotal": 1542,
      "producerType": "harness",
      "producerName": "example-harness",
      "producerVersion": "2.3.0",
      "evidenceBasis": "provider-usage-field"
    }
  ]
}
```

`inv-2` has no entry above: it is a missing token record, valid for
`TokenUsage` on its own, and resolved by `ObservedBudget` as described in
"Missing token records" above.
