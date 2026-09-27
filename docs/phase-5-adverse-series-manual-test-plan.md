# Phase 5 — plan de testare adversă manuală, ghidată

**Statut:** plan operațional; nu autorizează modificări de cod, push sau merge.
**Scop:** validarea ProgressTrace însuși, nu doar a shadow tooling-ului Hermes.
**Mod de lucru:** Dragos execută manual pașii cu un agent extern; Hermes pregătește inputurile, verifică outputurile și nu avansează fără evidence.
**Sursă:** review extern 2026-09-10 și protocolul din `phase-5-plan-and-decision-metrics.md` §11–§13.

---

## 1. Regula centrală

Nu declarăm ProgressTrace testat până când nu există dovadă că binarul ProgressTrace a fost invocat efectiv.

Artefactele Hermes/shadow nu sunt suficiente:

- `capture.jsonl`;
- `manifest.json`;
- `gate-outcomes.json` produs de tooling extern;
- `CorrelationManifest` authored de pilot;
- outputul `normalizer.py`.

Pentru fiecare output ProgressTrace trebuie să existe `provenance.json` cu:

```json
{
  "argv": [],
  "cwd": "...",
  "binarySha256": "...",
  "repoCommit": "...",
  "exitCode": 0,
  "stdout": "...",
  "startedAt": "...",
  "endedAt": "..."
}
```

Niciun output fără provenance nu este evidence.

---

## 2. Roluri

### Dragos

- rulează manual comenzile pregătite împreună;
- folosește agentul extern ales de el;
- păstrează outputurile exact, fără curățare sau reformatare;
- trimite outputul și calea artefactelor după fiecare pas;
- nu schimbă contractul sau comenzile fără să anunțe.

### Hermes

- pregătește inputul următor;
- verifică argv, cwd, commit, hash, exit code și schema;
- tratează outputul agentului ca declarație neverificată până la read-back;
- decide dacă pasul este PASS, FAIL sau BLOCKED;
- nu execută în locul lui Dragos sesiunea manuală;
- nu repară retrospectiv artefactele.

### Agentul extern

- lucrează pe proiectul extern;
- nu conduce măsurătoarea și nu primește obiectivul intern M4;
- nu este sursa de adevăr pentru faptul că ProgressTrace a rulat.

---

## 3. Proiect eligibil și izolare

Folosim un proiect non-ProgressTrace și non-Hermes, cu evidence persistentă pe disc:

```text
/srv/projects/.tooling/pilot-sibiul-nestiut-method-page
```

Înainte de testare se verifică și se arhivează:

```text
git status --short --branch
git rev-parse HEAD
git diff --exit-code
```

Se creează un director persistent de evidence, de exemplu:

```text
/srv/projects/.tooling/progresstrace-manual-adverse-20260910/
```

Nu se folosește `/tmp` pentru artefacte finale.

---

## 4. Ordinea obligatorie a seriei

```text
P0  înghețare și baseline
P1  control negativ
P2  contract advers authored
P3  generate-ledger ProgressTrace real
P4  execuție manuală adversă cu agent extern
P5  advise ProgressTrace real
P6  RealDecisionRecord authored
P7  reproducere independentă byte-for-byte
P8  feedback.json generat din artefacte
P9  evaluare M1–M10
P10 verdict GO/FREEZE/PIVOT
```

Fiecare pas este o poartă. Dacă un pas eșuează, ne oprim și verificăm; nu continuăm pentru a obține un verdict favorabil.

---

## 5. P0 — baseline

Input pregătit de Hermes, executat de Dragos:

```text
repo path
branch
commit
status
ProgressTrace binary path
binary SHA-256
ProgressTrace version/help output
```

Se arhivează:

```text
baseline.json
provenance-baseline.json
```

Criteriu PASS:

- repository extern curat;
- commit fixat;
- binar identificat;
- SHA-256 calculat;
- outputul binarului provenit din comanda exactă este păstrat.

---

## 6. P1 — control negativ, primul

Înainte de seria reală se rulează inputuri deliberate invalide folosind ProgressTrace real:

