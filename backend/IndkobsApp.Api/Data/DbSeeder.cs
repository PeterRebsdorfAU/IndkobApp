using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Data;

/// <summary>
/// Lægger lidt startdata ind, så appen viser noget med det samme.
/// Kører kun hvis databasen er tom (idempotent).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Categories.AnyAsync() || await db.Recipes.AnyAsync())
            return; // allerede seedet

        // --- Kategorier (butiksrækkefølge) ---
        var grønt    = new Category { Name = "Grønt", SortOrder = 10 };
        var mejeri   = new Category { Name = "Mejeri", SortOrder = 20 };
        var kød      = new Category { Name = "Kød", SortOrder = 30 };
        var kolonial = new Category { Name = "Kolonial", SortOrder = 40 };
        var frost    = new Category { Name = "Frost", SortOrder = 50 };
        var brød     = new Category { Name = "Brød", SortOrder = 60 };
        var nonfood  = new Category { Name = "Non-food / Toilet", SortOrder = 70 };
        var rengøring = new Category { Name = "Rengøring", SortOrder = 80 };
        db.Categories.AddRange(grønt, mejeri, kød, kolonial, frost, brød, nonfood, rengøring);

        // --- Ingredienser ---
        Ingredient Ing(string name, Category? cat) =>
            new() { Name = name, NormalizedName = Ingredient.Normalize(name), Category = cat };

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

        // --- Ret 1: Spaghetti med kødsovs (4 personer) ---
        var spagBolognese = new Recipe
        {
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
