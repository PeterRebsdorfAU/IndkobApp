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
    private readonly IngredientService _ingredients;
    public CatalogController(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    [HttpGet("recipes")]
    public async Task<IEnumerable<CatalogRecipeDto>> GetAll()
    {
        var recipes = await _db.CatalogRecipes
            .Include(r => r.Ingredients)
            .OrderBy(r => r.Title)
            .ToListAsync();

        // Navne på husstande der har delt opskrifter (vises som "delt af X").
        var householdIds = recipes.Where(r => r.SourceHouseholdId != null)
            .Select(r => r.SourceHouseholdId!.Value).Distinct().ToList();
        var names = await _db.Households
            .Where(h => householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.Name);

        return recipes.Select(r => new CatalogRecipeDto(
            r.Id, r.Title, r.Note, r.Servings,
            (r.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            r.Ingredients.Select(i => new CatalogLineDto(i.Name, i.Quantity, i.Unit))
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            r.SourceHouseholdId is int shid ? names.GetValueOrDefault(shid) : null));
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
        // så gentagne "Tilføj" ikke giver dubletter).
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(r => r.HouseholdId == hid && r.Name == cat.Title);
        if (recipe == null)
        {
            recipe = new Recipe
            {
                HouseholdId = hid,
                Name = cat.Title,
                Note = cat.Note,
                Servings = cat.Servings
            };
            foreach (var line in cat.Ingredients)
            {
                var ing = await _ingredients.GetOrCreateAsync(line.Name);
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Ingredient = ing,
                    Quantity = line.Quantity,
                    Unit = line.Unit
                });
            }
            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();
        }

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
