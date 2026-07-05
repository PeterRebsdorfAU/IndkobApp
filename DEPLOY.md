# Udrulning til skyen (gratis) — Neon + Render

Mål: appen er **altid online** og kan tilgås **alle vegne** (mobildata, i butikken),
uden at din PC er tændt. Alt kører på gratis niveauer uden kreditkort.

```
 iPhone ──HTTPS──>  Render Static Site (Angular PWA)
                         │  henter data
                         ▼
                    Render Web Service (.NET API, Docker)
                         │  EF Core
                         ▼
                    Neon (PostgreSQL)
```

> **Forbehold (gratis):** Backend "sover" efter ~15 min inaktivitet og er ~30–60 sek om at
> vågne ved første kald. Derefter er den hurtig. Neon-databasen suspenderer også compute ved
> inaktivitet og vågner automatisk ved forbindelse. Data bevares.

---

## Trin 1 — Opret databasen på Neon

1. Gå til **https://neon.com** → **Sign up** (vælg "Continue with GitHub").
2. **Create project**: vælg region **Europe (Frankfurt)** (tættest på Danmark). Navngiv fx `indkobapp`.
3. Når projektet er oprettet, vis **Connection string** og vælg fanen/typen **.NET** (Npgsql).
   Den ser nogenlunde sådan ud:
   ```
   Host=ep-xxxx-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=xxxxxxxx;SSL Mode=Require;Trust Server Certificate=true
   ```
   - Brug gerne **"Pooled connection"**-varianten (har `-pooler` i host-navnet).
   - **Kopiér hele strengen.** (Hvis Neon kun viser et `postgresql://...`-URI, så send det til mig — jeg konverterer det til .NET-format.)

> Inden vi deployer, kan jeg **teste backenden lokalt mod din Neon-database**, så vi ved at
> migrations og seed virker, før Render overhovedet er i spil. Send mig blot connection string'en.

---

## Trin 2 — Deploy på Render

> **Er GitHub-login blokeret (fx AU-konto)?** Så brug **"Public Git Repository"**-metoden i afsnit
> 2B nedenfor (kræver at repoet er offentligt). Virker GitHub-login, kan du bruge det hurtigere
> Blueprint i 2A.

### 2A — Via Render Blueprint (kræver GitHub/GitLab-forbindelse)

Repoet indeholder allerede en `render.yaml`, så Render opretter **både** API og frontend i ét hug.

1. Gå til **https://render.com** → **Sign up** (Continue with GitHub) og giv adgang til repoet
   `PeterRebsdorfAU/IndkobApp`.
2. **New +** → **Blueprint** → vælg repoet. Render læser `render.yaml` og viser to services:
   - `indkobapp-api` (Docker — backend)
   - `indkobapp-web` (Static — frontend)
3. Render spørger om værdien for den hemmelige env-var **`ConnectionStrings__Default`**
   (på `indkobapp-api`). Indsæt din **Neon .NET connection string** fra Trin 1.
4. Klik **Apply**. Render bygger nu begge services (Docker-build af API'et tager et par minutter
   første gang). API'et kører `Database.Migrate()` + seed ved opstart, så tabeller og startdata
   oprettes automatisk i Neon.

### 2B — Via "Public Git Repository" (uden GitHub-login)

Forudsætning: repoet er **offentligt**. Opret Render-konto med **email** (ikke GitHub).
Opret de to services manuelt. **Lav backenden først.**

**Backend (Web Service):**
1. **New +** → **Web Service** → vælg **"Public Git Repository"**.
2. Indsæt URL: `https://github.com/PeterRebsdorfAU/IndkobApp` → Continue.
3. Indstil:
   - **Name:** `indkobapp-api`
   - **Branch:** `cloud-deploy`
   - **Region:** Frankfurt · **Instance Type:** Free
   - **Language/Runtime:** Docker (Render finder `Dockerfile` i roden automatisk)
4. **Environment Variables** → tilføj tre:
   - `ConnectionStrings__Default` = din Neon .NET-connection string
   - `Jwt__Key` = en lang tilfældig streng (mindst 32 tegn) — brug Renders **"Generate"**-knap
   - `Admin__Key` = en tilfældig streng (brug **"Generate"**) — den bruger du til at oprette husstande
5. **Create Web Service** → vent på build → test `…/api/auth/login` (se afsnittet **Login og husstande** nedenfor).

