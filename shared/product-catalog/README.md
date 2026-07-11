# shared/product-catalog — kanonisk vare-katalog (fælles fundament)

**Status:** 🔵 Foreslået, ikke bygget endnu. Denne mappe reserverer pladsen og beskriver ansvaret.

## Mission
Give **én kanonisk identitet pr. vare**, som alle apps refererer, plus mapping til butiks-SKU'er
og stregkoder. Det er "limet" der får madplan, køkkenlager og pris-optimering til at tale om
*samme* vare.

## Ejer (planlagt datamodel)
- `CanonicalItem { Id, Name, Category, DefaultUnit, Synonyms[] }`
- `ItemMapping { CanonicalItemId, Barcode?, StoreId?, Sku? }`

## Migreringssti
1. Eksportér Madplan-appens `Ingredient`-liste (normaliseret navn) som første sæt kanoniske varer.
2. Lad nye apps (pantry, price) referere disse id'er.
3. Over tid bliver kataloget kilden, Madplan slår op i (i stedet for sin lokale `Ingredient`-tabel).

Se `docs/ECOSYSTEM.md` §4.6.
