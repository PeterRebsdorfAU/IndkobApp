# Household Ecosystem (monorepo)

Et økosystem af samarbejdende apps omkring **"hvad skal vi spise, og hvad skal vi købe"** —
husstands-baseret, mobil-først, gratis-venligt (Render + Neon).

> **Start her hvis du er en agent:** læs [`docs/ECOSYSTEM.md`](docs/ECOSYSTEM.md) for helheden og
> hvert systems ansvarsområde. Arbejd derefter i den relevante `apps/<system>/`-mappe.

## Struktur
```
household-ecosystem/
├─ apps/
│  └─ meal-shopping/     # Madplan & Indkøb — FINDES I DRIFT (Angular PWA + .NET API + Neon)
│     ├─ backend/  frontend/  tools/  Dockerfile
│     ├─ README.md  ARCHITECTURE.md  DEPLOY.md
│  └─ (pantry/ shopper/ price-optimizer/  ← kommer senere)
├─ shared/
│  ├─ contracts/         # fælles "sprog": vare-id, enheder, liste-linje-DTO'er
│  └─ product-catalog/   # kanonisk vare-katalog (fælles fundament)
├─ docs/
│  └─ ECOSYSTEM.md       # vision, systemer, ansvarsområder, roadmap
└─ render.yaml           # deploy-reference (hver app = egen Render-service via Root Directory)
```

## Apps
| App | Hvad | Status | Docs |
|---|---|---|---|
| **meal-shopping** | Vælg retter → automatisk, aggregeret indkøbsliste | ✅ I drift | [README](apps/meal-shopping/README.md) · [ARCHITECTURE](apps/meal-shopping/ARCHITECTURE.md) |
| pantry | Køkkenlager: hvad har vi hjemme; trækkes fra listen | 🟡 Planlagt | — |
| shopper | Send/deleger indkøbslisten til en anden | 🟡 Planlagt | — |
| price-optimizer | Hvor er det billigst / hvilken butik | 🔵 Foreslået | — |

## Principper (kort)
- **Husstand = tenant.** Data isoleret pr. husstand.
- **Kanonisk vare-identitet** binder systemerne sammen (se `shared/`).
- **Hver app** er sin egen deployerbare service med eget datalager; de taler via API'er + `shared/`-kontrakter.

Se detaljer i [`docs/ECOSYSTEM.md`](docs/ECOSYSTEM.md).
