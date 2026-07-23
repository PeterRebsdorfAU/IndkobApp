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
    private readonly IPasswordHasher<User> _userHasher;
    // Bevares til legacy-fallback: verificér mod husstandens gamle login hvis en husstand
    // endnu ikke har en bruger-række (fx før migration er kørt et sted).
    private readonly IPasswordHasher<Household> _householdHasher;
    private readonly TokenService _tokens;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, IPasswordHasher<User> userHasher,
        IPasswordHasher<Household> householdHasher, TokenService tokens, IEmailSender email, IConfiguration cfg)
    {
        _db = db;
        _userHasher = userHasher;
        _householdHasher = householdHasher;
        _tokens = tokens;
        _email = email;
        _cfg = cfg;
    }

    // -------------------------------------------------------------------------
    // Login
    // -------------------------------------------------------------------------
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Login(LoginDto dto)
    {
        var email = Household.NormalizeEmail(dto.Email);

        // 1) Foretruk individuel bruger (T2).
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            if (_userHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password ?? "")
                == PasswordVerificationResult.Failed)
                return InvalidLogin();

            var hh = await _db.Households.FindAsync(user.HouseholdId);
            if (hh == null) return InvalidLogin();
            return BuildResult(hh, user);
        }

        // 2) Legacy-fallback: en husstand uden bruger-række (skulle normalt være backfilled
        //    ved migration). Verificér mod husstandens login og opret dovent en bruger, så
        //    intet eksisterende login går i stykker og fremtidige logins går via bruger-stien.
        var household = await _db.Households.FirstOrDefaultAsync(h => h.Email == email);
        if (household == null ||
            _householdHasher.VerifyHashedPassword(household, household.PasswordHash, dto.Password ?? "")
                == PasswordVerificationResult.Failed)
            return InvalidLogin();

        var migrated = new User
        {
            HouseholdId = household.Id,
            Email = household.Email,
            PasswordHash = household.PasswordHash, // samme Identity-hashformat → kan verificeres af _userHasher
            DisplayName = household.Name,
            EmailConfirmed = true, // eksisterende login betragtes som bekræftet
            CreatedUtc = household.CreatedUtc
        };
        _db.Users.Add(migrated);
        await _db.SaveChangesAsync();
        return BuildResult(household, migrated);
    }

    private ActionResult<AuthResultDto> InvalidLogin() =>
        Unauthorized(new { message = "Forkert email eller adgangskode." });

    // -------------------------------------------------------------------------
    // Signup — opret ny husstand + første bruger, ELLER join via invitation
    // -------------------------------------------------------------------------
    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Signup(SignupDto dto)
    {
        var email = Household.NormalizeEmail(dto.Email);
        var display = (dto.DisplayName ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(display))
            return BadRequest(new { message = "Navn og email skal udfyldes." });
        if ((dto.Password ?? "").Length < 8)
            return BadRequest(new { message = "Adgangskoden skal være mindst 8 tegn." });
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Der findes allerede en bruger med den email." });

        Household household;

        if (!string.IsNullOrWhiteSpace(dto.InviteToken))
        {
            // --- Join eksisterende husstand via invitationslink ---
            var invite = _tokens.ValidatePurpose(dto.InviteToken, TokenService.PurposeInvite);
            if (invite == null)
                return BadRequest(new { message = "Invitationslinket er ugyldigt eller udløbet." });

            var joined = await _db.Households.FindAsync(invite.Value.SubjectId);
            if (joined == null)
                return BadRequest(new { message = "Husstanden i invitationen findes ikke længere." });
            household = joined;
        }
        else
        {
            // --- Opret ny husstand (denne bruger er den første) ---
            var householdName = (dto.HouseholdName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(householdName))
                return BadRequest(new { message = "Angiv et husstandsnavn." });
            if (await _db.Households.AnyAsync(h => h.Email == email))
                return Conflict(new { message = "Der findes allerede en husstand med den email." });

            household = new Household { Name = householdName, Email = email };
            // Husstandens login-felt holdes i sync med den første bruger (bagudkompatibelt).
            household.PasswordHash = _householdHasher.HashPassword(household, dto.Password ?? "");
            _db.Households.Add(household);
            await _db.SaveChangesAsync(); // få household.Id

            // Ny husstand starter med sit eget standard-kategorisæt (kategorier er private pr. husstand).
            DbSeeder.SeedDefaultCategories(_db, household.Id);
            await _db.SaveChangesAsync();
        }

        var user = new User
        {
            HouseholdId = household.Id,
            Email = email,
            DisplayName = display,
            EmailConfirmed = false
        };
        user.PasswordHash = _userHasher.HashPassword(user, dto.Password ?? "");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SendConfirmationEmailAsync(user);

        return BuildResult(household, user);
    }

    // -------------------------------------------------------------------------
    // Glemt kode / nulstil kode
    // -------------------------------------------------------------------------
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var email = Household.NormalizeEmail(dto.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            // Stamp bindes til den nuværende adgangskode → linket bliver ugyldigt så snart
            // koden er nulstillet (engangsbrug), uden at vi behøver DB-state.
            var (token, _) = _tokens.CreatePurpose(user.Id, TokenService.PurposeResetPassword,
                TimeSpan.FromHours(1), TokenService.Stamp(user.PasswordHash));
            var link = $"{AppBaseUrl()}/nulstil-kode?token={Uri.EscapeDataString(token)}";
            await _email.SendAsync(user.Email, "Nulstil din adgangskode",
                $"Hej {user.DisplayName},<br><br>Du (eller nogen) har bedt om at nulstille adgangskoden til Madplan &amp; Indkøb. " +
                $"Klik på linket for at vælge en ny kode (gyldigt i 1 time):<br><br><a href=\"{link}\">{link}</a><br><br>" +
                "Hvis det ikke var dig, kan du ignorere denne mail.");
        }
        // Samme svar uanset om emailen findes (afslør ikke gyldige emails).
        return Ok(new { message = "Hvis emailen findes, har vi sendt et link til at nulstille adgangskoden." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        if ((dto.NewPassword ?? "").Length < 8)
            return BadRequest(new { message = "Adgangskoden skal være mindst 8 tegn." });

        var res = _tokens.ValidatePurpose(dto.Token, TokenService.PurposeResetPassword);
        if (res == null)
            return BadRequest(new { message = "Linket er ugyldigt eller udløbet. Bed om et nyt." });

        var user = await _db.Users.FindAsync(res.Value.SubjectId);
        if (user == null || res.Value.Stamp != TokenService.Stamp(user.PasswordHash))
            return BadRequest(new { message = "Linket er ugyldigt eller allerede brugt. Bed om et nyt." });

        user.PasswordHash = _userHasher.HashPassword(user, dto.NewPassword ?? "");

        // Hold husstandens legacy-login i sync, hvis denne bruger ejer det.
        var household = await _db.Households.FindAsync(user.HouseholdId);
        if (household != null && household.Email == user.Email)
            household.PasswordHash = _householdHasher.HashPassword(household, dto.NewPassword ?? "");

        await _db.SaveChangesAsync();
        return Ok(new { message = "Din adgangskode er nulstillet. Du kan nu logge ind." });
    }

    // -------------------------------------------------------------------------
    // Bekræft email
    // -------------------------------------------------------------------------
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailDto dto)
    {
        var res = _tokens.ValidatePurpose(dto.Token, TokenService.PurposeConfirmEmail);
        if (res == null)
            return BadRequest(new { message = "Bekræftelseslinket er ugyldigt eller udløbet." });

        var user = await _db.Users.FindAsync(res.Value.SubjectId);
        if (user == null)
            return BadRequest(new { message = "Brugeren findes ikke længere." });

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _db.SaveChangesAsync();
        }
        return Ok(new { message = "Din email er bekræftet." });
    }

    // -------------------------------------------------------------------------
    // Invitation: en indlogget bruger genererer et link, så en anden kan
    // oprette sig i SAMME husstand.
    // -------------------------------------------------------------------------
    [HttpPost("invite")]
    [Authorize]
    public ActionResult<InviteResultDto> Invite()
    {
        var hid = User.GetHouseholdId();
        if (hid == 0) return Unauthorized();

        // Subjektet er her husstands-id'et (invitationen giver adgang til husstanden).
        var (token, _) = _tokens.CreatePurpose(hid, TokenService.PurposeInvite, TimeSpan.FromDays(7));
        var link = $"{AppBaseUrl()}/opret?invite={Uri.EscapeDataString(token)}";
        return new InviteResultDto(token, link);
    }

    // -------------------------------------------------------------------------
    // Refresh + me
    // -------------------------------------------------------------------------
    /// <summary>
    /// Bytter et gyldigt refresh-token til et nyt access-token (+ roteret refresh-token).
    /// Additivt endpoint (T4); refresh er stateless — se <see cref="TokenService"/>.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshDto dto)
    {
        var valid = _tokens.ValidateRefreshFull(dto.RefreshToken);
        if (valid == null)
            return Unauthorized(new { message = "Ugyldigt eller udløbet refresh-token." });

        var household = await _db.Households.FindAsync(valid.Value.HouseholdId);
        if (household == null)
            return Unauthorized(new { message = "Ugyldigt eller udløbet refresh-token." });

        // Bevar bruger-konteksten på tværs af fornyelse: brug userId fra tokenet hvis til stede,
        // ellers fald tilbage til husstandens ene bruger (typisk efter migration/legacy).
        User? user = null;
        if (valid.Value.UserId is int uid)
            user = await _db.Users.FindAsync(uid);
        if (user == null)
        {
            var users = await _db.Users.Where(u => u.HouseholdId == household.Id).Take(2).ToListAsync();
            if (users.Count == 1) user = users[0];
        }

        return BuildResult(household, user);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeDto>> Me()
    {
        var hid = User.GetHouseholdId();
        var h = await _db.Households.FindAsync(hid);
        if (h == null) return Unauthorized();

        var uid = User.GetUserId();
        User? user = uid != null ? await _db.Users.FindAsync(uid.Value) : null;
        return new MeDto(h.Id, h.Name, user?.Email ?? h.Email, user?.DisplayName, user?.Id);
    }

    // -------------------------------------------------------------------------
    // Hjælpere
    // -------------------------------------------------------------------------
    private AuthResultDto BuildResult(Household h, User? user)
    {
        var userId = user?.Id;
        var (token, expires) = _tokens.CreateAccess(h, userId);
        var (refresh, _) = _tokens.CreateRefresh(h, userId);
        return new AuthResultDto(token, expires.ToString("o"), h.Id, h.Name, refresh, user?.DisplayName, userId);
    }

    private async Task SendConfirmationEmailAsync(User user)
    {
        var (token, _) = _tokens.CreatePurpose(user.Id, TokenService.PurposeConfirmEmail, TimeSpan.FromDays(3));
        var link = $"{AppBaseUrl()}/bekraeft-email?token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(user.Email, "Bekræft din email",
            $"Hej {user.DisplayName},<br><br>Velkommen til Madplan &amp; Indkøb! Bekræft din email ved at klikke på linket:<br><br>" +
            $"<a href=\"{link}\">{link}</a><br><br>Linket er gyldigt i 3 dage.");
    }

    /// <summary>Frontend-URL til at bygge links i e-mails. Config: App:BaseUrl (ellers CORS-origin / localhost).</summary>
    private string AppBaseUrl()
    {
        var configured = _cfg["App:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
        var origins = _cfg.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 } && !string.IsNullOrWhiteSpace(origins[0])) return origins[0].TrimEnd('/');
        return "http://localhost:4200";
    }
}
