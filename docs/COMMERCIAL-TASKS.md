# Semi-kommerciel klargøring — parallelle opgaver

> Formål: gøre **Madplan & Indkøb** klar til en semi-kommerciel pilot (rigtige, evt. betalende, brugere).
> Denne fil er **kilden til sandhed** for opgave-opdelingen. Hver opgave er skrevet så en selvstændig
> agent kan udføre den alene og i parallel med de andre.
> Sidst opdateret: 2026-07-15. Se også [`COMMERCIAL.md`](COMMERCIAL.md) (forretning) og
> [`../apps/meal-shopping/ARCHITECTURE.md`](../apps/meal-shopping/ARCHITECTURE.md) (teknik).

---

## 0. Fælles spilleregler (gælder ALLE opgaver — læs først)

1. **Branch pr. opgave.** Lav en feature-branch **ud fra `develop`** (fx `feat/visual-modernisering`).
   Commit KUN til din egen branch. **Merg ikke selv** til `develop`/`main` — det gør ejeren (brugeren).
   Push din branch til `origin`, så den kan merges.
2. **Rør ikke deploy-modellen.** `main` = produktion (forbruger-app, ingen butik). `develop` = arbejde.
   Se branch-modellen i `ARCHITECTURE.md`.
3. **Dansk UI-tekst.** Al brugervendt tekst på dansk, som resten af appen.
4. **Database: kun additive, data-bevarende migrations.** ALDRIG drop/reset af produktions-DB
   (Neon `production`). Backend skal forblive **additiv** ift. `main`, så forbruger-appen (fra `main`)
   fortsat virker mod `develop`-backenden. Kun ÉN skema-opgave merges ad gangen — den næste rebaser
   og regenererer `Migrations/AppDbContextModelSnapshot.cs`.
5. **Ingen eksterne netværks-afhængigheder i frontend.** Appen er en offline-venlig PWA: self-host
   fonts/ikoner, brug inline-SVG, ingen CDN/eksterne scripts.
6. **Læs først:** `docs/ECOSYSTEM.md` + `apps/meal-shopping/ARCHITECTURE.md`.
7. **Test lokalt før push.** Backend: `dotnet run` i `apps/meal-shopping/backend/IndkobsApp.Api`
   (port 5298). Frontend: `npm start` i `apps/meal-shopping/frontend/indkobs-app` (port 4200).
   Verificér i browseren at intet er gået i stykker.
8. **Hemmeligheder kun via env/appsettings (dev).** Commit aldrig rigtige nøgler.

---

## 1. Opgave-oversigt & parallel-strategi

| ID | Opgave | Domæne (konflikt-zone) | Kan køre parallelt med | Afhænger af |
|----|--------|------------------------|------------------------|-------------|
| **T1** | Visuel modernisering (design system) | frontend: `styles.scss` + `app.ts` + ALLE page-templates | T4, T7 (andre domæner) | – |
| **T2** | Individuelle brugerkonti + signup/login/glemt-kode | backend-core + frontend auth-sider | T7 | (koordinér tokens m. T4) |
| **T3** | GDPR & jura: politik, betingelser, data-eksport/-sletning | docs + nye frontend-sider + ny backend-controller | T1, T4, T7 | signup-samtykke ↔ T2 |
| **T4** | Sikkerhedshærdning | backend: `Program.cs` + config + middleware | T1, T3, T7 | – |
| **T5** | Betaling / abonnement (Stripe) | backend-core + frontend billing-side | T7 | **T2** (anbefales) |
| **T6** | Fejllogning & overvågning (Sentry) | backend `Program.cs` + frontend `main.ts` | T1, T7 | (koordinér `Program.cs` m. T4) |
| **T7** | Automatiske tests + CI/CD | KUN nye filer (testprojekt + `.github/workflows`) | **alle** | – |
| **T8** | Onboarding, FAQ & support | nye frontend-sider + let shell-touch | T4, T7 | pænest efter T1 |
| **T9** | *(manuelt, ikke en agent)* Betalt hosting, backups, secrets-rotation, DPA'er | ops/Render+Neon | – | – |

