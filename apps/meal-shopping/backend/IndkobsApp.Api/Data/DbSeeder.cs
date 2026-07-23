using IndkobsApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Data;

/// <summary>
/// Lægger lidt startdata ind, så appen viser noget med det samme:
/// globale kategorier/ingredienser + en demo-husstand med eksempel-retter og -varegrupper.
/// Kører kun hvis der endnu ikke findes nogen husstand (idempotent).
/// </summary>
public static class DbSeeder
{
    /// <summary>Standard-kategorisæt (butiksrækkefølge) som nye husstande starter med.</summary>
    public static readonly (string Name, int SortOrder)[] DefaultCategories =
    {
        ("Grønt", 10), ("Mejeri", 20), ("Kød", 30), ("Kolonial", 40),
        ("Frost", 50), ("Brød", 60), ("Non-food / Toilet", 70), ("Rengøring", 80)
    };

    /// <summary>
    /// Giver en NY husstand sit eget standard-kategorisæt (kategorier er private
    /// pr. husstand — hver husstand har sin egen butiksrækkefølge).
    /// Kalderen er ansvarlig for SaveChanges.
    /// </summary>
    public static void SeedDefaultCategories(AppDbContext db, int householdId)
    {
        foreach (var (name, sort) in DefaultCategories)
            db.Categories.Add(new Category { HouseholdId = householdId, Name = name, SortOrder = sort });
    }

