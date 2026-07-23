using IndkobsApp.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Tests for server-side billed-behandlingen: store billeder nedskaleres og re-encodes
/// som JPEG (så DB'en ikke fyldes), små billeder opskaleres ikke, og ugyldigt input afvises.
/// </summary>
public class ImageServiceTests
{
    // Laver et test-billede med et støjmønster (så det ikke komprimerer til ingenting).
    private static byte[] MakePng(int width, int height)
    {
        using var img = new Image<Rgba32>(width, height);
        var rnd = new Random(1234);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Rgba32((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
            }
        });
        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    // JPEG starter altid med SOI-markøren 0xFF 0xD8.
    private static bool IsJpeg(byte[] bytes) => bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static (int Width, int Height) Dimensions(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var info = Image.Identify(ms);
        return (info.Width, info.Height);
    }

    [Fact]
    public async Task Stort_billede_nedskaleres_til_maks_1024px_og_bliver_jpeg()
    {
        using var input = new MemoryStream(MakePng(3000, 2000));

        var result = await ImageService.ProcessAsync(input);

        Assert.NotNull(result);
        Assert.Equal(ImageService.OutputContentType, result!.Value.ContentType);
        Assert.True(IsJpeg(result.Value.Bytes), "Resultatet skal være JPEG");
        var (w, h) = Dimensions(result.Value.Bytes);
        Assert.True(w <= ImageService.MaxDimension && h <= ImageService.MaxDimension,
            $"Forventede maks {ImageService.MaxDimension}px, fik {w}x{h}");
        // Aspect ratio bevares: længste led rammer grænsen.
        Assert.Equal(ImageService.MaxDimension, Math.Max(w, h));
    }

    [Fact]
    public async Task Lille_billede_opskaleres_ikke()
    {
        using var input = new MemoryStream(MakePng(200, 150));

        var result = await ImageService.ProcessAsync(input);

        Assert.NotNull(result);
        var (w, h) = Dimensions(result!.Value.Bytes);
        Assert.Equal(200, w);
        Assert.Equal(150, h);
    }

    [Fact]
    public async Task Ugyldigt_input_giver_null()
    {
        using var input = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        var result = await ImageService.ProcessAsync(input);

        Assert.Null(result);
    }
}
