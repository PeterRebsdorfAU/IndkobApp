using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize] // kræver login; alle handlinger scopes til den aktuelle husstand
public class RecipesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    public RecipesController(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    [HttpGet]
    public async Task<IEnumerable<RecipeDto>> GetAll()
    {
        var hid = User.GetHouseholdId();
        var recipes = await _db.Recipes
            .Where(r => r.HouseholdId == hid)
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category)
            .OrderBy(r => r.Name)
            .ToListAsync();
        // Hvilke af husstandens opskrifter er publiceret til inspirationssiden?
        var publicIds = await _db.CatalogRecipes
            .Where(c => c.SourceHouseholdId == hid && c.SourceRecipeId != null)
            .Select(c => c.SourceRecipeId!.Value)
            .ToHashSetAsync();
        return recipes.Select(r => Map(r, publicIds.Contains(r.Id)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Get(int id)
    {
        var hid = User.GetHouseholdId();
        var r = await _db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == hid);
        if (r == null) return NotFound();
        var isPublic = await _db.CatalogRecipes.AnyAsync(c => c.SourceRecipeId == id);
        return Map(r, isPublic);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(RecipeUpsertDto dto)
    {
        var r = new Recipe
        {
            HouseholdId = User.GetHouseholdId(),
            Name = dto.Name.Trim(),
            Note = dto.Note,
            Servings = dto.Servings
        };
        await ApplyLines(r, dto.Ingredients);
        _db.Recipes.Add(r);
        await _db.SaveChangesAsync();
        return await Get(r.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Update(int id, RecipeUpsertDto dto)
    {
        var hid = User.GetHouseholdId();
        var r = await _db.Recipes.Include(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == hid);
        if (r == null) return NotFound();

        r.Name = dto.Name.Trim();
        r.Note = dto.Note;
        r.Servings = dto.Servings;

        // Enkleste robuste strategi: ryd linjer og byg dem op igen fra input.
        _db.RecipeIngredients.RemoveRange(r.Ingredients);
        r.Ingredients.Clear();
        await ApplyLines(r, dto.Ingredients);

        await _db.SaveChangesAsync();
        return await Get(r.Id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var hid = User.GetHouseholdId();
        var r = await _db.Recipes.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == hid);
        if (r == null) return NotFound();
        _db.Recipes.Remove(r);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Publicér til fælles inspirationsside (community-deling) ----------
    /// <summary>
    /// Gør opskriften offentlig: lægger et SNAPSHOT ind i inspirations-kataloget,
    /// synligt for alle husstande. Gen-publicering opdaterer snapshottet.
    /// </summary>
    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        var hid = User.GetHouseholdId();
        var r = await _db.Recipes
            .Include(x => x.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == hid);
        if (r == null) return NotFound();

        var existing = await _db.CatalogRecipes
            .Include(c => c.Ingredients)
            .FirstOrDefaultAsync(c => c.SourceRecipeId == id);

        if (existing == null)
        {
            existing = new CatalogRecipe { SourceHouseholdId = hid, SourceRecipeId = id };
            _db.CatalogRecipes.Add(existing);
        }
        else
        {
            _db.CatalogRecipeIngredients.RemoveRange(existing.Ingredients);
            existing.Ingredients.Clear();
        }

        existing.Title = r.Name;
        existing.Note = r.Note;
        existing.Servings = r.Servings;
        foreach (var ri in r.Ingredients)
        {
            existing.Ingredients.Add(new CatalogRecipeIngredient
            {
                Name = ri.Ingredient.Name,
                Quantity = ri.Quantity,
                Unit = ri.Unit
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Fjern opskriften fra inspirationssiden igen (privat).</summary>
    [HttpDelete("{id:int}/publish")]
    public async Task<IActionResult> Unpublish(int id)
    {
        var hid = User.GetHouseholdId();
        // Kun ens egen publicering kan fjernes.
        var entry = await _db.CatalogRecipes
            .FirstOrDefaultAsync(c => c.SourceRecipeId == id && c.SourceHouseholdId == hid);
        if (entry == null) return NotFound();
        _db.CatalogRecipes.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Oversætter input-linjer til RecipeIngredient og sikrer normaliserede ingredienser
    // i husstandens egen varebank.
    private async Task ApplyLines(Recipe r, List<IngredientLineInputDto> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.IngredientName)) continue;
            var ing = await _ingredients.GetOrCreateAsync(r.HouseholdId, line.IngredientName);
            r.Ingredients.Add(new RecipeIngredient
            {
                Ingredient = ing,
                Quantity = line.Quantity,
                Unit = line.Unit
            });
        }
    }

    private static RecipeDto Map(Recipe r, bool isPublic = false) => new(
        r.Id, r.Name, r.Note, r.Servings,
        r.Ingredients.Select(ri => new IngredientLineDto(
            ri.Id, ri.IngredientId, ri.Ingredient.Name, ri.Ingredient.Category?.Name, ri.Quantity, ri.Unit))
            .OrderBy(l => l.IngredientName, StringComparer.OrdinalIgnoreCase).ToList(),
        isPublic);
}
