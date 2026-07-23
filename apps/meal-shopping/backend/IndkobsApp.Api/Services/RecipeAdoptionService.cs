using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Fælles "adopter"-logik: kopiér en opskrift ind i en husstands egne opskrifter.
/// Bruges både af inspirations-kataloget (publicér til ALLE) og af selektiv deling
/// (delt med én udvalgt modtager) — så adoptions-adfærden kun findes ét sted:
///  - Genbrug samme opskrift hvis husstanden allerede har en med samme navn (ingen dubletter ved gentagen "Tilføj").
///  - Ingredienser mappes/oprettes i modtagerens EGEN normaliserede varebank.
///  - Fremgangsmåde (Method) og billede kopieres med over.
/// Kalderen står selv for evt. at lægge den adopterede opskrift på en uge.
/// </summary>
public class RecipeAdoptionService
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    public RecipeAdoptionService(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    /// <summary>En enkelt ingredienslinje i kilde-opskriften (navn + mængde + enhed).</summary>
    public readonly record struct AdoptLine(string Name, decimal Quantity, string Unit);

    /// <summary>
    /// Adopterer en opskrift ind i <paramref name="householdId"/>'s egne opskrifter og returnerer
    /// den (eksisterende eller nye) opskrift. Idempotent pr. navn inden for husstanden.
    /// </summary>
    public async Task<Recipe> AdoptAsync(
        int householdId, string title, string? note, int servings,
        string? method, byte[]? image, string? imageContentType,
        IReadOnlyCollection<AdoptLine> lines)
    {
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.Name == title);
        if (recipe != null) return recipe; // allerede adopteret — undgå dubletter

        recipe = new Recipe
        {
            HouseholdId = householdId,
            Name = title,
            Note = note,
            Servings = servings,
            Method = method,           // fremgangsmåde følger med
            Image = image,             // billedet kopieres med
            ImageContentType = imageContentType
        };
        foreach (var line in lines)
        {
            var ing = await _ingredients.GetOrCreateAsync(householdId, line.Name);
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Ingredient = ing,
                Quantity = line.Quantity,
                Unit = line.Unit
            });
        }
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        return recipe;
    }
}
