using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for ingrediens-normalisering + get-or-create: samme vare med forskellig
/// store/små bogstaver og mellemrum må ikke give dubletter i husstanden, men skal
/// være adskilt PR. HUSSTAND.
/// </summary>
public class IngredientServiceTests
{
    private const int HouseholdA = 1;
    private const int HouseholdB = 2;

    private static AppDbContext Seed(out string name)
    {
        var db = TestDb.NewContext(out name);
        db.Households.Add(new Household { Id = HouseholdA, Name = "A", Email = "a@a", PasswordHash = "x" });
        db.Households.Add(new Household { Id = HouseholdB, Name = "B", Email = "b@b", PasswordHash = "x" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public void Normalize_trimmer_og_lowercaser()
    {
        Assert.Equal("løg", Ingredient.Normalize("  Løg "));
        Assert.Equal("løg", Ingredient.Normalize("LØG"));
        Assert.Equal("", Ingredient.Normalize("   "));
        Assert.Equal("", Ingredient.Normalize(null!));
    }

    [Fact]
    public async Task GetOrCreate_opretter_ny_ingrediens_med_trimmet_navn()
    {
        var db = Seed(out _);
        var svc = new IngredientService(db);

        var created = await svc.GetOrCreateAsync(HouseholdA, "  Gulerod ");
        await db.SaveChangesAsync();

        Assert.Equal("Gulerod", created.Name);          // vist navn er trimmet, bevarer store bogstav
        Assert.Equal("gulerod", created.NormalizedName); // normaliseret er lowercased
        Assert.Equal(HouseholdA, created.HouseholdId);
    }

    [Fact]
    public async Task GetOrCreate_genbruger_eksisterende_uanset_store_smaa_bogstaver()
    {
        var db = Seed(out var name);
        var svc = new IngredientService(db);

        var first = await svc.GetOrCreateAsync(HouseholdA, "Løg");
        await db.SaveChangesAsync();

        // Forskellig casing + mellemrum skal ramme SAMME række (via DB-opslag).
        var second = await svc.GetOrCreateAsync(HouseholdA, "  løg ");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await TestDb.Open(name).Ingredients.ToListAsync());
    }

    [Fact]
    public async Task GetOrCreate_dedupper_inden_for_samme_request_uden_save()
    {
        var db = Seed(out _);
        var svc = new IngredientService(db);

        // To linjer i samme request peger på samme nye (endnu ugemte) ingrediens.
        var a = await svc.GetOrCreateAsync(HouseholdA, "Peber");
        var b = await svc.GetOrCreateAsync(HouseholdA, "PEBER");

        Assert.Same(a, b); // fanget via change-trackerens Local uden SaveChanges
    }

    [Fact]
    public async Task GetOrCreate_er_scopet_pr_husstand()
    {
        var db = Seed(out var name);
        var svc = new IngredientService(db);

        var a = await svc.GetOrCreateAsync(HouseholdA, "Mælk");
        var b = await svc.GetOrCreateAsync(HouseholdB, "Mælk");
        await db.SaveChangesAsync();

        Assert.NotEqual(a.Id, b.Id); // hver husstand har sin egen varebank
        var all = await TestDb.Open(name).Ingredients.ToListAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetOrCreate_sætter_kategori_paa_ny_ingrediens()
    {
        var db = Seed(out _);
        db.Categories.Add(new Category { Id = 5, HouseholdId = HouseholdA, Name = "Mejeri", SortOrder = 1 });
        db.SaveChanges();
        var svc = new IngredientService(db);

        var created = await svc.GetOrCreateAsync(HouseholdA, "Yoghurt", categoryId: 5);

        Assert.Equal(5, created.CategoryId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrCreate_kaster_ved_tomt_navn(string navn)
    {
        var db = Seed(out _);
        var svc = new IngredientService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetOrCreateAsync(HouseholdA, navn));
    }
}
