using System.Net.Http;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for AI-scanning-logikken der IKKE rører nettet: enheds-mapping fra fri tekst til
/// vores de KENDTE enheder, kortlægning af et scanner-resultat til et RecipeUpsert-DTO,
/// samt at det hele fungerer bag <see cref="IRecipeScanner"/> med en fake (selve Gemini-kaldet
/// er isoleret bag interfacet). Live-kaldet testes af ejeren med en rigtig nøgle.
/// </summary>
public class RecipeScanTests
{
    [Theory]
    [InlineData("g", Units.G)]
    [InlineData("gram", Units.G)]
    [InlineData("Gram", Units.G)]
    [InlineData("kg", Units.Kg)]
    [InlineData("kilo", Units.Kg)]
    [InlineData("ml", Units.Ml)]
    [InlineData("milliliter", Units.Ml)]
    [InlineData("l", Units.L)]
    [InlineData("liter", Units.L)]
    [InlineData("spsk", Units.Spsk)]
    [InlineData("spiseskefuld", Units.Spsk)]
    [InlineData("tbsp", Units.Spsk)]
    [InlineData("tsk", Units.Tsk)]
    [InlineData("tsp", Units.Tsk)]
    [InlineData("dåse", Units.Daase)]
    [InlineData("dåser", Units.Daase)]
    [InlineData("daase", Units.Daase)]
    [InlineData("pakke", Units.Pakke)]
    [InlineData("pk", Units.Pakke)]
    [InlineData("knivspids", Units.Knivspids)]
    [InlineData("bundt", Units.Bundt)]
    [InlineData("fed", Units.Fed)]
    [InlineData("clove", Units.Fed)]
    [InlineData("stk", Units.Stk)]
    [InlineData("stk.", Units.Stk)]
    [InlineData("stykke", Units.Stk)]
    [InlineData("piece", Units.Stk)]
    public void Map_kortlægger_kendte_enheder(string raw, string expected)
    {
        Assert.Equal(expected, RecipeScanUnitMapper.Map(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Map_falder_tilbage_til_Stk_for_tomt(string? raw)
    {
        Assert.Equal(Units.Stk, RecipeScanUnitMapper.Map(raw));
    }

    [Theory]
    [InlineData("håndfuld", "håndfuld")] // ukendt dansk enhed — bevares nu som fri tekst
    [InlineData("blob", "blob")]         // ukendt — bevares (renset/trimmet)
    [InlineData("  Glas  ", "Glas")]     // trimmes, men egen skrivemåde bevares
    public void Map_bevarer_ukendt_enhed_som_fri_tekst(string raw, string expected)
    {
        Assert.Equal(expected, RecipeScanUnitMapper.Map(raw));
    }

    [Fact]
    public void ToUpsert_bygger_upsert_med_mappede_enheder_og_defaults()
    {
        var scan = new ScannedRecipe(
            "  Spaghetti  ", 2,
            new List<ScannedIngredient>
            {
                new("Hakket oksekød", 500m, "gram"),
                new("Løg", 1m, "stk"),
                new("Salt", null, "knivspids"),
                new("   ", 3m, "g"), // tom navn — skal frafiltreres
            },
            "  Brun kødet. Tilsæt løg.  ");

        var dto = RecipeScanMapper.ToUpsert(scan);

        Assert.Equal("Spaghetti", dto.Name);
        Assert.Equal(2, dto.Servings);
        Assert.Equal("Brun kødet. Tilsæt løg.", dto.Method);
        Assert.Equal(3, dto.Ingredients.Count); // den tomme linje er filtreret fra

        Assert.Equal("Hakket oksekød", dto.Ingredients[0].IngredientName);
        Assert.Equal(500m, dto.Ingredients[0].Quantity);
        Assert.Equal(Units.G, dto.Ingredients[0].Unit);

        Assert.Equal(Units.Stk, dto.Ingredients[1].Unit);

        // Manglende mængde → 1 som fornuftig default (brugeren kan rette).
        Assert.Equal(1m, dto.Ingredients[2].Quantity);
        Assert.Equal(Units.Knivspids, dto.Ingredients[2].Unit);
    }

    [Fact]
    public void ToUpsert_bruger_default_portioner_når_ingen_er_læst()
    {
        var scan = new ScannedRecipe("Pandekager", null, new List<ScannedIngredient>(), null);
        var dto = RecipeScanMapper.ToUpsert(scan);
        Assert.Equal(RecipeScanMapper.DefaultServings, dto.Servings);
        Assert.Null(dto.Method);
    }

    [Fact]
    public void ParseScannedRecipe_læser_gyldigt_model_json()
    {
        const string json = """
            {
              "title": "Tomatsuppe",
              "servings": 4,
              "ingredients": [
                { "name": "Tomater", "quantity": 400, "unit": "g" },
                { "name": "Fløde", "quantity": "1", "unit": "dl" }
              ],
              "method": "Kog og blend."
            }
            """;

        var scan = GeminiRecipeScanner.ParseScannedRecipe(json);

        Assert.Equal("Tomatsuppe", scan.Title);
        Assert.Equal(4, scan.Servings);
        Assert.Equal(2, scan.Ingredients.Count);
        Assert.Equal("Tomater", scan.Ingredients[0].Name);
        Assert.Equal(400m, scan.Ingredients[0].Quantity);
        Assert.Equal(1m, scan.Ingredients[1].Quantity); // mængde som streng parses
    }

    [Fact]
    public void ParseScannedRecipe_er_robust_over_for_tomt_svar()
    {
        var scan = GeminiRecipeScanner.ParseScannedRecipe("");
        Assert.Null(scan.Title);
        Assert.Empty(scan.Ingredients);
    }

    // ---- DVALE: uden Gemini-nøgle er featuren slået fra ------------------------------------
    private sealed class NoopHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    private static GeminiRecipeScanner Build(string? apiKey)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = apiKey })
            .Build();
        return new GeminiRecipeScanner(new NoopHttpClientFactory(), cfg, NullLogger<GeminiRecipeScanner>.Instance);
    }