1. `obligationId` inexistent în ledger → `PT402` / dangling obligation;
2. `traceId` diferit între session și ledger → reference mismatch;
3. două `outcomeId` identice → duplicate outcome;
4. `sourceDigest` non-64-hex → invalid source digest;
5. obligație legată fără evidence → `PT703` / insufficient evidence.

Pentru fiecare caz se păstrează:

```text
input/
stdout
stderr
exit code
provenance.json
expected-diagnostic.json
```

Criteriu PASS: fiecare control negativ eșuează cu diagnosticul așteptat. Dacă un caz invalid trece, seria se oprește.

---

## 7. P2 — contract advers

Contractul trebuie authored o singură dată și să conțină cel puțin trei `deliverables` distincte:

```text
deliverables:0 — rezultat de implementare/modificare
deliverables:1 — verificare build/check
deliverables:2 — integritate/documentare sau altă promisiune observabilă
```

Doar două acceptances primesc `obligationRef`:

```text
acceptance A → deliverables:0
acceptance B → deliverables:1
deliverables:2 → fără gate intenționat
```

Contractul nu trebuie să folosească:

- obligații identice cu gate keys;
- mapare pozițională implicită;
- manifest authored separat;
- inferență din cwd, prompt, ordine sau numele comenzii.

Dragos trimite contractul înainte de rulare. Hermes verifică schema, binding-urile și faptul că a treia obligație este realmente neacoperită.

---

## 8. P3 — generate-ledger real

Dragos rulează binarul ProgressTrace, nu un script shadow, cu contractul advers:

```text
ProgressTrace generate-ledger \
  task-contract.json \
  <trace-id> \
  ledger.json \
  ledger-generation-report.json \
  --manifest-output-path correlation-manifest.json
```

Forma exactă se adaptează CLI-ului verificat, fără a schimba semantic inputul.

Se păstrează pentru fiecare output:

```text
ledger.json
ledger-generation-report.json
correlation-manifest.json
provenance.json
```

Hermes verifică:

- `obligationId` de forma derivată din `taskContractId:sourceField:index`;
- `match.type` emis de ProgressTrace;
- existența tuturor celor trei deliverables;
- doar două binding-uri authored;
- digesturile și trace ID consistente.

Nu se acceptă un manifest produs de `normalizer.py` sau scris manual.

---

## 9. P4 — sesiunea adversă manuală

Dragos rulează agentul extern pe repository-ul extern. Agentul trebuie să lucreze efectiv și să raporteze terminat la final.

Agentul nu primește explicația că testăm M4 sau că trebuie să lase intenționat o obligație fără evidence.

Seria trebuie să conțină:

1. cel puțin un gate cu `exitCode != 0`;
2. cel puțin o obligație atinsă după o încercare eșuată;
3. cel puțin o obligație fără gate/evidence;
4. o stare finală în care agentul declară terminat.

Gate-urile se rulează prin wrapperul explicit:

```text
python3 /srv/projects/progresstrace-experiments/experiments/04-phase-4-7-v4-phase-a/gate_runner.py \
  --pt-gate-key <gate-key> -- <command>
```

Dar wrapperul nu înlocuiește ProgressTrace. El produce doar inputul operațional pentru lanțul ulterior.

Se arhivează:

```text
session.json
gate-outcomes.json
agent transcript/output
command stdout/stderr
provenance pentru fiecare comandă
```

---

## 10. P5 — advise real

După sesiune, Dragos rulează binarul ProgressTrace:

```text
ProgressTrace advise \
  --session-path session.json \
  --ledger-path ledger.json \
  --gate-outcomes-array-path gate-outcomes.json \
  --out advisory.json
```

Se păstrează:

```text
advisory.json
provenance-advise.json
```

Rezultatul căutat este, conceptual:

```text
deliverables:0 → satisfied / progress
deliverables:1 → regressed / recovery-after-failed-attempt
deliverables:2 → open / insufficient-evidence
```

