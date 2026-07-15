# Sikkerhed — Madplan & Indkøb (backend)

> Resultatet af **T4 (sikkerhedshærdning)**, jf. [`../../docs/COMMERCIAL-TASKS.md`](../../docs/COMMERCIAL-TASKS.md).
> Beskriver de sikkerhedsforanstaltninger der er lagt ind i backenden (`backend/IndkobsApp.Api`),
> hvordan hemmeligheder håndteres/roteres, og grænsefladen mod **T2 (brugerkonti)**.
> Alle ændringer er **additive og data-bevarende** — forbruger-appen fra `main` virker fortsat mod denne backend.

## 1. Hemmeligheder & konfiguration

Alle hemmeligheder læses fra konfiguration og sættes i produktion som **env-vars på Render**
(dobbelt-underscore = sektionsadskiller). De må **aldrig** committes — repoet er offentligt.

| Env-var | Config-nøgle | Formål | Krav |
|---------|--------------|--------|------|
| `ConnectionStrings__Default` | `ConnectionStrings:Default` | Neon PostgreSQL | Hemmelig |
| `Jwt__Key` | `Jwt:Key` | JWT-signering | ≥ 32 tegn, hemmelig, tilfældig |
| `Admin__Key` | `Admin:Key` | `X-Admin-Key` (husstands-admin) | Hemmelig |
| `Stores__AccessKey` | `Stores:AccessKey` | `X-Store-Key` (butiks-demo) | Hemmelig |
| `Cors__AllowedOrigins__0` | `Cors:AllowedOrigins` | Tilladte frontend-oprindelser | Sæt i prod |

`appsettings.json` indeholder **kun dev-standarder** (offentlige, tydeligt markerede). Ved opstart i
produktion (`ASPNETCORE_ENVIRONMENT != Development`) **nægter backenden at starte**, hvis `Jwt:Key`,
`Admin:Key` eller `Stores:AccessKey` stadig har dev-standardværdien (se `Program.cs`) — det tvinger
rotation. Lokale, ikke-committede overrides kan lægges i `appsettings.Local.json` eller `.env`
(begge er git-ignoreret).

### Nøgle-rotation (procedure)
1. Generér en ny stærk værdi, fx `openssl rand -base64 48` (Jwt) eller `openssl rand -hex 32` (Admin/Store).
2. Opdatér env-var i Render (backend-servicen) → **Deploy** (servicen genstarter med den nye værdi).
3. **Jwt__Key:** rotation invaliderer alle eksisterende access- og refresh-tokens → brugerne skal logge ind igen (forventet).
4. **Admin__Key / Stores__AccessKey:** opdatér de klienter/personer der bruger nøglen.
5. **Neon-adgangskode:** rotér i Neon, opdatér `ConnectionStrings__Default`, redeploy.
6. Rotér ved mistanke om lækage og ellers periodisk (fx hver 6.-12. måned). Del aldrig nøgler i klartekst-kanaler.

Bekræftet ved T4: der er **ingen hemmeligheder** committet i repoet (kun dev-standarder), og
`X-Admin-Key`/`X-Store-Key` er **env-drevne** (læses via `IConfiguration`, ingen hardcodede værdier i kode).

## 2. Token-model (access + refresh)

> Grænseflade mod **T2**. Ændringerne er additive: den hidtidige kontrakt (`AuthResultDto.token` +
> `expiresUtc`) er uændret; `refreshToken` er tilføjet med default `null`, så ældre klienter ignorerer det.

- **Access-token** (Bearer på alle kald): kortlivet, standard **12 timer** (`Jwt:AccessTokenMinutes`).
  Indeholder claims `householdId`, navn, email og `token_type=access`.
- **Refresh-token** (kun mod `POST /api/auth/refresh`): standard **30 dage** (`Jwt:RefreshTokenDays`).
  Minimalt: `householdId` + `token_type=refresh`.
