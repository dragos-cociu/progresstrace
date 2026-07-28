# ProgressTrace

ProgressTrace is a contract-first toolkit for capturing, normalizing, evaluating, comparing, and replaying AI-agent execution traces.

Its differentiator is not generic loop detection. ProgressTrace is intended to explain whether an agent reduced explicit task obligations, obtained useful evidence, stagnated, or regressed, while measuring the cost of false halts and late halts against simpler baselines.

## Current phase

**Phase 0: Canonical Trace Contract**

The first vertical slice stabilizes a versioned trace envelope, deterministic validation and normalization, conformance fixtures, and a local .NET 10 CLI. It intentionally does not implement evaluation, scoring, storage, UI, hosted services, or Python adapters yet.

## Architectural direction

- contract-first;
- modular monolith initially;
- one normative deterministic core;
- .NET 10 for core, CLI, and future backend;
- Python only at adapter/research boundaries where its ecosystem adds value;
- TypeScript only when UI or Node consumers exist;
- out-of-process extensions at untrusted or polyglot boundaries;
- deterministic checks outrank model verdicts;
- no automatic merge or publication.

See `docs/product-brief.md` and `docs/architecture/ADR-0001-contract-first-modular-monolith.md`.
