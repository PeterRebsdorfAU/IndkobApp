using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IndkobsApp.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Udsteder og validerer JWT-tokens til husstands-login.
///
/// GRÆNSEFLADE (koordineret med T2 "brugerkonti"): der udstedes to slags tokens —
/// et kortlivet <b>access-token</b> (bruges som Bearer på alle kald) og et længerelivet
/// <b>refresh-token</b> (bruges kun mod POST /api/auth/refresh til at hente et nyt access-token).
/// De skelnes på claimet <see cref="TokenTypeClaim"/> ("access"/"refresh"). Refresh er her
/// bevidst <i>stateless</i> (signeret JWT, ingen DB) for at holde T4 additiv/uden migration.
/// T2 kan senere gøre refresh DB-baseret med rotation/spærring pr. bruger UDEN at ændre
/// access-tokenets claims: <see cref="HouseholdIdClaim"/> bevares, og T2 tilføjer blot fx et
/// userId-claim additivt.
/// </summary>
public class TokenService
{
    private readonly IConfiguration _cfg;
    public TokenService(IConfiguration cfg) => _cfg = cfg;

    public const string HouseholdIdClaim = "householdId";
    public const string TokenTypeClaim = "token_type";
    public const string AccessTokenType = "access";
    public const string RefreshTokenType = "refresh";

    // Konfigurerbar levetid. Standard: kort access (12 t) + langt refresh (30 dage).
    // Kan overstyres via Jwt:AccessTokenMinutes / Jwt:RefreshTokenDays (env i prod).
    private int AccessTokenMinutes => _cfg.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 720;
    private int RefreshTokenDays => _cfg.GetValue<int?>("Jwt:RefreshTokenDays") ?? 30;

    /// <summary>Bagudkompatibelt alias for et access-token (uændret kaldeflade).</summary>
    public (string Token, DateTime ExpiresUtc) Create(Household h) => CreateAccess(h);

    /// <summary>Kortlivet access-token med husstandens fulde claims.</summary>
    public (string Token, DateTime ExpiresUtc) CreateAccess(Household h)
    {
        var claims = new[]
        {
            new Claim(HouseholdIdClaim, h.Id.ToString()),
            new Claim(ClaimTypes.Name, h.Name),
            new Claim(ClaimTypes.Email, h.Email),
            new Claim(TokenTypeClaim, AccessTokenType),
        };
        return Write(claims, DateTime.UtcNow.AddMinutes(AccessTokenMinutes));
    }

    /// <summary>Længerelivet refresh-token (minimalt: kun husstands-id + type).</summary>
    public (string Token, DateTime ExpiresUtc) CreateRefresh(Household h)
    {
        var claims = new[]
        {
            new Claim(HouseholdIdClaim, h.Id.ToString()),
            new Claim(TokenTypeClaim, RefreshTokenType),
        };
        return Write(claims, DateTime.UtcNow.AddDays(RefreshTokenDays));
    }

    /// <summary>
    /// Validerer et refresh-token (signatur + levetid + korrekt type) og returnerer
    /// husstands-id'et, eller null hvis tokenet er ugyldigt/udløbet/ikke er et refresh-token.
    /// </summary>
    public int? ValidateRefresh(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey(),
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst(TokenTypeClaim)?.Value != RefreshTokenType) return null;
            var hid = principal.FindFirst(HouseholdIdClaim)?.Value;
            return int.TryParse(hid, out var id) ? id : null;
        }
        catch
        {
            return null; // ugyldig signatur, udløbet, malformet osv.
        }
    }

    private SymmetricSecurityKey SigningKey()
    {
        var keyString = _cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(keyString) || keyString.Length < 32)
            throw new InvalidOperationException("Jwt:Key mangler eller er for kort (mindst 32 tegn).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
    }

    private (string Token, DateTime ExpiresUtc) Write(Claim[] claims, DateTime expires)
    {
        var creds = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);
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
