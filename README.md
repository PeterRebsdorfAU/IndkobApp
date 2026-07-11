# Madplan & Indkøbsliste

En app til privat brug: saml dine opskrifter ét sted, vælg retter (og varegrupper) til en uge,
og få automatisk **én samlet, kategori-sorteret indkøbsliste** hvor overlappende ingredienser slås sammen.

- **Frontend:** Angular 20 (standalone components, signals), mobil-først og responsiv.
- **Backend:** ASP.NET Core 10 Web API (C#, REST).
- **Database:** PostgreSQL, EF Core 10 **code-first migrations** (Npgsql). *(Oprindeligt SQL Server;
  skiftet til PostgreSQL for gratis cloud-hosting — se [DEPLOY.md](DEPLOY.md).)*
- **Én bruger, intet login.**

> **Vil du i skyen?** Hele udrulningen (gratis, uden kreditkort) er beskrevet i **[DEPLOY.md](DEPLOY.md)**.
> Resten af denne fil handler om at køre lokalt.

## 📚 Dokumentation
- **[docs/ECOSYSTEM.md](docs/ECOSYSTEM.md)** — visionen og de samarbejdende systemer (madplan, køkkenlager,
  indkøbs-delegering, pris-optimering) + deres ansvarsområder. *Send denne til andre agenter for kontekst.*
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — teknisk dybde-reference for denne app (datamodel, auth, API, deploy).
- **[DEPLOY.md](DEPLOY.md)** — cloud-udrulning (Neon + Render) trin for trin.

---

## 1. Forudsætninger

Allerede verificeret på denne maskine:

| Værktøj | Version |
|---|---|
| .NET SDK | 10.0.300 |
| dotnet-ef | 10.0.2 |
| Node.js | 24.x |
| npm | 11.x |
| Angular CLI | 20.3.x |
| PostgreSQL | En database, lokal eller i skyen (fx Neon) |

Hvis `dotnet ef` mangler: `dotnet tool install --global dotnet-ef`.

---

## 2. Database & connection string

Connection string'en læses fra konfiguration. Standard ligger i
[`backend/IndkobsApp.Api/appsettings.json`](backend/IndkobsApp.Api/appsettings.json) og peger på en
**lokal PostgreSQL** (placeholder, ingen hemmeligheder):

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=indkobsapp;Username=postgres;Password=postgres"
}
```

**Databasen oprettes automatisk:** når backenden starter, kører den `Database.Migrate()` (opretter
tabeller fra migrations) og lægger lidt seed-data ind, hvis databasen er tom.

### Vælg din lokale database — to nemme muligheder
- **A) Brug din cloud-database (Neon) også lokalt** *(enklest — ingen lokal installation)*:
  overskriv connection string'en med din Neon-streng via en miljøvariabel, så intet ændres i git:
  ```powershell
  $env:ConnectionStrings__Default = "Host=...neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
  cd backend\IndkobsApp.Api; dotnet run
  ```
- **B) Kør PostgreSQL lokalt** (fx via installer fra postgresql.org eller Docker) og brug
  standard-strengen ovenfor.

> Hemmeligheder (rigtige adgangskoder) hører **ikke** hjemme i `appsettings.json` i git — brug en
> miljøvariabel som ovenfor eller `dotnet user-secrets`.

### Se data i databasen
PostgreSQL ses ikke i SSMS. Brug i stedet **Neons web-konsol** (cloud), eller **pgAdmin** /
**DBeaver** mod din connection string (lokal eller Neon).

---

## 3. Kør backend

```powershell
cd backend\IndkobsApp.Api
dotnet run
```

- API'et lytter på **http://localhost:5298** (se `Properties/launchSettings.json`).
- Swagger/OpenAPI-dokument: `http://localhost:5298/openapi/v1.json` (Development).
- Migrations anvendes og databasen seedes ved opstart.

Manuel håndtering af migrations (valgfrit — appen gør det selv ved opstart):
```powershell
cd backend\IndkobsApp.Api
dotnet ef migrations add <Navn>     # ny migration efter modelændringer
dotnet ef database update           # anvend migrations
```