- **`POST /api/auth/refresh {refreshToken}`** → nyt access-token + **roteret** refresh-token.
- **Guard:** et refresh-token kan **ikke** bruges som Bearer på beskyttede endpoints (afvises i
  `JwtBearerEvents.OnTokenValidated`). Ældre tokens uden `token_type` behandles som access (bagudkompatibelt).
- **Stateless** design (signeret JWT, ingen DB, ingen migration) for at holde T4 additiv.

**Overdragelse til T2:** T2 (individuelle brugerkonti) kan gøre refresh **DB-baseret** med rotation/spærring
pr. bruger og tilføje et `userId`-claim **additivt** — `householdId`-claimet og `GetHouseholdId()` bevares,
så resten af API'et er uændret. T2's frontend bør implementere auto-refresh (kald `/auth/refresh` ved 401
før logout). Indtil da logges en klient blot ud ved udløbet access-token, som hidtil.

## 3. Rate limiting

Global ASP.NET `RateLimiter` (fixed window pr. klient-IP; se `Middleware/RateLimiting.cs`). Kræver ingen
attributter på controllere. Overskridelse → **HTTP 429** med `Retry-After` og en dansk JSON-besked.
Grænser (pr. IP pr. minut, konfigurerbare under `RateLimiting:*`):

| Spand | Endpoints | Standard |
|-------|-----------|----------|
| `auth` | `/api/auth/login`, `/api/auth/refresh`, `/api/admin/*`, `/api/store/*` | 10/min |
| `write` | Alle øvrige `POST/PUT/PATCH/DELETE` | 60/min |
| `read` | `GET` | 300/min |

## 4. CORS

Kun **kendte oprindelser** (`Cors:AllowedOrigins`). Uden konfiguration: i udvikling tillades
`http://localhost:4200`; i **produktion afvises alt** (fail-closed) + en advarsel logges. Aldrig permissiv
"tillad alle". Sæt frontend-URL'en i prod: `Cors__AllowedOrigins__0=https://indkobapp-web.onrender.com`.

## 5. Security headers

`Middleware/SecurityHeadersMiddleware.cs` sætter på alle svar: `X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy` (geolocation/kamera/mikrofon/betaling
slået fra), `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`,
`Cross-Origin-Resource-Policy: cross-origin`. **HSTS** (`Strict-Transport-Security`) tilføjes i produktion via
`app.UseHsts()`. Kestrels `Server`-header er slået fra, og request-body er begrænset til **1 MB**.

## 6. Input-validering (gennemgang)

- Alle data-controllere er `[Authorize]` og filtrerer/skriver strengt på `User.GetHouseholdId()` (husstands-isolation).
- `[ApiController]` giver automatisk **400** ved malformet JSON og manglende påkrævede felter (non-nullable
  reference-typer behandles implicit som required).
- Manuelle guards findes i Auth, Admin, Orders, Store, Pantry, Tasks, Weeks, Catalog og Categories
  (fx tomme felter, ukendt butik/ingrediens, `Quantity <= 0`, `IntervalDays >= 1`).
- Global request-body-grænse (1 MB) forhindrer oppustede payloads.

**Anbefalinger til T2/T5** (når de alligevel rører `Dtos.cs`/controllere — undgår merge-konflikt nu):
- `Range`-validering på `Servings` og `Quantity` (fx `> 0`) — beskytter bl.a. mod division i portions-skalering.
- `StringLength` på navne/noter og en øvre grænse på antal ingredienslinjer pr. ret/varegruppe.
- Overvej en dedikeret nøgle **pr. butik** i stedet for én fælles `Stores:AccessKey`, når butiks-flowet udskilles.

## 7. Afhængigheder
- `Microsoft.OpenApi` pinnet til patched 2.x (transitiv 2.0.0 ramtes af NU1903 / GHSA-v5pm-xwqc-g5wc).
  Backenden bygger nu uden advarsler. Kør jævnligt `dotnet list package --vulnerable`.
