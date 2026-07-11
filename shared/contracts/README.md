# shared/contracts — det fælles "sprog"

Fælles, stabile kontrakter som alle apps i økosystemet refererer, så de kan tale sammen.
Endnu ikke implementeret som kode — dette er kontrakt-specifikationen (kilde: `docs/ECOSYSTEM.md` §6).

## Kontrakter (v0 — udkast)
- **Husstands-identitet:** JWT med claim `householdId`. Alle apps scoper til den.
- **Kanonisk vare-id:** heltal/UUID fra Vare-kataloget (se `../product-catalog/`). Udveksling af
  varer bruger dette id — ikke fritekst-navne (undtagen ved oprettelse/opslag).
- **Mængde + enhed:** `{ quantity: decimal, unit: enum }`.
  - Familier: masse (`g`,`kg`), volumen (`ml`,`l`), styk/øvrige (`stk`,`pakke`,`dåse`,…).
  - Konvertering: `g↔kg`, `ml↔l`. Uforenelige enheder holdes adskilt.
  - Reference-implementering: `apps/meal-shopping/backend/IndkobsApp.Api/Models/Unit.cs` (`UnitMath`).
- **Liste-linje** (Madplan → Shopper/Pris): `{ canonicalItemId, name, quantity, unit, category?, note? }`.
- **API-stil:** REST/JSON, JWT-scoping, fejl som HTTP-status + problem-details.

## Åbne punkter
- Skal kontrakterne udgives som en delt kodepakke (fx et NuGet/npm-bibliotek) eller blot holdes
  som specifikation her? Besluttes når app nr. 2 (pantry) starter.
