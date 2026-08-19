# ProgressTrace — roadmap, status, evidence și backlog

**Document de referință:** 2026-08-19  
**Repository:** `/srv/projects/progresstrace`  
**Branch analizat:** `task/benchmark-analysis`  
**HEAD la momentul analizei:** `3c54d93` — `docs: save ProgressTrace validation checkpoint`
**Commitul acestui document:** `e8edf77` — `docs: consolidate project roadmap and status`
**Stare working tree la verificare:** curat
**Relația cu `main`:** branch-ul conține 10 commituri peste `main`; `main`/`origin/main` sunt la `8474fba`.

Acest document separă:

1. ce ne-am propus inițial să construim;
2. ce este implementat efectiv;
3. ce am testat și ce demonstrează testele;
4. ce rămâne pe backlog;
5. ce nu trebuie implementat încă.

## 1. Rezumat executiv

ProgressTrace este un toolkit contract-first pentru capturarea, normalizarea,
evaluarea și compararea trace-urilor de agenți AI.

Ideea centrală este că simpla repetare de mesaje sau atingerea unei limite de
turnuri nu este suficientă pentru a spune dacă un agent a făcut progres. Un
trace trebuie analizat în raport cu obligații explicite și semnale corelate cu
evenimentele observate.

Nucleul implementat poate:

- valida un trace envelope canonical;
- normaliza determinist ordinea evenimentelor;
- evalua obligații explicite ca `progress`, `stagnation`, `regression` sau
  `insufficient-evidence`;
- evalua modul în care s-a terminat observația unui trace;
- calcula dacă terminarea a fost on-target, târzie sau înainte ca obligațiile să
  fie atinse;
- compara terminarea cu un buget de evenimente authored, cu delimitarea strictă
  că acesta este un proxy contrafactual, nu un fapt observat;
- rula benchmark-uri deterministe și baseline-uri simple;
- analiza proiecții redactate de workflow-uri reale printr-un adaptor experimental
  intern.

Concluzia actuală este:

> **Core-ul deterministic și pipeline-ul de analiză sunt funcționale. Valoarea
> demonstrată pe trace-uri reale este de integrare și diagnostic. Ingestia
> semantică și authoring-ul obligațiilor rămân experimentale; nu este justificat
> încă un adaptor universal, un produs hosted sau un layer complet de atestare
> criptografică.**

---

## 2. Ce ne-am propus să construim

### 2.1 Viziunea de produs

Product brief-ul descrie un produs pe termen de 3–5 ani, nu doar un MVP:

- contracte de trace versionate;
- adaptoare de ingestie pentru surse și framework-uri;
- un core deterministic, obligation-aware;
- corpusuri de benchmark și proceduri de evaluare;
- execuție locală, în CI și batch;
- comparație și raportare;
- extensii izolate prin procese sau plugin-uri;
- eventual istoric hosted, UI și colaborare.

Acesta este orizontul de produs, nu lista de lucruri care trebuiau implementate
imediat.

### 2.2 Claim-ul central

ProgressTrace trebuie să explice, pe baza evidenței observate:

- dacă obligațiile explicite au avansat;
- dacă agentul a produs evidență utilă;
- dacă trace-ul a stagnat;
- dacă a existat regression;
- cum se compară aceste semnale cu baseline-uri simple;
- ce cost estimativ are o oprire prea devreme sau prea târzie.

Claim-ul are limite deliberate:

- nu pretinde că rezolvă problema halting;
- nu pretinde înțelegere semantică universală a textului liber;
- nu interpretează automat payload text opac în core;
- nu prezintă bugete authored ca măsurători sau adevăruri observate;
- dacă nu există obligații și semnale explicite suficiente, rezultatul onest este
  `insufficient-evidence`.

### 2.3 Principii arhitecturale stabilite

- contract-first;
- modular monolith la început;
- un singur core normativ și deterministic;
- contracte JSON Schema versionate;
- separare între raw evidence, inputuri normalizate și rezultate derivate;
- inputurile externe sunt date neîncrezătoare, niciodată instrucțiuni;
- fără network/database/model calls în core;
- zero dependențe third-party runtime în vertical slice-ul canonical;
- .NET 10 pentru core și CLI;
- Python doar la granițe de adapter/research unde aduce valoare;
- TypeScript doar când există UI sau consumatori Node reali;
- adaptoarele și plugin-urile nu reimplementează semantica normativă;
- rezultatele deterministic checks au prioritate față de verdictul unui model;
- fără merge sau publicare automată.

