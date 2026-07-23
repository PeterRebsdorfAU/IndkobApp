# Udgivelse til produktion — step by step

> Sådan får du alt fra `integration` live. Følg punkterne **i rækkefølge**.
> Verificeret 2026-07-23: `develop → integration` er fast-forward, og `integration → main`
> merger konfliktfrit. Alle tests grønne (135), begge builds rene.

**Hvad der bliver udgivet:** hele feature-bølgen (individuelle konti, opskrift-billeder + AI-scan,
fremgangsmåde, selektiv deling, fri-tekst enheder, Varer auto-gem, GDPR, sikkerhed, onboarding,
Lunar-design). Butiks-ordrer + kontakt-support er **skjult** via feature-flags (koden er der, vises ikke).

---

## FASE 0 — Tag backup FØRST (vigtigst) ⚠️
Deployet kører 6 nye migrations mod din **rigtige** Neon `production`-database. De fleste er additive,
men to ændrer eksisterende data:
- `DropPantryItems` — **sletter lager-tabellen** (bevidst; lager-featuren er fjernet).
- `FreeTextUnits` — omskriver enheds-værdier (fx `Daase`→`dåse`) + udvider kolonnen (data-bevarende).
- `AddUsers` — opretter én bruger pr. eksisterende husstand ud fra nuværende login (data-bevarende).

**Gør dette i Neon:**
1. Neon-dashboard → dit projekt → **Branches** → **New Branch**.
2. Navn fx `backup-foer-release`, **Parent: `production`**, **"Branch data and schema"**, Auto-delete: Never.
3. Klik Create. (Det er dit gendannelsespunkt — hvis noget går galt, kan du restore/pege på den.)

---

## FASE 1 — Sæt miljøvariabler på backenden i Render (FØR deploy)
Gå til Render → servicen **`indkobapp-api`** → **Environment**. Disse er **kritiske**:

| Variabel | Værdi | Hvorfor |
|---|---|---|
| `Cors__AllowedOrigins__0` | din frontend-URL, fx `https://indkobapp-web.onrender.com` | **Uden den blokeres ALLE kald fra frontenden** (fail-closed). Appen ser død ud. |
| `Stores__AccessKey` | en tilfældig værdi (ikke `butik1234`) | Ellers **nægter backenden at starte** (dev-standard afvises i prod). |
| `Jwt__Key` | (allerede genereret) | Bekræft at den findes og ikke er dev-standarden. |
| `Admin__Key` | (allerede genereret) | Bekræft findes. |
| `ConnectionStrings__Default` | din Neon `production` .NET-connection string | Skal pege på produktion. |

**Valgfrit (appen virker uden, men funktioner er så slået fra):**
| Variabel | Effekt |
|---|---|
| `Gemini__ApiKey` + `Gemini__Model` | Tænder AI-scan af opskrift-billeder. Uden = knappen skjult. |
| `Email__Provider=smtp` + `Email__Smtp__Host/Port/User/Password/From` | Rigtige mails (bekræftelse/glemt-kode). Uden = mails logges kun, sendes ikke. |
| `Sentry__Dsn` | Fejlovervågning. |

---

## FASE 1b — Bekræft backend-URL'en matcher
Frontenden kalder den URL der står i `apps/meal-shopping/frontend/indkobs-app/src/environments/environment.prod.ts`:
```
apiBase: 'https://indkobapp.onrender.com/api'
```
1. Find din backend-services faktiske URL i Render (øverst på `indkobapp-api`-siden).
2. **Hvis den er en anden** (fx `https://indkobapp-api.onrender.com`): sig til, så retter jeg
   `environment.prod.ts` på `integration` **før** du merger (det er en kode-ændring). Ellers rammer
   frontenden det forkerte sted, og intet loader.

---

## FASE 2 — Merge `integration` til produktions-branchene
Kør i en terminal i repoet (eller brug GitHub's "merge" hvis du foretrækker det):

```bash
git fetch origin
# Backend-branchen (rent fast-forward):
git checkout develop
git merge --ff-only origin/integration
git push origin develop
# Frontend-branchen (konfliktfri merge):
git checkout main
git merge origin/integration -m "Release: hele feature-boelgen live"
git push origin main
```
> Begge services ender med hele koden; hver Render-service bygger kun sin egen del
> (api = backend-Docker, web = frontend-static).

---

## FASE 3 — Deploy i Render (backend FØRST)
Ingen auto-deploy — du trykker manuelt.
1. **`indkobapp-api`** → **Manual Deploy → Deploy latest commit**. Vent til den er oppe.
   - Åbn **Logs** og bekræft: `Applying migration '…FreeTextUnits'` + `Now listening` og **ingen fejl**.
   - Test: åbn `https://<backend-url>/health` → skal svare `ok`/200.
2. **`indkobapp-web`** → **Manual Deploy → Deploy latest commit**. Vent til den er oppe.
3. (`indkobapp-butik` — butiks-demoen — er ikke i brug lige nu; lad den stå på `develop` eller sæt den på pause.)

---

## FASE 4 — Verificér live
1. Åbn frontend-URL'en. Log ind.
2. **Data loader** = CORS + API-URL er rigtige. (Loader intet? → tjek Fase 1 CORS + Fase 1b URL.)
3. Klik rundt: opret uge, indkøbsliste, opskrift med billede, Varer (auto-gem), enheder (fri tekst).
4. "Send til butik" og "Kontakt support" skal **ikke** ses (bevidst skjult).

---

## Hvis noget går galt (rollback)
- **App loader ikke / CORS-fejl i browser-konsollen:** sæt/ret `Cors__AllowedOrigins__0` og re-deploy backend.
- **Backend starter ikke:** tjek logs — mangler nok `Stores__AccessKey` eller en dev-standard-nøgle.
- **Data-problem efter migration:** i Neon, restore fra `backup-foer-release`-branchen (Fase 0).
- Produktion kan altid rulles tilbage ved at deploye et tidligere commit i Render.

---

## Efter en vellykket udgivelse (til senere)
- Rotér hemmeligheder der har været delt (Neon-kode, nøgler).
- Overvej betalt Render/Neon (ingen cold start), backups slået til.
- Se `COMMERCIAL.md` for den fulde kommercielle tjekliste.
