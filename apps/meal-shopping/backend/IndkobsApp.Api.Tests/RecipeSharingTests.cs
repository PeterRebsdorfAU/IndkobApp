using System.Security.Claims;
using IndkobsApp.Api.Controllers;
using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for selektiv deling af opskrifter: del/fjern, "delt med mig", adoption og
/// husstands-scoping (kun ejeren kan dele/fjerne; kun modtageren ser den delte opskrift).
/// </summary>
public class RecipeSharingTests
{
    private const int Owner = 1;      // ejer-husstand
    private const int Target = 2;     // modtager-husstand
    private const int Outsider = 3;   // uvedkommende husstand

    private static AppDbContext Seed(out string name)
    {
        var db = TestDb.NewContext(out name);
        db.Households.Add(new Household { Id = Owner, Name = "Ejer", Email = "ejer@test", PasswordHash = "x" });
        db.Households.Add(new Household { Id = Target, Name = "Modtager", Email = "modtager@test", PasswordHash = "x" });
        db.Households.Add(new Household { Id = Outsider, Name = "Fremmed", Email = "fremmed@test", PasswordHash = "x" });
        db.SaveChanges();
        return db;
    }

    // Opret en opskrift med én ingrediens + fremgangsmåde til ejer-husstanden.
    private static int SeedRecipe(string dbName, int householdId, string navn = "Lasagne")
    {
        using var db = TestDb.Open(dbName);
        var ing = new Ingredient { HouseholdId = householdId, Name = "Hakket oksekød", NormalizedName = "hakket oksekød" };
        var recipe = new Recipe
        {
            HouseholdId = householdId,
            Name = navn,
            Servings = 4,
            Method = "1) Brun kødet. 2) Saml lag.",
            Ingredients = { new RecipeIngredient { Ingredient = ing, Quantity = 500, Unit = Unit.G } }
        };
        db.Recipes.Add(recipe);
        db.SaveChanges();
        return recipe.Id;
    }

