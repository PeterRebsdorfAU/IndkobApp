using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/weeks")]
[Authorize] // kræver login; alle uger scopes til den aktuelle husstand
public class WeeksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IngredientService _ingredients;
    private readonly ShoppingListService _shoppingList;
    private readonly WeekCleanupService _cleanup;

    public WeeksController(AppDbContext db, IngredientService ingredients, ShoppingListService shoppingList,
        WeekCleanupService cleanup)
    {
        _db = db;
        _ingredients = ingredients;
        _shoppingList = shoppingList;
        _cleanup = cleanup;
    }

    // Er ugen ejet af den aktuelle husstand?
    private Task<bool> OwnsWeek(int weekId) =>
        _db.Weeks.AnyAsync(w => w.Id == weekId && w.HouseholdId == User.GetHouseholdId());

    [HttpGet]
    public async Task<IEnumerable<WeekDto>> GetAll()
    {
        // Opportunistisk oprydning af gamle uger (throttlet til hver 6. time).
        await _cleanup.RunIfDueAsync();

        var hid = User.GetHouseholdId();
        return await _db.Weeks.Where(w => w.HouseholdId == hid)
            .OrderByDescending(w => w.Year).ThenByDescending(w => w.WeekNumber)
            .Select(w => new WeekDto(w.Id, w.Year, w.WeekNumber)).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WeekDetailDto>> Get(int id)
    {
        var dto = await BuildDetail(id);
        return dto == null ? NotFound() : dto;
    }

    [HttpPost]
    public async Task<ActionResult<WeekDto>> Create(WeekCreateDto dto)
    {
        var hid = User.GetHouseholdId();
        // Returnér eksisterende uge hvis (husstand, år, ugenr) allerede findes.
        var existing = await _db.Weeks.FirstOrDefaultAsync(w =>
            w.HouseholdId == hid && w.Year == dto.Year && w.WeekNumber == dto.WeekNumber);
        if (existing != null) return Ok(new WeekDto(existing.Id, existing.Year, existing.WeekNumber));

        var w = new Week { HouseholdId = hid, Year = dto.Year, WeekNumber = dto.WeekNumber };
        _db.Weeks.Add(w);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = w.Id }, new WeekDto(w.Id, w.Year, w.WeekNumber));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var hid = User.GetHouseholdId();
        var w = await _db.Weeks.FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == hid);
        if (w == null) return NotFound();
        _db.Weeks.Remove(w);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Retter i ugen ----------
    [HttpPost("{id:int}/recipes")]
    public async Task<ActionResult<WeekDetailDto>> AddRecipe(int id, AddWeekRecipeDto dto)
    {
        var hid = User.GetHouseholdId();
        if (!await OwnsWeek(id)) return NotFound();
        // Man kan kun tilføje egne retter.
        if (!await _db.Recipes.AnyAsync(r => r.Id == dto.RecipeId && r.HouseholdId == hid))
            return BadRequest("Ukendt ret.");

        _db.WeekRecipes.Add(new WeekRecipe
        {
            WeekId = id,
            RecipeId = dto.RecipeId,
            Servings = dto.Servings,
            DayOfWeek = dto.DayOfWeek
        });
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    [HttpPut("{id:int}/recipes/{weekRecipeId:int}")]
    public async Task<ActionResult<WeekDetailDto>> UpdateRecipe(int id, int weekRecipeId, UpdateWeekRecipeDto dto)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var wr = await _db.WeekRecipes.FirstOrDefaultAsync(x => x.Id == weekRecipeId && x.WeekId == id);
        if (wr == null) return NotFound();
        wr.Servings = dto.Servings;
        wr.DayOfWeek = dto.DayOfWeek;
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    /// <summary>
    /// Markér retten som LAVET (historik-markering). Kan kun gøres én gang pr. ret pr. uge.
    /// </summary>
    [HttpPost("{id:int}/recipes/{weekRecipeId:int}/cooked")]
    public async Task<ActionResult<WeekDetailDto>> MarkCooked(int id, int weekRecipeId)
    {
        if (!await OwnsWeek(id)) return NotFound();

        var wr = await _db.WeekRecipes
            .FirstOrDefaultAsync(x => x.Id == weekRecipeId && x.WeekId == id);
        if (wr == null) return NotFound();
        if (wr.CookedUtc != null) return Conflict("Retten er allerede markeret som lavet.");

        wr.CookedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    /// <summary>
    /// Fortryd "lavet"-markeringen.
    /// </summary>
    [HttpDelete("{id:int}/recipes/{weekRecipeId:int}/cooked")]
    public async Task<ActionResult<WeekDetailDto>> UnmarkCooked(int id, int weekRecipeId)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var wr = await _db.WeekRecipes.FirstOrDefaultAsync(x => x.Id == weekRecipeId && x.WeekId == id);
        if (wr == null) return NotFound();
        wr.CookedUtc = null;
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    [HttpDelete("{id:int}/recipes/{weekRecipeId:int}")]
    public async Task<ActionResult<WeekDetailDto>> RemoveRecipe(int id, int weekRecipeId)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var wr = await _db.WeekRecipes.FirstOrDefaultAsync(x => x.Id == weekRecipeId && x.WeekId == id);
        if (wr == null) return NotFound();
        _db.WeekRecipes.Remove(wr);
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    // ---------- Varegrupper i ugen ----------
    [HttpPost("{id:int}/item-groups")]
    public async Task<ActionResult<WeekDetailDto>> AddItemGroup(int id, AddWeekItemGroupDto dto)
    {
        var hid = User.GetHouseholdId();
        if (!await OwnsWeek(id)) return NotFound();
        if (!await _db.ItemGroups.AnyAsync(g => g.Id == dto.ItemGroupId && g.HouseholdId == hid))
            return BadRequest("Ukendt varegruppe.");

        _db.WeekItemGroups.Add(new WeekItemGroup { WeekId = id, ItemGroupId = dto.ItemGroupId });
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    [HttpDelete("{id:int}/item-groups/{weekItemGroupId:int}")]
    public async Task<ActionResult<WeekDetailDto>> RemoveItemGroup(int id, int weekItemGroupId)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var wg = await _db.WeekItemGroups.FirstOrDefaultAsync(x => x.Id == weekItemGroupId && x.WeekId == id);
        if (wg == null) return NotFound();
        _db.WeekItemGroups.Remove(wg);
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    // ---------- Løse varer ----------
    [HttpPost("{id:int}/manual-items")]
    public async Task<ActionResult<WeekDetailDto>> AddManualItem(int id, AddWeekManualItemDto dto)
    {
        if (!await OwnsWeek(id)) return NotFound();

        int? ingredientId = dto.IngredientId;
        string? freeText = dto.FreeText?.Trim();

        // Hvis der ikke er valgt en eksisterende ingrediens, men der er fritekst,
        // kobler vi den til/opretter en master-ingrediens, så den aggregeres pænt.
        if (ingredientId == null && !string.IsNullOrWhiteSpace(freeText))
        {
            var ing = await _ingredients.GetOrCreateAsync(User.GetHouseholdId(), freeText);
            await _db.SaveChangesAsync();
            ingredientId = ing.Id;
            freeText = null;
        }

        if (ingredientId == null && string.IsNullOrWhiteSpace(freeText))
            return BadRequest("Angiv enten en ingrediens eller en tekst.");

        _db.WeekManualItems.Add(new WeekManualItem
        {
            WeekId = id,
            IngredientId = ingredientId,
            FreeText = freeText,
            Quantity = dto.Quantity,
            Unit = dto.Unit
        });
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    [HttpDelete("{id:int}/manual-items/{manualItemId:int}")]
    public async Task<ActionResult<WeekDetailDto>> RemoveManualItem(int id, int manualItemId)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var m = await _db.WeekManualItems.FirstOrDefaultAsync(x => x.Id == manualItemId && x.WeekId == id);
        if (m == null) return NotFound();
        _db.WeekManualItems.Remove(m);
        await _db.SaveChangesAsync();
        return (await BuildDetail(id))!;
    }

    // ---------- Indkøbsliste ----------
    [HttpGet("{id:int}/shopping-list")]
    public async Task<ActionResult<ShoppingListDto>> GetShoppingList(int id)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var list = await _shoppingList.BuildAsync(id);
        return list == null ? NotFound() : list;
    }

    [HttpPut("{id:int}/shopping-list/check")]
    public async Task<IActionResult> SetCheck(int id, CheckLineDto dto)
    {
        if (!await OwnsWeek(id)) return NotFound();

        var check = await _db.ShoppingListChecks
            .FirstOrDefaultAsync(c => c.WeekId == id && c.LineKey == dto.LineKey);
        if (check == null)
        {
            check = new ShoppingListCheck { WeekId = id, LineKey = dto.LineKey, IsChecked = dto.IsChecked };
            _db.ShoppingListChecks.Add(check);
        }
        else
        {
            check.IsChecked = dto.IsChecked;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Deling af indkøbslisten (link uden login) ----------
    /// <summary>Opret (eller genbrug) et delings-token for ugens indkøbsliste.</summary>
    [HttpPost("{id:int}/share")]
    public async Task<ActionResult<ShareTokenDto>> CreateShare(int id)
    {
        if (!await OwnsWeek(id)) return NotFound();

        var existing = await _db.WeekShareTokens.FirstOrDefaultAsync(t => t.WeekId == id);
        if (existing != null) return new ShareTokenDto(existing.Token);

        var share = new WeekShareToken { WeekId = id, Token = Guid.NewGuid().ToString("N") };
        _db.WeekShareTokens.Add(share);
        await _db.SaveChangesAsync();
        return new ShareTokenDto(share.Token);
    }

    /// <summary>Tilbagekald delingen — linket holder op med at virke.</summary>
    [HttpDelete("{id:int}/share")]
    public async Task<IActionResult> RevokeShare(int id)
    {
        if (!await OwnsWeek(id)) return NotFound();
        var tokens = await _db.WeekShareTokens.Where(t => t.WeekId == id).ToListAsync();
        _db.WeekShareTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Hjælper ----------
    private async Task<WeekDetailDto?> BuildDetail(int id)
    {
        var hid = User.GetHouseholdId();
        var w = await _db.Weeks
            .Include(x => x.Recipes).ThenInclude(wr => wr.Recipe)
            .Include(x => x.ItemGroups).ThenInclude(wg => wg.ItemGroup)
            .Include(x => x.ManualItems).ThenInclude(m => m.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id && x.HouseholdId == hid);
        if (w == null) return null;

        return new WeekDetailDto(
            w.Id, w.Year, w.WeekNumber,
            w.Recipes.Select(wr => new WeekRecipeDto(
                wr.Id, wr.RecipeId, wr.Recipe.Name, wr.Recipe.Servings, wr.Servings, wr.DayOfWeek, wr.CookedUtc))
                .OrderBy(x => x.DayOfWeek ?? 99).ThenBy(x => x.RecipeName).ToList(),
            w.ItemGroups.Select(wg => new WeekItemGroupDto(wg.Id, wg.ItemGroupId, wg.ItemGroup.Name))
                .OrderBy(x => x.ItemGroupName).ToList(),
            w.ManualItems.Select(m => new WeekManualItemDto(
                m.Id, m.IngredientId, m.Ingredient != null ? m.Ingredient.Name : (m.FreeText ?? ""), m.Quantity, m.Unit))
                .OrderBy(x => x.Name).ToList());
    }
}
