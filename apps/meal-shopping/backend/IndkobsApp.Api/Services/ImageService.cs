using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Behandler uploadede opskrift-billeder SERVER-SIDE, så vi aldrig gemmer store filer:
/// EXIF-orientering rettes op, billedet nedskaleres til maks. <see cref="MaxDimension"/> px
/// på den længste led, og det re-encodes som JPEG. Kvaliteten sænkes trinvist indtil
/// resultatet er under <see cref="TargetBytes"/> — så en typisk telefon-foto på flere MB
/// ender som ~100–250 KB i databasen (Neon-gratislaget er lille).
///
/// Fordi vi ALTID re-encoder, er dette robust uanset hvad klienten sender: kan billedet
/// ikke afkodes, returneres null (→ 400 i controlleren). ImageSharp er 100% managed kode
/// (ingen native afhængigheder), så det virker uændret i Docker/Linux-imaget.
/// </summary>
public static class ImageService
{
    /// <summary>Længste led efter nedskalering (px). Billeder mindre end dette opskaleres ikke.</summary>
    public const int MaxDimension = 1024;

    /// <summary>Mål-størrelse for det gemte billede (bytes). ~300 KB.</summary>
    public const long TargetBytes = 300 * 1024;

    /// <summary>Alt gemmes som JPEG efter re-encoding.</summary>
    public const string OutputContentType = "image/jpeg";

    // Faldende JPEG-kvaliteter der prøves indtil resultatet er under TargetBytes.
    private static readonly int[] Qualities = { 82, 72, 62, 52, 42 };

    /// <summary>
    /// Læser, nedskalerer og komprimerer et billede fra <paramref name="input"/>.
    /// Returnerer de komprimerede bytes + content-type, eller null hvis strømmen ikke
    /// er et gyldigt/understøttet billede.
    /// </summary>
    public static async Task<(byte[] Bytes, string ContentType)?> ProcessAsync(Stream input, CancellationToken ct = default)
    {
        try
        {
            using var image = await Image.LoadAsync(input, ct);

            // Ret op efter EXIF-orientering (telefonbilleder), så billedet vender rigtigt.
            image.Mutate(x => x.AutoOrient());

            // Nedskalér KUN hvis billedet er større end boksen (aldrig opskalering).
            if (image.Width > MaxDimension || image.Height > MaxDimension)
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                }));

            foreach (var quality in Qualities)
            {
                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = quality }, ct);
                // Accepter så snart vi er under målet — eller på laveste kvalitet uanset.
                if (ms.Length <= TargetBytes || quality == Qualities[^1])
                    return (ms.ToArray(), OutputContentType);
            }

            return null; // uopnåeligt (loopet returnerer altid på sidste kvalitet)
        }
        catch
        {
            // Ugyldigt/ukendt billedformat (eller beskadiget fil).
            return null;
        }
    }
}
