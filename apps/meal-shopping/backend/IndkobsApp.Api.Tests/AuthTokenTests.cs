using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IndkobsApp.Api.Controllers;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for token-udstedelse, claim-udtræk og login-flow (password-hashing).
/// </summary>
public class AuthTokenTests
{
    // Mindst 32 tegn — kravet i TokenService/Program.
    private const string ValidKey = "super-hemmelig-test-noegle-1234567890";

    private static IConfiguration Config(string? jwtKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = jwtKey })
            .Build();

    private static Household TestHousehold() =>
        new() { Id = 42, Name = "Familien Test", Email = "fam@test" };

    // ---- TokenService ------------------------------------------------------------

    [Fact]
    public void Create_udsteder_token_med_householdId_claim()
    {
        var (token, expires) = new TokenService(Config(ValidKey)).Create(TestHousehold());

        Assert.False(string.IsNullOrWhiteSpace(token));
        // Create() = kortlivet access-token (T4-standard ~12 t; refresh-token er separat).
        Assert.True(expires > DateTime.UtcNow.AddHours(11));
        Assert.True(expires < DateTime.UtcNow.AddHours(13));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("42", jwt.Claims.First(c => c.Type == TokenService.HouseholdIdClaim).Value);
    }

    [Fact]
    public void Create_producerer_token_der_kan_valideres_med_samme_nøgle()
    {
        var (token, _) = new TokenService(Config(ValidKey)).Create(TestHousehold());

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidKey))
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        Assert.Equal(42, principal.GetHouseholdId());
    }

    [Fact]
    public void Create_afviser_token_valideret_med_forkert_nøgle()
    {
        var (token, _) = new TokenService(Config(ValidKey)).Create(TestHousehold());

        var wrong = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("en-helt-anden-noegle-abcdefghijklmnop"))
        };

        Assert.ThrowsAny<SecurityTokenException>(
            () => new JwtSecurityTokenHandler().ValidateToken(token, wrong, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("for-kort")] // < 32 tegn
    public void Create_kaster_naar_nøgle_mangler_eller_er_for_kort(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => new TokenService(Config(key)).Create(TestHousehold()));
    }

    // ---- GetHouseholdId-extension ------------------------------------------------

    [Fact]
    public void GetHouseholdId_læser_claim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(TokenService.HouseholdIdClaim, "7") }));

        Assert.Equal(7, user.GetHouseholdId());
    }

    [Fact]
    public void GetHouseholdId_er_nul_uden_claim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.Equal(0, user.GetHouseholdId());
    }

    // ---- Login-flow (password-hashing) ------------------------------------------

    private static AuthController BuildAuthController(string dbName, string password, out Household household)
    {
        var hasher = new PasswordHasher<Household>();
        var db = TestDb.Open(dbName);
        household = new Household { Id = 1, Name = "Test", Email = Household.NormalizeEmail("Fam@Test.dk") };
        household.PasswordHash = hasher.HashPassword(household, password);
        db.Households.Add(household);
        db.SaveChanges();

        return new AuthController(TestDb.Open(dbName), hasher, new TokenService(Config(ValidKey)));
    }

    [Fact]
    public async Task Login_med_korrekt_kode_giver_token()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctrl = BuildAuthController(dbName, "hemmelig123", out _);

        // Email normaliseres, så anden casing skal stadig virke.
        var result = await ctrl.Login(new LoginDto("FAM@test.dk", "hemmelig123"));

        var dto = Assert.IsType<AuthResultDto>(result.Value);
        Assert.False(string.IsNullOrWhiteSpace(dto.Token));
        Assert.Equal(1, dto.HouseholdId);
    }

    [Fact]
    public async Task Login_med_forkert_kode_afvises()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctrl = BuildAuthController(dbName, "hemmelig123", out _);

        var result = await ctrl.Login(new LoginDto("fam@test.dk", "forkert"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_med_ukendt_email_afvises()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctrl = BuildAuthController(dbName, "hemmelig123", out _);

        var result = await ctrl.Login(new LoginDto("findes-ikke@test.dk", "hemmelig123"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }
}