Nu se cere exact această combinație dacă datele reale justifică alt rezultat; se verifică însă că obligația fără evidence este semnalată și că failure-ul este separat de lipsa încercării.

---

## 11. P6 — RealDecisionRecord

Dragos scrie manual, după consultarea lui `advisory.json`, un `RealDecisionRecord` valid, cu decizia efectivă:

```text
completed
```

Recordul nu se deduce din raportul agentului și nu se generează automat de Hermes.

Se verifică:

- schema;
- `aligned` calculabil, nu `null` din lipsă de record;
- legarea la aceeași sesiune/trace;
- provenance pentru authoring și validare.

---

## 12. P7 — reproducere independentă

Un al doilea executor, fără să modifice inputurile, reia:

```text
task-contract.json
ledger.json
ledger-generation-report.json
correlation-manifest.json
session.json
gate-outcomes.json
```

și compară byte-for-byte:

```text
ledger
ledger-generation-report
correlation-manifest
AgentSession
advisory
coverage output
```

Dacă outputurile nu sunt identice, seria este invalidă și nu se calculează metricile.

---

## 13. P8 — feedback generat automat

`feedback.json` trebuie produs de un script care citește artefactele, nu scris manual în funcție de narațiune.

Câmpuri obligatorii:

```json
{
  "sessionId": "...",
  "taskContractId": "...",
  "project": "...",
  "authoredArtifactCount": 1,
  "authoringMinutes": 0,
  "obligationsTotal": 3,
  "obligationsAutoBound": 2,
  "coreCommitsRequired": 0,
  "instrumentationCommitsAllStack": 0,
  "toldMeSomethingExitCodesDidNot": true,
  "realDecisionRecordPresent": true,
  "what": "...",
  "issues": []
}
```

Dacă o metrică nu poate fi calculată, valoarea rămâne `null`; nu se completează cu proxy.

---

## 14. P9 — metrici și criterii

### M1–M5 primare

- **M1:** doar task contract authored; fără manifest/AgentSession authored separat;
- **M2:** coverage real din `LedgerGenerationReport`, nu `gateKey == obligationId`;
- **M3:** timp măsurat contemporan, cu cronometru real;
- **M4:** `advise` a produs informație indisponibilă din exit codes;
- **M5:** toate commiturile de instrumentare din Core, Hermes și shadow tooling.

### M6–M10 secundare

- **M6:** insuficiență cauzată de agent versus instrumentare;
- **M7:** `aligned`, numai dacă există RealDecisionRecord;
- **M8:** contradicții reale, nu zero derivat din `aligned:null`;
- **M9:** proiecte externe non-ProgressTrace/non-Hermes cu evidence persistent;
- **M10:** reproducere deterministă byte-for-byte.

---

## 15. P10 — decizia

Criteriul este fixat înainte de execuție:

- `advise` detectează obligația neacoperită înainte de evaluarea umană → **GO**;
- nici în condiții adverse nu există informație peste exit codes → **FREEZE**;
- testul nu poate rula fără încă un increment de instrumentare → **PIVOT**.

Nu se schimbă pragurile după rezultate.

---

## 16. Protocol conversațional cu Dragos

Pentru fiecare pas:

1. Hermes trimite inputul exact și ce trebuie păstrat;
2. Dragos rulează manual cu agentul extern;
3. Dragos trimite stdout/stderr, exit code și lista fișierelor;
4. Hermes verifică read-only artefactele și provenance;
5. Hermes declară `PASS`, `FAIL` sau `BLOCKED`;
6. numai la `PASS` se pregătește pasul următor.

Nu sărim peste pași pe baza afirmației agentului extern. Nu reparăm artefacte produse greșit. Nu folosim rezultate din sesiuni anterioare pentru a completa lipsuri.

---

## 17. Human gates

Necesită aprobare explicită înainte de:

- modificarea codului ProgressTrace;
- modificarea Core;
- orice nou increment Hermes;
- push sau merge în `main`;
- schimbarea criteriului de decizie.

Testarea manuală descrisă aici poate începe fără modificarea codului și fără merge în `main`.
