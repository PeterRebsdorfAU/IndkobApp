# Fejllogning, overvågning & oppetid (T6)

> Hvordan **Madplan & Indkøb** ser fejl og oppetid i produktion. Se opgave **T6** i
> [`COMMERCIAL-TASKS.md`](COMMERCIAL-TASKS.md) og teknikken i
> [`../apps/meal-shopping/ARCHITECTURE.md`](../apps/meal-shopping/ARCHITECTURE.md).

## 1. Hvad er sat op (i koden)

| Lag | Værktøj | Aktiveres via | Uden konfiguration |
|-----|---------|---------------|--------------------|
| Backend fejl/tracing | Sentry (`Sentry.AspNetCore`) | env-var `Sentry__Dsn` | **No-op** — SDK'et wires slet ikke |
| Backend request-logging | Indbygget `HttpLogging` | altid aktiv | logger metode/sti/status/varighed |
| Frontend fejl | Sentry (`@sentry/angular`) | `environment.sentryDsn` | **No-op** — `Sentry.init` kaldes ikke |
| Readiness | `GET /health` (anonymt) | altid aktiv | svarer `ok`/`degraded` |

**Vigtigt designvalg:** Overvågning er 100% valgfri. Mangler DSN'en, kører appen præcis som før —
ingen netværkskald, ingen ekstra afhængighed i request-stien, og **ingen risiko for at overvågningen
crasher appen**. Det gør det trygt at køre lokalt/offline og at deploye uden at have en Sentry-konto endnu.

## 2. Sådan aktiveres Sentry

### Backend (Render Web Service)
Sæt env-var på servicen:
```
Sentry__Dsn = https://<key>@<org>.ingest.sentry.io/<project>
```
Valgfrit: `Sentry__TracesSampleRate` (default 0.2 i prod, 1.0 i dev). Ingen PII sendes
(`SendDefaultPii=false`) — emails/JWT ender ikke i Sentry.

### Frontend (Render Static Site)
DSN'en bages ind i bundtet ved build. Sæt den i
[`src/environments/environment.prod.ts`](../apps/meal-shopping/frontend/indkobs-app/src/environments/environment.prod.ts)
(`sentryDsn`) før build/deploy. Til lokal test kan man midlertidigt sætte `sentryDsn` i
`environment.ts`. Pakken er self-hostet i bundtet (ingen CDN) → PWA'en har fortsat ingen eksterne
script-afhængigheder; der sendes kun fejl-data til Sentry's ingest-endpoint i runtime.

> Backend- og frontend-DSN er **to forskellige projekter** i Sentry (ét .NET-projekt, ét
> browser/Angular-projekt). Commit aldrig en rigtig DSN i git.

## 3. `/health` (readiness)

`GET /health` er **anonymt** (modsat de `[Authorize]`-beskyttede controllers som `/api/categories`,
der returnerer 401 uden token og derfor er ubrugelige til uptime-checks). Endpointet verificerer
DB-forbindelsen:

```json
// 200 OK
{ "status": "ok", "db": "up", "utc": "2026-07-15T15:00:00Z" }
// 503 Service Unavailable (kan ikke nå DB)
{ "status": "degraded", "db": "down", "utc": "..." }
```

Brug `/health` (ikke `/api/categories`) som mål for ekstern overvågning og for Render's egne
health checks.

## 4. Ekstern oppetids-overvågning (anbefaling)

Sentry fanger *fejl i koden*, men ikke *"er sitet oppe?"*. Til det bruges en ekstern uptime-tjeneste,
der poller udefra og alarmerer ved nedetid.

**Anbefalet minimum:**
1. Opret en gratis konto hos en uptime-tjeneste — fx **UptimeRobot**, **Better Stack (Uptime)** eller
   **Healthchecks.io**. Alle har gratis niveauer der rækker til denne app.
2. Opret to monitorer (HTTP/HTTPS, 1–5 min interval):
   - **Backend:** `https://indkobapp.onrender.com/health` — forvent HTTP 200 + body indeholder `"status":"ok"`.
   - **Frontend:** `https://indkobapp-web.onrender.com/` — forvent HTTP 200.
3. **Cold start-forbehold (gratis Render):** backenden sover efter inaktivitet → første kald tager
   ~30–60 sek (jf. ARCHITECTURE §9). Sæt derfor:
   - request-timeout højt nok (≥ 60 sek), og/eller
   - "bekræft nedetid" så der kræves 2 fejlede tjek i træk før alarm (undgår falske alarmer),
   - alternativt kan et poll hvert 5.–10. min. holde servicen varm (bivirkning: mindre cold start for brugerne).
   - Når **T9** opgraderer Render til betalt (ingen sleep), kan intervallet strammes og timeouten sænkes.

## 5. Alarmering

- **Sentry-alarmer:** slå en alert-rule til pr. projekt (fx "ny issue" eller ">N fejl på Y min")
  → notifikation til e-mail og/eller Slack/Discord-webhook. Konfigureres i Sentry-projektets
  *Alerts*. Ingen kode nødvendig.
- **Uptime-alarmer:** uptime-tjenesten sender e-mail/SMS/webhook ved nedetid og ved genoprettelse.
- **Kanal:** til en semi-kommerciel pilot er e-mail til ejeren nok; overvej en fælles Slack/Discord-kanal
  hvis flere skal reagere. Undgå at spamme — brug "bekræft nedetid" og fornuftige tærskler.

## 6. Fremtidige forbedringer (ikke gjort nu — bevidst afgrænset)
- `/health` kan udbygges med afhængigheds-tjek (fx migrations-status) og et separat let `/health/live`
  (liveness) vs. `/health/ready` (readiness).
- Structured-logging-eksport (fx Serilog → Sentry/Seq) hvis logmængden vokser.
- Release-tracking i Sentry (git-SHA som `release`) for at koble fejl til deploys.
- Uptime-status-side (public) hvis pilot-brugere skal kunne se driftsstatus.
