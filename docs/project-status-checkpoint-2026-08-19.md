# ProgressTrace — checkpoint de stare și decizie

**Data checkpointului:** 2026-08-19  
**Branch:** `task/benchmark-analysis`  
**HEAD:** `df0e845`  
**Repository:** curat la momentul checkpointului

## Decizia curentă

**Continue, narrowly.** Nu se mai investește acum în extinderea benchmarkului sintetic,
adjudecare manuală pentru toate cele 60 de cazuri sau încă o rundă de optimizare a
workbook-ului. Corpusul actual devine regression suite. Următorul increment este un
vertical slice pe 5–10 trace-uri reale, redactate, din workflow-uri Claude/Codex/Hermes.

## Ce este verificat

### Nucleu și benchmark determinist

- 60 de cazuri sintetice: 20 originale și 40 de variante metamorfice.
- Rulare ProgressTrace: `60/60 matched`, `0` mismatch-uri, status `pass`.
- Build Release, teste de conformance, determinism și verificări de format: trecute
  în rundele anterioare.
- Rezultatul `60/60` validează reproducerea etichetelor authored din fixture-uri; nu
  este oracle independent și nu dovedește acuratețe pe date reale.

### Evaluări blind curate

Pachetul vechi avea leakage prin nume precum `01-progress` și este invalid ca blind.
Pachetul curent folosește doar `case-01` … `case-60`.

Evaluatori rerulați pe pachetul anonim:

- Claude: CSV valid, 60/60 cazuri;
- Codex: CSV valid, 60/60 cazuri;
- Gemini: API Google Direct, `gemini-3.5-flash-lite`, CSV valid, 60/60 cazuri.

Gemini a fost rulat prin aceeași familie de componente folosită pentru external review:
`countTokens` preflight, apoi un apel stateless `generateContent`, JSON schema,
`temperature=0`, fără tools.

### Agreement curat

| Pereche | traceLabel | maxTurns | exactRepeat | fuzzyRepeat/cycle |
|---|---:|---:|---:|---:|
| Claude–Codex | 54/60, κ=0.852 | 60/60, κ=1.000 | 60/60, κ=1.000 | 51/60, κ=0.688 |
| Claude–Gemini | 54/60, κ=0.855 | 54/60 | 48/60 | 33/60 |
| Codex–Gemini | 48/60, κ=0.710 | 54/60 | 48/60 | 42/60 |

Cei trei au cel puțin o majoritate de 2/3 în toate cele 60 de cazuri pentru fiecare
câmp. Acordul tuturor celor trei este:

- `traceLabel`: 48/60;
- `maxTurnsHalt`: 54/60;
- `exactRepeatHalt`: 48/60;
- `fuzzyRepeatCycleHalt`: 33/60.

Gemini a marcat toate cele 60 de cazuri `false` pentru cele trei baseline-uri. Aceasta
este tratată ca limitare a protocolului/rubricii pentru detectorii mecanici, nu ca
adevăr despre corpus. Rubrica nu a definit suficient de concret semnătura exactă,
fereastra și pragurile fuzzy/cycle pentru un evaluator extern.

### Comparație cu majoritatea provizorie

Output-ul determinist ProgressTrace coincide cu majoritatea provizorie a celor trei:

- `traceLabel`: 60/60;
- `maxTurnsHalt`: 60/60;
- `exactRepeatHalt`: 60/60;
- `fuzzyRepeatCycleHalt`: 54/60.

Majoritatea este **provisional-majority**, nu oracle adjudecat. Nu se fac afirmații de
acuratețe sau generalizare pe baza ei.

## Artefacte persistente

Artifactele brute și manifestul lor sunt păstrate în afara repository-ului:

```text
/srv/projects/.tooling/audit/progresstrace-benchmark-validation-20260819/
```

Manifestul include SHA-256 pentru cele 146 de fișiere păstrate. Subdirectoare:

- `blind-evaluation-clean/claude/`;
- `blind-evaluation-clean/codex/`;
- `blind-evaluation-clean/normalized/`;
- `gemini-evaluation/`;
- `authored-benchmark-run.json` — separat, deoarece conține `groundTruth` authored;
- `MANIFEST.json`.

Rezultatele vechi din pachetul cu leakage nu sunt folosite ca dovezi blind.

## Proveniență și atestare

A fost adăugat:

```text
docs/architecture/ADR-0006-session-provenance-and-attestation.md
```

Statusul este `Proposed`, neimplementat. Propune un Evidence/Provenance Layer cu:

- jurnal canonical;
- hash chain și Merkle root;
- semnătură cu cheie din afara workspace-ului agentului;
- timestamp/ancorare externă;
- receipt provider;
- verificator read-only independent.

Hash-urile locale existente oferă integritate locală și trasabilitate, nu dovadă
infalsificabilă că un provider a primit exact payload-ul sau că jurnalul nu a fost
rescris înainte de hashing.

## Ce NU se face acum

- nu se cere evaluare manuală a celor 60 de cazuri;
- nu se extinde corpusul sintetic;
- nu se implementează încă layer-ul complet de atestare criptografică;
- nu se construiește încă adaptor OpenTelemetry sau produs hosted;
- nu se declară oracle uman, acuratețe sau product-market fit.

## Următorul increment aprobat conceptual

1. adaptor minimal pentru trace-uri reale Claude/Codex/Hermes;
2. 5–10 rulări reale redactate, fără credentiale sau date sensibile;
3. ingestie → normalizare → obligation ledger → ProgressTrace → raport;
4. măsurarea authoring-ului manual și a utilității operaționale;
5. reevaluarea deciziei după date reale.

Detectorul `fuzzyRepeatCycle` rămâne experimental și poate fi calibrat ulterior; nu
blochează următorul vertical slice.