---

## 4. Kør frontend

```powershell
cd frontend\indkobs-app
npm install        # kun første gang
ng serve
```

- Åbn **http://localhost:4200**.
- Frontenden kalder backenden på `http://localhost:5298/api`. Skal det ændres,
  rettes konstanten `API` øverst i [`frontend/indkobs-app/src/app/api.ts`](frontend/indkobs-app/src/app/api.ts).
- CORS for `http://localhost:4200` er sat op i backendens `Program.cs`.

> Start **backend først**, så frontenden har noget at hente data fra.

---

## 5. Seed-data

Ved første opstart (tom database) oprettes:
- **Kategorier** i butiksrækkefølge: Grønt, Mejeri, Kød, Kolonial, Frost, Brød, Non-food/Toilet, Rengøring.
- **Retter:** *Spaghetti med kødsovs* (4 pers.) og *Gulerodssuppe* (2 pers.) — de deler løg/smør,
  så du straks kan se sammenlægning på indkøbslisten.
- **Varegrupper:** *Frokost* (rugbrød, hamburgerryg, smør) og *Toilet* (toiletpapir, håndsæbe).

Seeding ligger i [`Data/DbSeeder.cs`](backend/IndkobsApp.Api/Data/DbSeeder.cs) og kører kun hvis databasen er tom.

---

## 6. Sådan bruges appen

1. **Uge** – vælg/opret en uge (forudfyldt med aktuelt ISO-ugenummer). Tilføj retter
   (med valgfrit antal personer og ugedag) og varegrupper. Tilføj evt. løse varer.
2. **Indkøb** – den samlede, kategori-grupperede liste for den valgte uge. Kryds af, og
   tilføj hurtigt en løs vare.
3. **Retter** / **Grupper** – fuld CRUD med ingredienslinjer (ingrediens + mængde + enhed).
4. **Admin** – administrér ingredienser og kategorier.

---

## 7. Indkøbsliste-aggregering (kernelogikken)

Implementeret i [`Services/ShoppingListService.cs`](backend/IndkobsApp.Api/Services/ShoppingListService.cs):

- **Sammenlægning:** samme ingrediens + samme enhed lægges sammen (2 stk + 3 stk løg = 5 stk).
- **Enhedskonvertering:** `g ↔ kg` og `ml ↔ l` inden for samme måleform; vises i en fornuftig
  enhed (1500 g → 1,5 kg, 1000 ml → 1 l).
- **Uforenelige enheder:** kan to linjer for samme ingrediens ikke slås sammen
  (fx 200 g smør + 1 pakke smør), vises de som **separate linjer** under samme ingrediens.
- **Portion-skalering:** en ret i en uge kan have et ønsket antal personer; mængderne ganges
  med `ønskede / basis-portioner`.
- **Kategori-sortering:** listen grupperes og sorteres efter ingrediensens kategori (butiksrækkefølge).
  Løse fritekst-varer og ingredienser uden kategori havner under *"Andet / løse varer"*.
- **Afkrydsning huskes:** hver aggregeret linje har en stabil nøgle (`LineKey`), så afkrydsning
  bevares selvom listen genberegnes.

**Ingrediens-normalisering:** ingredienser matches trimmet + case-insensitivt
(`" Løg "` = `"løg"` = `"Løg"`) via et unikt indeks på `NormalizedName`, så du ikke får dubletter.

---

## 8. Projektstruktur

```
IndkøbsApp/
├─ backend/
│  ├─ IndkobsApp.sln
│  └─ IndkobsApp.Api/
│     ├─ Models/         # entiteter + Unit-enum og enhedsmatematik
│     ├─ Data/           # AppDbContext, DbSeeder, Migrations/
│     ├─ Dtos/           # API-kontrakter
│     ├─ Services/       # IngredientService, ShoppingListService
│     ├─ Controllers/    # Recipes, ItemGroups, Ingredients, Categories, Weeks
│     ├─ Program.cs      # DI, CORS, auto-migrate + seed
│     └─ appsettings.json
└─ frontend/
   └─ indkobs-app/
      └─ src/app/
         ├─ api.ts        # typed HTTP-service + ISO-uge-hjælper
         ├─ models.ts     # TS-modeller + enheder
         ├─ app.routes.ts
         ├─ shared/       # ingredient-lines editor, week-state
         └─ pages/        # week-plan, shopping-list, recipes, item-groups, admin
```

