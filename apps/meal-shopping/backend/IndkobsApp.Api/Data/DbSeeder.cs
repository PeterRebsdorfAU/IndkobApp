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
    /// Seeder/opgraderer inspirations-kataloget (fælles, ikke husstands-data).
    /// ADDITIV PR. TITEL: tilføjer kun kuraterede opskrifter hvis titlen ikke allerede
    /// findes (idempotent, ingen dubletter), og backfiller <see cref="CatalogRecipe.Method"/>
    /// på ældre kuraterede seed-opskrifter der mangler den. Rører ALDRIG community-publicerede
    /// opskrifter (SourceHouseholdId != null) eller husstandsdata.
    /// </summary>
    public static async Task SeedCatalogAsync(AppDbContext db)
    {
        var seed = BuildCatalogSeed();

        // Kun kuraterede opslag (uden kilde-husstand) må matches/opdateres pr. titel.
        var existing = await db.CatalogRecipes
            .Where(c => c.SourceHouseholdId == null)
            .ToListAsync();
        var byTitle = new Dictionary<string, CatalogRecipe>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existing) byTitle.TryAdd(c.Title, c);

        var changed = false;
        foreach (var def in seed)
        {
            if (byTitle.TryGetValue(def.Title, out var current))
            {
                // Opgradér ældre seed: tilføj fremgangsmåde hvis den mangler (bevarer alt andet).
                if (string.IsNullOrWhiteSpace(current.Method) && !string.IsNullOrWhiteSpace(def.Method))
                {
                    current.Method = def.Method;
                    changed = true;
                }
            }
            else
            {
                db.CatalogRecipes.Add(def);
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    /// <summary>
    /// Bygger det kuraterede start-katalog. Hver opskrift har en fornuftig fremgangsmåde
    /// (<see cref="CatalogRecipe.Method"/>) + realistiske ingredienser/enheder, så det nye
    /// felt vises frem på inspirationssiden.
    /// </summary>
    private static List<CatalogRecipe> BuildCatalogSeed()
    {
        static CatalogRecipeIngredient L(string name, decimal qty, Unit unit) =>
            new() { Name = name, Quantity = qty, Unit = unit };

        return new List<CatalogRecipe>
        {
            new CatalogRecipe
            {
                Title = "Butter chicken", Servings = 4, Tags = "aftensmad,indisk",
                Note = "Cremet indisk klassiker — servér med ris.",
                Method = "Skær kyllingen i mundrette stykker og brun den i smør. Tilsæt hakket løg og hvidløg og svits det klart. Rør garam masala i, hæld hakkede tomater og fløde ved, og lad det simre 15-20 min. Smag til med salt og servér med kogte ris.",
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
                Method = "Svits hakket løg og hvidløg bløde i lidt olie. Tilsæt karry og de skyllede linser og rør rundt. Hæld hakkede tomater og kokosmælk ved og lad det simre 20-25 min til linserne er møre. Smag til med salt og servér med ris.",
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
                Method = "Brun det hakkede oksekød på en pande. Drys tacokrydderi over, tilsæt en skvæt vand og lad det simre et par minutter. Snit salat og skær tomat i tern. Servér kød, grønt, revet ost og creme fraiche i tortillas — så bygger man selv.",
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
                Method = "Tænd ovnen på 200°C. Skær kartoflerne i både, vend dem i olie og salt og bag ca. 30 min. Læg laksen i et fad med smør, citronskiver og salt de sidste 15 min. Kog eller damp broccoli og servér ved siden af.",
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
                Method = "Skær kylling og løg i mundrette stykker og brun dem i lidt fedtstof. Riv æblet i og drys karry over. Hæld fløde ved og lad retten simre 10-15 min. Smag til med salt og servér med ris.",
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
                Method = "Kog spaghetti al dente. Steg bacon sprødt på en pande. Pisk æg og revet parmesan sammen med rigeligt peber. Vend den varme, afdryppede pasta med bacon, tag panden af varmen og rør æggeblandingen i, så den bliver cremet (ikke røræg). Spæd evt. med lidt pastavand.",
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
                Method = "Skær gulerod, kartoffel, porre og løg i tern. Svits grøntsagerne kort i en gryde. Hæld bouillon på og kog til grøntsagerne er møre, ca. 20 min. Blend evt. suppen glat og smag til. Servér med rugbrød.",
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
                Method = "Rør hakket svinekød med æg, revet løg, havregryn, mælk og salt, og lad farsen hvile 10 min. Kog kartoflerne og lad dem køle af. Steg frikadellerne gyldne i smør på panden. Vend de skårne kartofler med creme fraiche og purløg og servér til.",
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
                Method = "Svits løg, peberfrugt og hvidløg bløde i olie. Tilsæt spidskommen og hakkede tomater og lad saucen simre 10 min. Lav fordybninger og slå æggene ud i saucen; læg låg på til hviderne er stivnet. Drys feta over og servér med brød.",
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
                Method = "Kog nudlerne efter anvisning og hæld dem fra. Steg kyllingen i strimler ved høj varme. Tilsæt revet gulerod, snittet spidskål, hvidløg og revet ingefær og svits det kort. Vend nudler og soyasauce i og steg det hele godt varmt igennem.",
                Ingredients =
                {
                    L("Nudler", 250, Unit.G), L("Kyllingebryst", 400, Unit.G),
                    L("Gulerod", 200, Unit.G), L("Spidskål", 0.5m, Unit.Stk),
                    L("Soyasauce", 4, Unit.Spsk), L("Hvidløg", 2, Unit.Fed),
                    L("Ingefær", 1, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Boller i karry", Servings = 4, Tags = "aftensmad,dansk,klassiker",
                Note = "Mild dansk klassiker med ris.",
                Method = "Rør en fars af hakket kød med æg, revet løg, mel og salt. Form små boller og kog dem 8-10 min i letsaltet vand; tag dem op. Lav en karrysovs af smør, mel og karry, spæd med kogevand og mælk til passende tykkelse. Vend bollerne i sovsen og servér med ris.",
                Ingredients =
                {
                    L("Hakket kalv og flæsk", 500, Unit.G), L("Æg", 1, Unit.Stk),
                    L("Løg", 1, Unit.Stk), L("Hvedemel", 100, Unit.G),
                    L("Karry", 2, Unit.Spsk), L("Smør", 40, Unit.G),
                    L("Mælk", 300, Unit.Ml), L("Ris", 300, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Lasagne", Servings = 4, Tags = "aftensmad,italiensk,familie",
                Note = "Klassisk lasagne med kødsovs og hvid sovs.",
                Method = "Brun hakket oksekød med løg og hvidløg. Tilsæt hakkede tomater og lad kødsovsen simre 20 min; smag til. Rør en hvid sovs af smør, mel og mælk. Lag lasagneplader, kødsovs og hvid sovs skiftevis i et fad, slut med revet ost og bag ved 200°C i ca. 35 min.",
                Ingredients =
                {
                    L("Hakket oksekød", 500, Unit.G), L("Løg", 1, Unit.Stk),
                    L("Hvidløg", 2, Unit.Fed), L("Hakkede tomater", 2, Unit.Daase),
                    L("Lasagneplader", 1, Unit.Pakke), L("Smør", 40, Unit.G),
                    L("Hvedemel", 40, Unit.G), L("Mælk", 500, Unit.Ml),
                    L("Revet ost", 150, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Chili con carne", Servings = 4, Tags = "aftensmad,mexicansk,billig",
                Method = "Brun hakket oksekød med løg og hvidløg. Tilsæt hakkede tomater, kidneybønner, majs og chilikrydderi. Lad gryden simre mindst 20 min og smag til med salt. Servér med ris eller brød.",
                Ingredients =
                {
                    L("Hakket oksekød", 500, Unit.G), L("Løg", 1, Unit.Stk),
                    L("Hvidløg", 2, Unit.Fed), L("Hakkede tomater", 2, Unit.Daase),
                    L("Kidneybønner", 1, Unit.Daase), L("Majs", 1, Unit.Daase),
                    L("Chilikrydderi", 1, Unit.Spsk), L("Ris", 300, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Æggekage", Servings = 4, Tags = "aftensmad,vegetar,hurtig",
                Note = "Nem æggekage med bacon.",
                Method = "Pisk æg med mælk, salt og peber. Steg bacon sprødt på en pande. Hæld æggemassen ved og lad den stivne ved svag varme under låg. Pynt med purløg og tomat og servér med rugbrød.",
                Ingredients =
                {
                    L("Æg", 8, Unit.Stk), L("Mælk", 100, Unit.Ml),
                    L("Bacon", 150, Unit.G), L("Purløg", 1, Unit.Bundt),
                    L("Tomat", 2, Unit.Stk), L("Rugbrød", 4, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Millionbøf med kartoffelmos", Servings = 4, Tags = "aftensmad,dansk,billig",
                Method = "Brun hakket oksekød med hakket løg. Drys mel over, rør rundt og spæd med bouillon til en sovs; lad det simre 10 min. Kog kartofler møre og mos dem med smør og mælk. Servér bøffen over mosen.",
                Ingredients =
                {
                    L("Hakket oksekød", 500, Unit.G), L("Løg", 1, Unit.Stk),
                    L("Hvedemel", 2, Unit.Spsk), L("Oksebouillon", 0.5m, Unit.L),
                    L("Kartofler", 800, Unit.G), L("Smør", 30, Unit.G),
                    L("Mælk", 100, Unit.Ml)
                }
            },
            new CatalogRecipe
            {
                Title = "Fiskefrikadeller med remoulade", Servings = 4, Tags = "aftensmad,fisk,dansk",
                Method = "Blend fisken til en fars med æg, mel, revet løg og salt. Form frikadeller og steg dem gyldne i smør på panden. Servér med remoulade, citron og rugbrød eller kogte kartofler.",
                Ingredients =
                {
                    L("Hvidfiskefilet", 500, Unit.G), L("Æg", 1, Unit.Stk),
                    L("Hvedemel", 2, Unit.Spsk), L("Løg", 1, Unit.Stk),
                    L("Smør", 40, Unit.G), L("Remoulade", 1, Unit.Stk),
                    L("Citron", 1, Unit.Stk), L("Rugbrød", 4, Unit.Stk)
                }
            },
            new CatalogRecipe
            {
                Title = "Biksemad", Servings = 4, Tags = "aftensmad,dansk,rester",
                Note = "God måde at bruge rester af kød og kartofler.",
                Method = "Skær kogte kartofler, kød og løg i små tern. Steg kartoflerne sprøde, tilsæt løg og kød og varm det godt igennem. Smag til med salt og peber. Servér med spejlæg og rødbeder.",
                Ingredients =
                {
                    L("Kartofler", 800, Unit.G), L("Løg", 2, Unit.Stk),
                    L("Kogt kød", 400, Unit.G), L("Æg", 4, Unit.Stk),
                    L("Rødbeder", 1, Unit.Daase), L("Smør", 30, Unit.G)
                }
            },
            new CatalogRecipe
            {
                Title = "Hjemmelavet pizza", Servings = 4, Tags = "aftensmad,italiensk,familie",
                Method = "Ælt en dej af mel, gær, lunkent vand, olie og salt og lad den hæve ca. en time. Rul dejen ud, smør tomatsauce på og fordel revet ost og fyld. Bag ved 250°C i 10-12 min til bunden er sprød.",
                Ingredients =
                {
                    L("Hvedemel", 500, Unit.G), L("Gær", 25, Unit.G),
                    L("Olivenolie", 2, Unit.Spsk), L("Tomatsauce", 1, Unit.Daase),
                    L("Revet ost", 200, Unit.G), L("Skinke", 100, Unit.G)
                }
            }
        };
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
