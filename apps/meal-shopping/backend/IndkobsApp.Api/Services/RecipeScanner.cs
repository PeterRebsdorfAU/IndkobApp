using System.Text;
using System.Text.Json;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;

namespace IndkobsApp.Api.Services;

// ---------------------------------------------------------------------------
// AI-scanning af opskrift-billeder: billede ind → struktureret opskrift ud.
//
// Selve kaldet til Google Gemini ligger BAG dette interface, så resten af appen
// (og testene) kan arbejde mod en fake. Kortlægning fra scanner-resultatet til
// vores egne enheder/DTO'er er ren, testbar logik (se RecipeScanUnitMapper +
// RecipeScanMapper) — den kræver ingen netværkskald.
// ---------------------------------------------------------------------------

/// <summary>En rå ingredienslinje som scanneren læste (før enheds-mapping).</summary>
public record ScannedIngredient(string Name, decimal? Quantity, string? Unit);

/// <summary>Rå, struktureret opskrift som scanneren læste fra billedet.</summary>
public record ScannedRecipe(string? Title, int? Servings, List<ScannedIngredient> Ingredients, string? Method);

/// <summary>
/// Læser en opskrift ud af et billede. Implementeringen kalder en ekstern vision-model;
/// uden konfigureret nøgle er featuren slået fra (<see cref="Enabled"/> = false).
/// </summary>
public interface IRecipeScanner
{
    /// <summary>Er scanning tilgængelig? False når ingen API-nøgle er konfigureret (featuren er i dvale).</summary>
    bool Enabled { get; }

