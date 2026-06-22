namespace IndkobsApp.Api.Models;

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
