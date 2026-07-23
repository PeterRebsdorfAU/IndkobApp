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
    private readonly IRecipeScanner _scanner;
    private readonly RecipeAdoptionService _adoption;
    public RecipesController(AppDbContext db, IngredientService ingredients, IRecipeScanner scanner,
        RecipeAdoptionService adoption)
    {
        _db = db;
        _ingredients = ingredients;
        _scanner = scanner;
        _adoption = adoption;
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

    // ---------- Selektiv deling: del én opskrift med én udvalgt modtager-husstand ----------

    /// <summary>
    /// Del opskriften med den husstand hvis login-email matcher <c>dto.Email</c>. Kun ejeren af
    /// opskriften kan dele. Modtageren slås op via login-email (Household eller individuel bruger).
    /// Idempotent: samme (opskrift, modtager) kan deles igen uden fejl.
    /// </summary>
    [HttpPost("{id:int}/share")]
    public async Task<ActionResult<RecipeShareTargetDto>> Share(int id, ShareRecipeDto dto)
    {
        var hid = User.GetHouseholdId();
        var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.HouseholdId == hid);
        if (recipe == null) return NotFound(); // kun ejeren kan dele (husstands-scoping)

        var email = Household.NormalizeEmail(dto.Email);
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Angiv en modtager-email.");

        var target = await ResolveHouseholdByEmailAsync(email);
        if (target == null) return NotFound(new { message = "Ingen konto med den email." });
        if (target.Id == hid) return BadRequest("Du kan ikke dele med din egen husstand.");

        var existing = await _db.RecipeShares
            .FirstOrDefaultAsync(s => s.RecipeId == id && s.TargetHouseholdId == target.Id);
        if (existing == null)
        {
            existing = new RecipeShare { RecipeId = id, TargetHouseholdId = target.Id };
            _db.RecipeShares.Add(existing);
            await _db.SaveChangesAsync();
        }
        return new RecipeShareTargetDto(target.Id, target.Name, existing.CreatedUtc.ToString("o"));
    }

    /// <summary>Fjern en deling igen (kun ejeren). Ukendt deling → 404.</summary>
    [HttpDelete("{id:int}/share/{targetHouseholdId:int}")]
    public async Task<IActionResult> Unshare(int id, int targetHouseholdId)
    {
        var hid = User.GetHouseholdId();
        // Verificér ejerskab: delingen skal pege på en opskrift DENNE husstand ejer.
        var share = await _db.RecipeShares
            .FirstOrDefaultAsync(s => s.RecipeId == id && s.TargetHouseholdId == targetHouseholdId
                && s.Recipe.HouseholdId == hid);
        if (share == null) return NotFound();
        _db.RecipeShares.Remove(share);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Hvem opskriften er delt med (kun ejeren ser listen).</summary>
    [HttpGet("{id:int}/shares")]
    public async Task<ActionResult<IEnumerable<RecipeShareTargetDto>>> GetShares(int id)
    {
        var hid = User.GetHouseholdId();
        var owns = await _db.Recipes.AnyAsync(r => r.Id == id && r.HouseholdId == hid);
        if (!owns) return NotFound();

        var shares = await _db.RecipeShares
            .Where(s => s.RecipeId == id)
            .Join(_db.Households, s => s.TargetHouseholdId, h => h.Id,
                (s, h) => new RecipeShareTargetDto(h.Id, h.Name, s.CreatedUtc.ToString("o")))
            .OrderBy(x => x.HouseholdName)
            .ToListAsync();
        return shares;
    }

    /// <summary>Opskrifter delt TIL min husstand (skrivebeskyttet visning + mulighed for adoption).</summary>
    [HttpGet("shared-with-me")]
    public async Task<IEnumerable<SharedRecipeDto>> SharedWithMe()
    {
        var hid = User.GetHouseholdId();
        // Projektér UDEN billed-blob (kun HasImage) — samme mønster som listning af egne opskrifter.
        var rows = await _db.RecipeShares
            .Where(s => s.TargetHouseholdId == hid)
            .OrderByDescending(s => s.CreatedUtc)
            .Select(s => new
            {
                s.CreatedUtc,
                R = s.Recipe,
                OwnerName = _db.Households.Where(h => h.Id == s.Recipe.HouseholdId)
                    .Select(h => h.Name).FirstOrDefault(),
                HasImage = s.Recipe.Image != null,
                Ingredients = s.Recipe.Ingredients.Select(ri => new IngredientLineDto(
                    ri.Id, ri.IngredientId, ri.Ingredient.Name,
                    ri.Ingredient.Category == null ? null : ri.Ingredient.Category.Name,
                    ri.Quantity, ri.Unit)).ToList()
            })
            .ToListAsync();

        return rows.Select(x => new SharedRecipeDto(
            x.R.Id, x.R.Name, x.R.Note, x.R.Servings,
            x.Ingredients.OrderBy(l => l.IngredientName, StringComparer.OrdinalIgnoreCase).ToList(),
            x.R.Method, x.HasImage, x.OwnerName ?? "", x.CreatedUtc.ToString("o")));
    }

    /// <summary>
    /// Adoptér en opskrift der er delt til min husstand: kopiér den til mine egne opskrifter
    /// (genbruger den fælles adoptions-logik; tager Method + billede med) og læg den evt. på en uge.
    /// Kun tilladt hvis opskriften faktisk er delt til min husstand.
    /// </summary>
    [HttpPost("shared-with-me/{recipeId:int}/adopt")]
    public async Task<ActionResult<AdoptResultDto>> AdoptShared(int recipeId, AdoptCatalogRecipeDto? dto)
    {
        var hid = User.GetHouseholdId();
        var isSharedToMe = await _db.RecipeShares
            .AnyAsync(s => s.RecipeId == recipeId && s.TargetHouseholdId == hid);
        if (!isSharedToMe) return NotFound(); // ikke delt til mig

        var source = await _db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == recipeId);
        if (source == null) return NotFound();

        var recipe = await _adoption.AdoptAsync(
            hid, source.Name, source.Note, source.Servings,
            source.Method, source.Image, source.ImageContentType,
            source.Ingredients.Select(ri => new RecipeAdoptionService.AdoptLine(
                ri.Ingredient.Name, ri.Quantity, ri.Unit)).ToList());

        // Læg evt. på en af MINE uger med det samme.
        int? weekId = null;
        if (dto?.WeekId is int wid)
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

    /// <summary>Serverer billedet for en opskrift der er delt til min husstand.</summary>
    [HttpGet("shared-with-me/{recipeId:int}/image")]
    public async Task<IActionResult> GetSharedImage(int recipeId)
    {
        var hid = User.GetHouseholdId();
        var row = await _db.RecipeShares
            .Where(s => s.RecipeId == recipeId && s.TargetHouseholdId == hid)
            .Select(s => new { s.Recipe.Image, s.Recipe.ImageContentType })
            .FirstOrDefaultAsync();
        if (row?.Image == null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(row.Image, row.ImageContentType ?? "application/octet-stream");
    }

    // Slår en husstand op ud fra en login-email: enten husstandens eget login (Household.Email)
    // eller en individuel brugers login (User.Email → dennes husstand). Begge normaliseres ens.
    private async Task<Household?> ResolveHouseholdByEmailAsync(string normalizedEmail)
    {
        var household = await _db.Households.FirstOrDefaultAsync(h => h.Email == normalizedEmail);
        if (household != null) return household;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null) return null;
        return await _db.Households.FirstOrDefaultAsync(h => h.Id == user.HouseholdId);
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
                Unit = Units.Clean(line.Unit)
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

    // ---------- AI-scanning af opskrift-billede (valgfri; kræver Gemini-nøgle) ----------

    /// <summary>
    /// Fortæller frontend om scanning er tilgængelig, så "Scan opskrift"-knappen kan skjules
    /// når featuren er i dvale (ingen Gemini-nøgle). Kræver login som resten af controlleren.
    /// </summary>
    [HttpGet("scan/enabled")]
    public ActionResult<ScanEnabledDto> ScanEnabled() => new ScanEnabledDto(_scanner.Enabled);

    /// <summary>
    /// Læser en opskrift ud af et foto (multipart-felt "file") og returnerer et
    /// RecipeUpsert-lignende DTO til gennemsyn i editoren. GEMMER INTET — brugeren
    /// retter og gemmer selv via de eksisterende opret/gem-endpoints.
    /// Svarer 503 hvis scanning ikke er konfigureret (dvale uden nøgle).
    /// </summary>
    [HttpPost("scan")]
    [RequestSizeLimit(8 * 1024 * 1024)] // 8 MB rå-upload; komprimeres ned før afsendelse (som billede-upload)
    public async Task<ActionResult<RecipeUpsertDto>> Scan(IFormFile? file)
    {
        if (!_scanner.Enabled)
            return StatusCode(503, new { message = "AI-scanning er ikke aktiveret." });
        if (file == null || file.Length == 0) return BadRequest("Ingen fil modtaget.");

        // Genbrug billed-behandlingen: normalisér orientering, nedskalér og re-encode som JPEG.
        await using var stream = file.OpenReadStream();
        var processed = await ImageService.ProcessAsync(stream);
        if (processed == null) return BadRequest("Filen kunne ikke læses som et billede.");

        try
        {
            var scanned = await _scanner.ScanAsync(processed.Value.Bytes, processed.Value.ContentType);
            return RecipeScanMapper.ToUpsert(scanned);
        }
        catch (Exception)
        {
            // Netværks-/model-/parsefejl: giv en pæn fejl (detaljer logges i scanneren).
            return StatusCode(502, new { message = "Kunne ikke læse opskriften fra billedet. Prøv igen eller et andet foto." });
        }
    }
}
