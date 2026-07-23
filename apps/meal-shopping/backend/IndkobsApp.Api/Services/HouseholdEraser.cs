using IndkobsApp.Api.Data;
using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Services;

/// <summary>
/// Sletter en husstand og ALT dens data i afhængighedsorden. Fælles kilde for både
/// admin-sletning (<see cref="Controllers.AdminController"/>) og brugerens egen
/// GDPR-sletning (<see cref="Controllers.PrivacyController"/>), så cascade-logikken
/// kun findes ét sted og de to aldrig kommer ud af sync.
/// </summary>
public static class HouseholdEraser
{
    /// <summary>
    /// Fjerner husstanden med <paramref name="householdId"/> og alle dens rækker.
    /// Kalderen skal have verificeret at husstanden findes/må slettes.
    /// </summary>
    public static async Task EraseAsync(AppDbContext db, int householdId)
    {
        // Slet først alt der refererer ingredienser (FK = Restrict), så husstandens
        // varebank/kategorier, og til sidst selve husstanden. Øvrige relationer
        // (uge-indhold, checks, delings-tokens, katalog-snapshots, ordrer) kaskaderer.
        db.Users.RemoveRange(db.Users.Where(u => u.HouseholdId == householdId));
        db.Recipes.RemoveRange(db.Recipes.Where(r => r.HouseholdId == householdId));
        db.ItemGroups.RemoveRange(db.ItemGroups.Where(g => g.HouseholdId == householdId));
        db.Weeks.RemoveRange(db.Weeks.Where(w => w.HouseholdId == householdId));
        db.PantryItems.RemoveRange(db.PantryItems.Where(p => p.HouseholdId == householdId));
        db.HouseholdTasks.RemoveRange(db.HouseholdTasks.Where(t => t.HouseholdId == householdId));
        await db.SaveChangesAsync();

        db.Ingredients.RemoveRange(db.Ingredients.Where(i => i.HouseholdId == householdId));
        db.Categories.RemoveRange(db.Categories.Where(c => c.HouseholdId == householdId));
        await db.SaveChangesAsync();

        var household = await db.Households.FindAsync(householdId);
        if (household != null)
        {
            db.Households.Remove(household);
            await db.SaveChangesAsync();
        }
    }
}
