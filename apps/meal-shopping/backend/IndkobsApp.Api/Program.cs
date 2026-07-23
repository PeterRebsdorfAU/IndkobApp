using System.Text;
using System.Text.Json.Serialization;
using IndkobsApp.Api.Data;
using IndkobsApp.Api.Middleware;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Observability;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Fejllogning & overvågning (T6): Sentry + struktureret request-logging. No-op uden Sentry:Dsn.
builder.AddObservability();

// JSON: serialisér enums (Unit) som tekst, så frontend ser "G"/"Kg" frem for tal.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// Kestrel-hærdning: skjul server-header og begræns request-størrelse. Alle payloads er
// små JSON-objekter, så 1 MB er rigeligt og lukker for oppustede request-bodies.
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 1_048_576; // 1 MB
});

// EF Core mod PostgreSQL. Connection string kommer fra konfiguration:
// lokalt fra appsettings.json, i skyen fra env-var ConnectionStrings__Default (Render/Neon).
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<ShoppingListService>();
builder.Services.AddScoped<WeekCleanupService>();

// Auth: password-hashing + JWT-udstedelse/validering.
builder.Services.AddSingleton<IPasswordHasher<Household>, PasswordHasher<Household>>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<TokenService>();

// E-mail (T2): bekræftelse/kode-nulstilling/invitation. Udbyder vælges via Email:Provider.
// Standard "console" (dev: log mailen). Sæt "smtp" + Email:Smtp:* i produktion for rigtig afsendelse.
if (string.Equals(builder.Configuration["Email:Provider"], "smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();

// AI-scanning af opskrift-billeder (valgfri). Scanneren ligger bag IRecipeScanner og er i
// DVALE uden Gemini:ApiKey — så registreringen er altid additiv (samme mønster som IEmailSender).
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IRecipeScanner, GeminiRecipeScanner>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key mangler eller er for kort (mindst 32 tegn). Sæt env-var Jwt__Key i produktion.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30) // stramt slip, så korte access-tokens udløber ~til tiden
        };
        // Et refresh-token må ALDRIG bruges som Bearer på beskyttede endpoints — kun access.
        // Ældre tokens (før token_type blev indført) mangler claimet og behandles som access,
        // så eksisterende logins ikke ryger ud ved deploy.
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var type = ctx.Principal?.FindFirst(TokenService.TokenTypeClaim)?.Value;
                if (type == TokenService.RefreshTokenType)
                    ctx.Fail("Refresh-token kan ikke bruges til adgang.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Rate limiting (se Middleware/RateLimiting.cs): 429 ved burst på auth-/skrive-endpoints.
builder.Services.AddAppRateLimiter(builder.Configuration);

// CORS: tillad KUN kendte oprindelser fra konfiguration (Cors:AllowedOrigins), fx den
// udrullede frontend-URL. I udvikling falder vi tilbage til lokale dev-oprindelser.
// I produktion UDEN konfiguration afvises alt (fail-closed) — aldrig permissivt "tillad alle".
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    if (allowedOrigins.Length > 0)
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else if (builder.Environment.IsDevelopment())
        p.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200").AllowAnyHeader().AllowAnyMethod();
    // ellers: ingen oprindelser tilladt (fail-closed) — se advarsel efter build.
}));

var app = builder.Build();

// Secrets-hygiejne: i produktion må dev-standardnøglerne ALDRIG bruges. Nægt opstart,
// hvis en nøgle stadig er dev-standarden — tvinger rotation (se SECURITY.md).
if (!app.Environment.IsDevelopment())
{
    RejectDevDefault(app.Configuration, "Jwt:Key", "dev-only-noegle-skift-i-produktion-1234567890", "Jwt__Key");
    RejectDevDefault(app.Configuration, "Admin:Key", "dev-admin-noegle-skift-i-produktion", "Admin__Key");
    RejectDevDefault(app.Configuration, "Stores:AccessKey", "butik1234", "Stores__AccessKey");

    if (allowedOrigins.Length == 0)
        app.Logger.LogWarning("Cors:AllowedOrigins er ikke sat i produktion — ALLE cross-origin-kald afvises. " +
                              "Sæt env-var Cors__AllowedOrigins__0 til frontend-URL'en.");
}

// Opret/migrér databasen og seed startdata ved opstart.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Household>>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    // Relationel DB (Npgsql i prod/dev) migreres normalt. Ikke-relationelle providers
    // (fx InMemory i integrationstests) understøtter ikke migrations → opret skemaet direkte.
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db, hasher, cfg);
    await DbSeeder.SeedCatalogAsync(db); // inspirations-katalog (kører også på eksisterende DB)
    await scope.ServiceProvider.GetRequiredService<WeekCleanupService>().RunAsync(); // slet uger > 5 uger gamle
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseObservability();            // Sentry-tracing (kun m. DSN) + struktureret request-logging (først, så alt fanges)
app.UseSecurityHeaders();          // sikkerheds-headers på alle svar
if (!app.Environment.IsDevelopment())
    app.UseHsts();                 // Strict-Transport-Security (kun prod/HTTPS)
app.UseRateLimiter();              // 429 ved burst (global limiter)
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoints(); // anonymt /health readiness-endpoint

app.Run();

// Kaster hvis en nøgle stadig har dev-standardværdien i produktion.
static void RejectDevDefault(IConfiguration cfg, string key, string devValue, string envName)
{
    if (string.Equals(cfg[key], devValue, StringComparison.Ordinal))
        throw new InvalidOperationException(
            $"{key} bruger dev-standardværdien i produktion. Sæt env-var {envName} til en stærk, hemmelig værdi (se SECURITY.md).");
}

// Gør entrypointet synligt for integrationstests (WebApplicationFactory<Program>). Ingen runtime-effekt.
public partial class Program { }
