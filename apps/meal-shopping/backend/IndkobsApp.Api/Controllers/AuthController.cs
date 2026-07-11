using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<Household> _hasher;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, IPasswordHasher<Household> hasher, TokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginDto dto)
    {
        var email = Household.NormalizeEmail(dto.Email);
        var household = await _db.Households.FirstOrDefaultAsync(h => h.Email == email);

        // Samme svar uanset om email findes eller ej (undgå at afsløre gyldige emails).
        if (household == null ||
            _hasher.VerifyHashedPassword(household, household.PasswordHash, dto.Password ?? "")
                == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Forkert email eller adgangskode." });
        }

        var (token, expires) = _tokens.Create(household);
        return new AuthResultDto(token, expires.ToString("o"), household.Id, household.Name);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeDto>> Me()
    {
        var id = User.GetHouseholdId();
        var h = await _db.Households.FindAsync(id);
        return h == null ? Unauthorized() : new MeDto(h.Id, h.Name, h.Email);
    }
}
