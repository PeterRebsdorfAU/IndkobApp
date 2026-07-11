using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Køkkenlager: hvad husstanden har hjemme lige nu. Bruges af indkøbslisten
/// til at trække "haves" fra, så man kun køber det man mangler.
/// </summary>
[ApiController]
[Route("api/pantry")]
[Authorize]
public class PantryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    public PantryController(AppDbContext db, IngredientService ingredients)
    {
        _db = db;
        _ingredients = ingredients;
    }

    [HttpGet]
    public async Task<IEnumerable<PantryItemDto>> GetAll()
    {
        var hid = User.GetHouseholdId();
        var items = await _db.PantryItems
            .Where(p => p.HouseholdId == hid)
            .Include(p => p.Ingredient).ThenInclude(i => i.Category)
            .ToListAsync();

        return items
            .OrderBy(p => p.Ingredient.Category?.SortOrder ?? int.MaxValue)
            .ThenBy(p => p.Ingredient.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Map);
    }

    /// <summary>
    /// Tilføj til lageret. Findes varen allerede med en forenelig enhed
    /// (samme måle-familie, eller samme count-enhed), lægges mængderne sammen
    /// i stedet for at oprette en dublet-linje.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PantryItemDto>> Add(PantryUpsertDto dto)
    {
        var hid = User.GetHouseholdId();

        Ingredient ing;
        if (dto.IngredientId is int ingId)
        {
            var found = await _db.Ingredients.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == ingId);
            if (found == null) return BadRequest("Ukendt ingrediens.");
            ing = found;
        }
        else if (!string.IsNullOrWhiteSpace(dto.IngredientName))
        {
            ing = await _ingredients.GetOrCreateAsync(dto.IngredientName);
            await _db.SaveChangesAsync(); // sikrer Id
        }
        else return BadRequest("Angiv en ingrediens.");

        if (dto.Quantity <= 0) return BadRequest("Mængden skal være større end 0.");

        var family = UnitMath.FamilyOf(dto.Unit);
        var existing = await _db.PantryItems
            .Where(p => p.HouseholdId == hid && p.IngredientId == ing.Id)
            .ToListAsync();

        // Merge ind i eksisterende linje hvis enhederne kan lægges sammen.
        var target = existing.FirstOrDefault(p =>
            family == MeasureFamily.Count
                ? p.Unit == dto.Unit
                : UnitMath.FamilyOf(p.Unit) == family);

        if (target != null)
        {
            if (family == MeasureFamily.Count)
            {
                target.Quantity += dto.Quantity;
            }
            else
            {
                var sumBase = UnitMath.ToBase(target.Quantity, target.Unit) + UnitMath.ToBase(dto.Quantity, dto.Unit);
                (target.Quantity, target.Unit) = UnitMath.FromBase(sumBase, family);
            }
        }
        else
        {
            target = new PantryItem { HouseholdId = hid, IngredientId = ing.Id, Quantity = dto.Quantity, Unit = dto.Unit };
            _db.PantryItems.Add(target);
        }

        await _db.SaveChangesAsync();
        await _db.Entry(target).Reference(p => p.Ingredient).LoadAsync();
        await _db.Entry(target.Ingredient).Reference(i => i.Category).LoadAsync();
        return Map(target);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PantryUpdateDto dto)
    {
        var hid = User.GetHouseholdId();
        var item = await _db.PantryItems.FirstOrDefaultAsync(p => p.Id == id && p.HouseholdId == hid);
        if (item == null) return NotFound();

        if (dto.Quantity <= 0)
        {
            // Mængde 0 = varen er brugt op → fjern linjen.
            _db.PantryItems.Remove(item);
        }
        else
        {
            item.Quantity = dto.Quantity;
            item.Unit = dto.Unit;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var hid = User.GetHouseholdId();
        var item = await _db.PantryItems.FirstOrDefaultAsync(p => p.Id == id && p.HouseholdId == hid);
        if (item == null) return NotFound();
        _db.PantryItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static PantryItemDto Map(PantryItem p) => new(
        p.Id, p.IngredientId, p.Ingredient.Name, p.Ingredient.Category?.Name,
        p.Quantity, p.Unit);
}
