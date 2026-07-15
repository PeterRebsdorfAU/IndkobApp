using IndkobsApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Observability;

/// <summary>
/// Fejllogning &amp; overvågning (T6). Alt er additivt og bevidst isoleret her, så ændringerne i
/// <c>Program.cs</c> holdes minimale (letter rebase oveni sikkerheds-opgaven T4, der ejer Program.cs).
///
/// Sentry er 100% valgfrit: uden en DSN (env-var <c>Sentry__Dsn</c> / config <c>Sentry:Dsn</c>) wires
/// SDK'et slet ikke — appen kører præcis som før og kan ikke crashe pga. manglende overvågning.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Læser Sentry-DSN'en fra konfiguration; tom/whitespace behandles som "ikke sat".</summary>
    public static string? GetSentryDsn(this IConfiguration config)
    {
        var dsn = config["Sentry:Dsn"];
        return string.IsNullOrWhiteSpace(dsn) ? null : dsn.Trim();
    }

    /// <summary>
    /// Kobler Sentry på web-hosten HVIS en DSN er sat. Uden DSN gøres intet (no-op) — ingen
    /// netværkskald, ingen initialisering, ingen risiko for crash i dev/lokalt/offline.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        // Struktureret HTTP request-logging (indbygget). Aktiv uanset Sentry — logger metode, sti,
        // status og varighed. Undlad body/headers for at undgå at logge følsomme data (JWT m.m.).
        builder.Services.AddHttpLogging(o =>
        {
            o.LoggingFields =
                Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestMethod
                | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPath
                | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponseStatusCode
                | Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.Duration;
        });

        var dsn = builder.Configuration.GetSentryDsn();
        if (dsn is null)
            return builder; // no-op: Sentry slået fra (request-logging kører stadig)

        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = dsn;
            o.Environment = builder.Environment.EnvironmentName;
            // Send strukturerede logs (Information+) og fejl med som breadcrumbs/events.
            o.MinimumBreadcrumbLevel = LogLevel.Information;
            o.MinimumEventLevel = LogLevel.Error;
            // Konservativ sampling: 100% i dev, 20% i prod (kan overstyres via Sentry__TracesSampleRate).
            o.TracesSampleRate = builder.Environment.IsDevelopment() ? 1.0 : 0.2;
            // Ingen PII (emails/JWT) sendes til Sentry — privat app.
            o.SendDefaultPii = false;
        });

        return builder;
    }

    /// <summary>
    /// Tilføjer request-tracing-middlewaren fra Sentry (kun aktiv når DSN er sat) samt struktureret
    /// HTTP request-logging. Kald tidligt i pipelinen.
    /// </summary>
    public static WebApplication UseObservability(this WebApplication app)
    {
        if (app.Configuration.GetSentryDsn() is not null)
            app.UseSentryTracing();

        // Struktureret request-logging (indbygget, ingen ekstra afhængigheder). Logger metode, sti,
        // status og varighed pr. request på Information-niveau — havner også i Sentry som breadcrumbs.
        app.UseHttpLogging();
        return app;
    }

    /// <summary>
    /// Readiness-/liveness-endpoint. <c>/health</c> er anonymt (modsat de [Authorize]'de controllers,
    /// fx /api/categories) og verificerer at DB-forbindelsen svarer, så ekstern oppetids-overvågning
    /// og container-orkestrering kan bruge det uden login.
    /// </summary>
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        {
            bool dbOk;
            try
            {
                dbOk = await db.Database.CanConnectAsync(ct);
            }
            catch
            {
                dbOk = false;
            }

            var payload = new
            {
                status = dbOk ? "ok" : "degraded",
                db = dbOk ? "up" : "down",
                utc = DateTime.UtcNow
            };
            return dbOk ? Results.Ok(payload) : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Health")
        .AllowAnonymous();
    }
}
