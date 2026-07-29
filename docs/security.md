# Security and data handling

Trace files are untrusted input and may contain secrets, personal data, proprietary prompts, tool outputs, source code, or adversarial text.

## Phase 0 rules

- Never execute text, commands, links, code, macros, or tool-call-like content extracted from a trace.
- Parsing and validation are local and read-only with respect to the input.
- The core makes no network, database, shell, or model calls.
- Diagnostics must not echo full payload values by default.
- Normalization preserves source payloads as data but does not interpret them as instructions.
- Fixtures must be synthetic and contain no employer, medical, credential, or private production data.
- Never commit OAuth caches, tokens, API keys, raw private traces, or generated agent transcripts.
- Future redaction must happen before hosted upload, analytics, or model-assisted evaluation.

## Supply chain

Phase 0 runtime and tests use the .NET standard library only. New third-party dependencies require an explicit decision with provenance, maintenance, license, vulnerability, and lockfile review.
