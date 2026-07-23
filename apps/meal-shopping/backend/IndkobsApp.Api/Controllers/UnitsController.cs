using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Forslag til enheds-vælgeren. Enheder er fri tekst, men vi hjælper brugeren ved at foreslå
/// dels standard-sættet (<see cref="Units.Suggestions"/>), dels de enheder husstanden ALLEREDE
/// har brugt (afledt af eksisterende data — ingen ny tabel). Brugeren kan altid skrive en ny.
/// </summary>
[ApiController]
[Route("api/units")]
[Authorize]
public class UnitsController : ControllerBase
{
    private readonly AppDbContext _db;
    public UnitsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IEnumerable<string>> Get()
    {
        var hid = User.GetHouseholdId();

        // Alle enheder husstanden har brugt i retter, varegrupper og løse varer.
        var used = await _db.RecipeIngredients.Where(ri => ri.Recipe.HouseholdId == hid).Select(ri => ri.Unit)
            .Concat(_db.ItemGroupIngredients.Where(gi => gi.ItemGroup.HouseholdId == hid).Select(gi => gi.Unit))
            .Concat(_db.WeekManualItems.Where(m => m.Week.HouseholdId == hid).Select(m => m.Unit))
            .Distinct()
            .ToListAsync();

        // Standard-forslag + brugte enheder, deduplikeret case-insensitivt (bevar første skrivemåde).
        return Units.Suggestions.Concat(used)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .GroupBy(u => u.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
