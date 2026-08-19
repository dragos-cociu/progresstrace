# ProgressTrace — roadmap, status, evidence și backlog

**Document de referință:** 2026-08-19  
**Repository:** `/srv/projects/progresstrace`  
**Branch analizat:** `task/benchmark-analysis`  
**HEAD la momentul analizei:** `d65e2e2` — `docs: consolidate project roadmap and status`
**Stare working tree la verificare:** curat
**Relația cu `main`:** branch-ul conține 11 commituri peste `main`; `main`/`origin/main` sunt la `8474fba`.

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

## 6. Phase 4 — utilizare operațională în pipeline-ul Hermes

Directiva Phase 4 înlocuiește backlog-ul P2 și schimbă scopul proiectului de la
validare externă la dogfooding operațional.

### Decizii închise

- **Narrow:** singura sursă de ingestie este gate log-ul Hermes.
- Nu se promite compatibilitate universală, OpenTelemetry sau adaptor generic.
- Track-ul de adjudicare/oracle al celor 60 de cazuri este închis.
- Corpusul de 60 de cazuri rămâne exclusiv regression suite în CI.
- Nu se adaugă cazuri sintetice noi și nu se ajustează praguri pentru agreement.
- `fuzzyRepeatCycle` este explorator; raportarea implicită folosește `maxTurns`
  și `exactRepeat`.
- Review-ul extern adversarial este opțional și non-blocant.
- Dogfooding-ul produce issue-uri de produs, nu verdicte `continue/stop` despre
  viabilitatea proiectului.

### Schimbarea de surse

În Phase 4, inputurile nu mai sunt reconstruite retrospectiv din conversație:

| Input canonic | Sursa Phase 4 |
|---|---|
| `ledger.json` | task contract-ul scris înainte de rulare |
| semnale | gate log Hermes și verdicte deterministe |
| `declaration.json` | motivul de terminare raportat de orchestrator |
| buget operațional | consum observat: invocări, tokeni, timp |

Obligațiile se citesc din plan, iar semnalele din verificări deterministe. Core-ul
nu primește interpretare de text liber și rămâne offline, fail-closed și fără
dependențe runtime terțe.

### Taskuri Phase 4

#### Task 4.1 — Contract de sesiune și tentative

De implementat:

- contract `AgentSession` 1.0;
- identificator de sesiune;
- listă ordonată de invocări;
- mapare invocare → obligații vizate;
- număr de tentativă;
- ordering determinist între invocări;
- evaluare la nivel de sesiune;
- detecție de tentativă repetată fără avans;
- fixture-uri valide/invalide, diagnostice, conformance și CI.

Excluderi: fără persistență, network sau UI.

#### Task 4.2 — Ingestie Hermes-native din gate log

De implementat:

- contract `GateOutcome` 1.0;
- comandă, exit code, timestamp, obligație vizată și digest sursă;
- verdict închis: `pass`, `fail`, `skipped`, `error`;
- emitere după fiecare verificare deterministă Hermes;
- adaptor gate log → trace events + signals;
- provenance explicit pentru câmpurile derivate;
- câmpurile nederivabile rămân authored și marcate ca atare.

Excluderi: fără parsing conversațional și fără OpenTelemetry.

#### Task 4.3 — Generator de ledger din task contract

De implementat:

- `tasks/phase-*.json` → `ObligationLedger` 1.0;
- trasabilitate pentru fiecare obligație către clauza task contract-ului;
- raport de coverage: derivat automat, manual, fără suport în sursă;
- validare referențială cu core-ul existent.

#### Task 4.4 — Assessment in-flight advisory

De implementat:

- CLI `advise <session> <ledger>` fără declarație de terminare;
- output cu `continue`, `stop-recommended` sau `insufficient-evidence`;
- motiv corelat cu obligații și semnale;
- fail-closed: lipsa evidenței produce `insufficient-evidence`;
- recomandarea nu întrerupe automat nimic.

#### Task 4.5 — Observed budget

De implementat:

- contract `ObservedBudget` 1.0 pentru invocări, tokeni și timp;
- comparație între costul observat și avansul obligațiilor;
- folosirea lui în contextul operațional în locul dependenței de `eventBudget`;
- păstrarea `eventBudget` pentru benchmark, cu limitarea semantică actuală.

#### Task 4.6 — Integrare în shadow mode

De implementat:

- hook Hermes după fiecare gate;
- apel `advise` și persistarea rezultatului;
- verdict ProgressTrace comparat cu decizia reală;
- shadow mode obligatoriu: rezultatele se înregistrează, nu acționează;
- trecerea la mod activ rămâne o decizie separată, ulterioară.

### Ordine obligatorie

```text
4.1 + 4.2
→ 4.3
→ 4.4
→ 4.5
→ 4.6
```

4.1 și 4.2 trebuie proiectate împreună, deoarece schema de sesiune determină
forma evenimentelor provenite din gate log.

### Bucla de feedback

Fiecare task Hermes devine o sesiune observată:

```text
task contract
→ ledger generat
→ gate log → sesiune + semnale
→ advise în shadow mode
→ feedback.json
```

Nicio sesiune nu se închide fără `feedback.json`. Acesta trebuie să noteze:

- câmpuri indisponibile la momentul utilizării;
- authoring manual și motivul;
- ambiguități de schemă;
- `advise` versus decizia reală;
- output-uri utile;
- output-uri neacționabile.

Fiecare observație devine issue de produs sau trebuie închisă explicit cu
confirmarea că nimic nu a lipsit.

### Indicatori de progres

Sunt ținte de îmbunătățire, nu porți de arhivare:

- proporția câmpurilor de ledger derivate automat;
- efortul manual per sesiune;
- sesiuni cu verdict `advise` acționabil;
- diferențe între verdictul ProgressTrace și decizia umană;
- issue-uri de contract deschise și închise.

---

## 7. Lucruri în afara Phase 4

Rămân în afara scope-ului:

- layer complet de attestation din ADR-0006;
- UI;
- serviciu hosted;
- persistence;
- plugin execution;
- întrerupere online automată;
- inferență semantică din payload text în core;
- adaptor universal sau OpenTelemetry;
- product-market-fit claims;
- adjudicare formală a celor 60 de cazuri;
- oracle uman și confusion matrices pentru corpusul sintetic.

---

## 8. Concluzia de proiect

ProgressTrace are acum un core deterministic implementat și verificat, un benchmark
regression suite de 60 de cazuri și un adaptor experimental care a trecut prin
5 proiecții reale și 15 etape CLI.

Phase 4 nu mai încearcă să demonstreze dacă proiectul trebuie continuat. El
folosește proiectul în propriul pipeline pentru a descoperi cerințe de produs.

Întrebarea operațională devine:

> **Cât dintr-o sesiune ProgressTrace poate fi derivat în mod sigur din task
> contract și gate log, fără authoring retrospectiv și fără inferență semantică
> în core?**

Răspunsul trebuie obținut prin implementarea în ordine a taskurilor 4.1–4.6,
cu shadow mode obligatoriu și cu `feedback.json` pentru fiecare sesiune.
