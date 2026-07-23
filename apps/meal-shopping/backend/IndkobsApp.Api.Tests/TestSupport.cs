using IndkobsApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Tests;

/// <summary>
/// Fælles hjælpere til test: opretter en isoleret in-memory <see cref="AppDbContext"/>.
/// Hver test får sin egen navngivne database (via Guid), så tests ikke deler tilstand
/// og kan køre parallelt. Al kernelogik testes mod EF Core's in-memory-provider —
/// vi rører ikke en rigtig PostgreSQL.
/// </summary>
internal static class TestDb
{
    /// <summary>En helt frisk, tom kontekst med sit eget database-navn.</summary>
    public static AppDbContext NewContext(out string dbName)
    {
        dbName = Guid.NewGuid().ToString();
        return Open(dbName);
    }

    /// <summary>Åbn en (evt. ny) kontekst mod en navngiven in-memory-database.</summary>
    public static AppDbContext Open(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            // In-memory-provideren advarer om at transaktioner ignoreres; det er uden
            // betydning for de rene logik-tests, så vi undertrykker støjen.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
