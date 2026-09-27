# ProgressTrace — Phase 5 final report

**Date:** 2026-09-27 · **Decision:** B — freeze at `v1.0.0` after a bounded F5/F1 bugfix
(`docs/architecture/ADR-0017-phase-5-decision-freeze.md`) · **Evidence:**
`progresstrace-experiments/experiments/07-phase-5-adversarial-series-sibiul/` (not public, see §7)

## 1. The question

ProgressTrace was built to answer: *how do you know an AI agent actually did what it says it
did?* Phase 5 reduced that to one falsifiable question (M4, plan §12.7):

> Under real conditions, does ProgressTrace report something that the agent's own report and
> the exit codes do not?

## 2. Method

**Project.** Sibiul Nestiut, a small trilingual (RO/DE/EN) static Astro site deployed on
Cloudflare Pages: a real external project, unrelated to ProgressTrace and to the orchestrator.

**Development track.** The usual Hermes flow: Hermes as PM/orchestrator, Claude Code for
architecture, Codex for implementation, Gemini for review. Hermes received ordinary task
prompts and was not told that the work was being measured.

**Measurement track (level 0, outside Hermes).**

1. Negative control first (§12.3): five deliberately broken inputs, expected diagnostics
   pinned from the code *before* running. Result: 5/5 failed as expected.
2. Per session, committed before the session started: a task contract, gate scripts, a
   falsification criterion and the prompt sent to Hermes. The commit history of the evidence
   repository (bundled in the archive) proves the ordering.
3. Every gate validated in both directions before use: it fails on `main` or on a deliberately
   naive implementation, and passes on a correct one.
4. At every push: clean build, gates run by the measurer, `generate-ledger` → `advise` with a
   binary built from a pinned commit, `provenance.json` (argv, cwd, binary SHA-256, exit
   code, stdout/stderr) for every run.
5. Human review on the Cloudflare preview; a `RealDecisionRecord` authored by the human; a
   `feedback.json` computed from artifacts only.

**Threshold, fixed before S1.** A *behavioral signal* is an obligation with a gate that is not
`satisfied` at the final checkpoint while the agent's final report claims it is done, **and**
the human confirms the problem is real. Zero confirmed signals across S1–S3 ⇒ B.

## 3. Sessions

| Session | Task | Checkpoints | Gates not satisfied at final | Confirmed signals | Real defects (found by) |
|---|---|---|---|---|---|
| S1 | Active language in selector, localized site name | 3 | 0 | 0 | 2 — contrast; hover regression introduced by the first fix (human, visually) |
| S2 | Language switch keeps the reader on the equivalent article | 1 | 0 | 0 | 0 |
| S3 | Real, localized 404 on the deployed site | 1 | 2 | 0 — both rejected: the gate assumed server-side localization, which the task did not require | 0 |

**Result: 0 confirmed behavioral signals ⇒ B.**

## 4. What we learned

1. **The agent never reported anything false.** In every session Hermes' final report matched
   reality and was explicit about what it could not verify (for example, a browser that
   would not start). A coverage verifier has nothing to catch when there are no false claims.
2. **The real defects were visual.** Both S1 defects were CSS-cascade problems (an unlayered
   `a { color: inherit }` overriding a Tailwind utility; then a hover background overriding
   the fix). The HTML and the source were correct, so the agent's checks, the LLM review, the
   gates and ProgressTrace all passed. Only a human looking at the rendered page caught them.
3. **The value came from gate design, not from aggregation.** The two most informative checks
   were a hidden fixture (S2: an article with no translation, built in a temporary copy, which
   confirmed a claim the agent had made without ever exercising the case) and a check against
   the *deployed* site (S3: HTTP 404 on Cloudflare, which a local build cannot show).
   ProgressTrace only recorded their results.
4. **Gates are wrong too, in both directions.** Three false failures came from the measurement
   side: gates run before the build (F0), an HTML parser that grouped elements by reused
   Python `id()` values (F6), and a gate that encoded one implementation strategy (S3). Without
   human review, ProgressTrace would have reported three false problems and missed the two
   real ones.
5. **Running on real data found real product bugs that conformance had missed:**
   - **F5:** `advise` took an obligation's status from its single latest signal, so a failing
     gate followed by a different passing gate reported `satisfied`. A verifier that hides
     failures is worse than none. Fixed in `v1.0.0`.
   - **F1:** per-obligation labels contradicted the documented contract (a current failure
     reported as recovery; two passing gates as no advancement). The advisory golden fixture
     itself encoded the bug and was never compared with output. Fixed in `v1.0.0` (new value
     `failed-attempt`; the golden is now compared).
   - **F2 / F4:** limitations, documented in the README.
6. **Cost.** Writing a contract and its validated gates took the measuring agent roughly
   2–3 minutes per session, plus one-off tooling. Human review time dominated and was not
   measured, so M3 is only partially answered.

## 5. Limits of this study

- One project, three sessions, one orchestrator. The project is small and mostly visual,
  which favors defects that no gate on source/HTML can see.
- The agent under test was careful and honest; the series says nothing about agents that
  over-claim, which is the case ProgressTrace was designed for.
- The measurer (Claude Code) also designed the gates and made three measurement errors, all
  caught and recorded.
- The human review cost was not timed.

A different result would need a different setting: an agent that over-claims, and
obligations whose truth is checkable by a gate. That is a new question, not a Phase 5.x.

## 6. What remains useful: the verification practice

Independently of the product, the procedure that produced every real finding here is worth
keeping as a practice:

1. Write the task contract and the acceptance criteria **before** the work starts, and commit
   them.
2. Validate each gate in both directions: it must fail on the unchanged code or on a naive
   implementation, and pass on a correct one.
3. Prefer hidden fixtures for behavior the data does not exercise yet.
4. Check the deployed artifact, not only the local build.
5. Read gate output, not just exit codes (two of the measurement errors were exit codes read
   without their output).
6. Keep a human look at the rendered result; it caught everything that mattered.
7. Decide against a threshold fixed in advance, with "stop" as an acceptable outcome.

## 7. Artifacts

- Decision: `docs/architecture/ADR-0017-phase-5-decision-freeze.md`
- Plan and protocol: `docs/phase-5-plan-and-decision-metrics.md` (§12–§15),
  `docs/phase-5-adverse-series-manual-test-plan.md`
- Bugfix: `advise` F5/F1 (PR #15), conformance cases reproducing the series
- Evidence: `progresstrace-experiments/experiments/07-phase-5-adversarial-series-sibiul/`

The evidence archive (`progresstrace-experiments`) is a separate, **non-public** repository:
it contains session material from the author's private environment. The numbers and
findings in this report are the ones recorded there; the method is described in enough
detail (section 2 and `docs/phase-5-plan-and-decision-metrics.md` §12–§14) to be repeated
on another project. Absolute paths such as `/srv/projects/...` that appear in the
documentation refer to the author's machine.
  (`series-result.md`, `findings.md`, `sessions/S*/`, `evidence-history.bundle`)
