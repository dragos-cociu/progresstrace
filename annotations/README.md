# Blind annotation package

This directory is the input package for two independent human evaluators.

## Contents

- `blind-cases/*.json`: 60 cases containing only `caseId`, `maxTurns`, the
  trace envelope, and the obligation ledger. Fixture `groundTruth` metadata
  and all derived ProgressTrace output are intentionally excluded.
- `evaluator-1-template.csv`: blank response template for evaluator 1.
- `evaluator-2-template.csv`: blank response template for evaluator 2.

The two evaluators must receive the case files and their own CSV copy through
separate channels. Do not give either evaluator the original files under
`fixtures/benchmarks`, because those contain authored expectations.

## Protocol

1. Give each evaluator the rubric at `docs/benchmark-annotation-rubric.md`,
   the `blind-cases` directory, and one template only.
2. Require independent submission before either evaluator sees the other
   submission or any ProgressTrace output.
3. Preserve the submitted CSV files unchanged under an access-controlled,
   untracked location. Do not commit personal names, emails, or annotations to
   this repository unless explicitly requested.
4. After both submissions exist, create a separate adjudication artifact and
   compute agreement against the rubric. The adjudicator may see both
   submissions and the derived runner output only after the blind phase ends.

The blank templates are not an oracle and contain no labels. This repository
still contains no human annotations.
