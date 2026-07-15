using IndkobsApp.Api.Models;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Enheds-aritmetikken (<see cref="UnitMath"/>) er fundamentet under indkøbsliste-
/// aggregeringen: familie-inddeling, konvertering til basisenhed og valg af pæn
/// visningsenhed. Fejl her forplanter sig til hele listen.
/// </summary>
public class UnitMathTests
{
    [Theory]
    [InlineData(Unit.G, MeasureFamily.Mass)]
    [InlineData(Unit.Kg, MeasureFamily.Mass)]
    [InlineData(Unit.Ml, MeasureFamily.Volume)]
    [InlineData(Unit.L, MeasureFamily.Volume)]
    [InlineData(Unit.Stk, MeasureFamily.Count)]
    [InlineData(Unit.Spsk, MeasureFamily.Count)]
    [InlineData(Unit.Pakke, MeasureFamily.Count)]
    [InlineData(Unit.Daase, MeasureFamily.Count)]
    [InlineData(Unit.Fed, MeasureFamily.Count)]
    public void FamilyOf_grupperer_enheder_korrekt(Unit unit, MeasureFamily expected)
    {
        Assert.Equal(expected, UnitMath.FamilyOf(unit));
    }

    [Fact]
    public void FamilyOf_dækker_alle_enheder_i_enum()
    {
        // Beskytter mod at en ny Unit tilføjes uden at blive kortlagt (ellers smider Map).
        foreach (Unit u in Enum.GetValues<Unit>())
        {
            var ex = Record.Exception(() => UnitMath.FamilyOf(u));
            Assert.Null(ex);
        }
    }

    [Theory]
    [InlineData(2, Unit.Kg, 2000)]   // kg → g
    [InlineData(500, Unit.G, 500)]   // g → g (identitet)
    [InlineData(1.5, Unit.L, 1500)]  // l → ml
    [InlineData(750, Unit.Ml, 750)]  // ml → ml (identitet)
    [InlineData(3, Unit.Stk, 3)]     // count: uændret
    public void ToBase_konverterer_til_basisenhed(decimal qty, Unit unit, decimal expectedBase)
    {
        Assert.Equal(expectedBase, UnitMath.ToBase(qty, unit));
    }

    [Theory]
    [InlineData(800, MeasureFamily.Mass, 800, Unit.G)]      // < 1000 g → g
    [InlineData(1000, MeasureFamily.Mass, 1, Unit.Kg)]      // grænse → kg
    [InlineData(1500, MeasureFamily.Mass, 1.5, Unit.Kg)]    // > 1000 g → kg
    [InlineData(999, MeasureFamily.Volume, 999, Unit.Ml)]   // < 1000 ml → ml
    [InlineData(2500, MeasureFamily.Volume, 2.5, Unit.L)]   // > 1000 ml → l
    public void FromBase_vælger_pæn_visningsenhed(decimal baseQty, MeasureFamily family, decimal expectedQty, Unit expectedUnit)
    {
        var (qty, unit) = UnitMath.FromBase(baseQty, family);
        Assert.Equal(expectedQty, qty);
        Assert.Equal(expectedUnit, unit);
    }

    [Fact]
    public void ToBase_og_FromBase_er_konsistente_for_masse()
    {
        // 1200 g udtrykt som kg og tilbage skal give samme basis-mængde.
        var baseQty = UnitMath.ToBase(1.2m, Unit.Kg);
        var (qty, unit) = UnitMath.FromBase(baseQty, MeasureFamily.Mass);
        Assert.Equal(Unit.Kg, unit);
        Assert.Equal(1.2m, qty);
    }
}