### Anbefalede bølger (for færrest merge-konflikter)
- **Bølge A – start samtidigt nu:** **T1** (frontend), **T4** (backend), **T7** (nye filer), **T3** (mest nye filer).
  Fire forskellige domæner → minimal overlap.
- **Bølge B – efter A er merget:** **T2** (rebase på T1+T4), **T6** (rebase på T4's `Program.cs`), **T8** (rebase på T1).
- **Bølge C:** **T5** (kræver T2).
- **T9** kan gøres når som helst (manuelt af ejeren).

### Konflikt-hotspots at kende
- `apps/meal-shopping/backend/IndkobsApp.Api/Program.cs`: T4 + T6 → merg sekventielt.
- Backend-migrations + `AppDbContextModelSnapshot.cs`: T2 + T5 (+ evt. T3) → kun én skema-opgave merges ad gangen.
- Frontend `styles.scss`, `app.ts`, `app.routes.ts`: T1 ejer disse; T3/T8/T2 tilføjer ruter → merg T1 til sidst i frontend-gruppen (den polerer det der findes), eller rebase de andre på T1.

---

## 2. Opgave-beskrivelser (briefs)

### T1 — Visuel modernisering (professionelt look) ⭐
**Mål:** Løft appen fra "hobby" til et poleret, moderne, professionelt SaaS-udtryk — uden at ændre funktionalitet eller dansk tekst.

**Omfang:**
- **Design system i `src/styles.scss`:** forfin farvepalet (behold den grønne brand-identitet, men modernisér nuancer/kontrast); definér design-tokens (farver, typografi-skala, spacing, radius, elevation/skygger); ensartede komponenter: knapper (primary/secondary/ghost/danger) med hover/active/**focus-ring**, inputs med fokus-tilstand, cards, badges/pills, list-items, tomme tilstande, enkel skeleton/loading, og et diskret toast/snackbar-mønster. Bløde transitions/mikro-animationer.
- **Shell i `app.ts`:** modernisér topbjælke (brand/wordmark, evt. husstandsnavn) og bundnavigation (udskift emoji med ensartede **inline-SVG-ikoner**, tydelige aktive tilstande).
- **Anvend systemet på alle sider:** `pages/login.ts`, `week-plan.ts`, `shopping-list.ts`, `recipes.ts`, `pantry.ts`, `home-tasks.ts`, `admin.ts` (Varer), `item-groups.ts`, `shared-list.ts`.
- **Brand-aktiver:** simpelt logo/wordmark (inline-SVG) + polér PWA-ikoner/favicon.
- Bevar responsivt, touch-venligt og tilgængeligt (kontrast, fokus, `aria-*` hvor oplagt).

**Filer (ejer):** `src/styles.scss`, `src/app/app.ts`, alle `src/app/pages/*.ts`, PWA-ikoner i `public/`.
**Accept:** sammenhængende, professionelt look på tværs af alle sider; ingen eksterne net-afhængigheder; bygger rent; vis før/efter-screenshots via preview.
**Konflikt:** højeste frontend-overlap — merg til sidst i frontend-gruppen, eller lad de andre rebase på denne.

---

### T2 — Individuelle brugerkonti + selvbetjent signup/login/glemt-kode
**Mål:** Fra ét delt husstands-login til individuelle brugere der tilhører en husstand (til fakturering, personalisering, sikkerhed).

**Omfang (backend):**
- Ny `User`-entitet (`Id`, `HouseholdId` FK, `Email` unik, `PasswordHash`, `DisplayName`, `CreatedUtc`, `EmailConfirmed`). **Additiv, data-bevarende migration** — opret ved migrering en `User` pr. eksisterende husstand ud fra dens nuværende login, så intet eksisterende login går i stykker.
- `AuthController`: `signup` (opret husstand + første bruger, ELLER join eksisterende via invitation), `login` (email+kode), `forgot-password` (email-token), `reset-password`, `confirm-email`. JWT skal indeholde både `userId` og `householdId`.
- **E-mail bag et interface** (`IEmailSender`): dev = log til konsol; prod = pluggbar (SMTP/SendGrid) via config. Ingen hardcodet udbyder.

**Omfang (frontend):** split `pages/login.ts` i login + signup + glemt-/nulstil-kode-sider; opdatér `auth.ts` til at holde bruger; vis display-navn.
**Filer:** backend `Controllers/AuthController.cs`, `Models/Entities.cs`, `Data/AppDbContext.cs`, ny migration, `Services/TokenService.cs`, ny `Services/EmailSender.cs`; frontend `app/auth.ts`, `pages/login.ts` (+ nye sider), `app.routes.ts`, `api.ts`.
**Accept:** eksisterende husstande kan stadig logge ind; ny bruger kan selv oprette sig; glemt-kode virker end-to-end i dev (token i log).
**Koordinér:** token-levetid/refresh med **T4**.

---

### T3 — GDPR & jura: politik, betingelser, samtykke, data-eksport/-sletning
**Mål:** Opfyld EU/GDPR-minimum så man lovligt kan tage rigtige brugere.

**Omfang:**
- **Indholdssider (dansk):** Privatlivspolitik, Handelsbetingelser, kort cookie/samtykke-note. Som selvstændige rute-komponenter (fx `pages/legal-privacy.ts`, `pages/legal-terms.ts`) + links i menu/footer. Tilføj ruter i `app.routes.ts`.
- **Samtykke ved signup** (checkbox til politik/betingelser) — koordinér med **T2** (stub hvis T2 ikke merget endnu).
- **Backend selvbetjening:** ny `PrivacyController`: `export` (download af husstandens data som JSON) + `delete` (slet egen konto/husstand med bekræftelse; genbrug cascade-sletnings-logik fra `AdminController`).
- **Docs:** databehandleraftale-tjekliste (Neon, Render) + bekræft data i EU. Skriv i `docs/COMMERCIAL.md` eller ny `docs/GDPR.md`.
**Filer:** nye `pages/legal-*.ts`, `app.routes.ts`, ny `Controllers/PrivacyController.cs`, `Dtos/Dtos.cs`, evt. `docs/GDPR.md`.
**Accept:** siderne kan tilgås; eksport returnerer JSON; sletning cascader sikkert; ansvarsfraskrivelser er til stede.

---

### T4 — Sikkerhedshærdning
**Mål:** Luk de vigtigste sikkerhedshuller før betalende brugere.

**Omfang (backend):**
- **Rate limiting** (ASP.NET `RateLimiter`) på auth- og skrive-endpoints (429 ved burst).
- **Stram CORS** til kendte origins via env (ikke permissiv).
- **Token-levetid:** kortere access-token + refresh-token-mekanisme (koordinér med **T2**).
- **Security headers** (HSTS, `X-Content-Type-Options`, `Referrer-Policy`, m.fl.) som middleware.
- **Secrets-hygiejne:** bekræft ingen hemmeligheder i repo; dokumentér rotation; verificér `X-Admin-Key`/`X-Store-Key` er env-drevne; overvej nøgle pr. butik.
- **Input-validering** gennemgået på controllers.
**Filer (ejer):** `Program.cs`, `appsettings.json`, evt. nye `Middleware/*.cs`.
**Accept:** burst → 429; ukendt origin afvises; headers til stede; eksisterende flows virker; bygger.
**Konflikt:** ejer `Program.cs` — **T6** rebaser på denne.

---

### T5 — Betaling / abonnement (Stripe)
**Mål:** Kunne tage betaling for appen (abonnement).

**Omfang:**
- **Abonnements-model:** husstand får `SubscriptionStatus`/`Plan`/`TrialEnds`. Stripe Checkout + Customer Portal + **webhook** der opdaterer status. Nøgler via config (test-nøgler i dev).
- **Gating:** definér gratis vs. betalt (hold det enkelt — fx ordrer/butik + katalog-publicering som premium). Håndhæv i backend.
- **Frontend:** abonnements-/billing-side, opgraderings-CTA, status-visning.
**Filer:** ny `Controllers/BillingController.cs`, `Models/Entities.cs`, migration, `Services/StripeService.cs`, `Program.cs` (webhook-route), frontend ny `pages/billing.ts`, `app.routes.ts`, `api.ts`.
**Accept:** test-mode checkout virker; webhook flipper status; gated feature respekterer status.
**Afhænger:** **T2** (individuelle konti) stærkt anbefalet.

---

### T6 — Fejllogning, overvågning & observability
**Mål:** Se fejl og oppetid i produktion.

**Omfang:**
- **Sentry** (eller tilsvarende) i backend + frontend; DSN via env; **no-op hvis DSN mangler**.
- Struktureret logging + request-logging; `/health` (readiness) endpoint (udover eksisterende `/api/categories`).
- Noter om ekstern oppetids-overvågning + alarmering.
**Filer:** backend `Program.cs`, frontend `src/main.ts`, config.
**Accept:** kastet fejl havner i Sentry (dev m. DSN); `/health` svarer ok; ingen crash når DSN mangler.
**Konflikt:** `Program.cs` (koordinér med **T4**); rebase på T4.

---

### T7 — Automatiske tests + CI/CD  ✅ (mest parallel-sikker)
**Mål:** Beskyt kernelogik mod regressioner + automatisér build.

**Omfang:**
- **Backend unit-tests (xUnit)** for kernelogik: `ShoppingListService` (aggregering + enheds-konvertering g↔kg / ml↔l, inkompatible enheder, kategori-sortering), pantry-afstemning, `IngredientService`-normalisering, auth/token. Nyt projekt `apps/meal-shopping/backend/IndkobsApp.Api.Tests`.
- **Frontend:** nogle få nøgle-tests (som konfigureret) eller minimum lint+build i CI.
- **GitHub Actions:** workflow der bygger backend + kører tests, bygger frontend, ved PR mod `develop`/`main`.
**Filer (kun nye):** `apps/meal-shopping/backend/IndkobsApp.Api.Tests/**`, `.github/workflows/ci.yml`, evt. `.sln` (koordinér).
**Accept:** `dotnet test` grøn; CI-workflow grøn.

---

### T8 — Onboarding, FAQ & support
**Mål:** Ny bruger forstår appen og kan få hjælp.

**Omfang:**
- Første-gangs-onboarding (få trin/guidede tomme tilstande der forklarer madplan → indkøbsliste → lager → ordrer).
- FAQ-side + "Kom godt i gang" + support-link (mail).
- Forbedrede tomme tilstande (koordinér med **T1**'s design system).
**Filer:** nye `pages/onboarding.ts`, `pages/faq.ts`, `app.routes.ts`, let touch i `app.ts`.
**Accept:** ny bruger ser onboarding én gang; FAQ kan tilgås.
**Afhænger:** pænest efter **T1** (kan bruge eksisterende styles indtil da).

---

### T9 — (Manuelt) Betalt hosting, backups, secrets-rotation, DPA'er
**Ikke en agent-opgave — ejeren gør dette i Render/Neon-dashboards:**
- Opgradér Render (backend) + Neon til betalt niveau (ingen cold start / lagerloft).
- Slå **backups** til (Neon).
- **Rotér alle hemmeligheder:** Neon-adgangskode, `Jwt__Key`, `Admin__Key`, `Stores__AccessKey`.
- Underskriv databehandleraftaler (Neon, Render); bekræft data i EU (Frankfurt).
