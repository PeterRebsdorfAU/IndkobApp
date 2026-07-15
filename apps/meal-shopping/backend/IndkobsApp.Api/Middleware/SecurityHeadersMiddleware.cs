namespace IndkobsApp.Api.Middleware;

/// <summary>
/// Sætter sikkerheds-headers på ALLE svar (også fejlsvar), så de rammer klienten
/// uanset resultat. HSTS håndteres separat af <c>app.UseHsts()</c> (kun over HTTPS/prod).
/// Middleware'et er bevidst afhængighedsfrit og additivt — det ændrer ikke på indhold.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Ingen MIME-sniffing (undgår at browseren gætter en anden content-type).
        headers["X-Content-Type-Options"] = "nosniff";
        // API'et må aldrig indlejres i en ramme (clickjacking-beskyttelse).
        headers["X-Frame-Options"] = "DENY";
        // Læk ikke referer på tværs af oprindelser.
        headers["Referrer-Policy"] = "no-referrer";
        // Slå browser-funktioner fra som API'et aldrig behøver.
        headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";
        // Rent JSON-API uden egen HTML — lås alt ned og forbyd indlejring.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        // API'et deles med frontend på en anden oprindelse (CORS styrer selve adgangen).
        headers["Cross-Origin-Resource-Policy"] = "cross-origin";

        return _next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>Tilføj sikkerheds-headers tidligt i pipelinen, så de gælder alle svar.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
