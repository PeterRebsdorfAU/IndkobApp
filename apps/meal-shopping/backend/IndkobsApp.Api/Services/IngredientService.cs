using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Sørger for at ingredienser er normaliserede og uden dubletter — PR. HUSSTAND
/// (hver husstand har sin egen varebank og butiksopsætning).
/// </summary>
public class IngredientService
{
    private readonly AppDbContext _db;
    public IngredientService(AppDbContext db) => _db = db;

    /// <summary>
    /// Finder husstandens eksisterende ingrediens ud fra (trimmet, lowercased) navn,
    /// ellers oprettes en ny i husstandens varebank. Returnerer den sporede entitet.
    /// Kalderen er ansvarlig for SaveChanges.
    /// </summary>
    public async Task<Ingredient> GetOrCreateAsync(int householdId, string name, int? categoryId = null)
    {
        var normalized = Ingredient.Normalize(name);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Ingrediensnavn må ikke være tomt.");

        // Tjek både allerede-sporede (ikke gemte) og databasen, så vi ikke laver dubletter
        // når flere linjer i samme request peger på samme nye ingrediens.
        var local = _db.Ingredients.Local
            .FirstOrDefault(i => i.HouseholdId == householdId && i.NormalizedName == normalized);
        if (local != null) return local;

        var existing = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.HouseholdId == householdId && i.NormalizedName == normalized);
        if (existing != null) return existing;

        var created = new Ingredient
        {
            HouseholdId = householdId,
            Name = name.Trim(),
            NormalizedName = normalized,
            CategoryId = categoryId
        };
        _db.Ingredients.Add(created);
        return created;
    }
}