### 2.4 Ce trebuia să rămână replaceable

Nu am vrut să fixăm prematur:

- framework-ul UI;
- topologia hosted;
- baza de date/query engine;
- provider-ul cloud;
- detaliile transportului de plugin-uri;
- limbajul de optimizare;
- tehnologia de rendering a rapoartelor.

---

## 3. Roadmap-ul inițial și starea pe faze

## 3.1 Phase 0 — canonical trace contract

### Obiectiv propus

Un vertical slice local, în .NET 10, pentru un trace envelope canonical:

1. parsare de JSON neîncrezător, fără network/database;
2. validare structurală și semantică;
3. normalizare deterministă a ordinii evenimentelor;
4. păstrarea provenance-ului sursei și a payload-ului source-specific;
5. diagnostice machine-readable stabile;
6. fixture-uri valide/invalide și conformance suite versionată.

### Implementat

- canonical trace envelope și JSON Schema;
- parser și validator deterministic;
- normalizare canonicală a evenimentelor;
- CLI `validate`;
- CLI `normalize`;
- diagnostice cu coduri/pointeri stabile;
- limite de input și fail-closed behavior;
- fixture-uri sintetice valide și invalide;
- conformance executable fără dependențe third-party runtime;
- integrare CI.

### Ce a fost explicit exclus

- obligation model;
- scoring progress/stagnation;
- adaptoare OpenTelemetry/framework;
- persistence Parquet/DuckDB;
- Python/TypeScript packages;
- web API/UI;
- LLM-as-judge;
- plugin execution;
- online interruption a agentului.

### Status

**Finalizat și integrat.**

---

## 3.2 Phase 1 — obligation ledger și evaluation result

### Obiectiv propus

Adăugarea unui model explicit de obligații și semnale, corelat cu event IDs din
trace, astfel încât evaluatorul să poată clasifica determinist:

- `progress`;
- `stagnation`;
- `regression`;
- `insufficient-evidence`.

### Implementat

- `ObligationLedger` 1.0;
- `EvaluationResult` 1.0;
- enum closed pentru statusuri:
  - `open`;
  - `in-progress`;
  - `satisfied`;
  - `regressed`;
  - `abandoned`;
- validare referențială trace/ledger;
- respingere pentru event IDs sau obligation IDs inexistente;
- reguli pentru abandon terminal și ordering;
- evaluator per-obligație;
- precedence la nivel de trace;
- normalizare byte-idempotentă;
- CLI `evaluate <trace-path> <ledger-path>`;
- fixture-uri golden și invalid cu coduri diagnostice exacte;
- conformance și CI checks.

### Limitare importantă

Ledger-ul conține obligații și semnale explicite, dar nu are în schema 1.0
provenance complet pentru fiecare afirmație. Core-ul nu pretinde că derivă
semantic progresul din textul liber.

### Status

**Finalizat și integrat.**

---

## 3.3 Phase 2a — termination declaration și stop assessment

### Obiectiv propus

Să distingem între progresul semantic și modul în care s-a terminat observația:

- de ce s-a oprit trace-ul;
- dacă termination event este valid și terminal;
- dacă obligațiile erau stabile la momentul opririi;
- dacă oprirea a fost on-target sau târzie;
- dacă un obiectiv a rămas neatins la terminare;
- dacă observația este incompletă.

### Implementat

- `TerminationDeclaration` 1.0;
- `StopAssessmentResult` 1.0;
- CLI `assess <trace> <ledger> <declaration>`;
- termination kinds closed:
  - `agent-self-reported-stop`;
  - `harness-declared-stop`;
  - `natural-completion`;
  - `external-cancellation`;
  - `timeout`;
  - `crash-or-error`;
  - `capture-truncated`;
  - `unknown`;
- `declarationSource` cu separarea producerului de evidence basis;
- reguli de coerență pentru producer/evidence/termination kind;
- `terminationAttested` ca commitment la un cause, nu ca scor de adevăr;
- stable attainment și rank-distance;
- clasificări:
  - `on-target`;
  - `late-termination`;
  - `unmet-target-at-termination-present`;
  - `incomplete-observation`;
- recomputarea internă a evaluării, fără acceptarea unui rezultat precomputat;
- fixture-uri golden/invalid și conformance;
- verificări CLI, schema și CI.

### Status

**Finalizat și integrat.**

---

## 3.4 Phase 2b — baseline comparison și authored counterfactual budget

### Obiectiv propus

