using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
public class IngredientsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    public IngredientsController(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    [HttpGet]
    public async Task<IEnumerable<IngredientDto>> GetAll() =>
        await _db.Ingredients.Include(i => i.Category).OrderBy(i => i.Name)
            .Select(i => new IngredientDto(i.Id, i.Name, i.CategoryId, i.Category != null ? i.Category.Name : null))
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<IngredientDto>> Create(IngredientUpsertDto dto)
    {
        // Genbruger eksisterende ingrediens hvis navnet (normaliseret) allerede findes.
        var ing = await _ingredients.GetOrCreateAsync(dto.Name, dto.CategoryId);
        if (dto.CategoryId.HasValue) ing.CategoryId = dto.CategoryId; // opdater kategori hvis angivet
        await _db.SaveChangesAsync();
        await _db.Entry(ing).Reference(i => i.Category).LoadAsync();
        return Ok(new IngredientDto(ing.Id, ing.Name, ing.CategoryId, ing.Category?.Name));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, IngredientUpsertDto dto)
    {
        var ing = await _db.Ingredients.FindAsync(id);
        if (ing == null) return NotFound();

        var normalized = Ingredient.Normalize(dto.Name);
        // Hvis navnet ændres, sørg for at det ikke kolliderer med en anden ingrediens.
        if (normalized != ing.NormalizedName &&
            await _db.Ingredients.AnyAsync(i => i.NormalizedName == normalized && i.Id != id))
            return Conflict($"Ingrediensen '{dto.Name.Trim()}' findes allerede.");

        ing.Name = dto.Name.Trim();
        ing.NormalizedName = normalized;
        ing.CategoryId = dto.CategoryId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ing = await _db.Ingredients.FindAsync(id);
        if (ing == null) return NotFound();

        // Bloker sletning hvis ingrediensen er i brug (FK Restrict ville ellers fejle).
        var inUse = await _db.RecipeIngredients.AnyAsync(r => r.IngredientId == id)
                 || await _db.ItemGroupIngredients.AnyAsync(g => g.IngredientId == id)
                 || await _db.WeekManualItems.AnyAsync(m => m.IngredientId == id);
        if (inUse) return Conflict("Ingrediensen bruges i en ret, varegruppe eller løs vare og kan ikke slettes.");

        _db.Ingredients.Remove(ing);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
