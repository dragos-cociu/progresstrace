# Blind annotation package

This directory is the input package for two independent human evaluators.

## Contents

- `blind-cases/*.json`: 60 cazuri anonimizate, cu identificatori `case-01` ... `case-60`, conținând doar `caseId`, `maxTurns`, trace envelope și obligation ledger. Numele originale și orice rezultat derivat sunt excluse.
- `evaluator-1-template.csv`: blank response template pentru evaluator 1.
- `evaluator-2-template.csv`: blank response template pentru evaluator 2.

**Important:** pachetele și răspunsurile generate înainte de anonimizare nu mai sunt considerate blind, deoarece identificatorii originali conțineau uneori etichete semantice. Pentru orice evaluare nouă se folosește numai pachetul anonimizat curent.
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

## Agreement analysis

After both evaluators have completed their copies outside the repository, run:

```sh
python3 annotations/analyze_agreement.py \
  --evaluator-1 /secure/path/evaluator-1.csv \
  --evaluator-2 /secure/path/evaluator-2.csv \
  --output /secure/path/agreement.json
```

The tool validates the schema and labels, requires matching case sets, reports
per-field agreement and Cohen's kappa, lists disagreements, and marks the
result `oracleStatus: not-adjudicated`. It rejects blank templates and does
not produce an oracle or accuracy metrics by itself.
