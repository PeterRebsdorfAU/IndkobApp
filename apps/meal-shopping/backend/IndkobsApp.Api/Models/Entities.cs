namespace IndkobsApp.Api.Models;

/// <summary>
/// En husstand = én konto med ét delt login. Alle opskrifter, varegrupper og uger
/// tilhører en husstand, så husstande ikke kan se hinandens data.
/// </summary>
public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Login (unikt, gemmes normaliseret i lowercase).</summary>
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Butikskategori brugt til at gruppere/sortere indkøbslisten.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public List<Ingredient> Ingredients { get; set; } = new();
}

/// <summary>
/// Normaliseret master-ingrediens. <see cref="NormalizedName"/> er trimmet +
/// lowercased og har et unikt index, så "løg", "Løg" og " Løg " peger på samme række.
/// </summary>
public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public static string Normalize(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>En opskrift/ret med basis-portioner og ingredienslinjer.</summary>
public class Recipe
{
    public int Id { get; set; }
    public int HouseholdId { get; set; } // ejer-husstand
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int Servings { get; set; } = 4; // basis-portioner

    public List<RecipeIngredient> Ingredients { get; set; } = new();
}

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

/// <summary>Varegruppe/skabelon der ikke er en ret (fx Frokost, Toilet, Rengøring).</summary>
public class ItemGroup
{
    public int Id { get; set; }
    public int HouseholdId { get; set; } // ejer-husstand
    public string Name { get; set; } = string.Empty;

    public List<ItemGroupIngredient> Ingredients { get; set; } = new();
}

public class ItemGroupIngredient
{
    public int Id { get; set; }
    public int ItemGroupId { get; set; }
    public ItemGroup ItemGroup { get; set; } = null!;

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

/// <summary>En planlagt uge (år + ugenummer, unikt).</summary>
public class Week
{
    public int Id { get; set; }
    public int HouseholdId { get; set; } // ejer-husstand
    public int Year { get; set; }
    public int WeekNumber { get; set; }

    public List<WeekRecipe> Recipes { get; set; } = new();
    public List<WeekItemGroup> ItemGroups { get; set; } = new();
    public List<WeekManualItem> ManualItems { get; set; } = new();
    public List<ShoppingListCheck> Checks { get; set; } = new();
}

/// <summary>En ret tilføjet til en uge. Servings kan overstyre rettens basis (portion-skalering).</summary>
public class WeekRecipe
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public Week Week { get; set; } = null!;

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    /// <summary>Ønsket antal portioner i ugen. Null = brug rettens basis-portioner.</summary>
    public int? Servings { get; set; }

    /// <summary>Valgfri ugedag (0=mandag ... 6=søndag). Null = ikke planlagt til en bestemt dag.</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>
    /// Sat når retten er markeret "lavet" — ingredienserne blev da trukket fra
    /// køkkenlageret. Null = ikke lavet endnu.
    /// </summary>
    public DateTime? CookedUtc { get; set; }
}

public class WeekItemGroup
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public Week Week { get; set; } = null!;

    public int ItemGroupId { get; set; }
    public ItemGroup ItemGroup { get; set; } = null!;
}

/// <summary>Løs vare tilføjet direkte til en uges indkøbsliste (engangsting).</summary>
public class WeekManualItem
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public Week Week { get; set; } = null!;

    /// <summary>Knyt evt. til en master-ingrediens (så den aggregeres med resten).</summary>
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Fritekst-navn, hvis varen ikke skal være en master-ingrediens.</summary>
    public string? FreeText { get; set; }

    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

/// <summary>
/// Afkrydsningstilstand pr. aggregeret linje i en uges indkøbsliste.
/// <see cref="LineKey"/> er en stabil nøgle som aggregeringen genererer,
/// så afkrydsning huskes selvom listen genberegnes.
/// </summary>
public class ShoppingListCheck
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public Week Week { get; set; } = null!;

    public string LineKey { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
}

/// <summary>
/// Inspirations-opskrift i det fælles katalog (IKKE husstands-scoped).
/// Ingredienser gemmes som navne (ikke Ingredient-FK), så kataloget ikke
/// forurener master-ingredienslisten — mapping sker først ved "adoption".
/// </summary>
public class CatalogRecipe
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int Servings { get; set; } = 4;
    /// <summary>Komma-separerede tags til filtrering (fx "hurtig,vegetar").</summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Sat hvis opskriften er PUBLICERET af en husstand (community-deling) — null for
    /// kuraterede opskrifter. Publicering er et snapshot: kilden kan gen-publicere for
    /// at opdatere. Slettes kilde-opskriften/husstanden, fjernes katalog-kopien.
    /// </summary>
    public int? SourceHouseholdId { get; set; }
    public int? SourceRecipeId { get; set; }

    public List<CatalogRecipeIngredient> Ingredients { get; set; } = new();
}

public class CatalogRecipeIngredient
{
    public int Id { get; set; }
    public int CatalogRecipeId { get; set; }
    public CatalogRecipe CatalogRecipe { get; set; } = null!;

    public string Name { get; set; } = string.Empty; // ingrediens-navn (mappes ved adoption)
    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

/// <summary>
/// En vare husstanden har hjemme (køkkenlager). Bruges til at trække "haves"
/// fra indkøbslisten, så man kun køber det man mangler.
/// </summary>
public class PantryItem
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }

    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

/// <summary>
/// Husstandens opgaver — én motor for tre ting:
///  - Engangsopgaver ("ring til tandlægen"): IntervalDays = null, afkrydses med IsDone.
///  - Gentagne pligter ("støvsug hver uge"): IntervalDays sat; "gjort" ruller NextDueDate frem.
///  - Vedligehold ("afkalk hver 6. uge"): samme som pligter, bare længere interval.
/// Valgfri tur-rotation: Assignees = komma-separerede navne; AssigneeIndex peger på
/// hvis tur det er, og rykker videre ved hver "gjort".
/// </summary>
public class HouseholdTask
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Null = engangsopgave. Ellers antal dage mellem gentagelser.</summary>
    public int? IntervalDays { get; set; }

    /// <summary>Næste forfaldsdato (kun gentagne). Forfalden når &lt;= i dag.</summary>
    public DateOnly? NextDueDate { get; set; }

    /// <summary>Komma-separerede navne til tur-rotation (fx "Peter,Clara"). Null = ingen.</summary>
    public string? Assignees { get; set; }
    public int AssigneeIndex { get; set; }

    /// <summary>Kun engangsopgaver: afkrydset/færdig.</summary>
    public bool IsDone { get; set; }

    public DateTime? LastCompletedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Delings-token for en uges indkøbsliste: giver læse-/afkrydsningsadgang
/// via link UDEN login (fx til den der handler).
/// </summary>
public class WeekShareToken
{
    public int Id { get; set; }
    public int WeekId { get; set; }
    public Week Week { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
