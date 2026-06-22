using System.Text.Json.Serialization;
using IndkobsApp.Api.Data;
using IndkobsApp.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// JSON: serialisér enums (Unit) som tekst, så frontend ser "G"/"Kg" frem for tal.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

// EF Core mod SQL Server (.\SQLEXPRESS, Windows Auth – se appsettings.json).
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IngredientService>();
builder.Services.AddScoped<ShoppingListService>();

// CORS: tillad frontenden uanset om den åbnes via localhost (PC) eller PC'ens
// LAN-IP (telefon på samme WiFi). Privat app uden login/credentials, så vi
// tillader enhver oprindelse. Ved cloud-udrulning bør dette låses til din faste URL.
const string CorsPolicy = "frontend";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()));

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
