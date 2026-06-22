using System.Text.Json.Serialization;
using IndkobsApp.Api.Data;
using IndkobsApp.Api.Services;
using Microsoft.EntityFrameworkCore;

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

// CORS: tillad de oprindelser der er angivet i konfiguration (Cors:AllowedOrigins),
// fx den udrullede frontend-URL. Er intet angivet (lokal udvikling), tillades alt,
// så appen virker både via localhost og PC'ens LAN-IP (telefon på samme WiFi).
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
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(CorsPolicy);
app.MapControllers();

app.Run();
