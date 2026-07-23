namespace IndkobsApp.Api.Models;

/// <summary>
/// Hvilken "måleform" en enhed tilhører. Kun enheder i samme familie kan
/// konverteres/lægges sammen (g↔kg, ml↔l). Count-enheder lægges kun sammen,
/// hvis det er præcis samme enhed (case-insensitivt).
/// </summary>
public enum MeasureFamily
{
    Mass,
    Volume,
    Count
}

/// <summary>
/// Måleenheder er <b>fri tekst</b> (<see cref="string"/>): brugeren kan skrive en hvilken
/// som helst enhed (fx "glas", "kviste", "dåser"). De omregnelige basisenheder (masse g/kg,
/// volumen ml/l) genkendes og lægges automatisk sammen; alt andet behandles "count-agtigt"
/// og lægges sammen pr. distinkt enhed (case-insensitivt), mens forskellige enheder holdes
/// på hver sin linje.
///
/// Denne klasse samler de <b>kendte forslag</b> og hjælpe-funktioner til rensning/normalisering
/// ét sted. Enhver anden streng er også gyldig — listen er bevidst IKKE lukket.
/// </summary>
public static class Units
{
    // --- Omregnelige basisenheder (kanoniske, små bogstaver) ---
    public const string G = "g";
    public const string Kg = "kg";
    public const string Ml = "ml";
    public const string L = "l";

    // --- Kendte "count"-enheder (forslag — ikke en lukket liste) ---
    public const string Stk = "stk";
    public const string Spsk = "spsk";
    public const string Tsk = "tsk";
    public const string Daase = "dåse";
    public const string Pakke = "pakke";
    public const string Knivspids = "knivspids";
    public const string Bundt = "bundt";
    public const string Fed = "fed";

    /// <summary>Fald-tilbage-enhed når intet er angivet.</summary>
    public const string Default = Stk;

    /// <summary>Maks. længde for en enheds-streng (matcher DB-kolonnen).</summary>
    public const int MaxLength = 40;

    /// <summary>
    /// Standard-forslag husstanden altid ser i enheds-vælgeren (kombineres i UI/endpoint
    /// med de enheder husstanden allerede HAR brugt).
    /// </summary>
    public static readonly string[] Suggestions =
        { Stk, G, Kg, Ml, L, Spsk, Tsk, Daase, Pakke, Knivspids, Bundt, Fed };

    /// <summary>
    /// Renser en bruger-indtastet enhed: trim + længde-begrænsning; tom → <see cref="Default"/>.
    /// Brugerens tekst/casing bevares (fri tekst) — normalisering til sammenlægning sker
    /// separat via <see cref="NormalizeKey"/>.
    /// </summary>
    public static string Clean(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0) return Default;
        return s.Length > MaxLength ? s[..MaxLength] : s;
    }

    /// <summary>
    /// Nøgle til case-insensitiv sammenlægning af count-enheder, så "Glas" og "glas" lander
    /// på samme linje, mens "glas" og "dl" holdes adskilt.
    /// </summary>
    public static string NormalizeKey(string? unit) => (unit ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>
/// Hjælpefunktioner til enhedsaritmetik brugt af indkøbsliste-aggregeringen. Arbejder på
/// fri-tekst enheder: masse (g/kg) og volumen (ml/l) — inkl. et par almindelige synonymer —
/// genkendes og konverteres til en basisenhed; alt andet er "count" (ingen konvertering).
/// </summary>
public static class UnitMath
{
    // Genkendte omregnelige enheder + synonymer → (familie, faktor til basisenhed g/ml).
    // Opslag er case-insensitivt, så "Kg", "KG" og "kg" er samme enhed.
    private static readonly Dictionary<string, (MeasureFamily Family, decimal ToBase)> Map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // Masse (basis = gram)
        [Units.G]  = (MeasureFamily.Mass, 1m),
        ["gram"]   = (MeasureFamily.Mass, 1m),
        [Units.Kg] = (MeasureFamily.Mass, 1000m),
        ["kilo"]   = (MeasureFamily.Mass, 1000m),
        ["kilogram"] = (MeasureFamily.Mass, 1000m),
        // Volumen (basis = milliliter)
        [Units.Ml]   = (MeasureFamily.Volume, 1m),
        ["milliliter"] = (MeasureFamily.Volume, 1m),
        [Units.L]    = (MeasureFamily.Volume, 1000m),
        ["liter"]    = (MeasureFamily.Volume, 1000m),
    };

    /// <summary>
    /// Familien for en enhed. Ukendte/tomme enheder er "count" (fri-tekst enheder som
    /// "glas", "kviste", "stk", "pakke" …) — de kan ikke konverteres, kun sammenlægges pr. enhed.
    /// </summary>
    public static MeasureFamily FamilyOf(string? unit) =>
        unit != null && Map.TryGetValue(unit.Trim(), out var m) ? m.Family : MeasureFamily.Count;

    /// <summary>
    /// Konverterer en mængde til familiens basisenhed (g eller ml). Count-enheder (alt ukendt)
    /// returneres uændret, så de akkumuleres i selve enheden.
    /// </summary>
    public static decimal ToBase(decimal quantity, string? unit) =>
        unit != null && Map.TryGetValue(unit.Trim(), out var m) ? quantity * m.ToBase : quantity;

    /// <summary>
    /// Vælger en fornuftig visningsenhed for en mængde udtrykt i basisenheden.
    /// Fx 1500 g → (1,5; "kg") og 800 g → (800; "g").
    /// </summary>
    public static (decimal Quantity, string Unit) FromBase(decimal baseQuantity, MeasureFamily family) =>
        family switch
        {
            MeasureFamily.Mass   => baseQuantity >= 1000m ? (baseQuantity / 1000m, Units.Kg) : (baseQuantity, Units.G),
            MeasureFamily.Volume => baseQuantity >= 1000m ? (baseQuantity / 1000m, Units.L)  : (baseQuantity, Units.Ml),
            // Bør ikke ske for count – kaldere håndterer count separat.
            _ => (baseQuantity, Units.Stk),
        };
}
