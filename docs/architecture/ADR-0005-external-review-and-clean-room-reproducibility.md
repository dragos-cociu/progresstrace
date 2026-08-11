# ADR-0005: External review and clean-room reproducibility

## Status

Accepted and active by explicit human direction from Dragos on 2026-08-10.

Supersedes only the external-review and reproducibility *execution
mechanics* named in `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`'s
"Execution ownership" section, `docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`'s
"Execution ownership" section, and `docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`'s
"Execution ownership" and "Independent-review gate" sections. It does not
reopen, rewrite, or weaken any product, contract, diagnostic, formula, or
provenance decision in ADR-0001 through ADR-0004, and it does not alter any
completed task contract. Antigravity references in those documents and in
`tasks/phase-1-obligation-evaluation.json`, `tasks/phase-2a-stop-assessment.json`,
and `tasks/phase-2b-baseline-comparison.json` are historical records of the
review tool used at the time each was authored or executed; they are
interpreted through this ADR going forward and are not edited.

## Context

Earlier ADRs named a specific external-review tool, Antigravity, directly in
durable architecture text (a read-only "Mode A" architecture/documentation
review, and a disposable-checkout "Mode B" build/conformance/CLI
reproducibility review). Antigravity has since been retired from
ProgressTrace's active engineering workflow. Naming a specific vendor CLI
inside durable product ADRs, rather than the capability it provided, left
those documents reading as if a now-retired tool were still an active
requirement, which is misleading to any reader relying on them for current
process. Separately, Phase 2b's own review record exposed a real boundary
that earlier documents left implicit: a stateless external reviewer that
judges submitted evidence is a categorically different capability from a
tool that reproduces a build or test result by actually executing commands,
and the two must never be described, logged, or claimed as the same thing.
This ADR fixes both problems by defining durable capability roles instead of
vendor names, and by fixing exactly what a model-based review is and is not
permitted to claim.

## Decision

