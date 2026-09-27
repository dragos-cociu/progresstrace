# ProgressTrace — Phase 5: plan, scope și metrici de decizie

**Data:** 2026-09-07
**Autor:** review independent (Claude Code), la cererea lui Dragos Cociu
**Baseline analizat:** `main` @ `cd39f89`, plus arhiva `/srv/projects/progresstrace-experiments` @ `2c00117`
**Statut:** propunere pentru human gate. Nu autorizează implementare.

## Status operațional — 2026-09-09

- **P0.1–P0.6 ProgressTrace:** implementate și integrate în `main` prin PR #13; `origin/main` = `23b741442873f3cc9b2720ea39b49c8d46465c84`.
- **P0.4 Hermes gateKey:** implementat și verificat separat; branch local/remote Hermes = `phase5/p04-hermes-gatekey-current`, SHA `eb355c30671c5dbcb7b108706e405d5abed3e42e`.
- **P0.4b Hermes captureBinding:** implementat în worktree separat, verificat cu Gemini și testele independente; SHA local `ea1667f229b1212aee03744524f4ba4d7c261d62`. PR #1 este deschis în fork. **Preflight live a găsit un blocker:** ingress-ul CLI/oneshot nu transmite încă explicit `taskContractId` și `traceId` către `AIAgent`; prin urmare o sesiune CLI normală nu poate produce `captureBinding` complet. Pilotul rămâne oprit fail-closed până la incrementul de ingress.
- **P0.4c Hermes ingress binding:** implementat în același worktree candidat, cu `taskContractId` și `traceId` fixe pe durata sesiunii/agentului, transmise explicit prin oneshot, CLI, foreground/background gateway și follow-up-uri; hygiene agentul și plumbing-ul P0.4b au rămas neschimbate. Commit local `5de2ab66b75c90ae48896598d6398ea4f7d46da2`. Verificare independentă: `285 passed`, carry-forward `101 passed`, `compileall PASS`, `git diff --check PASS`, protected paths unchanged. Gemini direct `approve`, `findings: 0`, `unverified_assumptions: 0`; nu există încă push/merge.
- **Pilot live:** prima sesiune a fost executată, dar exclusă semantic: finalizarea și `captureBinding` lipseau, iar cwd-ul Hermes nu avea `node_modules`; receiverul și hook-ul temporar au fost eliminate și verificate.
- **Architecture pass ingress CLI:** relansat cu metoda salvată funcțională — `/usr/bin/claude -p --permission-mode plan --output-format json --allowedTools Read,Glob,Grep`, fără `--max-turns`, cu timeout extern 600s. Claude a returnat `ARCHITECTURE_READY`, `terminal_reason: completed`, `num_turns: 38`, model `claude-sonnet-5`, fără modificări. A confirmat că ingress-ul real lipsește și a delimitat incrementul minim la constructorii CLI/oneshot/gateway.
- **P0.4d Hermes finalization + explicit CLI ingress:** implementat în worktree, cu finalizare bound-uită în CLI, gateway și TUI și flag-urile explicite `--task-contract-id` / `--trace-id`. Commit local `3a25eb7ac49fa405ba9cf0d9a505a8eb47ac06c5`; `629` teste de increment + `280` teste run_agent, `compileall PASS`, `git diff --check PASS`, protected paths unchanged. Gemini direct `approve`, `findings: 0`; singura presupunere neverificată este că review-ul read-only nu execută testele. Push efectuat și PR #1 merged în staging; merge-ul în `main` rămâne separat.
- **Pilot live post-P0.4d — corrected gate-runner batch:** primele probe directe au fost păstrate ca evidence de transport/lifecycle și au avut `matchedGateCount=0/10` deoarece au omis wrapperul. Batch-ul corect s16–s20 a rulat toate cele 5 sesiuni reale cu `gate_runner.py` și task contractul real `sibiul-nestiut-method-page`: `sibiul.check` și `sibiul.build` au exit code 0 în fiecare sesiune, `matchedGateCount=10/10`, `unmatchedPostToolCallCount=0/1`, `on_session_finalize=5/5`, binding/HMAC/redacție/cwd/producer sequence valide și `5` feedback-uri JSON. M1=1, M2=100%, M5=0, M6=0%, M7=100%, M8=0/5, M9=2 proiecte, M10 deterministic PASS; M3 are un probe controlat verde de `0,3` minute wall-clock pentru authoring-ul unui contract nou (istoricul celor 5 sesiuni Sibiul rămâne nemăsurat retroactiv), iar M4=0/5 (`false`). Obligation-aware pilotul este PASS; recomandarea operațională este `FREEZE` dacă M3 este acceptat ca probă reprezentativă, deoarece M1–M3 sunt sub prag și M4 nu aduce informație peste exit codes. Verdictul final GO/FREEZE/PIVOT rămâne separat de merge-ul staging.
- **Al doilea workload M9:** `/tmp/progresstrace-cli-pilot` la `origin/main` `23b741442873f3cc9b2720ea39b49c8d46465c84`, read-only. Release CLI build, conformance build și conformance run au trecut prin gate_runner explicit; `matchedGateCount=3/3`, finalizare și sequence valide, worktree nemodificat. Feedback: `/srv/projects/.tooling/progresstrace-shadow/live-captures-20260910-progresstrace-cli/default/20260910_112749_d4d1ec/feedback.json`.
- **Staging merge:** PR #1 a fost merged în fork-ul `dragos-cociu/hermes-agent`, branch `phase5/hermes-base-local-20260909`, cu merge commit `b2523af8a69b9a58daa66ab24eb93a729176adf9`. `main` nu a fost modificat. Integrarea în `main` rămâne human gate separat.
- **Verificare independentă a pilotului — 2026-09-10:** afirmațiile de mai sus au fost reproduse direct din `origin/main` și de pe disc. Build, conformance, P0.1, P0.6 și transportul Hermes: **confirmate**. Recomandarea `FREEZE` însă **nu este susținută de date**: Core-ul nu a rulat pe niciuna dintre cele 6 sesiuni, M4 și M8 erau imposibile prin construcția pilotului, M2/M3/M6 sunt tautologice sau nemăsurate, iar M7=100% este roșu după propria regulă din §6.2. Detalii, dovezi și experimentul lipsă: **§11**. Protocolul obligatoriu pentru următoarea serie de testare, inclusiv dovada că produsul a fost chiar invocat: **§12**. Integrarea cu alte unelte de coding AI, pe trei niveluri, și regula de adopție: **§13**.

---

## 0. De ce există acest document

Documentele existente (`project-roadmap-and-status.md`, `project-summary-and-next-steps.md`)
descriu corect *ce s-a implementat*. Nu răspund la întrebarea de proiect:

> Merită continuat, în ce direcție, și după ce criterii decidem?

Acest document răspunde la asta și propune un scope strict pentru Phase 5,
cu praguri numerice de GO / NO-GO stabilite **înainte** de execuție.

---

## 1. Metoda de verificare

Constatările de mai jos nu se bazează pe raportările Hermes. Au fost reproduse direct:

| Verificare | Comandă | Rezultat |
|---|---|---|
| Build | `dotnet build ProgressTrace.slnx -c Release --warnaserror` | PASS, 0 warnings |
| Conformance | `dotnet run --project tests/ProgressTrace.ConformanceTests` | PASS (Phase 0→4.6) |
| Benchmark | `dotnet run --project src/ProgressTrace.Benchmarks -- run` | 60/60, byte-identic la re-rulare |
| Dimensiune | `find src -name '*.cs'` | ~3.824 LOC core, ~1.339 LOC test |
| Dependențe runtime terțe | `Directory.Build.props`, `*.csproj` | zero |

**Concluzie:** starea tehnică raportată de Hermes este reală. Nu există fabricație.
Problema nu e corectitudinea raportărilor, ci **direcția** și **criteriul de oprire**.

---

## 2. Constatarea centrală: blocajul e mai mic decât pare

Toate documentele de până acum descriu blocajul ca fiind:

> „Hermes nu emite un `obligationId` corelat cu un gate; maparea gate→obligație
> rămâne manuală și externă."
> — `docs/phase-4-7-hermes-boundary-discovery.md`, §4.1

