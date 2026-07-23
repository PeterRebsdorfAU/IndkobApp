using IndkobsApp.Api.Models;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Enheds-aritmetikken (<see cref="UnitMath"/>) er fundamentet under indkøbsliste-
/// aggregeringen: familie-inddeling, konvertering til basisenhed og valg af pæn
/// visningsenhed. Enheder er nu FRI TEKST — kun de omregnelige basisenheder (g/kg, ml/l)
/// genkendes; alt andet er "count". Fejl her forplanter sig til hele listen.
/// </summary>
public class UnitMathTests
{
    [Theory]
    [InlineData("g", MeasureFamily.Mass)]
    [InlineData("kg", MeasureFamily.Mass)]
    [InlineData("KG", MeasureFamily.Mass)]      // case-insensitivt
    [InlineData("gram", MeasureFamily.Mass)]    // synonym
    [InlineData("ml", MeasureFamily.Volume)]
    [InlineData("l", MeasureFamily.Volume)]
    [InlineData("liter", MeasureFamily.Volume)] // synonym
    [InlineData("stk", MeasureFamily.Count)]
    [InlineData("spsk", MeasureFamily.Count)]
    [InlineData("pakke", MeasureFamily.Count)]
    [InlineData("dåse", MeasureFamily.Count)]
    [InlineData("fed", MeasureFamily.Count)]
    [InlineData("glas", MeasureFamily.Count)]   // egen enhed → count
    [InlineData("kviste", MeasureFamily.Count)] // egen enhed → count
    public void FamilyOf_grupperer_enheder_korrekt(string unit, MeasureFamily expected)
    {
        Assert.Equal(expected, UnitMath.FamilyOf(unit));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FamilyOf_tom_eller_ukendt_er_count(string? unit)
    {
        // Ukendte/tomme enheder må ALDRIG kaste — de behandles bare som count.
        Assert.Equal(MeasureFamily.Count, UnitMath.FamilyOf(unit));
    }

    [Fact]
    public void FamilyOf_dækker_alle_kendte_forslag()
    {
        // Ingen af standard-forslagene må kaste (robusthed mod nye forslag).
        foreach (var u in Units.Suggestions)
        {
            var ex = Record.Exception(() => UnitMath.FamilyOf(u));
            Assert.Null(ex);
        }
    }

    [Theory]
    [InlineData(2, "kg", 2000)]   // kg → g
    [InlineData(500, "g", 500)]   // g → g (identitet)
    [InlineData(1.5, "l", 1500)]  // l → ml
    [InlineData(750, "ml", 750)]  // ml → ml (identitet)
    [InlineData(3, "stk", 3)]     // count: uændret
    [InlineData(2, "glas", 2)]    // egen enhed: uændret
    public void ToBase_konverterer_til_basisenhed(decimal qty, string unit, decimal expectedBase)
    {
        Assert.Equal(expectedBase, UnitMath.ToBase(qty, unit));
    }

    [Theory]
    [InlineData(800, MeasureFamily.Mass, 800, "g")]      // < 1000 g → g
    [InlineData(1000, MeasureFamily.Mass, 1, "kg")]      // grænse → kg
    [InlineData(1500, MeasureFamily.Mass, 1.5, "kg")]    // > 1000 g → kg
    [InlineData(999, MeasureFamily.Volume, 999, "ml")]   // < 1000 ml → ml
    [InlineData(2500, MeasureFamily.Volume, 2.5, "l")]   // > 1000 ml → l
    public void FromBase_vælger_pæn_visningsenhed(decimal baseQty, MeasureFamily family, decimal expectedQty, string expectedUnit)
    {
        var (qty, unit) = UnitMath.FromBase(baseQty, family);
        Assert.Equal(expectedQty, qty);
        Assert.Equal(expectedUnit, unit);
    }

    [Fact]
    public void ToBase_og_FromBase_er_konsistente_for_masse()
    {
        // 1200 g udtrykt som kg og tilbage skal give samme basis-mængde.
        var baseQty = UnitMath.ToBase(1.2m, "kg");
        var (qty, unit) = UnitMath.FromBase(baseQty, MeasureFamily.Mass);
        Assert.Equal("kg", unit);
        Assert.Equal(1.2m, qty);
    }

    [Theory]
    [InlineData("Glas", "glas")]
    [InlineData("  DL  ", "dl")]
    [InlineData("Dåse", "dåse")]
    public void NormalizeKey_er_case_og_whitespace_insensitiv(string input, string expected)
    {
        Assert.Equal(expected, Units.NormalizeKey(input));
    }

    [Theory]
    [InlineData(null, "stk")]   // tom → default
    [InlineData("", "stk")]
    [InlineData("  glas ", "glas")] // trimmes, men casing bevares
    [InlineData("Kviste", "Kviste")]
    public void Clean_trimmer_og_falder_tilbage_til_default(string? input, string expected)
    {
        Assert.Equal(expected, Units.Clean(input));
    }
}
