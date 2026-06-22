using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/item-groups")]
public class ItemGroupsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    public ItemGroupsController(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    [HttpGet]
    public async Task<IEnumerable<ItemGroupDto>> GetAll()
    {
        var groups = await _db.ItemGroups
            .Include(g => g.Ingredients).ThenInclude(gi => gi.Ingredient).ThenInclude(i => i.Category)
            .OrderBy(g => g.Name)
            .ToListAsync();
        return groups.Select(Map);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemGroupDto>> Get(int id)
    {
        var g = await _db.ItemGroups
            .Include(g => g.Ingredients).ThenInclude(gi => gi.Ingredient).ThenInclude(i => i.Category)
            .FirstOrDefaultAsync(g => g.Id == id);
        return g == null ? NotFound() : Map(g);
    }

    [HttpPost]
    public async Task<ActionResult<ItemGroupDto>> Create(ItemGroupUpsertDto dto)
    {
        var g = new ItemGroup { Name = dto.Name.Trim() };
        await ApplyLines(g, dto.Ingredients);
        _db.ItemGroups.Add(g);
        await _db.SaveChangesAsync();
        return await Get(g.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ItemGroupDto>> Update(int id, ItemGroupUpsertDto dto)
    {
        var g = await _db.ItemGroups.Include(x => x.Ingredients).FirstOrDefaultAsync(x => x.Id == id);
        if (g == null) return NotFound();

        g.Name = dto.Name.Trim();
        _db.ItemGroupIngredients.RemoveRange(g.Ingredients);
        g.Ingredients.Clear();
        await ApplyLines(g, dto.Ingredients);

        await _db.SaveChangesAsync();
        return await Get(g.Id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var g = await _db.ItemGroups.FindAsync(id);
        if (g == null) return NotFound();
        _db.ItemGroups.Remove(g);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task ApplyLines(ItemGroup g, List<IngredientLineInputDto> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.IngredientName)) continue;
            var ing = await _ingredients.GetOrCreateAsync(line.IngredientName);
            g.Ingredients.Add(new ItemGroupIngredient
            {
                Ingredient = ing,
                Quantity = line.Quantity,
                Unit = line.Unit
            });
        }
    }

    private static ItemGroupDto Map(ItemGroup g) => new(
        g.Id, g.Name,
        g.Ingredients.Select(gi => new IngredientLineDto(
            gi.Id, gi.IngredientId, gi.Ingredient.Name, gi.Ingredient.Category?.Name, gi.Quantity, gi.Unit))
            .OrderBy(l => l.IngredientName, StringComparer.OrdinalIgnoreCase).ToList());
}
