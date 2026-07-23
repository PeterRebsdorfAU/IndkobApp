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
    /// <summary>Bruger-id (T2). Additivt oven på <see cref="HouseholdIdClaim"/> — ældre tokens mangler det.</summary>
    public const string UserIdClaim = "userId";
    public const string TokenTypeClaim = "token_type";
    public const string AccessTokenType = "access";
    public const string RefreshTokenType = "refresh";

    // Formåls-scopede engangs-tokens (T2): email-bekræftelse, kode-nulstilling, invitation.
    // Bevidst STATELESS (signeret JWT, ingen DB) på linje med refresh-tokenet — se ValidatePurpose.
    public const string PurposeClaim = "purpose";
    public const string StampClaim = "stamp"; // binder et token til en tilstand (fx nuværende PasswordHash) → engangsbrug
    public const string PurposeConfirmEmail = "confirm_email";
    public const string PurposeResetPassword = "reset_password";
    public const string PurposeInvite = "invite";

    // Konfigurerbar levetid. Standard: kort access (12 t) + langt refresh (30 dage).
    // Kan overstyres via Jwt:AccessTokenMinutes / Jwt:RefreshTokenDays (env i prod).
    private int AccessTokenMinutes => _cfg.GetValue<int?>("Jwt:AccessTokenMinutes") ?? 720;
    private int RefreshTokenDays => _cfg.GetValue<int?>("Jwt:RefreshTokenDays") ?? 30;

    /// <summary>Bagudkompatibelt alias for et access-token (uændret kaldeflade).</summary>
    public (string Token, DateTime ExpiresUtc) Create(Household h) => CreateAccess(h);

    /// <summary>
    /// Kortlivet access-token med husstandens fulde claims. <paramref name="userId"/> tilføjes
    /// additivt (T2) når kaldet sker for en individuel bruger; er null for legacy/husstands-login.
    /// </summary>
    public (string Token, DateTime ExpiresUtc) CreateAccess(Household h, int? userId = null)
    {
        var claims = new List<Claim>
        {
            new(HouseholdIdClaim, h.Id.ToString()),
            new(ClaimTypes.Name, h.Name),
            new(ClaimTypes.Email, h.Email),
            new(TokenTypeClaim, AccessTokenType),
        };
        if (userId is int uid) claims.Add(new Claim(UserIdClaim, uid.ToString()));
        return Write(claims.ToArray(), DateTime.UtcNow.AddMinutes(AccessTokenMinutes));
    }

    /// <summary>Længerelivet refresh-token (minimalt: husstands-id + evt. bruger-id + type).</summary>
    public (string Token, DateTime ExpiresUtc) CreateRefresh(Household h, int? userId = null)
    {
        var claims = new List<Claim>
        {
            new(HouseholdIdClaim, h.Id.ToString()),
            new(TokenTypeClaim, RefreshTokenType),
        };
        if (userId is int uid) claims.Add(new Claim(UserIdClaim, uid.ToString()));
        return Write(claims.ToArray(), DateTime.UtcNow.AddDays(RefreshTokenDays));
    }

    /// <summary>
    /// Udsteder et formåls-scopet engangs-token (email-bekræftelse / kode-nulstilling / invitation).
    /// <paramref name="stamp"/> binder tokenet til en tilstand: fx en hash af brugerens nuværende
    /// PasswordHash, så et nulstillingslink automatisk bliver ugyldigt efter første brug.
    /// </summary>
    public (string Token, DateTime ExpiresUtc) CreatePurpose(int subjectId, string purpose, TimeSpan lifetime, string? stamp = null)
    {
        var claims = new List<Claim>
        {
            new(UserIdClaim, subjectId.ToString()),
            new(PurposeClaim, purpose),
        };
        if (stamp != null) claims.Add(new Claim(StampClaim, stamp));
        return Write(claims.ToArray(), DateTime.UtcNow.Add(lifetime));
    }

    /// <summary>
    /// Validerer et formåls-token (signatur + levetid + korrekt formål) og returnerer
    /// subjekt-id'et + evt. stamp, eller null hvis ugyldigt/udløbet/forkert formål.
    /// Kalderen sammenligner selv stamp'en (fx mod brugerens nuværende PasswordHash).
    /// </summary>
    public (int SubjectId, string? Stamp)? ValidatePurpose(string? token, string purpose)
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

            if (principal.FindFirst(PurposeClaim)?.Value != purpose) return null;
            var sid = principal.FindFirst(UserIdClaim)?.Value;
            if (!int.TryParse(sid, out var id)) return null;
            return (id, principal.FindFirst(StampClaim)?.Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Stabil, kort "stamp" udledt af en hemmelighed (fx PasswordHash). Ændrer hemmeligheden
    /// sig (kode nulstillet), matcher et tidligere udstedt token ikke længere → engangsbrug.
    /// </summary>
    public static string Stamp(string secret)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(secret ?? string.Empty));
        return Convert.ToHexString(bytes, 0, 8); // 16 hex-tegn er rigeligt
    }

    /// <summary>
    /// Validerer et refresh-token (signatur + levetid + korrekt type) og returnerer
    /// husstands-id'et, eller null hvis tokenet er ugyldigt/udløbet/ikke er et refresh-token.
    /// </summary>
    public int? ValidateRefresh(string? token) => ValidateRefreshFull(token)?.HouseholdId;

    /// <summary>
    /// Som <see cref="ValidateRefresh"/>, men returnerer også bruger-id'et (T2) hvis tokenet
    /// bærer det — så en fornyelse bevarer den individuelle bruger-kontekst.
    /// </summary>
    public (int HouseholdId, int? UserId)? ValidateRefreshFull(string? token)
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
            if (!int.TryParse(hid, out var id)) return null;
            var uidRaw = principal.FindFirst(UserIdClaim)?.Value;
            int? uid = int.TryParse(uidRaw, out var u) ? u : null;
            return (id, uid);
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

    /// <summary>Henter den aktuelle brugers Id fra JWT-claims, eller null (legacy husstands-token
    /// eller ikke logget ind). Data er husstands-scopet, så de fleste controllers bruger stadig
    /// <see cref="GetHouseholdId"/>; userId bruges til personalisering/audit.</summary>
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var v = user.FindFirst(TokenService.UserIdClaim)?.Value;
        return int.TryParse(v, out var id) ? id : null;
    }
}
