using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Inspirations-kataloget: fælles opskrifter man kan bladre i og "adoptere" —
/// dvs. kopiere til husstandens egne opskrifter og evt. lægge på en uge med det samme.
/// </summary>
[ApiController]
[Route("api/catalog")]
[Authorize]
public class CatalogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RecipeAdoptionService _adoption;
    public CatalogController(AppDbContext db, RecipeAdoptionService adoption)
    {
        _db = db;
        _adoption = adoption;
    }

    [HttpGet("recipes")]
    public async Task<IEnumerable<CatalogRecipeDto>> GetAll()
    {
        // Projektér, så det (potentielt store) billede-bytea IKKE hentes ved listning af hele
        // det fælles katalog — kun et HasImage-flag. Billedet serveres separat pr. opskrift.
        var rows = await _db.CatalogRecipes
            .OrderBy(r => r.Title)
            .Select(r => new
            {
                r.Id, r.Title, r.Note, r.Servings, r.Tags, r.Method, r.SourceHouseholdId,
                HasImage = r.Image != null,
                Ingredients = r.Ingredients.Select(i => new CatalogLineDto(i.Name, i.Quantity, i.Unit)).ToList()
            })
            .ToListAsync();

        // Navne på husstande der har delt opskrifter (vises som "delt af X").
        var householdIds = rows.Where(r => r.SourceHouseholdId != null)
            .Select(r => r.SourceHouseholdId!.Value).Distinct().ToList();
        var names = await _db.Households
            .Where(h => householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Name);

        return rows.Select(r => new CatalogRecipeDto(
            r.Id, r.Title, r.Note, r.Servings,
            (r.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            r.Ingredients.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            r.Method,
            r.SourceHouseholdId is int shid ? names.GetValueOrDefault(shid) : null,
            r.HasImage));
    }

    /// <summary>Serverer katalog-opskriftens billede (fælles/offentligt indhold).</summary>
    [HttpGet("recipes/{id:int}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var row = await _db.CatalogRecipes
            .Where(r => r.Id == id)
            .Select(r => new { r.Image, r.ImageContentType })
            .FirstOrDefaultAsync();
        if (row?.Image == null) return NotFound();
        // Kataloget er fælles for alle husstande → offentlig cache er fint.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(row.Image, row.ImageContentType ?? "application/octet-stream");
    }

    /// <summary>
    /// Adoptér en katalog-opskrift: kopiér den til husstandens egne opskrifter
    /// (ingredienser mappes via den normaliserede master-liste) og læg den evt.
    /// på en uge, så den straks bidrager til indkøbslisten.
    /// </summary>
    [HttpPost("recipes/{id:int}/adopt")]
    public async Task<ActionResult<AdoptResultDto>> Adopt(int id, AdoptCatalogRecipeDto dto)
    {
        var hid = User.GetHouseholdId();

        var cat = await _db.CatalogRecipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (cat == null) return NotFound();

        // Kopiér til husstandens egne opskrifter (genbrug hvis samme navn allerede findes,
        // så gentagne "Tilføj" ikke giver dubletter). Fælles adoptions-logik.
        var recipe = await _adoption.AdoptAsync(
            hid, cat.Title, cat.Note, cat.Servings,
            cat.Method, cat.Image, cat.ImageContentType,
            cat.Ingredients.Select(l => new RecipeAdoptionService.AdoptLine(l.Name, l.Quantity, l.Unit)).ToList());

        // Læg evt. på en uge med det samme (kun husstandens egen uge).
        int? weekId = null;
        if (dto.WeekId is int wid)
        {
            var ownsWeek = await _db.Weeks.AnyAsync(w => w.Id == wid && w.HouseholdId == hid);
            if (!ownsWeek) return BadRequest("Ukendt uge.");

            _db.WeekRecipes.Add(new WeekRecipe
            {
                WeekId = wid,
                RecipeId = recipe.Id,
                Servings = dto.Servings,
                DayOfWeek = dto.DayOfWeek
            });
            await _db.SaveChangesAsync();
            weekId = wid;
        }

        return new AdoptResultDto(recipe.Id, recipe.Name, weekId);
    }
}
