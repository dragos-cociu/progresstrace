# Benchmark annotation rubric

## Purpose

This rubric defines the independent human annotation protocol for the
expanded benchmark corpus. It is separate from the fixture `groundTruth`
fields, which remain operator-authored expectations used only for deterministic
implementation checks.

An annotation is a judgment about the trace evidence available at the end of
the recorded window. Annotators must not infer hidden future events, tool
success from intent alone, or correctness from confident wording.

## Annotation unit

Annotate each case at two levels:

1. one trace-level label;
2. one row per obligation, when the ledger exposes multiple obligations.

The admissible labels are:

- `progress`: at least one explicit obligation has materially advanced toward
  satisfaction, and no higher-severity label is justified;
- `stagnation`: the trace contains activity or repeated status without
  material advancement, including an obligation left open or abandoned;
- `regression`: an explicit obligation moved backward, was invalidated, or
  ended in a regressed state;
- `insufficient-evidence`: the available events do not support a reliable
  progress/stagnation/regression judgment.

Use the same precedence for the trace-level label as the deterministic Core:

```text
regression > insufficient-evidence > stagnation > progress
```

## Decision procedure

For each obligation, answer these questions in order:

1. Is there at least one signal or event that bears on this obligation?
   If not, label `insufficient-evidence`.
2. Is the final observed state `regressed`? Label `regression`.
3. Is the final observed state `abandoned` or still `open` without material
   advancement? Label `stagnation`.
4. Did the observed state improve from an earlier state, or reach
   `satisfied`? Label `progress`.
5. If the evidence is contradictory, missing, or too vague to distinguish the
   alternatives, label `insufficient-evidence` and record the ambiguity.

A message saying that work is complete is not evidence of completion unless
an event or signal in the trace supports it. Conversely, an explicit tool
result or ledger signal is evidence even when the assistant's prose is vague.

## Baseline annotations

Annotators independently record whether each baseline would recommend a halt
within the observed window:

- `max-turns`: halt at the configured limit;
- `exact-repeat`: halt when an identical event signature recurs;
- `fuzzy-repeat/cycle`: halt when the configured fuzzy detector reports a
  repeat or cycle.

This is a detector judgment, not a semantic label. A repeat can coexist with
`progress`; a max-turn halt can occur before semantic completion. Record the
first halt rank and the reason when visible.

## Required annotation record

Each evaluator submits one JSON or CSV row per case containing:

- `caseId`;
- `traceLabel`;
- `obligationLabels` (ordered by ledger obligation id);
- `maxTurnsHalt` and `maxTurnsRank`;
- `exactRepeatHalt` and `exactRepeatRank`;
- `fuzzyRepeatCycleHalt` and `fuzzyRepeatCycleRank`;
- `confidence`: `high`, `medium`, or `low`;
- `ambiguityNote`: short evidence-based note, empty only when there is no
  ambiguity.

No evaluator sees ProgressTrace output, another evaluator's labels, or the
fixture `groundTruth` before submitting an initial annotation.

## Adjudication and agreement

Two evaluators label independently. Disagreements are preserved, not silently
rewritten. A third adjudicator resolves only disagreements and records the
reason and final label. Report:

- raw agreement and Cohen's kappa for trace labels;
- per-label confusion matrix against the adjudicated oracle;
- agreement for each baseline halt decision;
- count and proportion of low-confidence or ambiguous cases.

If the rubric cannot distinguish a case without external context, keep the
adjudicated label `insufficient-evidence` rather than inventing context.

## Scope boundary

This rubric does not establish that the expanded synthetic corpus represents
production traces. It creates a reproducible annotation protocol. Claims of
accuracy, generalization, cost savings, or user value require an independent
oracle and external or realistically replayed traces.
