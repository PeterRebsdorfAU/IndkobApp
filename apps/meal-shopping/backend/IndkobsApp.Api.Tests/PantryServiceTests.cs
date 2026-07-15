using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for lager-aritmetikken: at lægge på lageret merger forenelige enheder,
/// og at "forbrug" (når en ret laves) trækkes korrekt fra — aldrig i minus.
/// </summary>
public class PantryServiceTests
{
    private const int HouseholdId = 1;
    private const int IngredientId = 10;

    private static AppDbContext Seed(out string name)
    {
        var db = TestDb.NewContext(out name);
        db.Households.Add(new Household { Id = HouseholdId, Name = "T", Email = "t@t", PasswordHash = "x" });
        db.Ingredients.Add(new Ingredient { Id = IngredientId, HouseholdId = HouseholdId, Name = "Mel", NormalizedName = "mel" });
        db.SaveChanges();
        return db;
    }

    // ---- AddOrMerge --------------------------------------------------------------

    [Fact]
    public async Task AddOrMerge_opretter_ny_linje_naar_intet_findes()
    {
        var db = Seed(out var name);
        await new PantryService(db).AddOrMergeAsync(HouseholdId, IngredientId, 3m, Unit.Stk);
        await db.SaveChangesAsync();

        var item = Assert.Single(await TestDb.Open(name).PantryItems.ToListAsync());
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(Unit.Stk, item.Unit);
    }

    [Fact]
    public async Task AddOrMerge_lægger_forenelige_masse_enheder_sammen_og_normaliserer()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 500m, Unit = Unit.G });
        db.SaveChanges();

        // 500 g + 1 kg = 1500 g → normaliseres til 1,5 kg i én linje.
        await new PantryService(db).AddOrMergeAsync(HouseholdId, IngredientId, 1m, Unit.Kg);
        await db.SaveChangesAsync();

        var item = Assert.Single(await TestDb.Open(name).PantryItems.ToListAsync());
        Assert.Equal(1.5m, item.Quantity);
        Assert.Equal(Unit.Kg, item.Unit);
    }

    [Fact]
    public async Task AddOrMerge_samme_count_enhed_lægges_sammen()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 2m, Unit = Unit.Stk });
        db.SaveChanges();

        await new PantryService(db).AddOrMergeAsync(HouseholdId, IngredientId, 3m, Unit.Stk);
        await db.SaveChangesAsync();

        var item = Assert.Single(await TestDb.Open(name).PantryItems.ToListAsync());
        Assert.Equal(5m, item.Quantity);
    }

    [Fact]
    public async Task AddOrMerge_forskellige_count_enheder_giver_separate_linjer()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 2m, Unit = Unit.Stk });
        db.SaveChanges();

        // Pakke ≠ Stk (begge count, men ikke samme enhed) → ny linje.
        await new PantryService(db).AddOrMergeAsync(HouseholdId, IngredientId, 1m, Unit.Pakke);
        await db.SaveChangesAsync();

        var items = await TestDb.Open(name).PantryItems.ToListAsync();
        Assert.Equal(2, items.Count);
    }

    // ---- Consume -----------------------------------------------------------------

    [Fact]
    public async Task Consume_traekker_delvist_fra_lageret()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 1m, Unit = Unit.Kg });
        db.SaveChanges();

        // Forbrug 300 g af 1000 g → 700 g tilbage.
        await new PantryService(db).ConsumeAsync(HouseholdId, IngredientId, 300m, Unit.G);
        await db.SaveChangesAsync();

        var item = Assert.Single(await TestDb.Open(name).PantryItems.ToListAsync());
        Assert.Equal(700m, item.Quantity);
        Assert.Equal(Unit.G, item.Unit);
    }

    [Fact]
    public async Task Consume_fjerner_linjen_naar_den_bruges_helt_op()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 200m, Unit = Unit.G });
        db.SaveChanges();

        await new PantryService(db).ConsumeAsync(HouseholdId, IngredientId, 200m, Unit.G);
        await db.SaveChangesAsync();

        Assert.Empty(await TestDb.Open(name).PantryItems.ToListAsync());
    }

    [Fact]
    public async Task Consume_går_aldrig_i_minus_ved_for_stort_forbrug()
    {
        var db = Seed(out var name);
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 100m, Unit = Unit.G });
        db.SaveChanges();

        // Forbrug 500 g selvom kun 100 g findes → linjen tømmes/fjernes, ingen negativ rest.
        await new PantryService(db).ConsumeAsync(HouseholdId, IngredientId, 500m, Unit.G);
        await db.SaveChangesAsync();

        Assert.Empty(await TestDb.Open(name).PantryItems.ToListAsync());
    }

    [Fact]
    public async Task Consume_rører_ikke_uforenelig_enhed()
    {
        var db = Seed(out var name);
        // Lager i pakke (count); forbrug i gram (masse) → intet trækkes fra.
        db.PantryItems.Add(new PantryItem { Id = 1, HouseholdId = HouseholdId, IngredientId = IngredientId, Quantity = 2m, Unit = Unit.Pakke });
        db.SaveChanges();

        await new PantryService(db).ConsumeAsync(HouseholdId, IngredientId, 500m, Unit.G);
        await db.SaveChangesAsync();

        var item = Assert.Single(await TestDb.Open(name).PantryItems.ToListAsync());
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(Unit.Pakke, item.Unit);
    }
}
