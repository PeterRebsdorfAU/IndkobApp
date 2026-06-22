namespace IndkobsApp.Api.Models;

/// <summary>
/// Måleenheder. Gemmes som tekst i databasen (se DbContext-konfiguration), så
/// kolonnen er læsbar i SSMS i stedet for et tal.
/// </summary>
public enum Unit
{
    Stk,
    G,
    Kg,
    Ml,
    L,
    Spsk,
    Tsk,
    Daase,
    Pakke,
    Knivspids,
    Bundt,
    Fed
}

/// <summary>
/// Hvilken "måleform" en enhed tilhører. Kun enheder i samme familie kan
/// konverteres/lægges sammen (g↔kg, ml↔l). Count-enheder lægges kun sammen,
/// hvis det er præcis samme enhed.
/// </summary>
public enum MeasureFamily
{
    Mass,
    Volume,
    Count
}

/// <summary>
/// Hjælpefunktioner til enhedsaritmetik brugt af indkøbsliste-aggregeringen.
/// </summary>
public static class UnitMath
{
    // Familie + faktor til basisenhed (g for masse, ml for volumen).
    private static readonly Dictionary<Unit, (MeasureFamily Family, decimal ToBase)> Map = new()
    {
        [Unit.G]  = (MeasureFamily.Mass,   1m),
        [Unit.Kg] = (MeasureFamily.Mass,   1000m),
        [Unit.Ml] = (MeasureFamily.Volume, 1m),
        [Unit.L]  = (MeasureFamily.Volume, 1000m),
        // Alle øvrige er "count": ingen konvertering, kun sammenlægning af identisk enhed.
        [Unit.Stk]       = (MeasureFamily.Count, 1m),
        [Unit.Spsk]      = (MeasureFamily.Count, 1m),
        [Unit.Tsk]       = (MeasureFamily.Count, 1m),
        [Unit.Daase]     = (MeasureFamily.Count, 1m),
        [Unit.Pakke]     = (MeasureFamily.Count, 1m),
        [Unit.Knivspids] = (MeasureFamily.Count, 1m),
        [Unit.Bundt]     = (MeasureFamily.Count, 1m),
        [Unit.Fed]       = (MeasureFamily.Count, 1m),
    };

    public static MeasureFamily FamilyOf(Unit unit) => Map[unit].Family;

    /// <summary>Konverterer en mængde til familiens basisenhed (g eller ml).</summary>
    public static decimal ToBase(decimal quantity, Unit unit) => quantity * Map[unit].ToBase;

    /// <summary>
    /// Vælger en fornuftig visningsenhed for en mængde udtrykt i basisenheden.
    /// Fx 1500 g → (1,5; Kg) og 800 g → (800; G).
    /// </summary>
    public static (decimal Quantity, Unit Unit) FromBase(decimal baseQuantity, MeasureFamily family)
    {
        switch (family)
        {
            case MeasureFamily.Mass:
                return baseQuantity >= 1000m
                    ? (baseQuantity / 1000m, Unit.Kg)
                    : (baseQuantity, Unit.G);
            case MeasureFamily.Volume:
                return baseQuantity >= 1000m
                    ? (baseQuantity / 1000m, Unit.L)
                    : (baseQuantity, Unit.Ml);
            default:
                // Bør ikke ske for count – kaldere håndterer count separat.
                return (baseQuantity, Unit.Stk);
        }
    }
}
