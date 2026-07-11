using System.Text;
using System.Text.Json.Serialization;
using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JSON: serialisér enums (Unit) som tekst, så frontend ser "G"/"Kg" frem for tal.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// EF Core mod PostgreSQL. Connection string kommer fra konfiguration:
// lokalt fra appsettings.json, i skyen fra env-var ConnectionStrings__Default (Render/Neon).
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<ShoppingListService>();

// Auth: password-hashing + JWT-udstedelse/validering.
builder.Services.AddSingleton<IPasswordHasher<Household>, PasswordHasher<Household>>();
builder.Services.AddScoped<TokenService>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// CORS: tillad de oprindelser der er angivet i konfiguration (Cors:AllowedOrigins),
// fx den udrullede frontend-URL. Er intet angivet (lokal udvikling), tillades alt.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    if (allowedOrigins is { Length: > 0 })
        p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// Opret/migrér databasen og seed startdata ved opstart.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Household>>();
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, hasher, cfg);
    await DbSeeder.SeedCatalogAsync(db); // inspirations-katalog (kører også på eksisterende DB)
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
