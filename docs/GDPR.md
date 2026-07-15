# GDPR & jura — Madplan & Indkøb

> Intern arbejds- og tjekliste for at opfylde **EU/GDPR-minimum**, så appen lovligt kan tage
> rigtige (evt. betalende) brugere. Hører til opgave **T3** i [`COMMERCIAL-TASKS.md`](COMMERCIAL-TASKS.md).
> Brugervendt jura ligger i appen: `/privatliv` (Privatlivspolitik) og `/handelsbetingelser`.
> Sidst opdateret: 2026-07-15.

---

## 1. Hvad er implementeret (kode)

| Krav (GDPR) | Hvor | Status |
|-------------|------|--------|
| Privatlivspolitik (art. 13-14) | `frontend/.../pages/legal-privacy.ts` → rute `/privatliv` | ✅ side (tekst-pladsholdere skal udfyldes) |
| Handelsbetingelser | `frontend/.../pages/legal-terms.ts` → rute `/handelsbetingelser` | ✅ side (tekst-pladsholdere skal udfyldes) |
| Cookie/samtykke-note | afsnit i Privatlivspolitik | ✅ (kun teknisk nødvendig localStorage → intet banner nødvendigt) |
| Samtykke ved signup | `frontend/.../shared/consent-checkbox.ts` | ⏳ **stub** — wires ind af **T2** (signup findes ikke endnu) |
| Ret til dataportabilitet (art. 20) | `GET /api/privacy/export` (`Controllers/PrivacyController.cs`) | ✅ JSON-download af husstandens data |
| Ret til sletning (art. 17) | `POST /api/privacy/delete` (bekræftes med adgangskode) | ✅ cascade-sletning via `Services/HouseholdEraser.cs` |
| Links til jura | sidefod i `app.html` + krydslinks + login-side | ✅ |

**Design-noter**
- Eksport og sletning er **husstands-scopede** (`User.GetHouseholdId()`), så ingen kan røre andres data.
- Sletning genbruger den **fælles cascade-logik** (`HouseholdEraser.EraseAsync`), som admin-sletning
  også bruger — de kan derfor ikke komme ud af sync. Sletter i afhængighedsorden pga. `Restrict`-FK'er
  på ingredienser/kategorier (se `ARCHITECTURE.md` §4).
- Eksporten er **flad og cykel-fri** (egne `Export*`-DTO'er) og udelader adgangskode-hash m.m.

---

## 2. Skal udfyldes af den dataansvarlige (ejeren)

Erstat pladsholderne `[…]` i **begge** juridiske sider samt afsnit 3-4 nedenfor:

- [ ] Dataansvarliges **navn/virksomhed**, **adresse**, **CVR-nr.**
- [ ] **Kontakt-email** for databeskyttelse (fx `privatliv@…`).
- [ ] Konkrete **abonnementsvilkår** (pris, periode, opsigelse) når betaling (T5) indføres.
- [ ] Bekræft/opdatér listen af **databehandlere** (afsnit 3) hvis der tilføjes flere (fx Stripe, Sentry).

---

## 3. Databehandleraftaler (DPA) — tjekliste

Vi (dataansvarlig) bruger følgende **databehandlere**. Der skal foreligge en gyldig DPA (Data Processing
Agreement) med hver, og databehandlingen skal ske i **EU/EØS** (eller med gyldigt overførselsgrundlag).

### Neon (PostgreSQL-database)
- [ ] DPA indgået/accepteret (Neon tilbyder en standard-DPA — accepteres i konsollen / via legal-side).
- [ ] Projektets database-region sat til **EU (Frankfurt / `eu-central-1`)** — bekræft i Neon-konsollen.
- [ ] Backups slået til (Neon point-in-time restore) — hører også under **T9**.
- [ ] Adgang begrænset; connection-string kun som secret (env `ConnectionStrings__Default`).
- Reference: Neon Trust Center / "Data Processing Addendum" på neon.tech.

### Render (server-hosting af backend)
- [ ] DPA indgået/accepteret (Render har en standard-DPA i deres legal-dokumenter).
- [ ] Web service kører i **EU-region (Frankfurt)** — bekræft i Render-dashboardet.
- [ ] Secrets kun som env-vars (ikke i repo): `Jwt__Key`, `Admin__Key`, `ConnectionStrings__Default`.
- Reference: Render "Data Processing Agreement" / Security-side på render.com.

### (Fremtidige — tilføj DPA før de tages i brug)
- [ ] **Stripe** (betaling, T5) — er selv dataansvarlig for betalingsdata; DPA + underretning i politik.
- [ ] **Sentry** (fejllogning, T6) — DPA + EU-datalagring / scrub af persondata i events.
- [ ] E-mail-udbyder (T2 `IEmailSender`, fx SendGrid/SMTP) — DPA + EU-region.

---

## 4. Data i EU — bekræftelse

| Tjeneste | Rolle | Region | Bekræftet |
|----------|-------|--------|-----------|
| Neon | Database (persondata) | EU – Frankfurt | [ ] |
| Render | Backend-hosting | EU – Frankfurt | [ ] |
| Render Static | Frontend (ingen persondata i sig selv) | (CDN) | [ ] |

Ved fuldt EU-hosting kræves der **ikke** særskilt overførselsgrundlag (SCC'er). Vælges en ikke-EU-region
skal grundlag (fx EU SCC'er / adequacy) på plads **før** brug.

---

## 5. Retningslinjer / tilbageværende (ikke kode)

- [ ] **Fortegnelse over behandlingsaktiviteter** (art. 30) — kort internt dokument (kan udvides herfra).
- [ ] **Procedure ved brud** (art. 33-34) — hvem gør hvad, 72-timers anmeldelse til Datatilsynet.
- [ ] **Slette-/opbevaringspolitik** dokumenteret (uge-oprydning = 5 uger; konto slettes on demand).
- [ ] Beslut om der reelt er behov for **DPO** (sandsynligvis nej ved denne skala) — dokumentér vurderingen.

---

## 6. Sådan wirer T2 samtykke ind ved signup

`shared/consent-checkbox.ts` er klar. Når signup-siden bygges (T2):

```html
<consent-checkbox [(accepted)]="consent" />
<button [disabled]="!consent()" (click)="signup()">Opret konto</button>
```

Registrér gerne accept-tidspunktet ved oprettelse (fx felt på `User`/`Household`) som dokumentation for
samtykke. Selve politik-/betingelses-teksten ligger allerede på `/privatliv` og `/handelsbetingelser`.
