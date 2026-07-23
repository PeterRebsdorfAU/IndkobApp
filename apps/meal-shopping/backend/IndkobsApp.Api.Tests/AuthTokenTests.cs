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

        return NewController(dbName);
    }

    private static AuthController NewController(string dbName, FakeEmailSender? email = null) =>
        new(TestDb.Open(dbName), new PasswordHasher<User>(), new PasswordHasher<Household>(),
            new TokenService(Config(ValidKey)), email ?? new FakeEmailSender(), Config(ValidKey));

    /// <summary>Fanger sendte e-mails, så tokens/links kan udtrækkes i tests.</summary>
    private sealed class FakeEmailSender : IndkobsApp.Api.Services.IEmailSender
    {
        public readonly List<(string To, string Subject, string Body)> Sent = new();
        public Task SendAsync(string toEmail, string subject, string htmlBody, System.Threading.CancellationToken ct = default)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    // Udtræk ?token=... fra et link i en mail-body.
    private static string ExtractToken(string body)
    {
        var m = System.Text.RegularExpressions.Regex.Match(body, @"[?&]token=([^""&\s<]+)");
        return Uri.UnescapeDataString(m.Groups[1].Value);
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

    // ---- T2: legacy-login opgraderes dovent til en bruger ------------------------

    [Fact]
    public async Task Login_paa_husstand_uden_bruger_opretter_bruger_og_giver_userId()
    {
        // En husstand UDEN bruger-række (som før migration). Login skal virke og
        // dovent oprette en bruger, så access-tokenet får et userId-claim.
        var dbName = Guid.NewGuid().ToString();
        var ctrl = BuildAuthController(dbName, "hemmelig123", out _);

        var result = await ctrl.Login(new LoginDto("fam@test.dk", "hemmelig123"));
        var dto = Assert.IsType<AuthResultDto>(result.Value);
        Assert.NotNull(dto.UserId);

        using var db = TestDb.Open(dbName);
        Assert.Single(db.Users);
        Assert.Equal("fam@test.dk", db.Users.Single().Email);
    }

    // ---- T2: signup -------------------------------------------------------------

    [Fact]
    public async Task Signup_opretter_husstand_bruger_og_kategorisaet()
    {
        var dbName = Guid.NewGuid().ToString();
        var email = new FakeEmailSender();
        var ctrl = NewController(dbName, email);

        var result = await ctrl.Signup(new SignupDto("Nyt@Bruger.dk", "kodekode12", "Clara", HouseholdName: "Familien Ny"));
        var dto = Assert.IsType<AuthResultDto>(result.Value);

        Assert.Equal("Clara", dto.DisplayName);
        Assert.NotNull(dto.UserId);
        Assert.False(string.IsNullOrWhiteSpace(dto.RefreshToken));

        using var db = TestDb.Open(dbName);
        Assert.Single(db.Households);
        Assert.Single(db.Users);
        var user = db.Users.Single();
        Assert.Equal("nyt@bruger.dk", user.Email);      // normaliseret
        Assert.False(user.EmailConfirmed);              // afventer bekræftelse
        Assert.NotEmpty(db.Categories);                 // standard-kategorisæt seedet
        Assert.Single(email.Sent);                      // bekræftelsesmail sendt
    }

    [Fact]
    public async Task Signup_afviser_for_kort_kode_og_dublet_email()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctrl = NewController(dbName);

        Assert.IsType<BadRequestObjectResult>(
            (await ctrl.Signup(new SignupDto("a@b.dk", "kort", "Navn", HouseholdName: "H"))).Result);

        await ctrl.Signup(new SignupDto("dublet@b.dk", "kodekode12", "Navn", HouseholdName: "H"));
        var dup = await ctrl.Signup(new SignupDto("Dublet@b.dk", "kodekode12", "Anden", HouseholdName: "H2"));
        Assert.IsType<ConflictObjectResult>(dup.Result);
    }

    [Fact]
    public async Task Signup_med_invitation_joiner_eksisterende_husstand()
    {
        var dbName = Guid.NewGuid().ToString();
        var ctrl = NewController(dbName);

        // Opret husstand + første bruger.
        var first = await ctrl.Signup(new SignupDto("ejer@b.dk", "kodekode12", "Ejer", HouseholdName: "Familien"));
        var householdId = Assert.IsType<AuthResultDto>(first.Value).HouseholdId;

        // Lav et invitationstoken (samme mekanik som /invite-endpointet).
        var (invite, _) = new TokenService(Config(ValidKey))
            .CreatePurpose(householdId, TokenService.PurposeInvite, TimeSpan.FromDays(7));

        var joinRes = await ctrl.Signup(new SignupDto("nr2@b.dk", "kodekode12", "Nummer2", InviteToken: invite));
        var joinDto = Assert.IsType<AuthResultDto>(joinRes.Value);

        Assert.Equal(householdId, joinDto.HouseholdId); // samme husstand
        using var db = TestDb.Open(dbName);
        Assert.Equal(2, db.Users.Count());
        Assert.Single(db.Households); // ingen ny husstand oprettet
    }

    // ---- T2: glemt kode -> nulstil kode -----------------------------------------

    [Fact]
    public async Task ForgotPassword_sender_link_og_ResetPassword_virker_endtoend()
    {
        var dbName = Guid.NewGuid().ToString();
        var email = new FakeEmailSender();
        var ctrl = NewController(dbName, email);
        await ctrl.Signup(new SignupDto("glemt@b.dk", "gammelkode1", "Glem", HouseholdName: "H"));
        email.Sent.Clear(); // fjern bekræftelsesmailen

        // Glemt kode: der sendes en mail med et nulstillingslink.
        var forgot = await ctrl.ForgotPassword(new ForgotPasswordDto("Glemt@b.dk"));
        Assert.IsType<OkObjectResult>(forgot);
        var token = ExtractToken(Assert.Single(email.Sent).Body);

        // Nulstil koden via tokenet.
        var reset = await ctrl.ResetPassword(new ResetPasswordDto(token, "heltnykode9"));
        Assert.IsType<OkObjectResult>(reset);

        // Ny kode virker, gammel virker ikke.
        Assert.IsType<AuthResultDto>((await ctrl.Login(new LoginDto("glemt@b.dk", "heltnykode9"))).Value);
        Assert.IsType<UnauthorizedObjectResult>((await ctrl.Login(new LoginDto("glemt@b.dk", "gammelkode1"))).Result);

        // Tokenet er engangsbrug: kan ikke bruges igen (stamp matcher ikke længere).
        Assert.IsType<BadRequestObjectResult>(await ctrl.ResetPassword(new ResetPasswordDto(token, "endnuenkode1")));
    }

    [Fact]
    public async Task ForgotPassword_paa_ukendt_email_afsloerer_ikke_noget()
    {
        var dbName = Guid.NewGuid().ToString();
        var email = new FakeEmailSender();
        var ctrl = NewController(dbName, email);

        var forgot = await ctrl.ForgotPassword(new ForgotPasswordDto("findes-ikke@b.dk"));
        Assert.IsType<OkObjectResult>(forgot); // samme svar
        Assert.Empty(email.Sent);              // men ingen mail sendt
    }

    // ---- T2: bekræft email ------------------------------------------------------

    [Fact]
    public async Task ConfirmEmail_saetter_flag()
    {
        var dbName = Guid.NewGuid().ToString();
        var email = new FakeEmailSender();
        var ctrl = NewController(dbName, email);
        await ctrl.Signup(new SignupDto("bekraeft@b.dk", "kodekode12", "Bek", HouseholdName: "H"));
        var token = ExtractToken(Assert.Single(email.Sent).Body);

        var res = await ctrl.ConfirmEmail(new ConfirmEmailDto(token));
        Assert.IsType<OkObjectResult>(res);

        using var db = TestDb.Open(dbName);
        Assert.True(db.Users.Single().EmailConfirmed);
    }
}
