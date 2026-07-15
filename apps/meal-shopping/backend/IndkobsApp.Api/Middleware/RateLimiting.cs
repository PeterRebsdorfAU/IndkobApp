using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace IndkobsApp.Api.Middleware;

/// <summary>
/// Global rate limiting (ASP.NET <c>RateLimiter</c>). Klassificerer hvert kald i en
/// "spand" pr. klient-IP og giver 429 ved burst. Alt sidder her + i Program.cs, så der
/// ikke skal sættes attributter på controllerne (holder ændringen additiv og konfliktfri).
///
/// Spande (grænser kan overstyres i konfiguration under "RateLimiting:*"):
///   auth  — login/refresh + nøgle-beskyttede admin/store-kald (brute-force-beskyttelse), stramt.
///   write — alle skrive-kald (POST/PUT/PATCH/DELETE) på øvrige endpoints.
///   read  — GET (rundhåndet, så normal brug aldrig rammer loftet).
/// </summary>
public static class RateLimiting
{
    public static IServiceCollection AddAppRateLimiter(this IServiceCollection services, IConfiguration cfg)
    {
        // Konfigurerbare grænser med sikre standardværdier (pr. IP, pr. minut).
        int authPermit = cfg.GetValue<int?>("RateLimiting:Auth:PermitPerMinute") ?? 10;
        int writePermit = cfg.GetValue<int?>("RateLimiting:Write:PermitPerMinute") ?? 60;
        int readPermit = cfg.GetValue<int?>("RateLimiting:Read:PermitPerMinute") ?? 300;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                response.ContentType = "application/json; charset=utf-8";
                await response.WriteAsync(
                    "{\"message\":\"For mange forespørgsler. Vent et øjeblik og prøv igen.\"}", token);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var (bucket, permit) = Classify(http, authPermit, writePermit, readPermit);
                return RateLimitPartition.GetFixedWindowLimiter($"{bucket}:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }

    private static (string Bucket, int Permit) Classify(HttpContext http, int authPermit, int writePermit, int readPermit)
    {
        var path = http.Request.Path;
        var method = http.Request.Method;

        // Nøgle-/kode-følsomme endpoints: strammest grænse mod gætning og burst.
        bool isAuthSensitive =
            path.StartsWithSegments("/api/auth/login") ||
            path.StartsWithSegments("/api/auth/refresh") ||
            path.StartsWithSegments("/api/admin") ||
            path.StartsWithSegments("/api/store");
        if (isAuthSensitive) return ("auth", authPermit);

        // Skrive-kald på alt andet.
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
            return ("write", writePermit);

        return ("read", readPermit);
    }
}
