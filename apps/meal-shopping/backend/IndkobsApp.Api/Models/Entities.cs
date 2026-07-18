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

/// <summary>
/// Butikskategori brugt til at gruppere/sortere indkøbslisten.
/// PRIVAT pr. husstand — hver husstand har sin egen butiksrækkefølge.
/// </summary>
public class Category
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public List<Ingredient> Ingredients { get; set; } = new();
}

/// <summary>
/// Normaliseret ingrediens. <see cref="NormalizedName"/> er trimmet + lowercased og
/// unik PR. HUSSTAND, så "løg"/"Løg" peger på samme række inden for husstanden —
/// men husstande deler ikke varebank (privat butiksopsætning).
/// </summary>
public class Ingredient
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
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

    /// <summary>Valgfri fremgangsmåde (fritekst, evt. flere linjer). Null = ingen angivet.</summary>
    public string? Method { get; set; }

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

    /// <summary>Valgfri fremgangsmåde (fritekst, evt. flere linjer). Null = ingen angivet.</summary>
    public string? Method { get; set; }

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

/// <summary>Status-flow for en ordre sendt til en butik.</summary>
public enum OrderStatus { Modtaget, Pakkes, Klar, Afhentet, Annulleret }

/// <summary>
/// En indkøbsordre en husstand sender til en butik (demo af butiks-flowet):
/// butikken pakker linjerne og markerer ordren klar; husstanden ser status.
/// Linjerne er et SNAPSHOT af indkøbslisten på afsendelsestidspunktet.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public string HouseholdName { get; set; } = string.Empty; // vises for butikken
    public string StoreName { get; set; } = string.Empty;     // valgt butik
    public OrderStatus Status { get; set; } = OrderStatus.Modtaget;
    public string? Note { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadyUtc { get; set; }

    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
    public string? CategoryName { get; set; } // så butikken kan pakke i rækkefølge
    public bool IsPacked { get; set; }
    public bool NotAvailable { get; set; }
}
