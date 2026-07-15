# Kommercialisering — køreplan & readiness-tjekliste

> Strategisk dokument for at gøre **Madplan & Indkøb** til et kommercielt produkt.
> Skrevet til en ny iværksætter (og til fremtidige agenter der arbejder på det).
> Sidst opdateret: 2026-07-15.

---

## 1. Den vigtigste indsigt
**Det svære er ikke koden — det er forretningen.** Produktet er en *to-sidet markedsplads*:
brugere vil ikke bruge appen uden butikker, og butikker vil ikke integrere uden brugere
(hønen-og-ægget). Det, og konkurrencen fra kædernes egne apps, afgør succes — ikke kodekvalitet.

**Konkurrence-virkelighed:** Salling (Netto/Føtex/Bilka), Coop, Rema, Lidl og nemlig.com har
allerede egne apps med online-bestilling og click & collect. De vil helst eje kunderelationen selv,
så en stor kæde adopterer sjældent en tredjeparts "modtag-liste-og-pak"-flow.

**→ Validér efterspørgsel FØR du bygger mere.** Tal med 5-10 brugere og 2-3 (lokale) butikker.
Byg det dyre (butiks-app + marketing) sidst.

## 2. Go-to-market — realistiske veje
1. **B2C-abonnement på forbruger-appen alene** (madplan + køkkenlager + indkøbsliste + husstands-opgaver).
   Tjener penge uden at overbevise en eneste kæde. Lavest risiko.
2. **Lokale/uafhængige butikker** (købmænd, gårdbutikker, specialbutikker) uden egen pakke-teknologi —
   der løser butiks-appen et reelt problem. Start med ÉN pilot-butik.
3. **Personal-shopper-model** (som Instacart) — operationelt tung (logistik, betaling, ansvar). Frarådes som start.

**Anbefaling:** Start med (1) og/eller en pilot af (2). Undgå at forpligte dig til de store kæder tidligt.

## 3. Arkitektur — to apps, ét monorepo
Behold **monorepoet**. Forbruger-appen er `apps/meal-shopping`; butiks-appen bliver `apps/supermarket`
(delt backend + kontrakter). Split først til separate repos ved separate teams/release-kadencer.

**Ordre-kontrakten** er limet mellem de to sider:
- `Order` med status-flow: **Modtaget → Pakkes → Klar → Afhentet** (+ evt. Annulleret).
- Forbruger *sender* ordren (snapshot af indkøbslisten); butik *pakker og opdaterer status*;
  forbruger ser status live.
- En **demo af dette flow er bygget som to adskilte apps**: forbruger-appen (`apps/meal-shopping`)
  sender ordren; butiks-appen (`apps/supermarket`, eget website) pakker og melder klar. Begge deler
  samme backend. Nok til at vise et supermarked konceptet; modnes til produktion (rigtige butikskonti,
  roller, notifikationer, betaling) når det er valideret.

## 4. Fra hobby til produkt — hvad der mangler
Nuværende stak er solidt hobby-niveau. Kommercielt kræver det (gør just-in-time, ikke alt på én gang):

- **Konti:** i dag ét *delt* husstands-login. Kommercielt: individuelle brugerkonti der tilhører en
  husstand (til fakturering, personalisering, sikkerhed) + selvbetjent signup, email-bekræftelse,
  glemt-adgangskode.
- **Sikkerhed:** rate limiting, kortere/tilbagekaldelige tokens, stram CORS, secrets-hygiejne, rotation.
- **Jura/GDPR (EU-krav):** privatlivspolitik, handelsbetingelser, samtykke, ret til indsigt/sletning/eksport,
  databehandleraftaler med underleverandører (Neon, Render), data i EU (Neon Frankfurt ✓).
- **Drift:** betalte hosting-niveauer (væk fra cold starts/0,5 GB), backups, overvågning, fejllogning
  (fx Sentry), oppetid.