**Frontend (Static Site):**
1. **New +** → **Static Site** → **"Public Git Repository"** → samme URL.
2. Indstil:
   - **Name:** `indkobapp-web` · **Branch:** `cloud-deploy`
   - **Root Directory:** `frontend/indkobs-app`
   - **Build Command:** `npm ci && npm run build`
   - **Publish Directory:** `dist/indkobs-app/browser`
3. **Create Static Site.**
4. Under **Settings → Redirects/Rewrites** tilføj: Source `/*`, Destination `/index.html`, Action **Rewrite**
   (så Angular-router virker ved refresh/dybe links).

> Bemærk: med offentlig-URL-metoden genudruller Render ikke nødvendigvis automatisk ved `git push`
> — brug "Manual Deploy → Deploy latest commit" i dashboardet efter ændringer.

---

## Trin 3 — Tjek URL'er (og ret evt. frontendens API-adresse)

Efter deploy får du to URL'er, fx:
- API: `https://indkobapp-api.onrender.com`
- Web: `https://indkobapp-web.onrender.com`

Frontenden er bygget til at kalde `https://indkobapp-api.onrender.com/api` (se
`frontend/indkobs-app/src/environments/environment.prod.ts`).

- **Hvis API-URL'en er præcis den ovenfor:** intet at gøre. ✅
- **Hvis Render gav et andet navn** (fx fordi navnet var optaget): ret `apiBase` i
  `environment.prod.ts` til den rigtige URL, og kør:
  ```powershell
  git add -A; git commit -m "Ret prod API-URL"; git push
  ```
  Render bygger automatisk frontenden igen ved push.

Test API'et direkte i browseren: `https://indkobapp-api.onrender.com/api/categories`
(første kald kan tage ~30–60 sek pga. cold start; derefter kommer der JSON).

---

## Trin 4 — Installér på iPhone

1. Åbn **web-URL'en** (`https://indkobapp-web.onrender.com`) i **Safari** — nu via HTTPS,
   så det er en ægte PWA.
2. **Del → Føj til hjemmeskærm.**
3. Færdig: appen virker nu **alle vegne** (også mobildata), og din PC behøver ikke være tændt. 🎉
   Fordi det er HTTPS, virker **offline-cache** nu også (app-skallen åbner selv uden net;
   data kræver dog forbindelse til API'et).

---

## Login og husstande

Appen kræver nu login. Hver **husstand** har ét fælles login (email + adgangskode) og sine egne
data. Kun du kan oprette husstande — via en beskyttet admin-nøgle (`Admin__Key`).

**Demo-login** (oprettes automatisk ved første start):
- Email: `demo@husstand.dk` · Adgangskode: `Skift1234!`
- Skift adgangskoden ved at oprette din egen husstand og slette demo (se nedenfor).

**Opret en ny husstand** (kør fra din PC; hent `Admin__Key`-værdien i Render-dashboardet under
API-servicens *Environment*):
```powershell
$api = "https://indkobapp.onrender.com"
$key = "DIN_ADMIN_KEY_FRA_RENDER"
$body = '{"name":"Familie Hansen","email":"hansen@eksempel.dk","password":"EtGodtKodeord"}'
Invoke-RestMethod -Method Post -Uri "$api/api/admin/households" -Headers @{ "X-Admin-Key" = $key } -ContentType "application/json" -Body $body
```
- **Liste husstande:** `Invoke-RestMethod "$api/api/admin/households" -Headers @{ "X-Admin-Key" = $key }`
- **Slet en husstand** (fjerner også dens data): `Invoke-RestMethod -Method Delete "$api/api/admin/households/2" -Headers @{ "X-Admin-Key" = $key }`

Del så email + adgangskode med den pågældende husstand — de logger ind i appen på forsiden.

## Se data i databasen
I stedet for SSMS bruger du **Neons web-konsol**: åbn dit projekt på neon.com → **SQL Editor**
eller **Tables**. (Du kan også forbinde med pgAdmin eller DBeaver via samme connection string.)

## Senere ændringer
Hver gang du `git push` til `main`, bygger og genudruller Render automatisk både API og frontend.

## Sikkerhed (vigtigt at vide)
Appen har **intet login** (som ønsket). Når den ligger offentligt, kan **enhver med URL'en**
se og ændre dine data. Til privat brug er det typisk fint, men sig til hvis du vil have en simpel
beskyttelse på (fx en adgangskode/API-nøgle) — det kan tilføjes uden den store omgang.
