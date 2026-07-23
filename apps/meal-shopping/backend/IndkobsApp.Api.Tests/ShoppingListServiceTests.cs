using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for indkøbsliste-aggregeringen — appens hjerte: sammenlægning pr. ingrediens,
/// enhedskonvertering (g↔kg, ml↔l), adskillelse af uforenelige enheder, kategori-
/// sortering og portions-skalering.
/// </summary>
public class ShoppingListServiceTests
{
    private const int HouseholdId = 1;

    // ---- Fælles fixture-hjælpere -------------------------------------------------

    /// <summary>Seed en husstand, en uge og (valgfrit) kategorier + ingredienser.</summary>
    private static void SeedBase(AppDbContext db, int weekId = 100)
    {
        db.Households.Add(new Household { Id = HouseholdId, Name = "Test", Email = "t@t", PasswordHash = "x" });
        db.Weeks.Add(new Week { Id = weekId, HouseholdId = HouseholdId, Year = 2026, WeekNumber = 30 });
        db.SaveChanges();
    }

    private static Category AddCategory(AppDbContext db, int id, string name, int sort)
    {
        var c = new Category { Id = id, HouseholdId = HouseholdId, Name = name, SortOrder = sort };
        db.Categories.Add(c);
        return c;
    }

    private static Ingredient AddIngredient(AppDbContext db, int id, string name, int? categoryId = null)
    {
        var i = new Ingredient
        {
            Id = id,
            HouseholdId = HouseholdId,
            Name = name,
            NormalizedName = Ingredient.Normalize(name),
            CategoryId = categoryId
        };
        db.Ingredients.Add(i);
        return i;
    }

    private static void AddRecipeToWeek(AppDbContext db, int weekId, int recipeId, int baseServings,
        int? weekServings, params (int ingredientId, decimal qty, string unit)[] lines)
    {
        var recipe = new Recipe { Id = recipeId, HouseholdId = HouseholdId, Name = $"Ret {recipeId}", Servings = baseServings };
        db.Recipes.Add(recipe);
        int riId = recipeId * 1000;
        foreach (var (ingredientId, qty, unit) in lines)
            db.RecipeIngredients.Add(new RecipeIngredient { Id = ++riId, RecipeId = recipeId, IngredientId = ingredientId, Quantity = qty, Unit = unit });
        db.WeekRecipes.Add(new WeekRecipe { Id = recipeId + 500, WeekId = weekId, RecipeId = recipeId, Servings = weekServings });
        db.SaveChanges();
    }

    /// <summary>Flad liste af alle linjer på tværs af kategori-grupper.</summary>
    private static List<ShoppingLineDto> AllLines(ShoppingListDto list) =>
        list.Groups.SelectMany(g => g.Lines).ToList();

    // ---- Aggregering & konvertering ---------------------------------------------