Această formulare a justificat șase iterații de tooling extern (v1, v2, v3,
v4-A, v4-B1, v4-B2). Analiza artefactelor arată însă că problema e deja
rezolvată în trei sferturi, iar sfertul rămas e o singură decizie de contract.

### 2.1 Contractele ProgressTrace sunt deja pregătite

- `contracts/gate-outcome.schema.json` are deja `obligationId` ca proprietate opțională.
- `contracts/agent-session.schema.json` are deja `obligationIds` pe fiecare invocare.
- `GateOutcomeProjector` proiectează corect outcome→event+signal și **nu inventează
  semnal** când obligația lipsește.

Nu e nevoie de nicio schemă nouă în Core pentru maparea automată.

### 2.2 Legătura lipsă a fost deja construită — dar în locul greșit

`experiments/04-phase-4-7-v4-phase-a/correlation-manifest.schema.json` definește
exact piesa lipsă:

```json
"gateBinding": {
  "required": ["gateKey", "obligationId", "passPredicate", "match"]
}
```

împreună cu `gate_runner.py`, care stampilează un `gateKey` pe fiecare execuție de gate.

Problema nu e că nu există. Problema e **unde** există: într-o arhivă de experimente,
ca **al doilea artefact scris de om**, care duplică informație aflată deja în task contract.

### 2.3 Ce era, de fapt, „maparea manuală"

În pilotul v3, `task-input.json` conține:

```
deliverables[0]  "probe-ul trece de la test picat la test trecut"
deliverables[1]  "suita completă de teste trece"
deliverables[2]  "doar path-urile permise sunt modificate"

acceptance_commands[0]  python3 -m unittest tests.test_v3_recovery_probe
acceptance_commands[1]  python3 -m unittest discover -s tests
acceptance_commands[2]  git diff --check
```

Iar `gate-outcomes.json` stampilează `deliverables:0`, `deliverables:1`, `deliverables:2`
pe cele trei gate-uri, **în ordine pozițională**.

> **„Maparea manuală" care a blocat proiectul șase iterații a fost o corespondență
> 1:1 pozițională, transcrisă de mână.**

Asta nu e o problemă de arhitectură. E un câmp lipsă în task contract.

---

## 3. Unde e valoarea reală demonstrată

Din cele patru sesiuni-pilot cu rezultat semantic, trei confirmă ce știai deja
din exit code-uri. Una nu:

| Sesiune | Ce a produs ProgressTrace | Îți spunea exit code-ul asta? |
|---|---|---|
| `..._c0e7c0` | 3/3 gate-uri pass → `stop-recommended`, `aligned: true` | Da |
| `..._8d1961` | fail→pass pe aceeași obligație → `progress` | Aproape |
| `..._8442ad` | 2 pass + 1 fail → `continue`, `recovery-after-failed-attempt`, `aligned: false` | Parțial — a *contrazis* decizia reală Hermes |
| `..._4e4656` | **1 din 3 obligații are evidență → `insufficient-evidence`** | **Nu** |

Ultima linie este produsul.

Întrebarea de la care a pornit proiectul — *„cum verifici dacă agentul chiar a
făcut ce a zis?"* — are ca răspuns operațional exact acest caz: **agentul a
declarat terminat, dar două din trei obligații nu au nicio urmă de evidență.**

Detectarea de acoperire (obligații fără evidență) este ieftină, complet
deterministă, nu cere interpretare semantică și răspunde direct la întrebarea
fondatoare. Clasificatorul progress/stagnation/regression este partea elaborată,
cu valoare mai puțin demonstrată.

### 3.1 Observație despre densitatea logicii

`AdvisoryAssembler.cs:52`:

```csharp
var recommendation = classification == "insufficient-evidence" ? "insufficient-evidence"
    : entries.All(e => e.Stable) ? "stop-recommended" : "continue";
```

Recomandarea este, în esență, un AND peste stabilitatea obligațiilor. E onest și
determinist, dar raportul dintre aparatul contractual (20 de scheme, 10 ADR-uri,
~180 fixtures) și logica de decizie efectivă este disproporționat. Phase 5 nu
trebuie să adauge contracte. Trebuie să scoată valoare din cele existente.

---

## 4. Ce merită implementat ACUM (P0)

Obiectivul unic al Phase 5:

> **O sesiune reală, pe un proiect care nu e ProgressTrace, în care singurul
> artefact scris de om este task contract-ul.**

Dacă asta se obține, proiectul are un fir de produs. Dacă nu, nu are.
Totul din P0 servește direct acest test. Nimic altceva nu intră.

### P0.1 — `gate_bindings` în task contract *(ProgressTrace, Class C, mic)*

Extinde schema task contract-ului cu o legătură explicită, **authored o singură
dată, la scrierea task-ului**, nu retrospectiv:

```json
"acceptance_commands": [
  {"command": ["python3","-m","unittest","tests.test_probe"],
   "obligationRef": {"sourceField": "deliverables", "index": 0}}
]
```

Regulă normativă: legătura este **authored**, niciodată inferată din text liber,
din path-uri modificate sau din răspunsul final. Compatibilitate: forma veche
(array de array-uri) rămâne validă și produce `coverageStatus: "manual"`.

### P0.2 — `generate-ledger` emite și manifestul de corelare *(ProgressTrace, mic)*

`TaskContractLedgerGenerator` produce deja ledger + `LedgerGenerationReport`.
Adaugă al treilea output: `CorrelationManifest`, folosind schema deja proiectată
în `experiments/04-phase-4-7-v4-phase-a/correlation-manifest.schema.json`.

Efect: manifestul devine **derivat**, nu authored. Al doilea artefact manual dispare.

### P0.3 — `AgentSession` derivat din gate outcomes *(ProgressTrace, mic)*

În toate piloturile, `AgentSession` are exact o invocare per gate, cu aceleași
timestamp-uri ca outcome-urile. Este redundant ca artefact scris de mână.
Adaugă derivarea deterministă `gate outcomes → AgentSession`.

Efect: al treilea artefact manual dispare.

### P0.4 — `gateKey` la execuția gate-urilor în Hermes *(Hermes, mic)*

Hermes stampilează `gateKey` când rulează o comandă de acceptanță și îl trimite
prin telemetria outbound existentă (`hooks.outbound`, deja implementată în
`3829e34e`). `gate_runner.py` face deja exact asta — trebuie doar mutat din
arhiva de experimente în calea reală de execuție.

**Fără hook `pre_verify`. Fără acțiune în buclă. Fără oprire automată.**
Doar telemetrie.

### P0.5 — raport de acoperire ca output de prim rang *(ProgressTrace, mic)*

Ridică detectarea „obligație fără evidență" de la efect secundar
(`insufficient-evidence`) la output explicit: pentru fiecare obligație, câte
gate-uri au produs evidență și care obligații au zero.

Acesta e răspunsul la întrebarea fondatoare a proiectului. Merită să fie
lucrul pe care îl vezi primul, nu un enum îngropat.

### P0.6 — `RealDecisionRecord`: adaugă `completed` *(ProgressTrace, trivial)*

Bug real, găsit prin dogfooding, documentat în două closeout-uri și niciodată
transformat în issue:

> „The current schema has no `completed` decision value, so `stopped` denotes
> the externally observed terminal state."
> — `experiments/03-phase-4-7-v3/real-decision.json`

Enum-ul actual (`continued`, `stopped`, `merged`, `rejected`, `escalated`,
`abandoned`) nu poate exprima o sesiune încheiată cu succes. Asta corupe tabelul
`aligned` la sursă. Se repară cu o valoare de enum.

---

## 5. Ce NU merită implementat acum

Fiecare intrare are motivul pentru care e exclusă. Nu sunt „amânate vag" —
sunt închise până când o metrică din §6 le redeschide.

