# apps/supermarket — Butiks-app

Selvstændigt website til **supermarkedet/butikken**. Adskilt fra forbruger-appen
(`apps/meal-shopping`), men taler med **samme backend** via `/api/store/*`.

## Hvad den gør
Butikken logger ind med en **butiks-adgangskode** (ikke husstands-login), vælger sin butik,
ser indkomne ordrer, pakker linjer (og markerer "ikke på lager") og trykker **Marker klar**.
Forbrugeren ser status skifte i sin app.

## Kør lokalt
```powershell
cd apps/supermarket
npm install      # kun første gang
ng serve --port 4300
```
Åbn http://localhost:4300. Kræver at backenden kører (lokalt port 5298, eller sæt prod-URL i
`src/environments/environment.prod.ts`). Demo-adgangskode lokalt: `butik1234` (config `Stores:AccessKey`).

## Deploy (Render)
Egen **Static Site** (se `render.yaml` → `indkobapp-butik`): rootDir `apps/supermarket`,
build `npm ci && npm run build`, publish `dist/supermarket/browser`, SPA-rewrite `/* → /index.html`.
Peger på den fælles backend via `environment.prod.ts` (`apiBase`).

## Status
🟡 **Demo/proof-of-concept** — nok til at vise et supermarked konceptet. Mangler før produktion:
rigtige butikskonti + medarbejder-roller, notifikationer, betaling/afregning. Se `../../docs/COMMERCIAL.md`.