    [Fact]
    public async Task Samme_ingrediens_paa_tvaers_af_ret_og_varegruppe_lægges_sammen()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Mel");
        // Ret bidrager med 500 g, varegruppe med 300 g.
        AddRecipeToWeek(db, 100, 1, baseServings: 4, weekServings: null, (10, 500m, Units.G));
        var group = new ItemGroup { Id = 1, HouseholdId = HouseholdId, Name = "Basis" };
        db.ItemGroups.Add(group);
        db.ItemGroupIngredients.Add(new ItemGroupIngredient { Id = 1, ItemGroupId = 1, IngredientId = 10, Quantity = 300m, Unit = Units.G });
        db.WeekItemGroups.Add(new WeekItemGroup { Id = 1, WeekId = 100, ItemGroupId = 1 });
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal("Mel", line.Name);
        Assert.Equal(800m, line.Quantity);
        Assert.Equal(Units.G, line.Unit);
        // Kilder samler både rettens og varegruppens navn.
        Assert.Contains("Ret 1", line.Sources);
        Assert.Contains("Basis", line.Sources);
    }

    [Fact]
    public async Task Masse_konverteres_g_til_kg_ved_visning()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Sukker");
        // 1 kg + 200 g = 1200 g → skal vises som 1,2 kg.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 1m, Units.Kg), (10, 200m, Units.G));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal(1.2m, line.Quantity);
        Assert.Equal(Units.Kg, line.Unit);
    }

    [Fact]
    public async Task Volumen_konverteres_ml_til_l_ved_visning()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Mælk");
        // 500 ml + 0,7 l = 1200 ml → 1,2 l.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 500m, Units.Ml), (10, 0.7m, Units.L));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal(1.2m, line.Quantity);
        Assert.Equal(Units.L, line.Unit);
    }

    [Fact]
    public async Task Uforenelige_enheder_for_samme_vare_holdes_paa_separate_linjer()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Smør");
        // 260 g (masse) + 1 pakke (count) kan ikke lægges sammen → to linjer.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 260m, Units.G), (10, 1m, Units.Pakke));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var lines = AllLines(list!);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("Smør", l.Name));
        Assert.Contains(lines, l => l.Unit == Units.G && l.Quantity == 260m);
        Assert.Contains(lines, l => l.Unit == Units.Pakke && l.Quantity == 1m);
    }

    [Fact]
    public async Task Forskellige_count_enheder_giver_hver_sin_linje()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Æg");
        // Stk og Pakke er begge "count", men forskellige enheder → ikke sammenlægges.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 6m, Units.Stk), (10, 2m, Units.Stk), (10, 1m, Units.Pakke));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var lines = AllLines(list!);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.Unit == Units.Stk && l.Quantity == 8m); // 6 + 2 samme enhed
        Assert.Contains(lines, l => l.Unit == Units.Pakke && l.Quantity == 1m);
    }

    // ---- Egne (fri-tekst) enheder ------------------------------------------------

    [Fact]
    public async Task Egen_enhed_lægges_sammen_pr_distinkt_enhed()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Syltetøj");
        // "glas" er ikke en kendt basisenhed → count. 2 glas + 3 glas = 5 glas på én linje.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 2m, "glas"), (10, 3m, "glas"));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal("Syltetøj", line.Name);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal("glas", line.Unit);
    }

    [Fact]
    public async Task Egen_enhed_lægges_sammen_case_insensitivt()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Syltetøj");
        // "Glas" og "glas" er samme enhed (case-insensitivt) → én linje. Første skrivemåde vises.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 2m, "Glas"), (10, 3m, "glas"));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal(5m, line.Quantity);
        Assert.Equal("Glas", line.Unit); // første-sete skrivemåde
    }

    [Fact]
    public async Task Egen_enhed_og_anden_enhed_holdes_adskilt()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Fløde");
        // "glas" (egen count-enhed) og "dl" (ukendt → også count, men anden enhed) → to linjer.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 2m, "glas"), (10, 3m, "dl"));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var lines = AllLines(list!);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.Unit == "glas" && l.Quantity == 2m);
        Assert.Contains(lines, l => l.Unit == "dl" && l.Quantity == 3m);
    }

    [Fact]
    public async Task Egen_enhed_og_omregnelig_basisenhed_giver_hver_sin_linje()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Mælk");
        // "dl" (egen count-enhed) kan ikke lægges sammen med "l" (volumen) → to linjer.
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 2m, "dl"), (10, 1m, Units.L));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var lines = AllLines(list!);
        Assert.Equal(2, lines.Count);
        Assert.Contains(lines, l => l.Unit == "dl" && l.Quantity == 2m);
        Assert.Contains(lines, l => l.Unit == Units.L && l.Quantity == 1m); // 1 l vises som "l"
    }

    // ---- Kategori-sortering ------------------------------------------------------

    [Fact]
    public async Task Linjer_grupperes_og_sorteres_efter_kategoriens_butiksrækkefølge()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        // Kategorier med bevidst "omvendt" sortering ift. indsættelse.
        AddCategory(db, 1, "Frugt & grønt", sort: 1);
        AddCategory(db, 2, "Mejeri", sort: 2);
        AddIngredient(db, 10, "Ost", categoryId: 2);      // mejeri (sort 2)
        AddIngredient(db, 11, "Æble", categoryId: 1);     // frugt (sort 1)
        AddIngredient(db, 12, "Ukendt vare", categoryId: null); // → "Andet / løse varer"
        AddRecipeToWeek(db, 100, 1, 4, null,
            (10, 1m, Units.Stk), (11, 3m, Units.Stk), (12, 1m, Units.Stk));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        // Rækkefølge: sort 1, sort 2, og til sidst kategori-løse (int.MaxValue).
        Assert.Equal(3, list!.Groups.Count);
        Assert.Equal("Frugt & grønt", list.Groups[0].CategoryName);
        Assert.Equal("Mejeri", list.Groups[1].CategoryName);
        Assert.Equal("Andet / løse varer", list.Groups[2].CategoryName);
        Assert.Null(list.Groups[2].CategoryId);
    }

    [Fact]
    public async Task Fritekst_vare_havner_i_andet()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        db.WeekManualItems.Add(new WeekManualItem { Id = 1, WeekId = 100, IngredientId = null, FreeText = "Grillkul", Quantity = 1m, Unit = Units.Stk });
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal("Grillkul", line.Name);
        Assert.Null(line.IngredientId);
        Assert.True(line.IsManual);
        Assert.Equal("Andet / løse varer", list!.Groups[0].CategoryName);
    }

    // ---- Portions-skalering ------------------------------------------------------

    [Fact]
    public async Task Ret_skaleres_efter_ønskede_portioner()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Pasta");
        // Basis 4 portioner, ugen ønsker 8 → faktor 2 → 400 g.
        AddRecipeToWeek(db, 100, 1, baseServings: 4, weekServings: 8, (10, 200m, Units.G));
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal(400m, line.Quantity);
    }

    [Fact]
    public async Task Varegruppe_skaleres_ikke()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Toiletpapir");
        var group = new ItemGroup { Id = 1, HouseholdId = HouseholdId, Name = "Husholdning" };
        db.ItemGroups.Add(group);
        db.ItemGroupIngredients.Add(new ItemGroupIngredient { Id = 1, ItemGroupId = 1, IngredientId = 10, Quantity = 2m, Unit = Units.Pakke });
        db.WeekItemGroups.Add(new WeekItemGroup { Id = 1, WeekId = 100, ItemGroupId = 1 });
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(Units.Pakke, line.Unit);
    }

    // ---- Afkrydsning huskes via LineKey -----------------------------------------

    [Fact]
    public async Task Afkrydsning_huskes_via_stabil_LineKey()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);
        AddIngredient(db, 10, "Kaffe");
        AddRecipeToWeek(db, 100, 1, 4, null, (10, 500m, Units.G));
        // LineKey for masse: "ing:{id}:Mass".
        db.ShoppingListChecks.Add(new ShoppingListCheck { Id = 1, WeekId = 100, LineKey = "ing:10:Mass", IsChecked = true });
        db.SaveChanges();

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(100);

        var line = Assert.Single(AllLines(list!));
        Assert.Equal("ing:10:Mass", line.LineKey);
        Assert.True(line.IsChecked);
    }

    [Fact]
    public async Task Ukendt_uge_giver_null()
    {
        var db = TestDb.NewContext(out var name);
        SeedBase(db);

        var list = await new ShoppingListService(TestDb.Open(name)).BuildAsync(weekId: 999);

        Assert.Null(list);
    }
}