---

## 9. API-overblik

| Metode | Endpoint | Beskrivelse |
|---|---|---|
| GET/POST/PUT/DELETE | `/api/recipes` | Retter (CRUD) |
| GET/POST/PUT/DELETE | `/api/item-groups` | Varegrupper (CRUD) |
| GET/POST/PUT/DELETE | `/api/ingredients` | Ingredienser (CRUD) |
| GET/POST/PUT/DELETE | `/api/categories` | Kategorier |
| GET/POST/DELETE | `/api/weeks` | Uger |
| POST/PUT/DELETE | `/api/weeks/{id}/recipes` | Retter i ugen (inkl. portioner/dag) |
| POST/DELETE | `/api/weeks/{id}/item-groups` | Varegrupper i ugen |
| POST/DELETE | `/api/weeks/{id}/manual-items` | Løse varer |
| GET | `/api/weeks/{id}/shopping-list` | Aggregeret, kategori-sorteret liste |
| PUT | `/api/weeks/{id}/shopping-list/check` | Sæt/fjern afkrydsning på en linje |

---

## 10. Test på iPhone hjemme (PWA)

Appen er en **PWA** (Progressive Web App) og kan lægges på iPhonens hjemmeskærm som et ikon,
der åbner i fuld skærm. Til hjemmetest bliver backenden på PC'en, og iPhonen tilgår den over WiFi.

**Forudsætninger:** iPhone og PC er på **samme WiFi**, og PC'en er tændt med begge servere kørende.

1. **Åbn Windows Firewall for portene** (én gang). Kør i en PowerShell **som administrator**:
   ```powershell
   New-NetFirewallRule -DisplayName "IndkobsApp Backend 5298" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5298 -Profile Private
   New-NetFirewallRule -DisplayName "IndkobsApp Frontend 4200" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 4200 -Profile Private
   ```

2. **Start backend** (lytter nu på hele netværket, `0.0.0.0:5298`):
   ```powershell
   cd backend\IndkobsApp.Api
   dotnet run
   ```

3. **Start frontend** bundet til netværket:
   ```powershell
   cd frontend\indkobs-app
   ng serve --host 0.0.0.0
   ```

4. **På iPhonen** (Safari): gå til **`http://<PC-IP>:4200`** — fx `http://192.168.8.188:4200`.
   Find PC'ens aktuelle IP med `ipconfig` (IPv4-adresse) hvis den har ændret sig.

5. Tryk **Del-ikonet → "Føj til hjemmeskærm"**. Nu ligger *Madplan* som et ikon og åbner i fuld skærm.

> Appen finder selv backenden på samme IP som den blev hentet fra (se `API` i `src/app/api.ts`),
> så du behøver ikke rette nogen adresse. Bemærk: over WiFi/HTTP virker hjemmeskærm-ikon og
> fuldskærm fint, men **offline-funktion** (service worker) kræver HTTPS og er derfor først aktiv
> ved en rigtig udrulning. PC'en skal være tændt for at appen virker.

### Senere: brug den også uden for hjemmet
Når du vil bruge appen i butikken, skal backend + database hostes online (fx Azure App Service +
Azure SQL). Så sætter du `API` i `src/app/api.ts` til den faste URL, strammer CORS i `Program.cs`
til netop den URL, og deployer frontenden (en PWA over HTTPS giver så også ægte installation + offline).

## 11. På sigt (fase 2 / mobil)

Datamodellen er allerede klar til portion-skalering (`WeekRecipe.Servings`) og dag-opdeling
(`WeekRecipe.DayOfWeek`) — begge er med i UI'et. Frontenden er bygget mobil-først og responsiv,
klar til senere pakning som PWA/Capacitor (uden for denne opgaves scope).