Compararea opririi observate cu un baseline extern authored, pentru a cuantifica
un posibil cost de oprire prea devreme, fără a pretinde că viitorul trace-ului este
cunoscut.

### Implementat

- `BaselineDefinition` 1.0;
- `BaselineComparisonResult` 1.0;
- CLI `compare <trace> <ledger> <declaration> <baseline>`;
- provenance pentru sursa baseline-ului;
- coverage exactă a obligațiilor;
- coherence rules producer/evidence basis;
- diagnosticele `PT400`–`PT404`;
- applicability:
  - `incomplete-observation`;
  - `not-applicable-stable-attainment`;
  - `unused-authored-budget`;
  - `budget-exhausted-exactly`;
  - `budget-exceeded`;
- formula `observedEventCount = terminationRank + 1`;
- `authoredEstimateUnusedEventBudget`;
- rollup cu maximum, nu sumă;
- normalizare canonicală și byte-idempotentă;
- fixture-uri valid/golden/invalid;
- integrare în conformance și CI.

### Limitare semantică obligatorie

Un `eventBudget` authored nu este:

- măsurătoare observată;
- dovadă că trace-ul ar fi reușit dacă continua;
- dovadă că oprirea a fost evitabilă;
- adevăr despre viitorul contrafactual al run-ului.

Este doar un proxy derivat dintr-o estimare externă authored.

### Status

**Finalizat și integrat.**

---

## 3.5 Phase 3 — benchmark MVP

### Obiectiv propus

Cel mai mic benchmark necesar pentru a compara ProgressTrace cu baseline-uri
simple:

- aproximativ 20 de cazuri sintetice/local-controlled;
- baseline `max-turns`;
- baseline `exact-repeat`;
- baseline `fuzzy-repeat/cycle`;
- runner determinist;
- output machine-readable stabil;
- fără adaptor de producție, UI sau extindere prematură de scope.

### Implementat

- benchmark runner;
- evaluatorul ProgressTrace și baseline-urile rulează pe aceleași cazuri;
- output JSON stabil;
- ordering ordinal al cazurilor;
- verificare actual-versus-expected;
- ground truth authored explicit;
- teste pentru acoperirea branch-urilor și mismatch handling;
- determinism byte-cu-byte.

### Extinderea la 60 de cazuri

Au fost adăugate 40 de variante metamorfice:

- variații de identitate;
- variații de evidence text;
- păstrarea secvenței intenționate de statusuri;
- păstrarea branch-ului baseline.

Cele 40 de variante nu sunt 40 de eșantioane independente din producție.
Rezultatul `60/60` înseamnă consistență deterministă și metamorphic fixture
validation, nu acuratețe pe producție.

### Status

**Finalizat ca benchmark engineering/validation suite.**

Nu mai tratăm extinderea sintetică drept următorul obiectiv principal.

---

## 3.6 Blind evaluation și agreement analysis

### Ce ne-am propus

Să reducem riscul ca ground truth-ul authored din fixture-uri să fie confundat cu
adevăr semantic, folosind un pachet blind și evaluare independentă.

### Implementat

- pachet anonim `case-01` … `case-60`;
- eliminarea leakage-ului din nume precum `01-progress`;
- rubrică de annotation;
- workbook personal pentru evaluare;
- template-uri CSV separate;
- analyzer de agreement;
- normalizarea inputurilor evaluatorilor;
- păstrarea rezultatelor brute în afara repository-ului;
- manifest cu SHA-256 pentru artefactele de audit.

### Rezultate

Evaluări blind au fost executate de Claude, Codex și Gemini.

Agreement-ul dintre perechi:

| Pereche | traceLabel | maxTurns | exactRepeat | fuzzyRepeat/cycle |
|---|---:|---:|---:|---:|
| Claude–Codex | 54/60, κ=0,852 | 60/60 | 60/60 | 51/60, κ=0,688 |
| Claude–Gemini | 54/60, κ=0,855 | 54/60 | 48/60 | 33/60 |
| Codex–Gemini | 48/60, κ=0,710 | 54/60 | 48/60 | 42/60 |

Toți trei au o majoritate 2/3 pentru fiecare câmp în toate cele 60 de cazuri.
Majoritatea provizorie coincide cu ProgressTrace în:

- `traceLabel`: 60/60;
- `maxTurnsHalt`: 60/60;
- `exactRepeatHalt`: 60/60;
- `fuzzyRepeatCycleHalt`: 54/60.

### Ce nu s-a obținut