1. **Deterministic verifier/controller.** One role executes every build,
   test, schema, and CLI command and owns ground truth about whether those
   commands passed. It holds repository, git, build, run, and (where a task
   contract's `allowed_paths` grants it) write access. It is the sole
   producer of command output, exit codes, and repository fingerprints used
   as evidence.
2. **Stateless external reviewer API.** A second role judges immutable,
   bounded evidence (diffs, command transcripts, fingerprints) submitted to
   it and returns a structured verdict. It holds no repository, git, build,
   run, network, or write tool of any kind, and executes nothing. Its
   judgment is scoped to the evidence it was given, not to the repository at
   large.
3. **Clean-room reproduction, when a phase contract requires it.** The
   controller (never the reviewer API) creates a credential-free source
   archive pinned to an exact commit or tree; sanitizes the environment of
   secrets, tokens, and credentials before any command runs against that
   archive; blocks network access at the OS boundary where the execution
   environment supports it; runs only the commands a task contract's
   `acceptance_commands` (or an equivalent explicit allowlist) names, never
   an ad hoc or improvised command; and fingerprints tracked source before
   and after execution to prove no unexpected mutation occurred. Only after
   that reproduction completes does the controller submit the resulting
   immutable evidence to the reviewer API for structured judgment.
4. **The reviewer API never executes anything, and this must never be
   claimed.** No record produced under this ADR — a verdict, a log, a
   status line in a document, a commit message, or a task contract — may
   state or imply that the model ran a build, test, command, or tool. If a
   command was run, the controller ran it; the reviewer API only read the
   resulting evidence.
5. **Provider and model are operational configuration, not product
   semantics.** The active external review route is currently the Google
   direct Gemini API, model `gemini-3.5-flash-lite`. Any future substitution
   of provider or model follows this project's ordinary policy and gating
   process and requires no change to product semantics, contracts, or this
   ADR's role definitions; only the operational configuration value changes.
6. **Deterministic failure outranks model approval.** A failing build, test,
   schema check, or acceptance command blocks integration regardless of any
   reviewer-API verdict. A reviewer-API "approve" never overrides, waives,
   or substitutes for a deterministic failure.
7. **A raw verdict is immutable.** Once the reviewer API returns a
   structured verdict against a given evidence submission, the controller
   persists it unedited. If the verdict declares an assumption that later
   needs resolving, the controller resolves it with separate, newly
   captured deterministic evidence and records that resolution alongside
   the original verdict; the controller never rewrites the original
   verdict's content and never silently resubmits the same evidence hoping
   for a different answer.
8. **Disposable copies are removed after evidence capture.** Any clean-room
   archive or disposable checkout created under Decision 3 is deleted once
   its evidence has been captured and submitted; it is never left behind as
   a second, driftable copy of the repository.
9. **Historical Antigravity references are interpreted, not edited.**
   Accepted ADR bodies and completed task contracts that name Antigravity,
   Mode A, or Mode B describe the review tool and mechanism in force at the
   time of that decision or task. They remain historical records. Under
   this ADR, "Antigravity Mode A strict read-only review" is read as an
   instance of the stateless external reviewer API role (Decision 2), and
   "Antigravity Mode B disposable reproducibility review" is read as an
   instance of controller-run clean-room reproduction followed by reviewer
   API judgment (Decisions 1 and 3). This document does not go back and
   rename those historical mentions.

## Normative boundaries

- **Role separation is structural, not a naming convention.** The
  deterministic verifier/controller and the stateless external reviewer API
  are never the same execution context, and the reviewer API is never
  granted a tool that would let it execute a command, reach the network, or
  write to the repository.
- **Clean-room reproduction inputs are exactly four things**: a
  commit/tree-pinned, credential-free source archive; a sanitized
  environment; an explicit, task-contract-derived command allowlist; and a
  before/after fingerprint of tracked source. Any reproduction missing one
  of these four is not a clean-room reproduction under this ADR and must
  not be described as one.
- **Human integration authority is unchanged.** Dragos reviews the
  controller's evidence and the reviewer API's verdict and owns the sole
  final integration decision via the normal PR flow, exactly as ADR-0002,
  ADR-0003, and ADR-0004 already fixed; this ADR grants no role the
  authority to merge, publish, or self-approve.

## Execution ownership

The controller (the role ADR-0002/ADR-0003/ADR-0004 named Hermes) executes
every deterministic acceptance command, assembles the evidence packet, and,
when a task contract requires implementation-reproducibility review,
performs clean-room reproduction per Decision 3 before submitting evidence
to the reviewer API. The reviewer API (the role those documents' now-retired
Antigravity mechanics filled) performs only stateless, tool-free judgment of
submitted evidence and returns a structured verdict. Dragos retains sole
final integration authority. No role edits another role's owned artifacts,
and no role substitutes for another; this mirrors, unmodified, the
role-separation principle ADR-0002 established and ADR-0003/ADR-0004 each
reused.

## Security

Clean-room reproduction under Decision 3 never carries credentials,
long-lived tokens, or write access to the original repository into the
disposable archive, matching `docs/security.md`'s existing rule against
committing OAuth caches or tokens. Network access is blocked at the OS
boundary where the execution environment supports it; where it is not
supported, the controller records that limitation in the evidence packet
rather than silently assuming isolation held. The reviewer API receives
only the bounded evidence submitted to it, never raw repository access,
network access, or a write tool, consistent with `docs/security.md`'s
no-network/no-model-write rule for the deterministic core.

## Consequences

- Future ADRs and task contracts should name capability roles
  (deterministic verifier/controller, stateless external reviewer API)
  rather than a specific vendor CLI, so a future provider or model
  substitution under Decision 5 requires no edit to durable architecture
  text.
- Accepted ADR bodies (ADR-0002, ADR-0003, ADR-0004) and completed task
  contracts keep their original Antigravity/Mode A/Mode B wording; a reader
  auditing this repository's history will see two review-mechanism
  vocabularies (old, tool-named; new, role-named) describing the same
  underlying separation of duties, bridged by Decision 9 above.
- Any process, tooling, or documentation change that would let the reviewer
  API execute a command, or that would describe its judgment as command
  execution, is a violation of this ADR and must be corrected before
  integration, not merged with a caveat.

## Verification checklist

- [ ] The controller, not the reviewer API, ran every deterministic command
      cited as evidence.
- [ ] No record produced for this review states or implies the model
      executed a build, test, command, or tool.
- [ ] If clean-room reproduction was required: the source archive is
      credential-free and pinned to a commit or tree; the environment was
      sanitized; network was blocked at the OS boundary where supported (or
      the limitation is recorded); only allowlisted commands ran; tracked
      source was fingerprinted before and after with no unexpected diff.
- [ ] Any deterministic failure blocked integration regardless of the
      reviewer-API verdict.
- [ ] The raw reviewer-API verdict was persisted unedited; any assumption it
      declared was resolved with separate deterministic evidence, not a
      silent rewrite or retry.
- [ ] The disposable clean-room copy, if one was created, was deleted after
      evidence capture.
- [ ] Dragos retains sole final integration authority via the normal PR
      flow.

## Non-goals

- Reopening or amending any contract, diagnostic code, formula, or
  provenance rule fixed by ADR-0001 through ADR-0004; this ADR touches only
  external-review and reproducibility execution mechanics.
- Editing historical Antigravity wording inside accepted ADR bodies or
  completed task contracts.
- Granting the reviewer API, or any future substitute provider or model,
  any tool, network, or write access; it remains stateless and read-only
  over the evidence it is given.
- Authorizing automatic merge, publication, or self-approval by any role;
  final integration authority remains with Dragos, unchanged from ADR-0002.
- Defining a general ingestion adapter, hosted API, UI, persistence, or
  field-level provenance model; those remain out of scope, as fixed in
  `docs/product-brief.md`.

## Related documents

- `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`
- `docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`
- `docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
- `docs/product-brief.md`
- `docs/security.md`
- `AGENTS.md`
