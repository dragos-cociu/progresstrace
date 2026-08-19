# ADR-0006: Proveniența și atestarea sesiunilor cu agenți AI

## Status

Proposed. Documentat pentru o eventuală continuare a proiectului; **nu este
implementat** și nu schimbă încă niciun contract sau flux existent.

## Context

ProgressTrace poate analiza rezultate produse într-un workflow cu agenți AI, dar
un rezultat local și hash-ul său local nu demonstrează singure că:

1. o sesiune cu un agent a avut loc;
2. payload-ul exact a fost trimis providerului declarat;
3. răspunsul provine din acea invocare;
4. jurnalul nu a fost rescris înainte de calcularea hash-ului.

În review-ul extern Gemini existent, sistemul păstrează deja hash-uri pentru
request, packet, verdict, payload și artefactele asociate, precum și commitul,
tree-ul, modelul, providerul și tokenii numărați. Acest lucru oferă integritate
locală și trasabilitate, dar nu o atestare externă rezistentă la falsificare de
către un proces care controlează același workspace.

## Decizie propusă

Dacă proiectul continuă, se va adăuga un **Evidence/Provenance Layer** separat
de motorul semantic. Stratul va produce dovezi verificabile pentru fiecare
sesiune, fără să pretindă că poate demonstra procesele interne de reasoning ale
modelului.

### 1. Jurnal canonical de evenimente

Fiecare etapă relevantă va fi serializată determinist și va conține cel puțin:

- `sessionId` și `sequence` monoton;
- timestamp UTC și sursa ceasului;
- actorul (`controller`, `provider`, `verifier`, `human`);
- tipul evenimentului;
- versiunea schemei și a clientului;
- hash-ul inputului și outputului;
- hash-ul evenimentului precedent;
- metadatele providerului, fără credentiale.

Payload-urile voluminoase vor fi păstrate ca artefacte content-addressed; jurnalul
va păstra hash-ul și referința lor, nu copii arbitrare ale conținutului.

### 2. Hash chain și root de sesiune

Pentru evenimentul `n`:

```text
H(n) = SHA-256(canonical_event(n) || H(n-1))
```

La închiderea sesiunii, evenimentele vor produce un root canonical. Pentru
sesiuni sau loturi mari se poate folosi un Merkle tree, cu manifestul frunzelor
și root-ul păstrate separat.

### 3. Semnătură în afara controlului agentului

Root-ul nu va fi considerat atestat dacă este doar hash-uit local. Semnătura
va fi generată de o cheie privată care nu este disponibilă procesului agentului
sau workspace-ului său, de exemplu:

- TPM sau hardware security key;
- KMS/remote signing service;
- un serviciu controlat separat de controller;
- o aprobare umană care semnează root-ul final.

Cheia publică, identificatorul cheii și algoritmul vor fi incluse în manifestul
verificabil. Credentialele providerilor nu vor fi incluse în manifest.

### 4. Ancorare externă și timestamp

Pentru o garanție mai puternică împotriva rescrierii ulterioare, root-ul semnat
va fi ancorat într-un sistem extern append-only, cum ar fi un serviciu RFC 3161,
Rekor/Sigstore, un artefact remote cu retenție sau un alt serviciu aprobat.
Sistemul ales trebuie să păstreze dovada timestamp-ului și răspunsul de
ancorare, nu doar URL-ul serviciului.

### 5. Receipt al providerului

Când providerul îl oferă, jurnalul va păstra și:

- modelul cerut și modelul raportat;
- providerul și endpointul logic;
- request ID/correlation ID;
- token usage și statusul răspunsului;
- hash-ul payload-ului trimis și al răspunsului primit;
- timestamp-ul și versiunea adapterului.

Aceste câmpuri întăresc dovada, dar un simplu request ID furnizat de client nu
va fi tratat automat ca atestare independentă.

## Ce poate și ce nu poate demonstra

Acest mecanism poate demonstra, cu o încredere proporțională cu protecția cheii
și cu independența ancorării, că un controller a înregistrat și a atestat un
payload, un răspuns și o secvență de evenimente la un anumit moment.

Nu poate demonstra că modelul a „gândit” într-un anumit mod și nici nu poate
exclude orice compromis al providerului. Formularea permisă este:

> La momentul T, controllerul a asociat payload-ul cu hash-ul X, l-a trimis pe
> ruta provider/model declarată, a primit răspunsul cu hash-ul Y și a închis
> jurnalul cu root-ul Z; modificările ulterioare detectabile invalidează
> semnătura sau lanțul.

Nu se va spune că un hash local dovedește singur că agentul a produs conținutul.

## Compatibilitate și introducere incrementală

Implementarea poate începe fără schimbarea motorului semantic:

1. manifest canonical pentru input, prompt, config, model și output;
2. hash chain local și verificator independent;
3. export semnat de root;
4. abia apoi remote signing și ancorare externă;
5. integrare în rezultatele ProgressTrace printr-un bloc de provenance versionat.

Versiunea provenance trebuie să fie independentă de versiunea clasificatorului,
iar lipsa unei atestări externe trebuie raportată explicit ca limitare, nu ca
succes implicit.

## Cerințe de securitate

- Nu se păstrează API keys, OAuth tokens sau parole în jurnal.
- Agentul nu primește acces la cheia de signing.
- Artefactele sunt scrise append-only sau într-un storage cu retenție verificabilă.
- Verificatorul trebuie să poată rula separat, read-only, pe manifest și artefacte.
- Orice reîncercare, fallback, schimbare de model sau eroare de transport devine
eveniment separat; nu se suprascrie evenimentul inițial.
- Un verdict invalid sau lipsa unui receipt rămâne `unattested`/`incomplete`, nu
se transformă în `success` printr-un hash calculat ulterior.

## Criterii pentru implementare ulterioară

Înainte de adoptare trebuie demonstrat prin teste că:

- o modificare a oricărui eveniment rupe hash chain-ul;
- o reordonare a evenimentelor schimbă root-ul;
- modificarea promptului, modelului sau outputului schimbă manifestul;
- o cheie disponibilă agentului nu este acceptată pentru atestare puternică;
- verificatorul detectează o semnătură invalidă sau un timestamp lipsă;
- exportul este byte-reproductibil pe două medii;
- un provider fără receipt independent este marcat corect ca având doar
  integritate locală.

## Consecințe

Se obține o separare clară între:

- integritate locală a artefactelor;
- provenance a controllerului;
- atestare criptografică;
- dovadă externă de timestamp/provider.

Costurile sunt complexitate operațională, protecția cheii, retenție și posibil
cost pentru serviciul de timestamp/signing. Aceste costuri sunt justificate doar
pentru workflow-uri în care auditabilitatea sesiunii are valoare reală.

## Relații

- `ADR-0005-external-review-and-clean-room-reproducibility.md`
- `docs/security.md`
- `docs/benchmark-annotation-rubric.md`
