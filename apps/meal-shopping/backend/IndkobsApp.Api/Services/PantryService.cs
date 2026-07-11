using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Fælles lager-aritmetik: læg til (med merge af forenelige enheder) og træk fra
/// (fx når en ret markeres "lavet"). Kalderen er ansvarlig for SaveChanges.
/// </summary>
public class PantryService
{
    private readonly AppDbContext _db;
    public PantryService(AppDbContext db) => _db = db;

    /// <summary>
    /// Læg en mængde på lageret. Findes varen med forenelig enhed (samme måle-familie,
    /// eller samme count-enhed), lægges mængderne sammen i én linje.
    /// </summary>
    public async Task<PantryItem> AddOrMergeAsync(int householdId, int ingredientId, decimal quantity, Unit unit)
    {
        var family = UnitMath.FamilyOf(unit);
        var existing = await _db.PantryItems
            .Where(p => p.HouseholdId == householdId && p.IngredientId == ingredientId)
            .ToListAsync();

        var target = existing.FirstOrDefault(p =>
            family == MeasureFamily.Count
                ? p.Unit == unit
                : UnitMath.FamilyOf(p.Unit) == family);

        if (target != null)
        {
            if (family == MeasureFamily.Count)
            {
                target.Quantity += quantity;
            }
            else
            {
                var sumBase = UnitMath.ToBase(target.Quantity, target.Unit) + UnitMath.ToBase(quantity, unit);
                (target.Quantity, target.Unit) = UnitMath.FromBase(sumBase, family);
            }
        }
        else
        {
            target = new PantryItem { HouseholdId = householdId, IngredientId = ingredientId, Quantity = quantity, Unit = unit };
            _db.PantryItems.Add(target);
        }
        return target;
    }

    /// <summary>
    /// Træk en mængde fra lageret (aldrig under 0; tomme linjer fjernes).
    /// Kun forenelige enheder rammes — samme regler som indkøbslistens afstemning.
    /// </summary>
    public async Task ConsumeAsync(int householdId, int ingredientId, decimal quantity, Unit unit)
    {
        var family = UnitMath.FamilyOf(unit);
        var rows = await _db.PantryItems
            .Where(p => p.HouseholdId == householdId && p.IngredientId == ingredientId)
            .ToListAsync();

        var remainingBase = UnitMath.ToBase(quantity, unit);
        foreach (var row in rows)
        {
            if (remainingBase <= 0) break;
            var compatible = family == MeasureFamily.Count
                ? row.Unit == unit
                : UnitMath.FamilyOf(row.Unit) == family;
            if (!compatible) continue;

            var rowBase = UnitMath.ToBase(row.Quantity, row.Unit);
            var take = Math.Min(rowBase, remainingBase);
            remainingBase -= take;
            var newBase = rowBase - take;

            if (newBase <= 0)
            {
                _db.PantryItems.Remove(row); // brugt op
            }
            else if (family == MeasureFamily.Count)
            {
                row.Quantity = newBase;
            }
            else
            {
                (row.Quantity, row.Unit) = UnitMath.FromBase(newBase, family);
            }
        }
        // Var der ikke nok på lager, stopper vi bare ved 0 — vi går ikke i minus.
    }
}