- nu există adjudicare finală;
- nu există oracle uman validat;
- nu există încă confusion matrix față de un oracle adjudicat;
- nu se pot declara accuracy, precision, recall sau generalizare;
- rubrică pentru baseline-urile fuzzy/cycle este insuficient de concretă;
- comportamentul Gemini pe baseline-uri arată o limitare de protocol, nu un adevăr
  despre corpus.

### Decizia

Nu se mai investește acum în adjudecarea manuală a tuturor celor 60 de cazuri sau
în optimizarea workbook-ului. Corpusul actual rămâne regression suite.

---

## 3.7 Reference adapter v0 și vertical slice real

### Obiectiv

Să testăm dacă pipeline-ul existent poate analiza proiecții redactate ale unor
workflow-uri reale Claude/Codex/Hermes fără a modifica core-ul.

### Implementat

În `experiments/reference-adapter-v0/` există 5 manifesturi concise, redactate,
provenite din rulări reale de dezvoltare ProgressTrace:

1. architecture discovery;
2. task-contract materialization;
3. build cu path-policy defect;
4. repair după defect;
5. semantic review, integration și hosted verification.

Adaptorul generează inputurile canonice existente:

- `trace.json`;
- `ledger.json`;
- `declaration.json`;
- `baseline.json`.

.NET CLI rămâne singurul evaluator normativ.

### Rezultat

- `5/5` cazuri completate;
- `15/15` etape CLI completate;
- toate apelurile `evaluate`, `assess` și `compare` au ieșit cu succes;
- testele adapterului: `5/5 PASS`.

### Ce a demonstrat

1. Pipeline-ul canonical poate procesa proiecții de workflow real.
2. Nu a fost necesară nicio modificare în core, schema sau CLI.
3. Core-ul a separat progresul, regression și recovery.
4. Oprirea la build-ul defect a rămas un unmet target, nu un false halt inventat.
5. Provenance coherence este verificabilă.
6. `late-termination` este determinist și explicabil.

### Fricțiunea principală

Metadata de lifecycle și digest-urile sursei nu sunt suficiente pentru a deriva
automat:

- obligațiile;
- semnalele;
- baseline-ul counterfactual;
- uneori declarația semantică de termination.

Acestea au necesitat authoring explicit.

### Decizia

Adaptorul rămâne:

```text
experimental / internal / v0
```

Nu este încă:

- stable adapter schema;
- general ingestion framework;
- compatibilitate OpenTelemetry/Hermes;
- field-level lineage system;
- UI sau hosted product.

---

## 4. Ce am testat efectiv

## 4.1 Verificări core/CLI/conformance

Au fost verificate repetat:

- build Release cu `--warnaserror`;
- conformance Phase 0;
- conformance Phase 1;
- conformance Phase 2a;
- conformance Phase 2b;
- JSON Schema validity;
- CLI `validate`;
- CLI `normalize`;
- CLI `evaluate`;
- CLI `assess`;
- CLI `compare`;
- diagnostice, pointeri și exit codes;
- fixture discovery;
- canonical serialization;
- normalization idempotence;
- determinism;
- `dotnet format --verify-no-changes`;
- `git diff --check`.

Ultima verificare locală a returnat:

```text
Build: PASS, 0 warnings, 0 errors
Conformance: PASS
Benchmark: 60/60 matched
Benchmark repeated output: byte-identical
Reference adapter tests: 5/5 PASS
Reference adapter CLI stages: 15/15 PASS
Format verification: PASS
Repository: clean
```

## 4.2 Testele benchmarkului

Cele 60 de cazuri acoperă:

- progress;
- insufficient evidence;
- regression;
- stagnation;
- max-turn halt;
- exact repeat;
- fuzzy repeat;
- cycles AB și ABC;
- no-repeat;
- late max-turn;
- repeated progress;
- empty signal;
- recovery după regression;
- mixed obligations;
- clean completion.

Rezultat:

```text
caseCount: 60
matchedCaseCount: 60
mismatchedCaseCount: 0
status: pass
```

Interpretarea corectă este consistență față de authored expectations, nu
acuratețe independentă.

## 4.3 Testele adapterului real

Fiecare dintre cele 5 cazuri a trecut prin:

```text
manifest
→ generate canonical inputs
→ evaluate
→ assess
→ compare
→ retain outputs and summary
```

Rezultat:

```text
caseCount: 5
successfulCaseCount: 5
stageCount: 15
successfulStageCount: 15
```

---

## 5. Artefacte importante

### Contracte și documentație

