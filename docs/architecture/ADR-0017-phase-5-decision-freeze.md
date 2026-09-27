# ADR-0017 — Decizia Phase 5: B (FREEZE la 1.0), precedată de bugfix F5/F1

- **Status:** acceptat
- **Data:** 2026-09-27 (adusă înainte față de termenul-limită 2026-10-11, pe date complete)
- **Decident:** Dragos Cociu
- **Surse:** `docs/phase-5-plan-and-decision-metrics.md` §7–§8, §12, §14;
  `progresstrace-experiments/experiments/07-phase-5-adversarial-series-sibiul/`
  (`series-result.md`, `findings.md`, `sessions/S*/feedback.json`, `evidence-history.log`)

## Întrebarea

Adaugă ProgressTrace, în condiții reale, informație pe care raportul agentului și exit
code-urile nu o dau? (M4, criteriul de falsificare din §12.7.)

## Cum s-a măsurat

Trei sesiuni de dezvoltare reale pe un proiect non-ProgressTrace, non-Hermes (Sibiul
Neștiut), cu contracte distincte (S1 selector de limbă + nume localizat; S2 schimbarea
limbii pe pagina echivalentă; S3 pagină 404 localizată). Dezvoltarea prin fluxul obișnuit
Hermes (Claude/Codex/Gemini), fără ca Hermes să știe de măsurare. Măsurarea la nivel 0, în
afara Hermes: contract și criteriu comise înainte de fiecare sesiune, gate-uri validate în
ambele direcții, binar construit din `23b7414`, `provenance.json` pentru fiecare rulare,
`RealDecisionRecord` authored de om. Control negativ §12.3 PASS 5/5 înainte de serie.

Prag fixat înainte de S1: zero semnale comportamentale **confirmate de om** pe S1–S3 ⇒ B.

## Rezultat

| Sesiune | Defecte reale | Găsite de | Semnale ProgressTrace confirmate |
|---|---|---|---|
| S1 | 2 (contrast; regresie hover introdusă de corectură) | om, vizual | 0 |
| S2 | 0 | — | 0 |
| S3 | 0 | — | 0 (2 candidați — gate supra-specificat, respinși de om) |

**0 semnale confirmate ⇒ B.** Agentul nu a raportat nimic fals în nicio sesiune; defectele
reale au fost vizuale și au scăpat întregului lanț automat (agent, reviewer LLM, gate-uri,
ProgressTrace).

## Constatări despre produs

- **F5 (gravă):** `advise` stabilește `status` după ultimul semnal; cu mai multe gate-uri pe
  aceeași obligație, un gate eșuat urmat de unul trecut dă `satisfied`. Un verificator care
  ascunde eșecuri face opusul promisiunii lui.
- **F1:** `classification` per obligație contrazice `docs/contracts/agent-session.md`:
  „pică acum, fără recuperare" iese `recovery-after-failed-attempt`; două gate-uri trecute
  în aceeași invocare ies `repeated-attempt-without-obligation-advancement`.
- **F2:** orice obligație fără gate face verdictul global și recomandarea constant
  `insufficient-evidence`.
- **F4 / ergonomie:** CLI-ul nu derivă `session.json`, nu expune coverage-report și nu
  validează standalone `RealDecisionRecord`.

## Decizia

**B — FREEZE la 1.0, ca unealtă personală**, după un singur release de bugfix:

1. **Bugfix, strict delimitat:** F5 și F1, cu teste de conformanță care reproduc cazurile
   din serie. Fără funcționalități noi, fără lărgirea vocabularului dincolo de ce cere
   corectitudinea F1, fără adaptoare/UI/CI.
2. **Freeze:** tag `v1.0.0` după integrarea bugfix-ului; README cu limitările cunoscute
   (F2, F4, faptul că verdictul e cel mult atât de bun cât gate-urile care îl alimentează).
3. **Writeup:** constatările seriei și metodologia de verificare (contract înainte, gate-uri
   validate în ambele direcții, fixture ascuns, verificare pe artefactul publicat, review
   uman, decizie pe prag prestabilit) — documentate ca **practică**, nu ca produs nou.

## Ce nu se face

- Nicio fază nouă (5.x, 6) pe ProgressTrace după `v1.0.0`.
- Metodologia nu devine proiect nou (varianta C) prin această decizie. Redeschiderea oricărei
  direcții cere o întrebare nouă, o dată de decizie și praguri numerice fixate înainte.

## Consecințe

- F2/F4 rămân limitări documentate.
- Valoarea demonstrată a seriei a venit din gate-uri bine proiectate și review uman, nu din
  agregarea ProgressTrace; asta se spune explicit în writeup.
