# Household Ecosystem (monorepo)

Et økosystem af samarbejdende apps omkring **"hvad skal vi spise, og hvad skal vi købe"** —
husstands-baseret, mobil-først, gratis-venligt (Render + Neon).

> **Start her hvis du er en agent:**
> 1. [`docs/ECOSYSTEM.md`](docs/ECOSYSTEM.md) — helheden, systemerne og hvert systems ansvarsområde.
> 2. [`docs/COMMERCIAL.md`](docs/COMMERCIAL.md) — produktets forretning, go-to-market og readiness-tjekliste.
> 3. [`apps/meal-shopping/ARCHITECTURE.md`](apps/meal-shopping/ARCHITECTURE.md) — teknisk dybde for den kørende app.
>
> Arbejd derefter i den relevante `apps/<system>/`-mappe.

## Struktur
```
household-ecosystem/
├─ apps/
│  ├─ meal-shopping/     # Forbruger-app — I DRIFT (Angular PWA + .NET API + Neon)
│  │  ├─ backend/  frontend/  tools/  Dockerfile
│  │  └─ README.md  ARCHITECTURE.md  DEPLOY.md
│  └─ supermarket/       # Butiks-app (demo) — eget website, deler backend
│     └─ README.md
├─ shared/
│  ├─ contracts/         # fælles "sprog": vare-id, enheder, liste-linje-DTO'er
│  └─ product-catalog/   # kanonisk vare-katalog (fælles fundament)
├─ docs/
│  ├─ ECOSYSTEM.md       # vision, systemer, ansvarsområder, roadmap
│  └─ COMMERCIAL.md      # kommercialisering: forretning + readiness-tjekliste
└─ render.yaml           # deploy-reference (hver app = egen Render-service)
```

## Apps
| App | Hvad | Status | Docs |
|---|---|---|---|
| **meal-shopping** | Forbruger-app: retter → aggregeret indkøbsliste, lager, hjem, ordrer | ✅ I drift | [README](apps/meal-shopping/README.md) · [ARCHITECTURE](apps/meal-shopping/ARCHITECTURE.md) |
| **supermarket** | Butiks-app: modtag ordrer, pak, meld klar (deler backend) | 🟡 Demo | [README](apps/supermarket/README.md) |
| pantry / shopper / price-optimizer | (indbygget som moduler i meal-shopping indtil videre) | — | [ECOSYSTEM](docs/ECOSYSTEM.md) |

## Principper (kort)
- **Husstand = tenant.** Data isoleret pr. husstand.
- **Kanonisk vare-identitet** binder systemerne sammen (se `shared/`).
- **Hver app** er sin egen deployerbare service med eget datalager; de taler via API'er + `shared/`-kontrakter.

Se detaljer i [`docs/ECOSYSTEM.md`](docs/ECOSYSTEM.md).