- `docs/product-brief.md`
- `docs/security.md`
- `docs/architecture/ADR-0001-contract-first-modular-monolith.md`
- `docs/architecture/ADR-0002-obligation-and-evaluation-contracts.md`
- `docs/architecture/ADR-0003-phase-2-architecture-and-stop-assessment.md`
- `docs/architecture/ADR-0004-baseline-comparison-and-authored-estimate.md`
- `docs/architecture/ADR-0005-external-review-and-clean-room-reproducibility.md`
- `docs/architecture/ADR-0006-session-provenance-and-attestation.md`
- `docs/project-status-checkpoint-2026-08-19.md`
- `docs/benchmark-analysis.md`
- `docs/benchmark-corpus-expansion.md`
- `docs/benchmark-annotation-rubric.md`

### Task contracts

- `tasks/phase-0-canonical-trace.json`
- `tasks/phase-1-obligation-evaluation.json`
- `tasks/phase-2a-stop-assessment.json`
- `tasks/phase-2b-baseline-comparison.json`
- `tasks/phase-3-benchmark-mvp.json`
- `tasks/phase-3-ground-truth.json`

### Benchmark

- `fixtures/benchmarks/`
- `src/ProgressTrace.Benchmarks/`
- `tests/ProgressTrace.BenchmarksTests/`
- `annotations/`

### Trace-uri reale redactate

- `experiments/reference-adapter-v0/cases/`
- `experiments/reference-adapter-v0/adapter.py`
- `experiments/reference-adapter-v0/run_dogfood.py`
- `experiments/reference-adapter-v0/RESULTS.md`
- `experiments/reference-adapter-v0/FRICTION.md`
- `experiments/reference-adapter-v0/outputs/`

Artefactele brute de audit pentru evaluarea blind sunt în afara repository-ului:

```text
/srv/projects/.tooling/audit/progresstrace-benchmark-validation-20260819/
```

---

## 6. Backlog curent

## P0 — alinierea documentației cu starea reală

Checkpoint-ul a fost scris înainte de finalizarea explicită a rulării celor 5
cazuri reale. Trebuie actualizate documentele de status pentru a reflecta:

- vertical slice-ul real finalizat;
- `5/5` cazuri;
- `15/15` etape CLI;
- concluzia că adapterul rămâne experimental;
- faptul că următoarea decizie este despre authoring friction și manifest, nu despre
  primul run real.

Acest document este primul artefact de aliniere.

## P0 — review și integrare Git

Branch-ul curent are 10 commituri peste `main` și este curat, dar nu este integrat.
Rămâne necesar:

1. review al diff-ului agregat față de `main`;
2. verificarea documentelor și artefactelor permise;
3. PR/merge prin human gate;
4. actualizarea `main` numai după decizia lui Dragos.

Nu se face merge automat și nu se publică direct în `main`.

## P1 — măsurarea authoring friction pe vertical slice

Trebuie documentat sistematic pentru cele 5 cazuri:

- câte câmpuri au fost completate manual;
- ce informații au putut fi extrase mecanic;
- ce informații au rămas nejustificate de sursă;
- cât timp/efort a consumat proiecția;
- ce output a fost util pentru diagnostic;
- ce output a schimbat sau ar putea schimba o decizie operațională;
- unde schema v0 a fost incomodă sau ambiguă.

## P1 — decizie privind manifestul intern

După măsurarea fricțiunii, alegem una dintre variante:

### Continue

- stabilizăm un manifest minimal intern;
- păstrăm obligațiile și semnalele explicit authored;
- mecanizăm doar extragerea sigură de lifecycle metadata și source digests;
- adăugăm teste pentru manifest;
- păstrăm core-ul .NET ca evaluator unic.

### Narrow

- limităm adaptorul la un singur controller/export;
- nu promitem compatibilitate universală;
- păstrăm restul câmpurilor ca authoring explicit;
- documentăm exact limitele sursei.

### Stop/archive

- păstrăm core-ul și fixture-urile;
- arhivăm adaptorul ca experiment;
- nu investim în contract de ingestie stabil dacă authoring-ul rămâne prea costisitor.

## P1 — cazuri reale suplimentare doar dacă aduc informație nouă

Cele 5 cazuri sunt suficiente pentru concluzia de integrare. Nu este necesar să
forțăm un număr de 10.

Dacă dorim o decizie mai robustă, următoarele cazuri ar trebui să fie diferite
semantic, nu duplicate:

- `insufficient-evidence` autentic;
- abandon;
- `capture-truncated`;
- oprire prematură;
- baseline authored cu buget contrafactual relevant.

## P2 — adjudicare formală a blind evaluation

Aceasta rămâne opțională și nu este următorul pas operațional imediat.

Este necesară doar dacă vrem să susținem public sau formal:

- accuracy;
- precision/recall;
- false-halt rate;
- late-halt cost;
- generalization.

Ar necesita:

1. clarificarea rubricii pentru baseline-urile fuzzy/cycle;
2. doi evaluatori independenți reali sau un protocol acceptat;
3. adjudicator;
4. oracle final;
5. confusion matrices;
6. metrici calculate față de oracle.

Nu trebuie prezentată majoritatea actuală 2/3 drept oracle.

## P2 — posibilă stabilizare a baseline detectors

`fuzzyRepeatCycle` are agreement-ul cel mai slab și rămâne experimental.
Înainte de optimizare trebuie decis dacă detectorul este:

- definit suficient de clar pentru evaluare;
- un baseline de cercetare sau unul operațional;
- util pentru decizie sau doar pentru explorare.

Nu se modifică pragurile doar pentru a potrivi evaluatorii existenți.

---

## 7. Lucruri propuse, dar amânate intenționat

### Adaptor OpenTelemetry/Hermes stabil

Amânat. Adapterul v0 a demonstrat integrarea, dar nu justifică încă un contract
universal sau un framework de ingestie.

### Provenance/attestation layer complet

ADR-0006 este `Proposed`, neimplementat.
Nu se implementează încă:

- hash chain canonical;
- Merkle root;
- cheie externă workspace-ului agentului;
- semnătură;
- timestamp anchoring;
- provider receipt;
- verifier independent.

### UI, hosted service și persistent storage

Rămân în afara scope-ului actual. Nu există încă dovadă că output-ul core este
suficient de stabil și valoros pentru a justifica aceste componente.

### Plugin execution și online interruption

Nu fac parte din vertical slice-ul actual.

### Semantic inference din payload text

Nu se adaugă în core fără o decizie arhitecturală nouă. Core-ul trebuie să rămână
onest atunci când evidența structurată lipsește.

### Product alpha / product-market fit

Nu sunt justificate de rezultatele actuale.

---

## 8. Ordinea recomandată de lucru de aici

1. **Integrare documentară:** actualizează checkpoint-ul și README-ul pentru
   rezultatul real `5/5`, `15/15`.
2. **Review Git:** review și human gate pentru branch-ul cu cele 10 commituri.
3. **Authoring-friction report:** măsoară concret costul proiecției celor 5 cazuri.
4. **Decizie manifest v0:** continue, narrow sau archive.
5. **Doar dacă decizia este continue:** implementează mecanizarea lifecycle/source
   metadata pentru o singură sursă/controller export.
6. **Adaugă cazuri reale noi numai pentru acoperirea unor situații neacoperite.**
7. **Reevaluează după datele operaționale:** utilitate, cost, limite și valoare
   distinctă față de baseline-uri.
8. **Abia ulterior:** adaptor mai stabil, raportare mai bogată sau un nou ADR de
   provenance.

Ordinea nu trebuie inversată în:

```text
adaptor universal → UI/hosted → încercăm să demonstrăm valoarea
```

Ci:

```text
core deterministic
→ benchmark regression suite
→ vertical slice real
→ măsurare authoring friction/utilitate
→ manifest limitat, dacă este justificat
→ adaptor mai stabil
→ eventual raportare/hosted
```

---

## 9. Concluzia de proiect

ProgressTrace a trecut de etapa de prototip pur contractual:

- contractele principale există;
- evaluatorul este deterministic;
- stop assessment și baseline comparison funcționează;
- benchmarkul de 60 de cazuri este reproductibil;
- blind evaluation a oferit un semnal de agreement, dar nu un oracle;
- 5 proiecții reale au trecut prin pipeline-ul complet.

Proiectul nu a demonstrat încă o soluție universală de semantic-progress
inference. A demonstrat însă ceva mai precis și util:

> **Dacă obligațiile, semnalele și contextul de termination sunt explicit
> authorate sau furnizate de un controller/harness, ProgressTrace poate produce
> evaluări și comparații deterministe, auditabile și explicabile pe un workflow
> real. Întrebarea deschisă este dacă acest authoring poate fi redus suficient
> pentru a avea valoare operațională repetabilă.**

Aceasta este întrebarea care trebuie să ghideze următorul increment.
