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
        // Projektér (i stedet for at loade hele entiteten), så det evt. store billede-bytea
        // ALDRIG hentes ved listning — kun et HasImage-flag. Billedet serveres separat.
        var rows = await _db.Recipes
            .Where(r => r.HouseholdId == hid)
            .OrderBy(r => r.Name)
            .Select(ToRow)
            .ToListAsync();
        // Hvilke af husstandens opskrifter er publiceret til inspirationssiden?
        var publicIds = await _db.CatalogRecipes
            .Where(c => c.SourceHouseholdId == hid && c.SourceRecipeId != null)
            .Select(c => c.SourceRecipeId!.Value)
            .ToHashSetAsync();
        return rows.Select(r => ToDto(r, publicIds.Contains(r.Id)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Get(int id)
    {
        var hid = User.GetHouseholdId();
        var row = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == hid)
            .Select(ToRow)
            .FirstOrDefaultAsync();
        if (row == null) return NotFound();
        var isPublic = await _db.CatalogRecipes.AnyAsync(c => c.SourceRecipeId == id);
        return ToDto(row, isPublic);
    }

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(RecipeUpsertDto dto)
    {
        var r = new Recipe
        {
            HouseholdId = User.GetHouseholdId(),
            Name = dto.Name.Trim(),
            Note = dto.Note,
            Servings = dto.Servings,
            Method = string.IsNullOrWhiteSpace(dto.Method) ? null : dto.Method.Trim()
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
        r.Method = string.IsNullOrWhiteSpace(dto.Method) ? null : dto.Method.Trim();

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
        existing.Method = r.Method; // fremgangsmåde følger med i snapshottet
        existing.Image = r.Image;   // billedet kopieres med i katalog-snapshottet
        existing.ImageContentType = r.ImageContentType;
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

    // Mellemform for projektion: alt undtagen billed-bytes (HasImage i stedet).
    private sealed record RecipeRow(
        int Id, string Name, string? Note, int Servings, string? Method, bool HasImage,
        List<IngredientLineDto> Ingredients);

    // EF-oversætbart udtryk: henter felterne + HasImage (r.Image != null) UDEN at loade blobben.
    private static readonly System.Linq.Expressions.Expression<Func<Recipe, RecipeRow>> ToRow = r => new RecipeRow(
        r.Id, r.Name, r.Note, r.Servings, r.Method, r.Image != null,
        r.Ingredients.Select(ri => new IngredientLineDto(
            ri.Id, ri.IngredientId, ri.Ingredient.Name,
            ri.Ingredient.Category == null ? null : ri.Ingredient.Category.Name,
            ri.Quantity, ri.Unit)).ToList());

    private static RecipeDto ToDto(RecipeRow r, bool isPublic) => new(
        r.Id, r.Name, r.Note, r.Servings,
        r.Ingredients.OrderBy(l => l.IngredientName, StringComparer.OrdinalIgnoreCase).ToList(),
        r.Method, isPublic, r.HasImage);

    // ---------- Billede (valgfrit; upload + visning) ----------

    /// <summary>Serverer opskriftens billede med korrekt content-type og en privat cache-header.</summary>
    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> GetImage(int id)
    {
        var hid = User.GetHouseholdId();
        var row = await _db.Recipes
            .Where(r => r.Id == id && r.HouseholdId == hid)
            .Select(r => new { r.Image, r.ImageContentType })
            .FirstOrDefaultAsync();
        if (row?.Image == null) return NotFound();
        // Privat (husstands-scopet indhold) + genbrug i en dag; billedet skifter sjældent.
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(row.Image, row.ImageContentType ?? "application/octet-stream");
    }

    /// <summary>
    /// Uploader/erstatter opskriftens billede (multipart-felt "file"). Billedet nedskaleres og
    /// komprimeres server-side (se <see cref="ImageService"/>), så DB'en ikke fyldes.
    /// Grænsen for request-størrelse hæves KUN her (den globale Kestrel-grænse er 1 MB), så
    /// store telefonbilleder kan modtages og derefter skæres ned til ~100–250 KB inden lagring.
    /// </summary>
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(8 * 1024 * 1024)] // 8 MB rå-upload; komprimeres ned før lagring
    public async Task<ActionResult<RecipeDto>> UploadImage(int id, IFormFile? file)
    {
        var hid = User.GetHouseholdId();
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == hid);
        if (recipe == null) return NotFound();
        if (file == null || file.Length == 0) return BadRequest("Ingen fil modtaget.");

        await using var stream = file.OpenReadStream();
        var processed = await ImageService.ProcessAsync(stream);
        if (processed == null) return BadRequest("Filen kunne ikke læses som et billede.");

        recipe.Image = processed.Value.Bytes;
        recipe.ImageContentType = processed.Value.ContentType;
        await _db.SaveChangesAsync();
        return await Get(id);
    }

    /// <summary>Fjerner opskriftens billede igen.</summary>
    [HttpDelete("{id:int}/image")]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var hid = User.GetHouseholdId();
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == hid);
        if (recipe == null) return NotFound();
        recipe.Image = null;
        recipe.ImageContentType = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
