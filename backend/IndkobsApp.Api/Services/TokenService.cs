using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IndkobsApp.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace IndkobsApp.Api.Services;

/// <summary>Udsteder JWT-tokens til husstands-login.</summary>
public class TokenService
{
    private readonly IConfiguration _cfg;
    public TokenService(IConfiguration cfg) => _cfg = cfg;

    public const string HouseholdIdClaim = "householdId";

    public (string Token, DateTime ExpiresUtc) Create(Household h)
    {
        var keyString = _cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(keyString) || keyString.Length < 32)
            throw new InvalidOperationException("Jwt:Key mangler eller er for kort (mindst 32 tegn).");

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(HouseholdIdClaim, h.Id.ToString()),
            new Claim(ClaimTypes.Name, h.Name),
            new Claim(ClaimTypes.Email, h.Email),
        };

        var expires = DateTime.UtcNow.AddDays(60); // langtlivet token – bekvemt til privat brug
        var token = new JwtSecurityToken(claims: claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>Henter den aktuelle husstands Id fra JWT-claims (0 hvis ikke logget ind).</summary>
    public static int GetHouseholdId(this ClaimsPrincipal user)
    {
        var v = user.FindFirst(TokenService.HouseholdIdClaim)?.Value;
        return int.TryParse(v, out var id) ? id : 0;
    }
}