    /// <summary>
    /// Analyserer et opskrift-billede og returnerer struktureret data.
    /// Kaster <see cref="InvalidOperationException"/> hvis <see cref="Enabled"/> er false.
    /// </summary>
    Task<ScannedRecipe> ScanAsync(byte[] imageBytes, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Kalder Google Gemini vision-API'et (generativelanguage.googleapis.com) med billedet som
/// <c>inline_data</c> og beder om struktureret JSON (titel, portioner, ingredienser, fremgangsmåde).
///
/// DVALE: Uden <c>Gemini:ApiKey</c> er <see cref="Enabled"/> false, og <see cref="ScanAsync"/>
/// kaldes aldrig (endpointet svarer 503). En rigtig nøgle sættes KUN som env-var
/// <c>Gemini__ApiKey</c> i produktion — commit den aldrig.
/// </summary>
public sealed class GeminiRecipeScanner : IRecipeScanner
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<GeminiRecipeScanner> _log;
    private readonly string? _apiKey;
    private readonly string _model;

    public GeminiRecipeScanner(IHttpClientFactory httpFactory, IConfiguration cfg, ILogger<GeminiRecipeScanner> log)
    {
        _httpFactory = httpFactory;
        _log = log;
        _apiKey = cfg["Gemini:ApiKey"];
        // Default-model. Model-navne hos Google skifter jævnligt — sæt Gemini__Model til det
        // Flash-vision-model-id du ser i Google AI Studio (fx "gemini-3-flash-preview").
        _model = string.IsNullOrWhiteSpace(cfg["Gemini:Model"]) ? "gemini-flash-latest" : cfg["Gemini:Model"]!;
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_apiKey);

    // Prompten der styrer modellen. Vi beder eksplicit om dansk + kun de felter vi kan bruge.
    private const string Prompt =
        "Du er en hjælper der læser en madopskrift fra et billede. " +
        "Udtræk opskriftens titel, antal portioner, ingredienslisten og fremgangsmåden. " +
        "For hver ingrediens: angiv navn, mængde (tal) og enhed (fx g, kg, ml, l, spsk, tsk, dåse, pakke, stk, fed). " +
        "Svar KUN med JSON efter det angivne skema. Brug dansk. Udelad felter du ikke kan læse.";

    public async Task<ScannedRecipe> ScanAsync(byte[] imageBytes, string contentType, CancellationToken ct = default)
    {
        if (!Enabled)
            throw new InvalidOperationException("Opskrift-scanning er ikke konfigureret (Gemini:ApiKey mangler).");

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = Prompt },
                        new { inline_data = new { mime_type = contentType, data = Convert.ToBase64String(imageBytes) } }
                    }
                }
            },
            generationConfig = new
            {
                // Tving struktureret JSON, så vi slipper for at parse fri tekst.
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string" },
                        servings = new { type = "integer" },
                        ingredients = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    quantity = new { type = "number" },
                                    unit = new { type = "string" }
                                }
                            }
                        },
                        method = new { type = "string" }
                    }
                }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        // Nøglen sendes som header (ikke i URL'en, så den ikke havner i logs).
        request.Headers.Add("x-goog-api-key", _apiKey);

        var client = _httpFactory.CreateClient(nameof(GeminiRecipeScanner));
        client.Timeout = TimeSpan.FromSeconds(60);

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning("Gemini-kald fejlede: {Status} {Body}", (int)response.StatusCode, payload);
            throw new InvalidOperationException($"Gemini svarede {(int)response.StatusCode}.");
        }

        var json = ExtractModelJson(payload);
        return ParseScannedRecipe(json);
    }

    /// <summary>
    /// Trækker det egentlige JSON-svar ud af Gemini-konvolutten
    /// (candidates[0].content.parts[*].text). Returnerer den samlede tekst.
    /// </summary>
    private static string ExtractModelJson(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var sb = new StringBuilder();
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        sb.Append(text.GetString());
            }
        }
        return sb.ToString();
    }

    /// <summary>Parser det struktuerede JSON-svar til en <see cref="ScannedRecipe"/> (defensivt). Public for at kunne testes uden netværk.</summary>
    public static ScannedRecipe ParseScannedRecipe(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ScannedRecipe(null, null, new List<ScannedIngredient>(), null);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? title = GetString(root, "title");
        string? method = GetString(root, "method");
        int? servings = null;
        if (root.TryGetProperty("servings", out var sv))
        {
            if (sv.ValueKind == JsonValueKind.Number && sv.TryGetInt32(out var s)) servings = s;
            else if (sv.ValueKind == JsonValueKind.String && int.TryParse(sv.GetString(), out var s2)) servings = s2;
        }

        var ingredients = new List<ScannedIngredient>();
        if (root.TryGetProperty("ingredients", out var ings) && ings.ValueKind == JsonValueKind.Array)
        {
            foreach (var it in ings.EnumerateArray())
            {
                if (it.ValueKind != JsonValueKind.Object) continue;
                var name = GetString(it, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                decimal? qty = null;
                if (it.TryGetProperty("quantity", out var q))
                {
                    if (q.ValueKind == JsonValueKind.Number && q.TryGetDecimal(out var d)) qty = d;
                    else if (q.ValueKind == JsonValueKind.String &&
                             decimal.TryParse(q.GetString()?.Replace(',', '.'),
                                 System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out var d2)) qty = d2;
                }

                ingredients.Add(new ScannedIngredient(name!.Trim(), qty, GetString(it, "unit")));
            }
        }

        return new ScannedRecipe(title, servings, ingredients, method);
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// Kortlægger en fri-tekst enhed (dansk/engelsk, ental/flertal) til vores <see cref="Unit"/>-enum.
/// Ukendte/tomme enheder falder tilbage til <see cref="Unit.Stk"/> — brugeren kan rette det
/// i editoren inden opskriften gemmes.
/// </summary>
public static class RecipeScanUnitMapper
{
    private static readonly Dictionary<string, Unit> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = Unit.G, ["gram"] = Unit.G, ["gr"] = Unit.G,
        ["kg"] = Unit.Kg, ["kilo"] = Unit.Kg, ["kilogram"] = Unit.Kg,
        ["ml"] = Unit.Ml, ["milliliter"] = Unit.Ml,
        ["l"] = Unit.L, ["liter"] = Unit.L, ["ltr"] = Unit.L,
        ["spsk"] = Unit.Spsk, ["spiseskefuld"] = Unit.Spsk, ["spiseske"] = Unit.Spsk, ["tbsp"] = Unit.Spsk,
        ["tsk"] = Unit.Tsk, ["teskefuld"] = Unit.Tsk, ["teske"] = Unit.Tsk, ["tsp"] = Unit.Tsk,
        ["dåse"] = Unit.Daase, ["dase"] = Unit.Daase, ["daase"] = Unit.Daase, ["can"] = Unit.Daase,
        ["pakke"] = Unit.Pakke, ["pk"] = Unit.Pakke, ["pakning"] = Unit.Pakke, ["pack"] = Unit.Pakke, ["package"] = Unit.Pakke,
        ["knivspids"] = Unit.Knivspids, ["pinch"] = Unit.Knivspids,
        ["bundt"] = Unit.Bundt, ["bunch"] = Unit.Bundt,
        ["fed"] = Unit.Fed, ["clove"] = Unit.Fed,
        ["stk"] = Unit.Stk, ["styk"] = Unit.Stk, ["stykke"] = Unit.Stk, ["stk."] = Unit.Stk,
        ["piece"] = Unit.Stk, ["pieces"] = Unit.Stk, ["pcs"] = Unit.Stk,
    };

    public static Unit Map(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Unit.Stk;
        // Normalisér: trim, fjern afsluttende punktum, lav flertal om til ental (fjern 'er'/'e'-hale sjældent nødvendigt).
        var key = raw.Trim().TrimEnd('.').ToLowerInvariant();
        if (Table.TryGetValue(key, out var unit)) return unit;
        // Prøv at strippe en dansk/engelsk flertals-endelse (fx "dåser"→"dåse", "pakker"→"pakke",
        // "cloves"→"clove"): fjern afsluttende 'r', 'er', 'e' eller 's' og slå op igen.
        foreach (var suffix in new[] { "r", "er", "e", "s" })
            if (key.EndsWith(suffix) && Table.TryGetValue(key[..^suffix.Length], out var u)) return u;
        return Unit.Stk;
    }
}

/// <summary>
/// Bygger et <see cref="RecipeUpsertDto"/> ud fra scanner-resultatet — samme form som
/// opret/gem-endpointene forventer. GEMMER INTET; brugeren gennemser og gemmer selv.
/// </summary>
public static class RecipeScanMapper
{
    public const int DefaultServings = 4;

    public static RecipeUpsertDto ToUpsert(ScannedRecipe scan)
    {
        var lines = scan.Ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new IngredientLineInputDto(
                null,
                i.Name.Trim(),
                i.Quantity is > 0 ? i.Quantity.Value : 1m,
                RecipeScanUnitMapper.Map(i.Unit)))
            .ToList();

        var servings = scan.Servings is > 0 ? scan.Servings.Value : DefaultServings;
        var name = string.IsNullOrWhiteSpace(scan.Title) ? "" : scan.Title.Trim();
        var method = string.IsNullOrWhiteSpace(scan.Method) ? null : scan.Method.Trim();

        return new RecipeUpsertDto(name, null, servings, lines, method);
    }
}