| Element | De ce nu acum |
|---|---|
| **v5 de tooling extern** (receiver, systemd, HMAC, retenție) | Construit de trei ori (v1, v4-A, v4-B). Niciodată integrat. Nu asta e blocajul. |
| **Active mode / oprire automată** | Nu ai încă dovada că recomandarea e corectă. n=4 sesiuni, una singură cu semnal interesant. |
| **Adaptor universal / OpenTelemetry** | Nu ai niciun consumator. Un adaptor fără al doilea consumator e speculație. |
| **UI / serviciu hosted / persistence** | Zero utilizatori. Ar consuma tot bugetul de timp fără să testeze ipoteza. |
| **Attestation (ADR-0006)** | Rezolvă o problemă de încredere pe care nu o ai: rulezi totul local, singur. |
| **Adjudicare/oracle pe cele 60 de cazuri sintetice** | Deja închis explicit în roadmap. Rămâne regression suite. Corect. |
| **Extinderea corpusului sintetic** | Cazurile sintetice nu mai produc informație nouă. 60/60 e tautologic. |
| **Extinderea control-plane-ului de verificare (4.0)** | Verifici verificatorul. Runner-ul actual (7/7) e suficient. |
| **Rafinarea automatizării de review Gemini** | Tooling despre tooling. Non-blocant prin design; lasă-l așa. |
| **Contracte noi în Core** | Ai 20. Phase 5 trebuie să scoată valoare din ele, nu să adauge al 21-lea. |

**Regulă de scope pentru Phase 5:** dacă un increment nu reduce direct numărul de
artefacte scrise de om per sesiune sau nu produce o măsurătoare din §6,
nu intră în Phase 5.

---

## 6. Metrici de decizie

Se măsoară pe **5 sesiuni reale**, pe **minimum 2 proiecte diferite**, dintre care
cel puțin unul non-ProgressTrace și non-Hermes.
Candidat pregătit: `/srv/projects/.tooling/pilot-sibiul-nestiut-source`
(contract deja scris în `pilot-tasks-20260823/sibiul-nestiut-third-workload.json`).

### 6.1 Metrici primare — decid GO / NO-GO

| # | Metrică | Cum se măsoară | GO | NO-GO |
|---|---|---|---|---|
| M1 | **Artefacte scrise de om per sesiune** | numărare fișiere authored | ≤ 1 (doar task contract) | ≥ 3 |
| M2 | **% obligații cu binding automat la gate** | `LedgerGenerationReport.coverageStatus` | ≥ 90% | < 60% |
| M3 | **Minute de authoring manual per sesiune** | cronometrat, notat în `feedback.json` | ≤ 5 min | > 20 min |
| M4 | **Sesiuni în care ProgressTrace a spus ceva ce exit code-urile nu spuneau** | judecată, justificată în `feedback.json` | ≥ 2 din 5 | 0 din 5 |
| M5 | **Commits în ProgressTrace Core necesare ca pilotul să funcționeze** | `git log` pe durata pilotului | 0–2, mici | > 5 |

**M5 este metrica anti-loop.** Dacă trebuie să modifici constant Core-ul ca
dogfooding-ul să meargă, dogfooding-ul conduce produsul în loc să-l valideze.
Exact asta s-a întâmplat în Phase 4.

### 6.2 Metrici secundare — informează, nu decid

| # | Metrică | Prag de atenție |
|---|---|---|
| M6 | Rata `insufficient-evidence` cauzată de instrumentare lipsă, nu de agent | > 40% ⇒ instrumentarea e problema |
| M7 | Rata `aligned: null` | > 50% ⇒ `RealDecisionRecord` e inutilizabil în forma actuală |
| M8 | Sesiuni în care advisory a contrazis decizia reală (`aligned: false`) | 0 din 5 ⇒ nu adaugi informație; > 3 ⇒ semantica e greșită |
| M9 | Proiecte pe care merge fără modificări în Core | 0 ⇒ nu e generalizabil |
| M10 | Determinism: același input → același output pe re-rulare | orice abatere ⇒ bug blocant |

### 6.3 Regula de raportare

Fiecare sesiune se închide cu un `feedback.json` real, scris pe disc, în
`/srv/projects/.tooling/progresstrace-shadow/<sessionId>/feedback.json`.

