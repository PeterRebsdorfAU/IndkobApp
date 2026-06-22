using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/recipes")]
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
        var recipes = await _db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category)
            .OrderBy(r => r.Name)
            .ToListAsync();
        return recipes.Select(Map);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Get(int id)
    {
        var r = await _db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category)
            .FirstOrDefaultAsync(r => r.Id == id);
        return r == null ? NotFound() : Map(r);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(RecipeUpsertDto dto)
    {
        var r = new Recipe { Name = dto.Name.Trim(), Note = dto.Note, Servings = dto.Servings };
        await ApplyLines(r, dto.Ingredients);
        _db.Recipes.Add(r);
        await _db.SaveChangesAsync();
        return await Get(r.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Update(int id, RecipeUpsertDto dto)
    {
        var r = await _db.Recipes.Include(x => x.Ingredients).FirstOrDefaultAsync(x => x.Id == id);
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
        var r = await _db.Recipes.FindAsync(id);
        if (r == null) return NotFound();
        _db.Recipes.Remove(r);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Oversætter input-linjer til RecipeIngredient og sikrer normaliserede ingredienser.
    private async Task ApplyLines(Recipe r, List<IngredientLineInputDto> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.IngredientName)) continue;
            var ing = await _ingredients.GetOrCreateAsync(line.IngredientName);
            r.Ingredients.Add(new RecipeIngredient
            {
                Ingredient = ing,
                Quantity = line.Quantity,
                Unit = line.Unit
            });
        }
    }

    private static RecipeDto Map(Recipe r) => new(
        r.Id, r.Name, r.Note, r.Servings,
        r.Ingredients.Select(ri => new IngredientLineDto(
            ri.Id, ri.IngredientId, ri.Ingredient.Name, ri.Ingredient.Category?.Name, ri.Quantity, ri.Unit))
            .OrderBy(l => l.IngredientName, StringComparer.OrdinalIgnoreCase).ToList());
}
