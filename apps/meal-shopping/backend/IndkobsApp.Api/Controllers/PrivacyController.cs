using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Selvbetjent GDPR: brugeren kan hente ALT sin husstands data (ret til
/// dataportabilitet) og slette sin husstand permanent (ret til at blive glemt).
/// Alt scoper til den indloggede husstand — ingen adgang til andres data.
/// </summary>
[ApiController]
[Route("api/privacy")]
[Authorize]
public class PrivacyController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<Household> _hasher;

    public PrivacyController(AppDbContext db, IPasswordHasher<Household> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>
    /// GET /api/privacy/export — hele husstandens data som JSON.
    /// Adgangskode-hash og andre hemmeligheder er bevidst udeladt.
    /// </summary>
    [HttpGet("export")]
    public async Task<ActionResult<DataExportDto>> Export()
    {
        var hid = User.GetHouseholdId();
        var household = await _db.Households.FindAsync(hid);
        if (household == null) return Unauthorized();

        var categories = await _db.Categories
            .Where(c => c.HouseholdId == hid)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ExportCategoryDto(c.Id, c.Name, c.SortOrder))
            .ToListAsync();

        var ingredients = await _db.Ingredients
            .Where(i => i.HouseholdId == hid)
            .Include(i => i.Category)
            .OrderBy(i => i.Name)
            .Select(i => new ExportIngredientDto(i.Id, i.Name, i.Category != null ? i.Category.Name : null))
            .ToListAsync();

        var recipeEntities = await _db.Recipes
            .Where(r => r.HouseholdId == hid)
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .OrderBy(r => r.Name)
            .ToListAsync();

        // Hvilke af husstandens opskrifter er publiceret til inspirations-kataloget?
        var publishedRecipeIds = await _db.CatalogRecipes
            .Where(cr => cr.SourceHouseholdId == hid && cr.SourceRecipeId != null)
            .Select(cr => cr.SourceRecipeId!.Value)
            .ToListAsync();
        var publishedSet = publishedRecipeIds.ToHashSet();

        var recipes = recipeEntities.Select(r => new ExportRecipeDto(
            r.Id, r.Name, r.Note, r.Servings, publishedSet.Contains(r.Id),
            r.Ingredients.Select(ri => new ExportLineDto(ri.Ingredient.Name, ri.Quantity, ri.Unit.ToString())).ToList()
        )).ToList();

        var itemGroups = (await _db.ItemGroups
            .Where(g => g.HouseholdId == hid)
            .Include(g => g.Ingredients).ThenInclude(gi => gi.Ingredient)
            .OrderBy(g => g.Name)
            .ToListAsync())
            .Select(g => new ExportItemGroupDto(g.Id, g.Name,
                g.Ingredients.Select(gi => new ExportLineDto(gi.Ingredient.Name, gi.Quantity, gi.Unit.ToString())).ToList()))
            .ToList();

        var weeks = (await _db.Weeks
            .Where(w => w.HouseholdId == hid)
            .Include(w => w.Recipes).ThenInclude(wr => wr.Recipe)
            .Include(w => w.ItemGroups).ThenInclude(wg => wg.ItemGroup)
            .Include(w => w.ManualItems).ThenInclude(mi => mi.Ingredient)
            .Include(w => w.Checks)
            .OrderByDescending(w => w.Year).ThenByDescending(w => w.WeekNumber)
            .ToListAsync())
            .Select(w => new ExportWeekDto(
                w.Id, w.Year, w.WeekNumber,
                w.Recipes.Select(wr => new ExportWeekRecipeDto(
                    wr.Recipe.Name, wr.Servings, wr.DayOfWeek, wr.CookedUtc?.ToString("o"))).ToList(),
                w.ItemGroups.Select(wg => new ExportWeekItemGroupDto(wg.ItemGroup.Name)).ToList(),
                w.ManualItems.Select(mi => new ExportWeekManualItemDto(
                    mi.Ingredient != null ? mi.Ingredient.Name : (mi.FreeText ?? ""), mi.Quantity, mi.Unit.ToString())).ToList(),
                w.Checks.Select(c => new ExportWeekCheckDto(c.LineKey, c.IsChecked)).ToList()))
            .ToList();

        var pantry = (await _db.PantryItems
            .Where(p => p.HouseholdId == hid)
            .Include(p => p.Ingredient).ThenInclude(i => i.Category)
            .ToListAsync())
            .Select(p => new ExportPantryItemDto(
                p.Ingredient.Name, p.Ingredient.Category != null ? p.Ingredient.Category.Name : null,
                p.Quantity, p.Unit.ToString()))
            .OrderBy(p => p.Ingredient)
            .ToList();

        // Materialisér før projektion: .ToString("o") på DateOnly/DateTime kan ikke
        // oversættes til SQL af EF Core, så det skal ske i hukommelsen.
        var tasks = (await _db.HouseholdTasks
            .Where(t => t.HouseholdId == hid)
            .OrderBy(t => t.CreatedUtc)
            .ToListAsync())
            .Select(t => new ExportTaskDto(
                t.Id, t.Title, t.IntervalDays,
                t.NextDueDate?.ToString("o"),
                t.Assignees, t.IsDone,
                t.LastCompletedUtc?.ToString("o"),
                t.CreatedUtc.ToString("o")))
            .ToList();

        var orders = (await _db.Orders
            .Where(o => o.HouseholdId == hid)
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedUtc)
            .ToListAsync())
            .Select(o => new ExportOrderDto(
                o.Id, o.StoreName, o.Status.ToString(), o.Note,
                o.CreatedUtc.ToString("o"), o.ReadyUtc?.ToString("o"),
                o.Lines.Select(l => new ExportOrderLineDto(
                    l.Name, l.Quantity, l.Unit.ToString(), l.CategoryName, l.IsPacked, l.NotAvailable)).ToList()))
            .ToList();

        var published = await _db.CatalogRecipes
            .Where(cr => cr.SourceHouseholdId == hid)
            .Select(cr => new ExportPublishedRecipeDto(cr.Id, cr.Title, cr.SourceRecipeId))
            .ToListAsync();

        var export = new DataExportDto(
            DateTime.UtcNow.ToString("o"),
            new ExportHouseholdDto(household.Id, household.Name, household.Email, household.CreatedUtc.ToString("o")),
            categories, ingredients, recipes, itemGroups, weeks, pantry, tasks, orders, published);

        // Foreslå et filnavn til download (frontend gemmer selv som fil via HttpClient).
        var fileName = $"mine-data-{household.Id}-{DateTime.UtcNow:yyyy-MM-dd}.json";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return Ok(export);
    }

    /// <summary>
    /// POST /api/privacy/delete — sletter husstanden og ALT dens data permanent.
    /// Kræver at brugeren gen-indtaster sin adgangskode som bekræftelse.
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountDto dto)
    {
        var hid = User.GetHouseholdId();
        var household = await _db.Households.FindAsync(hid);
        if (household == null) return Unauthorized();

        if (string.IsNullOrEmpty(dto.Password) ||
            _hasher.VerifyHashedPassword(household, household.PasswordHash, dto.Password)
                == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Forkert adgangskode. Sletning blev ikke gennemført." });
        }

        // Fælles cascade-sletning (samme logik som admin-sletning).
        await HouseholdEraser.EraseAsync(_db, hid);
        return NoContent();
    }
}