    /// <summary>
    /// Seeder inspirations-kataloget (fælles, ikke husstands-data). Kører uafhængigt
    /// af husstands-seed og kun hvis kataloget er tomt — rører ALDRIG eksisterende data.
    /// </summary>
    public static async Task SeedCatalogAsync(AppDbContext db)
    {
        if (await db.CatalogRecipes.AnyAsync())
            return; // allerede seedet

        CatalogRecipeIngredient L(string name, decimal qty, Unit unit) =>
            new() { Name = name, Quantity = qty, Unit = unit };

        db.CatalogRecipes.AddRange(
            new CatalogRecipe
            {
                Title = "Butter chicken", Servings = 4, Tags = "aftensmad,indisk",
                Note = "Cremet indisk klassiker — servér med ris.",
                Ingredients =
                {
                    L("Kyllingebryst", 600, Unit.G), L("Smør", 50, Unit.G),
                    L("Løg", 1, Unit.Stk), L("Hvidløg", 3, Unit.Fed),
                    L("Hakkede tomater", 1, Unit.Daase), L("Piskefløde", 250, Unit.Ml),
                    L("Garam masala", 1, Unit.Spsk), L("Ris", 300, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Dahl med røde linser", Servings = 4, Tags = "aftensmad,vegetar,billig",
                Note = "Nem vegetarret der mætter.",
                Ingredients =
                {
                    L("Røde linser", 300, Unit.G), L("Løg", 1, Unit.Stk),
                    L("Hvidløg", 2, Unit.Fed), L("Hakkede tomater", 1, Unit.Daase),
                    L("Kokosmælk", 1, Unit.Daase), L("Karry", 1, Unit.Spsk),
                    L("Ris", 300, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Tacos med oksekød", Servings = 4, Tags = "aftensmad,hurtig,fredag",
                Note = "Fredagsklassiker — byg selv.",
                Ingredients =
                {
                    L("Hakket oksekød", 500, Unit.G), L("Tortillas", 8, Unit.Stk),
                    L("Tacokrydderi", 1, Unit.Pakke), L("Salat", 1, Unit.Stk),
                    L("Tomat", 2, Unit.Stk), L("Revet ost", 200, Unit.G),
                    L("Creme fraiche", 1, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Ovnbagt laks med kartofler", Servings = 4, Tags = "aftensmad,fisk,sund",
                Ingredients =
                {
                    L("Laksefilet", 500, Unit.G), L("Kartofler", 800, Unit.G),
                    L("Citron", 1, Unit.Stk), L("Smør", 30, Unit.G),
                    L("Broccoli", 1, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Kylling i karry", Servings = 4, Tags = "aftensmad,klassiker",
                Ingredients =
                {
                    L("Kyllingebryst", 500, Unit.G), L("Løg", 2, Unit.Stk),
                    L("Æble", 1, Unit.Stk), L("Karry", 2, Unit.Spsk),
                    L("Piskefløde", 250, Unit.Ml), L("Ris", 300, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Pasta carbonara", Servings = 4, Tags = "aftensmad,hurtig,pasta",
                Note = "Ægte carbonara uden fløde.",
                Ingredients =
                {
                    L("Spaghetti", 400, Unit.G), L("Bacon", 150, Unit.G),
                    L("Æg", 3, Unit.Stk), L("Parmesan", 80, Unit.G),
                    L("Peber", 1, Unit.Tsk)
                }
            },
            new CatalogRecipe
            {
                Title = "Grøntsagssuppe", Servings = 4, Tags = "aftensmad,vegetar,sund,billig",
                Ingredients =
                {
                    L("Gulerod", 400, Unit.G), L("Kartofler", 400, Unit.G),
                    L("Porre", 1, Unit.Stk), L("Løg", 1, Unit.Stk),
                    L("Grøntsagsbouillon", 1, Unit.L), L("Rugbrød", 4, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Frikadeller med kartoffelsalat", Servings = 4, Tags = "aftensmad,dansk,klassiker",
                Ingredients =
                {
                    L("Hakket svinekød", 500, Unit.G), L("Æg", 1, Unit.Stk),
                    L("Løg", 1, Unit.Stk), L("Havregryn", 50, Unit.G),
                    L("Mælk", 100, Unit.Ml), L("Kartofler", 800, Unit.G),
                    L("Creme fraiche", 1, Unit.Stk), L("Purløg", 1, Unit.Bundt)
                }
            },
            new CatalogRecipe
            {
                Title = "Shakshuka", Servings = 2, Tags = "aftensmad,vegetar,hurtig",
                Note = "Æg pocheret i krydret tomatsauce.",
                Ingredients =
                {
                    L("Æg", 4, Unit.Stk), L("Hakkede tomater", 2, Unit.Daase),
                    L("Løg", 1, Unit.Stk), L("Peberfrugt", 1, Unit.Stk),
                    L("Hvidløg", 2, Unit.Fed), L("Spidskommen", 1, Unit.Tsk),
                    L("Fetaost", 100, Unit.G), L("Brød", 1, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Stegte nudler med kylling", Servings = 4, Tags = "aftensmad,asiatisk,hurtig",
                Ingredients =
                {
                    L("Nudler", 250, Unit.G), L("Kyllingebryst", 400, Unit.G),
                    L("Gulerod", 200, Unit.G), L("Spidskål", 0.5m, Unit.Stk),
                    L("Soyasauce", 4, Unit.Spsk), L("Hvidløg", 2, Unit.Fed),
                    L("Ingefær", 1, Unit.Stk)
                }
            });

        await db.SaveChangesAsync();
    }

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<Household> hasher, IConfiguration cfg)
    {
        if (await db.Households.AnyAsync())
            return; // allerede seedet

        // --- Demo-husstand (login). Kan overstyres via Seed:Email / Seed:Password. ---
        var email = Household.NormalizeEmail(cfg["Seed:Email"] ?? "demo@husstand.dk");
        var password = cfg["Seed:Password"] ?? "Skift1234!";
        var demo = new Household { Name = "Demo-husstand", Email = email };
        demo.PasswordHash = hasher.HashPassword(demo, password);
        db.Households.Add(demo);
        await db.SaveChangesAsync(); // få demo.Id

        // T2: en første individuel bruger for demo-husstanden (samme login).
        // Identity-hashformatet er ens for Household/User, så hash'en genbruges direkte.
        db.Users.Add(new User
        {
            HouseholdId = demo.Id,
            Email = email,
            PasswordHash = demo.PasswordHash,
            DisplayName = "Demo",
            EmailConfirmed = true
        });

        // --- Kategorier (butiksrækkefølge) — demo-husstandens egne ---
        var grønt    = new Category { HouseholdId = demo.Id, Name = "Grønt", SortOrder = 10 };
        var mejeri   = new Category { HouseholdId = demo.Id, Name = "Mejeri", SortOrder = 20 };
        var kød      = new Category { HouseholdId = demo.Id, Name = "Kød", SortOrder = 30 };
        var kolonial = new Category { HouseholdId = demo.Id, Name = "Kolonial", SortOrder = 40 };
        var frost    = new Category { HouseholdId = demo.Id, Name = "Frost", SortOrder = 50 };
        var brød     = new Category { HouseholdId = demo.Id, Name = "Brød", SortOrder = 60 };
        var nonfood  = new Category { HouseholdId = demo.Id, Name = "Non-food / Toilet", SortOrder = 70 };
        var rengøring = new Category { HouseholdId = demo.Id, Name = "Rengøring", SortOrder = 80 };
        db.Categories.AddRange(grønt, mejeri, kød, kolonial, frost, brød, nonfood, rengøring);

        // --- Ingredienser — demo-husstandens egen varebank ---
        Ingredient Ing(string name, Category? cat) =>
            new() { HouseholdId = demo.Id, Name = name, NormalizedName = Ingredient.Normalize(name), Category = cat };

        var løg       = Ing("Løg", grønt);
        var hvidløg   = Ing("Hvidløg", grønt);
        var gulerod   = Ing("Gulerod", grønt);
        var tomat     = Ing("Hakkede tomater", kolonial);
        var spaghetti = Ing("Spaghetti", kolonial);
        var hakkokse  = Ing("Hakket oksekød", kød);
        var smør      = Ing("Smør", mejeri);
        var mælk      = Ing("Mælk", mejeri);
        var parmesan  = Ing("Parmesan", mejeri);
        var oliven    = Ing("Olivenolie", kolonial);
        var salt      = Ing("Salt", kolonial);
        var peber     = Ing("Peber", kolonial);
        var rugbrød   = Ing("Rugbrød", brød);
        var hamburgerryg = Ing("Hamburgerryg", kød);
        var toiletpapir = Ing("Toiletpapir", nonfood);
        var sæbe      = Ing("Håndsæbe", nonfood);
        db.Ingredients.AddRange(løg, hvidløg, gulerod, tomat, spaghetti, hakkokse, smør, mælk,
            parmesan, oliven, salt, peber, rugbrød, hamburgerryg, toiletpapir, sæbe);

        // --- Ret 1: Spaghetti med kødsovs (4 personer) — tilhører demo-husstanden ---
        var spagBolognese = new Recipe
        {
            HouseholdId = demo.Id,
            Name = "Spaghetti med kødsovs",
            Note = "Klassisk hverdagsret.",
            Servings = 4,
            Ingredients =
            {
                new RecipeIngredient { Ingredient = spaghetti, Quantity = 400, Unit = Unit.G },
                new RecipeIngredient { Ingredient = hakkokse,  Quantity = 500, Unit = Unit.G },
                new RecipeIngredient { Ingredient = løg,       Quantity = 2,   Unit = Unit.Stk },
                new RecipeIngredient { Ingredient = hvidløg,   Quantity = 2,   Unit = Unit.Fed },
                new RecipeIngredient { Ingredient = tomat,     Quantity = 2,   Unit = Unit.Daase },
                new RecipeIngredient { Ingredient = oliven,    Quantity = 2,   Unit = Unit.Spsk },
                new RecipeIngredient { Ingredient = parmesan,  Quantity = 50,  Unit = Unit.G },
                new RecipeIngredient { Ingredient = salt,      Quantity = 1,   Unit = Unit.Tsk },
            }
        };

        // --- Ret 2: Gulerodssuppe (2 personer) — deler løg/smør med ret 1 så aggregering ses ---
        var gulerodssuppe = new Recipe
        {
            HouseholdId = demo.Id,
            Name = "Gulerodssuppe",
            Note = "Cremet og nem.",
            Servings = 2,
            Ingredients =
            {
                new RecipeIngredient { Ingredient = gulerod, Quantity = 500, Unit = Unit.G },
                new RecipeIngredient { Ingredient = løg,     Quantity = 1,   Unit = Unit.Stk },
                new RecipeIngredient { Ingredient = smør,    Quantity = 30,  Unit = Unit.G },
                new RecipeIngredient { Ingredient = mælk,    Quantity = 500, Unit = Unit.Ml },
            }
        };

        db.Recipes.AddRange(spagBolognese, gulerodssuppe);

        // --- Varegruppe: Frokost ---
        var frokost = new ItemGroup
        {
            HouseholdId = demo.Id,
            Name = "Frokost",
            Ingredients =
            {
                new ItemGroupIngredient { Ingredient = rugbrød, Quantity = 1, Unit = Unit.Pakke },
                new ItemGroupIngredient { Ingredient = hamburgerryg, Quantity = 1, Unit = Unit.Stk },
                new ItemGroupIngredient { Ingredient = smør, Quantity = 200, Unit = Unit.G },
            }
        };

        // --- Varegruppe: Toilet ---
        var toilet = new ItemGroup
        {
            HouseholdId = demo.Id,
            Name = "Toilet",
            Ingredients =
            {
                new ItemGroupIngredient { Ingredient = toiletpapir, Quantity = 1, Unit = Unit.Pakke },
                new ItemGroupIngredient { Ingredient = sæbe, Quantity = 2, Unit = Unit.Stk },
            }
        };
        db.ItemGroups.AddRange(frokost, toilet);

        await db.SaveChangesAsync();
    }
}
