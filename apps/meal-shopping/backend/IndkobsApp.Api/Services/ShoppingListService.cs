using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Bygger den aggregerede, kategori-sorterede indkøbsliste for en uge.
/// </summary>
public class ShoppingListService
{
    private readonly AppDbContext _db;
    public ShoppingListService(AppDbContext db) => _db = db;

    // Pseudo-kategori til løse fritekst-varer og ingredienser uden kategori.
    private const string MiscCategoryName = "Andet / løse varer";
    private const int MiscSortOrder = int.MaxValue;

    /// <summary>
    /// En linje under opbygning. Mængden akkumuleres i basisenheden (g/ml) for
    /// masse/volumen, eller i selve count-enheden for stk/pakke/osv.
    /// </summary>
    private sealed class Accumulator
    {
        public required string LineKey { get; init; }
        public int? IngredientId { get; init; }
        public required string Name { get; init; }
        public int? CategoryId { get; init; }
        public required string CategoryName { get; init; }
        public int CategorySort { get; init; }
        public bool IsManual { get; init; }
        public MeasureFamily Family { get; init; }
        public string CountUnit { get; init; } = Units.Default; // kun relevant for Family == Count (vist tekst)
        public decimal AccumulatedBase { get; set; }
        public bool IsChecked { get; set; }
        public HashSet<string> Sources { get; } = new();
    }

    public async Task<ShoppingListDto?> BuildAsync(int weekId)
    {
        var week = await _db.Weeks.FirstOrDefaultAsync(w => w.Id == weekId);
        if (week == null) return null;

        // Hent alle bidrag med tilhørende ingrediens + kategori.
        var weekRecipes = await _db.WeekRecipes
            .Where(wr => wr.WeekId == weekId)
            .Include(wr => wr.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category)
            .ToListAsync();

        var weekGroups = await _db.WeekItemGroups
            .Where(wg => wg.WeekId == weekId)
            .Include(wg => wg.ItemGroup).ThenInclude(g => g.Ingredients).ThenInclude(gi => gi.Ingredient).ThenInclude(i => i.Category)
            .ToListAsync();

        var manualItems = await _db.WeekManualItems
            .Where(m => m.WeekId == weekId)
            .Include(m => m.Ingredient).ThenInclude(i => i!.Category)
            .ToListAsync();

        var checks = await _db.ShoppingListChecks
            .Where(c => c.WeekId == weekId)
            .ToDictionaryAsync(c => c.LineKey, c => c.IsChecked);

        var acc = new Dictionary<string, Accumulator>();

        // --- Retter (skaleret efter ønskede portioner) ---
        foreach (var wr in weekRecipes)
        {
            // Skaleringsfaktor: ønskede portioner / basis-portioner (med fornuftig fallback).
            var effective = wr.Servings ?? wr.Recipe.Servings;
            var baseServ = wr.Recipe.Servings <= 0 ? 1 : wr.Recipe.Servings;
            var factor = (decimal)effective / baseServ;

            foreach (var ri in wr.Recipe.Ingredients)
                Add(acc, checks, ri.Ingredient, ri.Quantity * factor, ri.Unit, wr.Recipe.Name);
        }

        // --- Varegrupper (ingen skalering) ---
        foreach (var wg in weekGroups)
            foreach (var gi in wg.ItemGroup.Ingredients)
                Add(acc, checks, gi.Ingredient, gi.Quantity, gi.Unit, wg.ItemGroup.Name);

        // --- Løse varer ---
        foreach (var m in manualItems)
        {
            if (m.Ingredient != null)
                Add(acc, checks, m.Ingredient, m.Quantity, m.Unit, "Manuelt", isManual: true);
            else
                AddFreeText(acc, checks, m.FreeText ?? "(ukendt)", m.Quantity, m.Unit);
        }

        // --- Byg DTO: konverter akkumulerede mængder til visningsenhed og gruppér pr. kategori ---
        var groups = acc.Values
            .GroupBy(a => (a.CategoryId, a.CategoryName, a.CategorySort))
            .Select(g => new ShoppingCategoryGroupDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategorySort,
                g.Select(ToLineDto)
                 .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                 .ThenBy(l => l.Unit)
                 .ToList()))
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ShoppingListDto(week.Id, week.Year, week.WeekNumber, groups);
    }

    private static ShoppingLineDto ToLineDto(Accumulator a)
    {
        decimal qty;
        string unit;

        if (a.Family == MeasureFamily.Count)
        {
            qty = a.AccumulatedBase;
            unit = a.CountUnit;
        }
        else
        {
            // Vælg pæn enhed: fx 1500 g → 1,5 kg, 800 g → 800 g.
            (qty, unit) = UnitMath.FromBase(a.AccumulatedBase, a.Family);
        }

        return new ShoppingLineDto(
            a.LineKey,
            a.IngredientId,
            a.Name,
            Math.Round(qty, 3, MidpointRounding.AwayFromZero),
            unit,
            a.IsChecked,
            a.IsManual,
            a.Sources.OrderBy(s => s).ToList());
    }

    private void Add(Dictionary<string, Accumulator> acc, IReadOnlyDictionary<string, bool> checks,
        Ingredient ingredient, decimal quantity, string unit, string source, bool isManual = false)
    {
        var family = UnitMath.FamilyOf(unit);
        // Linjer slås kun sammen hvis: samme ingrediens OG samme familie (for count desuden
        // samme enhed, case-insensitivt). g↔kg og ml↔l lander i samme bøtte.
        var key = family == MeasureFamily.Count
            ? $"ing:{ingredient.Id}:cnt:{Units.NormalizeKey(unit)}"
            : $"ing:{ingredient.Id}:{family}";

        var a = GetOrCreate(acc, checks, key, () => new Accumulator
        {
            LineKey = key,
            IngredientId = ingredient.Id,
            Name = ingredient.Name,
            CategoryId = ingredient.CategoryId,
            CategoryName = ingredient.Category?.Name ?? MiscCategoryName,
            CategorySort = ingredient.Category?.SortOrder ?? MiscSortOrder,
            IsManual = isManual,
            Family = family,
            CountUnit = unit
        });

        a.AccumulatedBase += UnitMath.ToBase(quantity, unit);
        a.Sources.Add(source);
    }

    private void AddFreeText(Dictionary<string, Accumulator> acc, IReadOnlyDictionary<string, bool> checks,
        string text, decimal quantity, string unit)
    {
        var name = text.Trim();
        var normalized = name.ToLowerInvariant();
        var family = UnitMath.FamilyOf(unit);
        var key = family == MeasureFamily.Count
            ? $"txt:{normalized}:cnt:{Units.NormalizeKey(unit)}"
            : $"txt:{normalized}:{family}";

        var a = GetOrCreate(acc, checks, key, () => new Accumulator
        {
            LineKey = key,
            IngredientId = null,
            Name = name,
            CategoryId = null,
            CategoryName = MiscCategoryName,
            CategorySort = MiscSortOrder,
            IsManual = true,
            Family = family,
            CountUnit = unit
        });

        a.AccumulatedBase += UnitMath.ToBase(quantity, unit);
        a.Sources.Add("Manuelt");
    }

    private static Accumulator GetOrCreate(Dictionary<string, Accumulator> acc,
        IReadOnlyDictionary<string, bool> checks, string key, Func<Accumulator> factory)
    {
        if (!acc.TryGetValue(key, out var a))
        {
            a = factory();
            a.IsChecked = checks.TryGetValue(key, out var c) && c;
            acc[key] = a;
        }
        return a;
    }
}