    // En RecipesController hvis User.GetHouseholdId() returnerer householdId.
    private static RecipesController Controller(string dbName, int householdId)
    {
        var db = TestDb.Open(dbName);
        var ingredients = new IngredientService(db);
        var ctrl = new RecipesController(db, ingredients, new FakeScanner(), new RecipeAdoptionService(db, ingredients));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(TokenService.HouseholdIdClaim, householdId.ToString()) }));
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return ctrl;
    }

    private sealed class FakeScanner : IRecipeScanner
    {
        public bool Enabled => false;
        public Task<ScannedRecipe> ScanAsync(byte[] imageBytes, string contentType, CancellationToken ct = default)
            => throw new InvalidOperationException();
    }

    // ---- Del ---------------------------------------------------------------------

    [Fact]
    public async Task Share_deler_med_modtager_fundet_paa_email()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        var res = await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("MODTAGER@test"));
        var dto = Assert.IsType<RecipeShareTargetDto>(res.Value);
        Assert.Equal(Target, dto.TargetHouseholdId);

        using var check = TestDb.Open(name);
        var share = Assert.Single(check.RecipeShares);
        Assert.Equal(recipeId, share.RecipeId);
        Assert.Equal(Target, share.TargetHouseholdId);
    }

    [Fact]
    public async Task Share_er_idempotent()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        using var check = TestDb.Open(name);
        Assert.Single(check.RecipeShares); // ingen dublet
    }

    [Fact]
    public async Task Share_paa_ukendt_email_giver_404()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        var res = await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("findes-ikke@test"));
        Assert.IsType<NotFoundObjectResult>(res.Result);
    }

    [Fact]
    public async Task Share_kan_ikke_dele_med_sig_selv()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        var res = await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("ejer@test"));
        Assert.IsType<BadRequestObjectResult>(res.Result);
    }

    [Fact]
    public async Task Share_kun_ejeren_kan_dele_egen_opskrift()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        // Fremmed husstand forsøger at dele ejerens opskrift → 404 (ser den ikke).
        var res = await Controller(name, Outsider).Share(recipeId, new ShareRecipeDto("modtager@test"));
        Assert.IsType<NotFoundResult>(res.Result);
        using var check = TestDb.Open(name);
        Assert.Empty(check.RecipeShares);
    }

    // ---- Fjern -------------------------------------------------------------------

    [Fact]
    public async Task Unshare_fjerner_delingen()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        var res = await Controller(name, Owner).Unshare(recipeId, Target);
        Assert.IsType<NoContentResult>(res);
        using var check = TestDb.Open(name);
        Assert.Empty(check.RecipeShares);
    }

    [Fact]
    public async Task Unshare_kun_ejeren_kan_fjerne()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        // Modtageren (eller andre) kan ikke fjerne ejerens deling.
        var res = await Controller(name, Target).Unshare(recipeId, Target);
        Assert.IsType<NotFoundResult>(res);
        using var check = TestDb.Open(name);
        Assert.Single(check.RecipeShares); // stadig delt
    }

    // ---- Hvem er den delt med ----------------------------------------------------

    [Fact]
    public async Task GetShares_viser_modtagere_for_ejeren()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        var res = await Controller(name, Owner).GetShares(recipeId);
        var list = Assert.IsAssignableFrom<IEnumerable<RecipeShareTargetDto>>(res.Value);
        var one = Assert.Single(list);
        Assert.Equal(Target, one.TargetHouseholdId);
        Assert.Equal("Modtager", one.HouseholdName);
    }

    [Fact]
    public async Task GetShares_afvises_for_ikke_ejer()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);

        var res = await Controller(name, Outsider).GetShares(recipeId);
        Assert.IsType<NotFoundResult>(res.Result);
    }

    // ---- Delt med mig ------------------------------------------------------------

    [Fact]
    public async Task SharedWithMe_viser_kun_opskrifter_delt_til_min_husstand()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        // Modtageren ser den.
        var mine = (await Controller(name, Target).SharedWithMe()).ToList();
        var shared = Assert.Single(mine);
        Assert.Equal("Lasagne", shared.Name);
        Assert.Equal("Ejer", shared.SharedByHouseholdName);
        Assert.Equal("1) Brun kødet. 2) Saml lag.", shared.Method);
        Assert.Single(shared.Ingredients);

        // En uvedkommende husstand ser INTET (scoping).
        Assert.Empty(await Controller(name, Outsider).SharedWithMe());
    }

    // ---- Adopter en delt opskrift ------------------------------------------------

    [Fact]
    public async Task AdoptShared_kopierer_til_egne_med_method_og_mapper_ingredienser()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        var res = await Controller(name, Target).AdoptShared(recipeId, new AdoptCatalogRecipeDto(null, null, null));
        var dto = Assert.IsType<AdoptResultDto>(res.Value);
        Assert.Equal("Lasagne", dto.RecipeName);

        using var check = TestDb.Open(name);
        // Modtageren har nu sin EGEN kopi med samme navn + fremgangsmåde.
        var copy = check.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
            .Single(r => r.HouseholdId == Target && r.Name == "Lasagne");
        Assert.Equal("1) Brun kødet. 2) Saml lag.", copy.Method);
        // Ingrediensen er mappet ind i MODTAGERENS egen varebank (ikke ejerens).
        var line = Assert.Single(copy.Ingredients);
        Assert.Equal(Target, line.Ingredient.HouseholdId);
        Assert.Equal("Hakket oksekød", line.Ingredient.Name);
    }

    [Fact]
    public async Task AdoptShared_afvises_hvis_ikke_delt_til_mig()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        // Ikke delt med nogen.

        var res = await Controller(name, Target).AdoptShared(recipeId, null);
        Assert.IsType<NotFoundResult>(res.Result);
    }

    [Fact]
    public async Task AdoptShared_er_idempotent_paa_navn()
    {
        var db = Seed(out var name); db.Dispose();
        var recipeId = SeedRecipe(name, Owner);
        await Controller(name, Owner).Share(recipeId, new ShareRecipeDto("modtager@test"));

        await Controller(name, Target).AdoptShared(recipeId, null);
        await Controller(name, Target).AdoptShared(recipeId, null);

        using var check = TestDb.Open(name);
        Assert.Single(check.Recipes.Where(r => r.HouseholdId == Target)); // ingen dublet
    }

    // ---- Oprydning ved husstands-sletning ---------------------------------------

    [Fact]
    public async Task HouseholdEraser_rydder_delinger_i_begge_roller()
    {
        var db = Seed(out var name); db.Dispose();
        var ownerRecipe = SeedRecipe(name, Owner);
        var targetRecipe = SeedRecipe(name, Target, "Tærte");
        // Ejer deler MED modtager; modtager deler sin egen ret med ejer.
        await Controller(name, Owner).Share(ownerRecipe, new ShareRecipeDto("modtager@test"));
        await Controller(name, Target).Share(targetRecipe, new ShareRecipeDto("ejer@test"));

        using (var erase = TestDb.Open(name))
            await HouseholdEraser.EraseAsync(erase, Target);

        using var check = TestDb.Open(name);
        Assert.Empty(check.RecipeShares); // begge delinger involverede Target → væk
        Assert.False(check.Households.Any(h => h.Id == Target));
    }
}