    [Fact]
    public async Task Uden_nøgle_er_scanning_slået_fra()
    {
        var scanner = Build(apiKey: null);
        Assert.False(scanner.Enabled);
        // ScanAsync må aldrig røre nettet når featuren er i dvale — den kaster i stedet.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanAsync(new byte[] { 1 }, "image/jpeg"));
    }

    [Fact]
    public void Med_nøgle_er_scanning_slået_til()
    {
        Assert.True(Build(apiKey: "en-hemmelig-nøgle").Enabled);
    }

    /// <summary>Fake bag <see cref="IRecipeScanner"/>: beviser at kaldet kan isoleres i test uden net.</summary>
    private sealed class FakeRecipeScanner : IRecipeScanner
    {
        private readonly ScannedRecipe _result;
        public FakeRecipeScanner(ScannedRecipe result) => _result = result;
        public bool Enabled => true;
        public Task<ScannedRecipe> ScanAsync(byte[] imageBytes, string contentType, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    [Fact]
    public async Task Fake_scanner_flow_giver_klart_upsert_til_gennemsyn()
    {
        IRecipeScanner scanner = new FakeRecipeScanner(new ScannedRecipe(
            "Boller i karry", 4,
            new List<ScannedIngredient> { new("Kødboller", 500m, "g"), new("Karry", 2m, "spsk") },
            "Kog boller. Rør karrysauce."));

        var scanned = await scanner.ScanAsync(new byte[] { 1, 2, 3 }, "image/jpeg");
        var dto = RecipeScanMapper.ToUpsert(scanned);

        Assert.Equal("Boller i karry", dto.Name);
        Assert.Equal(2, dto.Ingredients.Count);
        Assert.Equal(Units.Spsk, dto.Ingredients[1].Unit);
    }
}