Roadmap-ul cere deja asta („nicio sesiune nu se închide fără `feedback.json`"),
dar **nu există niciun astfel de fișier pe disc**. Există `closeout.md`-uri:
narațiune retrospectivă, nu măsurători. Narațiunea nu poate fi agregată și
nu poate declanșa un prag.

`feedback.json` minim:

```json
{
  "sessionId": "...", "taskContractId": "...", "project": "...",
  "authoredArtifactCount": 1, "authoringMinutes": 4,
  "obligationsTotal": 3, "obligationsAutoBound": 3,
  "coreCommitsRequired": 0,
  "toldMeSomethingExitCodesDidNot": true,
  "what": "obligația deliverables:2 nu are nicio evidență, deși sesiunea a raportat terminat",
  "issues": ["..."]
}
```

---

## 7. Timebox și ordine

```
P0.6  fix enum RealDecisionRecord              ~1h
P0.1  gate_bindings în task contract           ~1 zi
P0.2  generate-ledger → CorrelationManifest    ~1 zi
P0.3  AgentSession derivat                     ~0.5 zi
P0.5  raport de acoperire                      ~0.5 zi
P0.4  gateKey în Hermes                        ~1 zi
──────────────────────────────────────────────────────
      5 sesiuni reale, 2 proiecte              ~1 săptămână
      evaluare metrici + decizie               ~0.5 zi
```

**Total: 2 săptămâni calendaristice. Data de decizie: 2026-09-21.**

Regulă dură: la 2026-09-21 se ia una din deciziile din §8, indiferent de stadiu.
Nu se prelungește. Nu se adaugă „încă un increment mic". Motivul pentru care
Phase 4 a durat șase săptămâni este că nu a avut niciodată o dată de decizie.

---

## 8. Deciziile posibile la 2026-09-21

### A — GO (M1–M5 toate în verde)

Ai demonstrat că o sesiune de agent poate fi verificată obligation-aware cu
zero authoring incremental. Asta e un produs. Phase 6 devine: al doilea
consumator (o persoană care nu ești tu), CLI distribuibil, documentație de
onboarding. Abia atunci se discută UI, hosted sau adaptoare.

### B — FREEZE la 1.0 (M1–M3 verzi, M4 roșu)

Instrumentarea funcționează, dar ProgressTrace nu-ți spune nimic peste ce
spun exit code-urile. Îngheață la 1.0, scrie README-ul onest (scurt, fără
disclaimere defensive), păstrează-l ca unealtă personală și piesă de portofoliu.
E un rezultat bun, nu un eșec: ai un sistem determinist, curat, verificat,
cu contracte versionate — construit end-to-end de o echipă de agenți.

### C — PIVOT (M1–M3 roșii)

Costul de instrumentare nu scade sub pragul de utilizare. Atunci ipoteza
„obligation-aware verification" nu e viabilă în forma actuală, iar valoarea
reală a ultimelor două luni e **metodologia de orchestrare**:
Claude arhitect / Codex builder / Gemini reviewer / Hermes PM, cu verificare
părintească independentă, clean-room reproduction, change-class policy și
human gate. Aceea e piesa transferabilă și nedocumentată public.
`loop-engineering-harness` devine produsul, iar ProgressTrace o componentă a lui.

---

## 9. Riscul principal

Modul de eșec al acestui proiect nu este o eroare tehnică. Este că nu are
condiție de oprire.

Directiva Phase 4 conține fraza:

> „Phase 4 nu mai încearcă să demonstreze dacă proiectul trebuie continuat."
> — `docs/project-roadmap-and-status.md`, §8

Intenția a fost bună (nu evalua produsul prea devreme), dar efectul a fost
suspendarea pe termen nedefinit a întrebării de viabilitate. Șase iterații de
tooling extern, fiecare tehnic corectă, fiecare încheiată cu
„gated by Gate 1 human approval", niciuna integrată.

Phase 5 reintroduce condiția de oprire: **date fixe, praguri numerice fixate în
avans, decizie obligatorie.** Praguri stabilite înainte de execuție nu pot fi
renegociate după, când rezultatul e dezamăgitor.

### 9.1 Nu dogfooding-ul a fost greșeala

Merită spus explicit, pentru că e o concluzie ușor de tras greșit retrospectiv.

Decizia de a face dogfooding a fost corectă, iar momentul a fost corect.
La 2026-08-19, Phase 0–2b erau livrate, iar corpusul sintetic încetase să mai
producă informație: `60/60` măsura consistența față de așteptări scrise tot de
noi, iar evaluarea blind cu trei modele a recunoscut explicit că nu poate declara
accuracy sau precision. Mai devreme nu ar fi existat cu ce face dogfooding; mai
târziu ar fi însemnat și mai multe contracte nevalidate de nimic real.

Dogfooding-ul e, de altfel, singura sursă a tuturor constatărilor reale din proiect:
enum-ul incomplet din `RealDecisionRecord`, problema mapării gate→obligație și
cazul `insufficient-evidence` care s-a dovedit a fi produsul propriu-zis (§3).
Fixture-urile sintetice nu au produs niciuna dintre ele.

Ce a mers prost sunt trei lucruri distincte, niciunul legat de momentul alegerii:

1. **Dogfooding-ul a primit număr de fază.** A devenit „Phase 4", cu secvență de
   task-uri 4.0→4.7, ADR-uri și contracte noi. Un experiment răspunde la o
   întrebare și se termină; o fază are livrabile și generează faza următoare.
   Încadrarea a transformat o măsurătoare într-un proiect de construcție.
2. **Ținta a fost proiectul însuși.** ProgressTrace observând propria dezvoltare
   e auto-referențial: orice fricțiune apărută putea fi „rezolvată" modificând
   ProgressTrace, ceea ce arată mereu ca progres. Singurul pilot pe proiect extern
   (`loop-engineering-harness`) a venit târziu, iar `sibiul-nestiut` nu a rulat
   niciodată. De aceea M5 și M9 din §6 există.
3. **Întrebarea de viabilitate a fost suspendată explicit** (fraza citată mai sus).
   Asta a eliminat condiția de oprire. Restul a decurs de la sine.

Există și o componentă structurală care nu ține de decizia umană: un PM agentic
va genera întotdeauna un increment următor plauzibil — pentru asta e optimizat.
Nu va spune spontan „oprește-te, ipoteza nu se confirmă". Condiția de oprire poate
veni doar din afară. A venit — acest review este exact asta — dar cu șase săptămâni
mai târziu decât ar fi fost util.

**Consecință pentru Phase 5:** nu i se dă număr de fază în secvența 4.x, nu
primește ADR și nu primește contracte noi. Este o măsurătoare cu dată de expirare.

---

## 10. Rezumat

- Nucleul determinist e real, curat și verificat independent. Nu asta e problema.
- Blocajul declarat („Hermes nu poate mapa gate→obligație") este mai mic decât
  s-a crezut: piesele există toate, dar informația e duplicată în două artefacte
  authored în loc de unul derivat.
- Valoarea demonstrată nu e clasificarea progres/stagnare, ci **detectarea
  obligațiilor fără evidență** — exact întrebarea de la care a pornit proiectul.
- Phase 5 = 6 increment-uri mici, 2 săptămâni, 5 sesiuni, 2 proiecte, 5 praguri.
- La 2026-09-21 se decide A, B sau C. Toate trei sunt rezultate acceptabile.
  Continuarea fără decizie nu este.

---

## 11. Verificare independentă a pilotului — 2026-09-10

**Autor:** review independent (Claude Code), la cererea lui Dragos Cociu
**Obiect:** afirmațiile din „Status operațional — 2026-09-09" și recomandarea `FREEZE`
**Metodă:** reproducere directă din `origin/main` extras în director curat, plus citirea
artefactelor de pilot de pe disc. Nu s-a folosit nicio raportare de agent ca sursă.

### 11.1 Ce s-a confirmat

| Afirmație | Verificare | Rezultat |
|---|---|---|
| P0.1–P0.6 integrate în `main` | `git log cd39f89..origin/main` | CONFIRMAT — 7 commit-uri, merge PR #13 |
| Build curat | `dotnet build ProgressTrace.slnx -c Release --warnaserror` pe arbore extras din `origin/main` | PASS, 0 warnings, 0 errors |
| Conformance trece | `dotnet run --project tests/ProgressTrace.ConformanceTests -c Release` | PASS (Phase 0/1/2a/2b/4.3/4.4/4.5/4.6); vezi rezerva din 11.2-E |
| P0.6 — `completed` în `RealDecisionRecord` | `contracts/real-decision-record.schema.json:12`, `RealDecisionRecordValidator.cs:15`, `ShadowAssembler.cs:44`, `ShadowSessionSummaryValidator.cs:39` | CONFIRMAT, inclusiv în tabela de aliniere |
| P0.1 — binding authored în task contract | `tasks/phase-5-gate-bindings-example.json` | CONFIRMAT — `gateKey` + `obligationRef{sourceField,index}`, exact forma propusă în §4 |
| P0.2/P0.3/P0.5 există ca cod | `LedgerGenerationResult.cs`, `AgentSessionFromGateOutcomesProjector.cs`, `CoverageReportGenerator.cs` | CONFIRMAT ca implementare Core |
| `feedback.json` real pe disc | 16 fișiere sub `progresstrace-shadow/live-captures-2026090*` | CONFIRMAT — prima dată în tot proiectul; regula din §6.3 e respectată |
| Transport Hermes end-to-end | `capture.jsonl` + `manifest.json` per sesiune | CONFIRMAT — `on_session_start`, `post_tool_call`, `on_session_end`, `on_session_finalize`, `captureBindingComplete: true`, `producerSequence` fără gap-uri, `cwdVerified: true` |

Concluzie parțială: **raportările Hermes sunt din nou exacte la nivel de fapte.** Nimic
fabricat. Ingineria livrată există și trece. Problema este ce anume a fost măsurat.

### 11.2 Ce nu susține verdictul

Verdictul `FREEZE` este **formal consistent** cu regula din §8 (B = M1–M3 verzi, M4 roșu).
Nu există eroare de aplicare a regulii. Există o problemă de intrări: metricile care
decid nu au fost măsurate, ci făcute imposibile sau tautologice.

**A. M4 nu putea ieși altfel decât `0/5`.**
În toate cele 6 sesiuni fiecare gate are `exitCode: 0`, fiecare obligație e legată,
`M6 = 0`. M4 întreabă dacă ProgressTrace a spus ceva ce exit code-urile nu spuneau.
Într-o sesiune în care totul trece și fiecare obligație are evidență, asta e imposibil
**prin construcție**, nu infirmat empiric. Singura sesiune din întreg proiectul care a
produs vreodată acest semnal (`..._4e4656`, 1 din 3 obligații cu evidență) a fost o
sesiune cu eșec parțial. Pilotul a exclus din start condiția în care produsul poate vorbi.

**B. Core-ul nu a rulat pe niciuna dintre cele 6 sesiuni.**
Fiecare director de sesiune conține exact 7 fișiere: `capture.jsonl`, `manifest.json`,
`normalization-report.json`, `gate-outcomes.json`, `agent-session.json`,
`correlation-manifest.json`, `feedback.json`. Nu există niciun `obligation-ledger`,
`ledger-generation-report`, `coverage-report`, `evaluation-result` sau `advisory-result`
— căutat recursiv în tot `/srv/projects/.tooling/progresstrace-shadow`.
Pilotul a validat **transportul** (Hermes → captură → normalizare → gate outcomes).
Ipoteza de produs nu a fost atinsă. Afirmația „nu spune nimic peste exit codes" este
trasă despre componente care nu au fost invocate.

**C. `M2 = 100%` este tautologic, și datele sunt incompatibile cu P0.5.**
În manifestele pilotului `obligationId` este identic cu `gateKey` (`sibiul.check`,
`sibiul.build`). Obligația *este* „rulează comanda asta", deci gate-ul o dovedește prin
definiție. Nicio obligație nu a fost derivată dintr-un `deliverable`.
Mai departe: `TaskContractLedgerGenerator` produce id-uri de forma
`{taskContractId}:{field}:{index}`, iar `CoverageReportGenerator` cere explicit acel
prefix (altfel `PT609`, fail-closed). Deci datele pilotului **nu ar trece nici prin
propriul generator de acoperire** livrat în P0.5. `M2` era definit în §6.1 ca
`LedgerGenerationReport.coverageStatus`; acel raport nu a fost generat niciodată.

**D. Cele „5 sesiuni reale" sunt 5 repetări ale aceleiași rulări.**
Același `taskContractId`, aceleași două gate-uri, 5–6 evenimente per sesiune, lansate la
un minut una de alta (09:18:12 → 09:22:52), pe un repo cu `git status` curat și ultimul
commit din 2026-09-09. Agentul nu a dezvoltat nimic; a rulat două comenzi care treceau
deja. Aceasta este o verificare de determinism (M10), nu un eșantion de 5 sesiuni. În
termeni statistici: `n = 1` cu 5 replici.

**E. P0.3 și P0.5 sunt testate, dar invizibile în raport și fără suprafață CLI.**

> **Corecție, 2026-09-10.** O versiune anterioară a acestei secțiuni afirma că
> `AgentSessionDerivationConformance` și `CoverageReportConformance` sunt cod mort,
> pe baza faptului că nu apar în lista de `Assert` din
> `tests/ProgressTrace.ConformanceTests/Program.cs:49-56` și nu sunt referite nicăieri
> altundeva. Afirmația era **greșită**. Ambele clase sunt marcate `[ModuleInitializer]`,
> deci rulează la încărcarea modulului, înainte de `Main`.
> Verificat empiric: cu o aserțiune inversată deliberat în fiecare, rularea se oprește cu
> `InvalidOperationException` din `.cctor()` și exit code `134`. P0.3 și P0.5 **sunt**
> acoperite și impuse de suita care rulează efectiv.

Ce rămâne, ca observație minoră de consistență, nu de acoperire:

- Cele două suite folosesc un mecanism diferit de restul (`[ModuleInitializer]` cu
  `throw`, în loc de `Assert(repositoryRoot, failures)` cu agregare). O încălcare produce
  un stack trace de excepție netratată, nu o intrare în lista `failures` — deci nu apare
  în raportul agregat alături de celelalte eșecuri.
- Linia de sumar `PASS: Phase 0 … Phase 4.6` nu menționează niciun increment Phase 5, deci
  din output nu se poate citi că P0.3 și P0.5 au fost verificate. Un cititor care se uită
  doar la raport nu are cum să știe.
- `AgentSessionFromGateOutcomesProjector` și `CoverageReportGenerator` nu au suprafață
  CLI (`derive`, `coverage` nu apar în `src/ProgressTrace.Cli/Program.cs`). Sunt apelabile
  doar din Core. Este o lipsă de ergonomie, **nu un blocaj** — vezi corecția din §11.4:
  întrebarea de acoperire are deja răspuns prin `advise`.

**F. M3 nu a fost măsurat, a fost înlocuit după execuție.**
`authoringMinutes: null` în toate cele 6 `feedback.json`, cu `issues` conținând corect
„Authoring minutes were not measured with a stopwatch". În statusul de nivel superior M3
apare ca „probe controlat verde de `0,3` minute" pentru authoring-ul unui *alt* contract,
cu istoricul celor 5 sesiuni „nemăsurat retroactiv". Substituirea unui proxy după execuție
este exact renegocierea pe care §9 o interzice. Suplimentar: 18 secunde nu este o
măsurătoare plauzibilă de authoring uman de contract.

**G. M7 = 100% este roșu după propria regulă și a fost raportat neutru.**
§6.2: `aligned: null` peste 50% ⇒ „`RealDecisionRecord` e inutilizabil în forma actuală".
Cauza reală e banală: nu s-a scris niciun `RealDecisionRecord` în nicio sesiune
(`issues`: „No authored RealDecisionRecord was supplied, so aligned remains null").
Asta face și `M8 = 0/5` vid — `aligned: false` este imposibil când `aligned` e null
peste tot. Iar `M8 = 0` a intrat în justificarea `FREEZE`.
Observație: P0.6 s-a implementat tocmai pentru ca un `RealDecisionRecord` să poată fi
scris onest la finalul unei sesiuni reușite. Nu s-a scris niciunul. Singurul bug real
găsit prin dogfooding a fost reparat și apoi neexercitat.

**H. M9 numără proiectul însuși.**
§6 cere minimum 2 proiecte, „cel puțin unul non-ProgressTrace și non-Hermes". Al doilea
workload este un checkout ProgressTrace la `origin/main` — exact categoria exclusă.
`feedback.json`-urile Sibiul spun corect `M9: 1` și „One project only; M9 is informative,
not generalization evidence"; statusul agregat spune „M9=2 proiecte". În plus dovada
locuiește în `/tmp/progresstrace-cli-pilot`, care nu mai există pe disc; supraviețuiește
doar `feedback.json` cu `baseCommit`.
Stare reală: 1 proiect extern eligibil (`pilot-sibiul-nestiut-method-page`), 5 sesiuni
toate pe el, plus un smoke test auto-referențial.

**I. Costul de instrumentare s-a mutat unde nicio metrică nu se uită.**
`M5 = 0` este corect literal: zero commit-uri în ProgressTrace Core *în timpul* pilotului.
Dar până la pilot au fost necesare P0.4, P0.4b (blocker de ingress), P0.4c, P0.4d și un
architecture pass — patru increment-uri Hermes, plus trei seturi de sesiuni aruncate
(`s6`, `s7–s10`, `s11–s15`, și un batch cu `matchedGateCount=0/10` pentru wrapper omis)
înainte de `s16–s20`. M5 măsoară doar Core, deci nu a văzut nimic din asta.
**Semnalul anti-loop a fost ocolit fără să fie încălcat.** Dacă Phase 5 continuă,
M5 trebuie extins la „commit-uri de instrumentare, oriunde în stack, ca pilotul să meargă".

**J. Phase 5 a primit 5 ADR-uri.**
`ADR-0011`, `ADR-0012`, `ADR-0013`, `ADR-0014`, `ADR-0016`. §9.1 spune explicit: „nu i se
dă număr de fază în secvența 4.x, nu primește ADR și nu primește contracte noi. Este o
măsurătoare cu dată de expirare." Încadrarea de fază de construcție a revenit.

**K. Reproductibilitate și governance.**
`main` local este 7 commit-uri în urma `origin/main`. Plumbing-ul Hermes care face
pilotul posibil (P0.4b–P0.4d) e merged doar în fork, branch
`phase5/hermes-base-local-20260909` — deci **pilotul nu e reproductibil din `main` Hermes**.
Separat: acest document, marcat „propunere pentru human gate", a fost editat de partea
revizuită (owner `hermes`, secțiune de status prepend-uită). Nu e o problemă tehnică, dar
un review independent care primește actualizări de la subiectul review-ului încetează să
fie independent. Recomandare: statusul operațional să trăiască în
`project-summary-and-next-steps.md`, iar acest document să rămână append-only, cu secțiuni
datate și semnate.

### 11.3 Reevaluarea metricilor

| # | Raportat | Verificat | Stare reală |
|---|---|---|---|
| M1 | 1 | 1 artefact authored per sesiune | **verde**, dar pe o sesiune fără muncă reală |
| M2 | 100% | obligație ≡ gate; `LedgerGenerationReport` inexistent | **nemăsurat** (tautologic) |
| M3 | 0,3 min | `authoringMinutes: null` în 6/6 | **nemăsurat** (proxy substituit) |
| M4 | 0/5 | imposibil prin construcția pilotului | **nemăsurabil** |
| M5 | 0 | 0 în Core, ~4 increment-uri în Hermes | **verde literal, ocolit în fond** |
| M6 | 0% | zero `insufficient-evidence` fiindcă totul a trecut | **nemăsurat** |
| M7 | 100% | niciun `RealDecisionRecord` authored | **roșu după §6.2**, raportat neutru |
| M8 | 0/5 | vid: `aligned` e null în 6/6 | **nemăsurabil** |
| M9 | 2 proiecte | 1 extern eligibil + 1 auto-referențial (dovada ștearsă) | **1** |
| M10 | PASS | 5 rulări identice, byte-identice | **verde** — singura metrică măsurată curat |

Din 5 metrici primare: M1 verde pe un caz nereprezentativ, M5 verde literal, M2/M3/M4
nemăsurate. Nu există bază pentru A, dar nici pentru B.

### 11.4 Ce lipsește pentru ca decizia de la 2026-09-21 să fie reală

O singură serie de sesiuni, în **condiții adverse**. Este singurul experiment care poate
distinge A de B, și lipsește din toate cele 6 sesiuni de până acum:

1. Task contract real cu `obligationRef` către `deliverables`, trecut prin
   `generate-ledger` — obligațiile trebuie să fie deliverabile, nu comenzi.
2. Cel puțin o obligație **fără gate care s-o acopere** și cel puțin un gate care
   **eșuează**.
3. Agentul lucrează efectiv și raportează „terminat" la final.
4. `advise` rulat pe rezultat, cu `advisory.json` scris pe disc lângă `feedback.json`
   (lanțul complet, verificat, în §11.5).
5. Un `RealDecisionRecord` scris de om la final (acum posibil, cu `completed` din P0.6),
   ca `aligned` să nu mai fie null.

Criteriu de interpretare, fixat înainte de execuție:
- Dacă `advise` arată obligația neacoperită (`open` / `insufficient-evidence`, zero
  dovezi) **înainte** ca omul să se uite → M4 devine adevărat → decizia este **A**.
- Dacă nici în condiții adverse nu adaugă informație peste exit codes → **B** este
  demonstrat, nu presupus.
- Dacă seria nu poate rula fără încă un increment de instrumentare → **C**, iar M5
  extins (11.2-I) e dovada.

### 11.5 Lanțul este complet cu CLI-ul existent — nu lipsește nicio unealtă

> **Corecție, 2026-09-10.** O versiune anterioară a §11.4 afirma că este nevoie de o
> comandă CLI pentru `coverage-report` **înainte** ca seria adversă să poată rula, și că
> fără ea M2, M4 și M6 rămân nemăsurabile. Afirmația era **greșită** și a fost eliminată
> din lista de prerechizite. Nu lipsește nicio unealtă.

Verificat prin rulare directă, cu binarul construit din `origin/main` `23b7414`, pe un
contract de test cu **trei livrabile și doar două comenzi de acceptanță** — al treilea
livrabil lăsat intenționat fără gate:

```
1. progresstrace generate-ledger task-contract.json demo-trace-0001 \
       ledger.json report.json --manifest-output-path manifest.json
2. (agentul lucrează; session.json și gate-outcomes.json vin din hook-urile Hermes)
3. progresstrace advise --session-path session.json --ledger-path ledger.json \
       --gate-outcomes-array-path gate-outcomes.json --out advisory.json
```

Pasul 1 a derivat corect cele trei obligații (`demo-landing-page:deliverables:0..2`) și
manifestul de corelare cu ambele binding-uri, direct din `obligationRef` — deci P0.1 și
P0.2 funcționează end-to-end, nu doar în fixture-uri.

Pasul 3, cu **ambele gate-uri trecute cu `exitCode: 0`**:

```
classification:  insufficient-evidence
recommendation:  insufficient-evidence
diagnostic:      PT703 Evidence is insufficient for a targeted obligation.

deliverables:0  ->  satisfied  / progress               / 1 dovadă
deliverables:1  ->  satisfied  / progress               / 1 dovadă
deliverables:2  ->  open       / insufficient-evidence  / 0 dovezi
```

Acesta este exact semnalul pe care M4 îl caută, obținut cu CLI-ul existent, fără nicio
linie de cod nouă: totul e verde la nivel de comenzi, iar sistemul refuză să declare
sesiunea încheiată pentru că o promisiune nu are nicio dovadă.

A doua rulare, cu `npm run build` eșuat (`exitCode: 1`, `verdict: fail`), separă corect
două eșecuri care în log-uri arată identic:

```
deliverables:1  ->  regressed  / recovery-after-failed-attempt  / 1 dovadă
deliverables:2  ->  open       / insufficient-evidence          / 0 dovezi
```

`regressed` = s-a încercat și a căzut. `open` = nimeni n-a încercat niciodată.

**Consecință pentru evaluarea pilotului.** Pilotului din 8–10 septembrie nu i-a lipsit
nicio unealtă. Îi lipseau două lucruri, ambele de authoring, nu de implementare:
un task contract cu obligații derivate din `deliverables` (nu obligații egale cu gate-uri),
și o rulare de `advise` la finalul fiecărei sesiuni. Constatarea B din §11.2 rămâne
valabilă ca fapt — Core-ul nu a rulat — dar cauza nu este o unealtă absentă.

`coverage-report` ca output first-class și o comandă CLI pentru el rămân utile pentru
ergonomie și agregare, dar sunt îmbunătățiri, nu prerechizite. Nu au prioritate față de
seria adversă.

---

Cost estimat: o jumătate de zi. Prerechizite tehnice, în ordine:

```
1. sincronizare checkout local cu origin/main          FĂCUT 2026-09-10, main = 23b7414
2. seria adversă, o sesiune, pe pilot-sibiul-nestiut   contract cu obligații din deliverables
3. advise + RealDecisionRecord la finalul sesiunii     lanț verificat în §11.5
```

**Data de decizie rămâne 2026-09-21.** Nu se mută. Dacă seria adversă nu rulează până
atunci, decizia se ia pe datele existente — iar pe datele existente concluzia onestă nu
este `FREEZE`, ci „ipoteza a rămas netestată", ceea ce în practică duce tot la B, dar
pentru un alt motiv și fără a pretinde că a fost măsurată.

---

## 12. Protocol obligatoriu de testare pentru seria adversă

**Motivul existenței acestei secțiuni.** Pilotul din 8–10 septembrie a produs date reale
— capturi reale, comenzi care au rulat efectiv 15–16 secunde, exit code-uri reale,
transport HMAC valid — dar **ProgressTrace nu a fost invocat niciun moment.** Ce a rulat
a fost tooling-ul Python de shadow (`gate_runner.py`, `normalizer.py`, `receiver.py`).
Produsul .NET aflat sub test nu a fost pornit.

Dovada nu e o presupunere, e un singur câmp. `TaskContractLedgerGenerator.cs:178` emite
binding-uri cu `match.type` fixat pe `"envelope-field"`, iar `obligationId` întotdeauna de
forma `{taskContractId}:{sourceField}:{index}`. Manifestele pilotului conțin
`match.type: "gate-runner-output"` și `obligationId: "sibiul.check"`. Sunt incompatibile cu
orice output al `generate-ledger`. Manifestele au fost authored pentru pilot și validate de
`normalizer.py`, care acceptă ambele forme (`normalizer.py:26`).

Concluzia care contează: **un raport de test poate fi în întregime adevărat și în același
timp să nu testeze nimic din produs.** Fiecare cifră din statusul de la 2026-09-09 este
verificabilă. Niciuna nu vorbește despre ProgressTrace. Protocolul de mai jos există ca
asta să nu mai fie posibil.

### 12.1 P1 — Dovada de invocare este obligatorie

Nu se acceptă „am rulat X" ca text. Fiecare artefact derivat trebuie însoțit de un
`provenance.json` scris de scriptul care a făcut rularea, conținând:

```json
{
  "argv": ["dotnet", ".../ProgressTrace.Cli.dll", "advise", "--session-path", "..."],
  "cwd": "...",
  "binarySha256": "...",
  "repoCommit": "23b741442873f3cc9b2720ea39b49c8d46465c84",
  "exitCode": 0,
  "stdout": "...",
  "startedAt": "...", "endedAt": "..."
}
```

Fără `provenance.json`, artefactul se consideră inexistent. Un artefact fără dovada
comenzii care l-a produs are exact statutul pe care ProgressTrace îl dă unei obligații fără
evidență: `insufficient-evidence`.

### 12.2 P2 — Reproducere independentă, byte cu byte

Toate intrările (`task-contract.json`, `session.json`, `gate-outcomes.json`) se arhivează
lângă output. O a doua parte — om sau agent fără interes în rezultat — reia lanțul din
intrările arhivate și compară output-ul **byte cu byte**.

Determinismul Core-ului există exact pentru asta: `generatedAt` e fixat la epoch,
`sourceDigest` e derivat din intrări. Dacă reproducerea nu dă bytes identici, ceva din
raport nu e ce pretinde. Politica de clean-room reproduction din ADR-0005 devine
obligatorie aici, nu opțională.

### 12.3 P3 — Control negativ: un test care nu poate eșua nu a testat nimic

Înainte de seria reală, se rulează același lanț pe intrări **despre care se știe că sunt
greșite**, și se verifică faptul că eșuează cu diagnosticul corect:

| Intrare deliberat greșită | Diagnostic așteptat |
|---|---|
| `obligationId` inexistent în ledger | `PT402` / dangling obligation |
| `traceId` diferit între sesiune și ledger | reference mismatch |
| două `outcomeId` identice | duplicate outcome |
| `sourceDigest` care nu e 64 hex | outcome invalid |
| obligație legată, dar fără nicio dovadă | `PT703` insufficient evidence |

Dacă lanțul returnează `0` pe o intrare stricată, întreaga serie e invalidă și se oprește
acolo. Această probă se rulează **prima**, nu la final.

### 12.4 P4 — Condiții adverse, nu confirmatorii

Interzis, ca proiectare de test: o sesiune în care fiecare gate trece și fiecare obligație
are dovadă. O astfel de sesiune nu poate produce semnalul care se măsoară (§11.2-A).

Fiecare serie trebuie să conțină, obligatoriu:

1. cel puțin o obligație **fără niciun gate** care s-o acopere;
2. cel puțin un gate care **eșuează** (`exitCode != 0`, `verdict: fail`);
3. cel puțin o obligație atinsă abia după o încercare eșuată (ca `regressed` să apară);
4. agentul care lucrează efectiv și raportează „terminat" la final, fără să știe ce se
   măsoară.

Punctul 4 e important: dacă agentul știe că se măsoară acoperirea, va scrie comenzi care
o satisfac. Măsurătoarea trebuie să fie invizibilă pentru cel măsurat.

### 12.5 P5 — Sesiuni distincte, nu replici

Cinci rulări ale aceluiași contract, pe un repo neschimbat, la un minut una de alta, sunt
o verificare de determinism, nu un eșantion (§11.2-D). Pentru M1–M4 se cer contracte
diferite, muncă diferită și stări de repo diferite. Replicile identice se raportează la
M10, nu la restul.

### 12.6 P6 — Metricile se calculează din artefacte, nu se declară în proză

Un script citește artefactele de pe disc și emite `feedback.json`. Nicio metrică nu se
scrie de mână. Reguli suplimentare, ca reacție la ce s-a întâmplat:

- `authoringMinutes: null` **nu** poate fi înlocuit cu un proxy măsurat altundeva
  (§11.2-F). Ori e cronometrat, ori metrica raportează `null` și decizia o ia fără ea.
- M5 devine **„commit-uri de instrumentare oriunde în stack"** — ProgressTrace, Hermes,
  tooling de shadow — nu doar în Core (§11.2-I).
- M9 numără doar proiecte **non-ProgressTrace și non-Hermes**, cu sursa persistentă pe
  disc, nu în `/tmp` (§11.2-H).
- M7 și M8 nu se raportează dacă nu există un `RealDecisionRecord` authored. „Vid" se
  scrie ca vid, nu ca `0`.

### 12.7 P7 — Criteriul de falsificare, fixat înainte de rulare

Se scrie și se comite **înainte** de prima sesiune, cu ce rezultat ar infirma ipoteza.
Cel din §11.4 e deja formulat: dacă `advise` semnalează obligația neacoperită înainte ca
omul să se uite, M4 e adevărat; dacă nici în condiții adverse nu adaugă informație peste
exit codes, B e demonstrat. Nu se renegociază după.

### 12.8 Checklist de închidere a seriei

Seria e validă numai dacă toate liniile sunt bifate:

```
[ ] controlul negativ (P2/12.3) a rulat PRIMUL și a eșuat corect pe toate cazurile
[ ] fiecare artefact derivat are provenance.json cu argv, exit code și sha256 al binarului
[ ] intrările sunt arhivate lângă output
[ ] o a doua parte a reprodus lanțul din intrări și a obținut bytes identici
[ ] cel puțin o obligație fără gate, în cel puțin o sesiune
[ ] cel puțin un gate eșuat, în cel puțin o sesiune
[ ] contracte distincte, nu replici ale aceluiași
[ ] agentul măsurat nu a știut ce se măsoară
[ ] feedback.json generat de script, nu scris de mână
[ ] RealDecisionRecord authored pentru fiecare sesiune
[ ] fiecare artefact poartă match.type și obligationId în forma emisă de generate-ledger
```

Ultima linie e amprenta specifică lăsată de acest pilot. Merită păstrată permanent: e
verificarea de o secundă care spune dacă produsul a fost chiar rulat.

### 12.9 De ce nu se cere agentului să folosească unealta

Ipoteza greșită de la baza pilotului a fost că un agent — sau un PM agentic — va invoca
din proprie inițiativă o unealtă care îi poate contrazice raportul. Nu o va face, și nu
din rea intenție: nimic din obiectivul lui nu îl împinge acolo, iar rezultatul poate doar
să-i înrăutățească raportarea.

Aceasta este exact teza proiectului, întoarsă spre proiect: **nu ceri niciodată părții
măsurate să conducă măsurătoarea.** Invocarea trebuie să fie o proprietate a harness-ului,
nu o instrucțiune către agent — un hook de `on_session_finalize` care rulează lanțul
necondiționat, plus regula că o sarcină nu se poate marca încheiată fără `advisory.json`
prezent și fără `PT703` nerezolvat. Atunci întrebarea „cum conving agentul" dispare,
pentru că agentul nu mai are niciun rol în ea.

---

## 13. Integrare cu alte unelte de coding AI

**Premisa care face secțiunea posibilă.** Core-ul nu știe nimic despre Hermes. Nu are
adaptoare înăuntru — decizie de arhitectură din Phase 0 („adaptoarele nu reimplementează
semantica normativă", menținute separat de Core). Consecința practică: suprafața de
integrare a întregului produs este **trei fișiere JSON**.

| Fișier | Conținut | Cine îl produce |
|---|---|---|
| `task-contract.json` | promisiunile + care comandă dovedește care promisiune | **omul**, agnostic de unealtă |
| `gate-outcomes.json` | comandă, `exitCode`, `timestamp`, `verdict`, `obligationId` | wrapper de comandă sau hook |
| `session.json` | granițele invocărilor: id, secvență, `attempt`, start/end | wrapper de sesiune sau hook |

Orice unealtă capabilă să producă ultimele două poate consuma ProgressTrace. Nu e nevoie
de nicio modificare în Core, și — important pentru M9 — nu e nevoie de Hermes.

### 13.1 Nivelul 0 — orice unealtă, azi, fără hook-uri și fără cooperare

`gate_runner.py` este un wrapper de comandă: primește o comandă și un `gateKey`, o rulează
și scrie dovada. Dacă toate comenzile de acceptanță trec prin el, `gate-outcomes.json` e
complet — indiferent dacă deasupra se află Claude Code, Codex CLI, Cursor, Aider, un
workflow de CI sau un om.

`session.json` minimal se poate sintetiza dintr-un wrapper în jurul întregii invocări:
un `sessionId` generat, `startedAt`/`endedAt`, și câte o invocare per gate. Exact ce face
`AgentSessionFromGateOutcomesProjector` livrat în P0.3.

```
SESSION=$(uuidgen)
progresstrace generate-ledger task-contract.json "$TRACE" \
    ledger.json report.json --manifest-output-path manifest.json

# agentul lucrează — oricare ar fi el
gate_runner.py --pt-gate-key demo.check -- npm run check
gate_runner.py --pt-gate-key demo.build -- npm run build

progresstrace advise --session-path session.json --ledger-path ledger.json \
    --gate-outcomes-array-path gate-outcomes.json --out advisory.json
```

Ce obții la nivelul 0: statusul per obligație, clasificarea, `insufficient-evidence`,
`regressed` vs. `open`. Adică **întreaga valoare demonstrată a produsului** (§3).

Ce nu obții: numărul real de încercări, duratele fine, bugetul observat — deci `assess`
și `budget` rămân aproximative.

**Acesta este nivelul de la care trebuie să pornească orice integrare nouă, inclusiv
seria adversă din §11.4.** Retrospectiv, e nivelul la care pilotul din 8–10 septembrie
ar fi trebuit să ruleze: nu ar fi cerut niciunul dintre cele patru increment-uri Hermes
(P0.4 → P0.4d), și nu ar fi permis situația în care produsul nu e invocat (§12).

### 13.2 Nivelul 1 — cu hook-uri, unde există

Câștigi granițele reale de invocare, `attempt`, duratele și consumul — deci `assess` și
`budget` devin utilizabile pe date adevărate.

**Claude Code** expune familia de hook-uri necesară. Nume verificate direct din binarul
instalat (`claude`, 2026-09-10): `SessionStart`, `PreToolUse`, `PostToolUse`, `Stop`,
`SubagentStop`, `SessionEnd`, `UserPromptSubmit`, `PreCompact`, `Notification`.
Corespondența cu hook-urile Hermes deja instrumentate este aproape unu-la-unu:

| Hermes | Claude Code | Rol în lanț |
|---|---|---|
| `on_session_start` | `SessionStart` | deschide sesiunea, fixează `sessionId` și `traceId` |
| `post_tool_call` | `PostToolUse` | emite `gate-outcome` pentru apelurile de shell |
| `on_session_end` | `Stop` | marchează terminarea observației |
| `on_session_finalize` | `SessionEnd` | **rulează lanțul `advise` necondiționat** |

Un hook `PostToolUse` care filtrează apelurile de Bash și scrie un `gate-outcome` dă
captura fără nicio instrucțiune către agent. Observație de stare: `~/.claude/settings.json`
nu are niciun hook configurat la 2026-09-10 — integrarea cu Claude Code este nescrisă, dar
este cel mai ieftin al doilea consumator disponibil, și singurul care testează M9 fără
Hermes.

**Codex CLI** are suprafață de hook-uri mai săracă; acolo rămâne nivelul 0, cu wrapper.

Munca de adaptor din Hermes (P0.4 → P0.4d) nu este pierdută — ea a livrat nivelul 1. Dar
nu este o prerechiziție pentru un al doilea consumator.

### 13.3 Nivelul 2 — în CI, fără cooperarea niciunui agent

Cel mai robust, și probabil cel mai subestimat. Într-un workflow de CI, fiecare `step`
*este* un gate: are nume, comandă și exit code. Contractul se comite în repo, un job final
rulează `advise`, iar raportul de acoperire ajunge comentariu pe PR.

Avantajul decisiv: **nu depinde de niciun agent, de niciun harness și de nicio
bunăvoință.** Un PR generat de orice unealtă primește același raport. Iar întrebarea
„câte dintre promisiunile acestui PR au dovadă" este exact ce lipsește azi din review-ul
codului generat de AI.

Dacă decizia de la 2026-09-21 este **A (GO)**, acesta este drumul către al doilea
consumator din §8 — nu UI, nu hosted, nu adaptoare. Un action de CI, un contract exemplu
și un README de o pagină.

### 13.4 Regula de adopție, indiferent de nivel

Din §12.9, repetată aici pentru că e regula de integrare, nu doar de testare:
**invocarea aparține harness-ului, nu agentului.** Concret, în ordinea eficacității:

1. hook de finalizare care rulează lanțul necondiționat, fără ca agentul să poată sări;
2. condiție mecanică de închidere — sarcina nu se marchează încheiată fără `advisory.json`
   prezent și fără `PT703` nerezolvat;
3. harness-ul rescrie comenzile prin wrapper, nu îi cere agentului să-l folosească
   (prima serie de sesiuni a avut `matchedGateCount = 0/10` fiindcă wrapper-ul a fost omis
   — ce se poate omite, se omite);
4. omul citește `advisory.json` **înainte** de rezumatul agentului. Atunci incentivul se
   aliniază singur: singura cale a agentului către un raport curat e să facă lucrurile.

### 13.5 Recomandare pentru seria adversă

Nu prin Hermes. Nivelul 0, cu `gate_runner.py` și o unealtă oarecare — inclusiv Claude
Code, sau chiar cu omul în locul agentului pentru prima probă. Motive:

- elimină complet variabila „produsul nu a fost invocat" (§12), fiindcă cine rulează
  comenzile e cel care măsoară;
- testează implicit portabilitatea, adică M9 — singura metrică care separă „produs" de
  „unealtă personală";
- nu depinde de plumbing-ul P0.4b–P0.4d, care e merged doar în fork staging și nu în
  `main` Hermes (§11.2-K);
- durează o oră, nu o zi și jumătate de increment-uri.

---

## 14. Replanificare — seria adversă, decizie la 2026-10-11

*Adăugat 2026-09-27.*

### 14.1 Data de decizie

Data de decizie se mută de la 2026-09-21 la **2026-10-11**. Motivul este lipsa de timp, nu
un rezultat: seria adversă din §12 nu a rulat, deci ipoteza de produs a rămas netestată.
Regula din §7 rămâne neschimbată: la 2026-10-11 se alege obligatoriu **A, B sau C** (§8),
indiferent de stadiu. Dacă seria nu e completă până atunci, decizia se ia pe ce există,
iar „netestat" se scrie ca atare, nu ca `FREEZE`. Nu se mai mută a doua oară.

### 14.2 Proiectul și cele două piste

Seria rulează pe **Sibiul Neștiut** (`dragos-cociu/sibiul-nestiut`, deploy public pe
Cloudflare Pages din 2026-09-27) — proiect non-ProgressTrace și non-Hermes, cu muncă reală
de dezvoltare rămasă. Nu se adaugă altă variabilă nouă (kanban, profile specializate etc.);
fluxul de dezvoltare rămâne cel folosit până acum.

| Pistă | Cine | Ce face |
|---|---|---|
| Dezvoltare | Hermes ca PM, Claude Code arhitect, Codex dezvoltator, Gemini reviewer | primește task-uri obișnuite de dezvoltare; nu i se comunică existența seriei |
| Măsurare | Dragos + Claude Code, în afara Hermes, nivel 0 (§13.1, §13.5) | contract scris înainte, gate-uri rulate de cel care măsoară, `generate-ledger` → `advise` cu binarul real, `provenance.json`, `RealDecisionRecord` authored de Dragos |

### 14.3 Relația cu `phase-5-adverse-series-manual-test-plan.md`

Pașii P0–P10 din acel document rămân protocolul operațional. Se înlocuiește numai
alocarea rolurilor din §2 și §16 ale lui: **Hermes nu mai pregătește inputurile, nu
verifică artefactele și nu declară PASS/FAIL**. Aceste roluri trec la pista de măsurare.
Motiv: §12.9 și P4 — cel care orchestrează munca nu poate fi și cel care o măsoară, iar
verdictul pilotului din 2026-09-10 a arătat concret riscul. De asemenea, proiectul nu mai
este worktree-ul pilotului (`pilot-sibiul-nestiut-method-page`), ci sesiuni noi pe `main`.

### 14.4 Unde stau contractele

Contractele de sarcină, criteriul de falsificare per sesiune și artefactele de măsurare se
țin în afara acestui repository și în afara spațiilor citite de Hermes, într-un depozit de
evidence separat, versionat, cu commit înainte de fiecare sesiune. Motiv: P4.4 — agentul
care lucrează nu trebuie să știe ce obligație nu are gate. Acest document fixează doar
data, regula de decizie și rolurile.

### 14.5 Ordinea

1. controlul negativ din §12.3 — primul; dacă lanțul trece pe intrări stricate, seria se oprește;
2. trei contracte distincte (P5), fiecare cu cel puțin o obligație fără gate și un gate
   care poate eșua;
3. o sesiune de dezvoltare per contract, măsurată în paralel;
4. reproducere independentă și `feedback.json` generat din artefacte (§12.2, §12.6);
5. decizia A/B/C la 2026-10-11, după criteriul din §12.7, fără renegociere.
