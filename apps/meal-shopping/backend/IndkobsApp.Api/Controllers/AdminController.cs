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
/// Administration af husstande. Beskyttet af en hemmelig nøgle (header "X-Admin-Key"),
/// så kun du kan oprette/liste husstande — ikke via almindeligt login.
/// </summary>
[ApiController]
[Route("api/admin")]
[AllowAnonymous]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<Household> _hasher;
    private readonly IConfiguration _cfg;

    public AdminController(AppDbContext db, IPasswordHasher<Household> hasher, IConfiguration cfg)
    {
        _db = db;
        _hasher = hasher;
        _cfg = cfg;
    }

    private bool AdminKeyOk()
    {
        var expected = _cfg["Admin:Key"];
        if (string.IsNullOrWhiteSpace(expected)) return false; // ingen nøgle sat = luk helt
        return Request.Headers.TryGetValue("X-Admin-Key", out var provided)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    [HttpGet("households")]
    public async Task<ActionResult<IEnumerable<HouseholdDto>>> List()
    {
        if (!AdminKeyOk()) return Unauthorized();
        return Ok(await _db.Households.OrderBy(h => h.Name)
            .Select(h => new HouseholdDto(h.Id, h.Name, h.Email)).ToListAsync());
    }

    [HttpPost("households")]
    public async Task<ActionResult<HouseholdDto>> Create(CreateHouseholdDto dto)
    {
        if (!AdminKeyOk()) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Navn, email og adgangskode skal udfyldes.");

        var email = Household.NormalizeEmail(dto.Email);
        if (await _db.Households.AnyAsync(h => h.Email == email))
            return Conflict("Der findes allerede en husstand med den email.");

        var household = new Household { Name = dto.Name.Trim(), Email = email };
        household.PasswordHash = _hasher.HashPassword(household, dto.Password);
        _db.Households.Add(household);
        await _db.SaveChangesAsync();

        // Ny husstand starter med sit eget standard-kategorisæt (kategorier er private).
        DbSeeder.SeedDefaultCategories(_db, household.Id);
        await _db.SaveChangesAsync();

        return Ok(new HouseholdDto(household.Id, household.Name, household.Email));
    }

    [HttpDelete("households/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!AdminKeyOk()) return Unauthorized();
        var h = await _db.Households.FindAsync(id);
        if (h == null) return NotFound();

        // Fælles cascade-sletning (samme logik som brugerens egen GDPR-sletning).
        await HouseholdEraser.EraseAsync(_db, id);
        return NoContent();
    }
}
