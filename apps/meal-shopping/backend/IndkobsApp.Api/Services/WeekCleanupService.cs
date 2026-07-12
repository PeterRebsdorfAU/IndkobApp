using IndkobsApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Automatisk oprydning: sletter uger (på tværs af alle husstande) der er ældre end
/// et antal uger (default 5, konfigurerbart via Cleanup:WeekRetentionWeeks), så
/// databasen ikke vokser unødigt. Sletning kaskaderer til ugens retter, varegrupper,
/// løse varer, afkrydsninger og delings-tokens — opskrifter/lager røres IKKE.
///
/// Kørsel: ved app-opstart + opportunistisk (throttlet) når ugelisten hentes.
/// (Gratis-hosting har ingen baggrundsjobs; instansen vågner alligevel ved hver brug.)
/// </summary>
public class WeekCleanupService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<WeekCleanupService> _log;

    // Throttle på tværs af requests (statisk = pr. proces).
    private static DateTime _lastRunUtc = DateTime.MinValue;
    private static readonly TimeSpan MinInterval = TimeSpan.FromHours(6);

    public WeekCleanupService(AppDbContext db, IConfiguration cfg, ILogger<WeekCleanupService> log)
    {
        _db = db;
        _cfg = cfg;
        _log = log;
    }

    /// <summary>Kør oprydningen hvis der er gået mindst 6 timer siden sidst.</summary>
    public async Task RunIfDueAsync()
    {
        if (DateTime.UtcNow - _lastRunUtc < MinInterval) return;
        await RunAsync();
    }

    public async Task RunAsync()
    {
        _lastRunUtc = DateTime.UtcNow;

        var retentionWeeks = _cfg.GetValue<int?>("Cleanup:WeekRetentionWeeks") ?? 5;
        if (retentionWeeks < 1) return; // 0/negativ = oprydning slået fra

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var currentMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var cutoffMonday = currentMonday.AddDays(-retentionWeeks * 7);

        // Uge-tabellen er lille — beregn aldre i hukommelsen (ISO-uge-aritmetik er grim i SQL).
        var weeks = await _db.Weeks.ToListAsync();
        var old = weeks.Where(w => IsoWeekMonday(w.Year, w.WeekNumber) < cutoffMonday).ToList();
        if (old.Count == 0) return;

        _db.Weeks.RemoveRange(old); // kaskaderer til ugens indhold + checks + delings-tokens
        await _db.SaveChangesAsync();
        _log.LogInformation("Uge-oprydning: slettede {Count} uge(r) ældre end {Weeks} uger.", old.Count, retentionWeeks);
    }

    /// <summary>Mandagen i en given ISO-uge (uge 1 = ugen der indeholder 4. januar).</summary>
    internal static DateOnly IsoWeekMonday(int year, int week)
    {
        var jan4 = new DateOnly(year, 1, 4);
        var week1Monday = jan4.AddDays(-(((int)jan4.DayOfWeek + 6) % 7));
        return week1Monday.AddDays((week - 1) * 7);
    }
}