- **Kvalitet:** automatiske tests + CI/CD (i dag manuelle deploys, ingen tests).
- **Betaling:** abonnement via Stripe e.l.
- **Distribution:** i dag PWA. App Store/Play kræver pakning (Capacitor) + Apple-udviklerkonto (~$99/år) + review.
- **Onboarding, support, analytics.**

## 5. Jura & virksomhed i Danmark (bekræft med revisor/advokat)
- **Virksomhedsform:** start evt. enkeltmandsvirksomhed (simpelt); overvej **ApS** (20.000 kr. kapital,
  begrænset hæftelse) ved omsætning/risiko.
- **CVR** på virk.dk. **Moms**-registrering ved omsætning > **50.000 kr./12 mdr.**
- Bogføring, evt. erhvervsforsikring, klart **ansvar** (fx forkert pakket ordre).

## 6. Faseinddelt køreplan
- **Fase 0 – Validering (uger):** interview brugere + lokale butikker. Vil de bruge/betale? Vis butiks-demoen.
- **Fase 1 – B2C-klar forbruger-app:** individuelle konti, GDPR-dokumenter, betaling, betalt hosting + backup, tests.
- **Fase 2 – Butiks-pilot:** modn ordre-flowet til `apps/supermarket` med én rigtig butik.
- **Fase 3 – Skalering:** flere butikker, App Store/Play, marketing, support-setup.

---

## 7. Commercial-readiness — prioriteret tjekliste
Rækkefølge = anbefalet prioritet. ☐ = ikke gjort.

### P0 — Før ENHVER betalende bruger (must-have)
- ☐ **Validér efterspørgsel** (brugere + butikker) — vigtigste punkt overhovedet.
- ☐ **Rotér alle hemmeligheder** (Neon-adgangskode, Jwt__Key, Admin__Key) og bekræft de kun er env-vars.
- ☐ **Privatlivspolitik + handelsbetingelser** (GDPR) synligt i appen.
- ☐ **Databehandleraftaler** med Neon og Render; bekræft data i EU.
- ☐ **Betalt hosting + database** (ingen cold starts / 0,5 GB-loft) + **backups** slået til.
- ☐ **Individuelle brugerkonti** (login pr. person, ikke delt husstand) + email-bekræftelse + glemt-kode.
- ☐ **Betaling/abonnement** (Stripe) hvis der skal tages penge.

### P1 — Kort efter launch (bør-have)
- ☐ Rate limiting + tilbagekaldelige tokens (refresh/short-lived).
- ☐ Fejllogning (Sentry) + oppetids-overvågning + alarmer.
- ☐ Automatiske tests for kernelogik (aggregering, afstemning, auth) + CI der kører dem.
- ☐ Ret-til-sletning/eksport af persondata (selvbetjent).
- ☐ Onboarding-flow + support-kanal (mail/chat) + simpel FAQ.
- ☐ Analytics (privatlivsvenlig) til at forstå brug.

### P2 — Skalering/vækst (nice-to-have)
- ☐ App Store/Google Play via Capacitor (+ Apple-udviklerkonto).
- ☐ `apps/supermarket` som selvstændig butiks-app (fra demoen).
- ☐ Butiks-onboarding, roller/medarbejdere, notifikationer.
- ☐ Betalings-/afregningsflow mellem bruger og butik (hvis relevant) — udløser ekstra jura.
- ☐ Flersproget (hvis ud over DK), tilgængelighed (WCAG).

### Teknisk gæld fra hobby-fasen (løbende)
- ☐ Formalisér release-flowet (i dag bevidst: `main` = produktion/brugervendt, `develop` = arbejde + uudgivet butik).
  På sigt: git-tags/miljøer + feature-flags i stedet for at styre "hvad er live" via branch-divergens.
- ☐ Auto-deploy ved push (kræver privat repo + Git-integration — se note nedenfor).
- ☐ Overvej privat repo (kræver løsning på GitHub-adgang; i dag offentligt pga. AU-konto-blokering).
