using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IndkobsApp.Api.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Klient mod Tilbudsdata.dk's API (danske dagligvaretilbud).
/// Kræver adgang (UserId + ApiKey) som rekvireres hos support@effectmanager.com og
/// sættes via konfiguration: Tilbudsdata:UserId / Tilbudsdata:ApiKey
/// (env-vars Tilbudsdata__UserId / Tilbudsdata__ApiKey i produktion).
///
/// Signering (jf. deres docs): Signature = SHA-256( BaseParams + ApiKey ), hvor
/// BaseParams = "/offer/search?searchString=...&page=1&_userId=...&_expiration=&lt;unix&gt;".
/// Svar caches i 6 timer pr. søgeord for at skåne API'et.
/// </summary>
public class TilbudsdataClient
{
    private const string BaseUrl = "https://api.tilbudsdata.dk";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly string? _userId;
    private readonly string? _apiKey;

    public TilbudsdataClient(HttpClient http, IMemoryCache cache, IConfiguration cfg)
    {
        _http = http;
        _cache = cache;
        _userId = cfg["Tilbudsdata:UserId"];
        _apiKey = cfg["Tilbudsdata:ApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_userId) && !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>Søg tilbud på et søgeord. Returnerer tom liste ved fejl (best effort).</summary>
    public async Task<List<OfferDto>> SearchAsync(string query)
    {
        if (!IsConfigured) return new List<OfferDto>();

        var key = "offers:" + query.Trim().ToLowerInvariant();
        if (_cache.TryGetValue(key, out List<OfferDto>? cached) && cached != null)
            return cached;

        try
        {
            var expiration = DateTimeOffset.UtcNow.AddMinutes(4).ToUnixTimeSeconds();
            var baseParams = $"/offer/search?searchString={Uri.EscapeDataString(query)}&page=1&_userId={_userId}&_expiration={expiration}";
            var signature = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(baseParams + _apiKey))).ToLowerInvariant();

            var resp = await _http.GetAsync($"{BaseUrl}{baseParams}&_signature={signature}");
            if (!resp.IsSuccessStatusCode) return new List<OfferDto>();

            var json = await resp.Content.ReadAsStringAsync();
            var offers = ParseOffers(json);

            _cache.Set(key, offers, CacheTtl);
            return offers;
        }
        catch
        {
            // Netværksfejl m.m. må aldrig vælte indkøbslisten — tilbud er "nice to have".
            return new List<OfferDto>();
        }
    }

    /// <summary>
    /// Robust parsing: dokumentationen viser ikke det præcise felt-layout, så vi
    /// prøver de gængse feltnavne og springer over hvad vi ikke forstår.
    /// (Finjusteres når rigtige API-nøgler er på plads og svaret kan inspiceres.)
    /// </summary>
    private static List<OfferDto> ParseOffers(string json)
    {
        var result = new List<OfferDto>();
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        // Svaret kan være et array direkte eller pakket ind i et objekt.
        var array = root.ValueKind == JsonValueKind.Array ? root
            : root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d
            : root.TryGetProperty("offers", out var o) && o.ValueKind == JsonValueKind.Array ? o
            : default;
        if (array.ValueKind != JsonValueKind.Array) return result;

        foreach (var el in array.EnumerateArray().Take(10))
        {
            if (el.ValueKind != JsonValueKind.Object) continue;

            string? Str(params string[] names)
            {
                foreach (var n in names)
                    if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();
                return null;
            }
            decimal? Num(params string[] names)
            {
                foreach (var n in names)
                    if (el.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number)
                        return v.GetDecimal();
                return null;
            }

            var heading = Str("heading", "name", "title", "offerName", "description");
            if (string.IsNullOrWhiteSpace(heading)) continue;

            result.Add(new OfferDto(
                heading,
                Str("description", "text"),
                Num("price", "offerPrice", "priceValue"),
                Str("brand", "store", "storeName", "brandName", "dealer"),
                Str("validUntil", "endDate", "expirationDate", "runTill")));
        }
        return result;
    }
}
