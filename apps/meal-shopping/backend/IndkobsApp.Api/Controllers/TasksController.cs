using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Hjemmets opgaver: engangs-to-dos + gentagne pligter/vedligehold med valgfri
/// tur-rotation. Én motor — se HouseholdTask-entiteten.
/// </summary>
[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    public TasksController(AppDbContext db) => _db = db;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    [HttpGet]
    public async Task<IEnumerable<HouseholdTaskDto>> GetAll()
    {
        var hid = User.GetHouseholdId();
        var tasks = await _db.HouseholdTasks
            .Where(t => t.HouseholdId == hid)
            .OrderBy(t => t.NextDueDate).ThenBy(t => t.CreatedUtc)
            .ToListAsync();
        return tasks.Select(Map);
    }

    /// <summary>Lille opsummering til badge i navigationen (forfaldne pligter + åbne to-dos).</summary>
    [HttpGet("summary")]
    public async Task<TasksSummaryDto> Summary()
    {
        var hid = User.GetHouseholdId();
        var today = Today;
        var overdue = await _db.HouseholdTasks
            .CountAsync(t => t.HouseholdId == hid && t.IntervalDays != null && t.NextDueDate <= today);
        var openTodos = await _db.HouseholdTasks
            .CountAsync(t => t.HouseholdId == hid && t.IntervalDays == null && !t.IsDone);
        return new TasksSummaryDto(overdue, openTodos);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdTaskDto>> Create(HouseholdTaskUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Titel skal udfyldes.");
        if (dto.IntervalDays is < 1) return BadRequest("Interval skal være mindst 1 dag.");

        var task = new HouseholdTask
        {
            HouseholdId = User.GetHouseholdId(),
            Title = dto.Title.Trim(),
            IntervalDays = dto.IntervalDays,
            // Gentagne starter som forfaldne i dag — første "gjort" starter kadencen.
            NextDueDate = dto.IntervalDays != null ? Today : null,
            Assignees = NormalizeAssignees(dto.Assignees)
        };
        _db.HouseholdTasks.Add(task);
        await _db.SaveChangesAsync();
        return Map(task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<HouseholdTaskDto>> Update(int id, HouseholdTaskUpsertDto dto)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Titel skal udfyldes.");
        if (dto.IntervalDays is < 1) return BadRequest("Interval skal være mindst 1 dag.");

        var wasRecurring = task.IntervalDays != null;
        task.Title = dto.Title.Trim();
        task.IntervalDays = dto.IntervalDays;
        task.Assignees = NormalizeAssignees(dto.Assignees);
        if (task.IntervalDays == null) task.NextDueDate = null;               // blev engangs
        else if (!wasRecurring || task.NextDueDate == null) task.NextDueDate = Today; // blev gentagen

        await _db.SaveChangesAsync();
        return Map(task);
    }

    /// <summary>
    /// "Gjort": engangsopgaver afkrydses; gentagne ruller NextDueDate frem fra i dag
    /// og skifter tur til den næste i rotationen.
    /// </summary>
    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<HouseholdTaskDto>> Complete(int id)
    {
        var task = await Find(id);
        if (task == null) return NotFound();

        task.LastCompletedUtc = DateTime.UtcNow;
        if (task.IntervalDays is int interval)
        {
            task.NextDueDate = Today.AddDays(interval);
            var names = SplitAssignees(task.Assignees);
            if (names.Count > 0) task.AssigneeIndex = (task.AssigneeIndex + 1) % names.Count;
        }
        else
        {
            task.IsDone = true;
        }
        await _db.SaveChangesAsync();
        return Map(task);
    }

    /// <summary>Fortryd afkrydsning af en engangsopgave.</summary>
    [HttpPost("{id:int}/uncomplete")]
    public async Task<ActionResult<HouseholdTaskDto>> Uncomplete(int id)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        if (task.IntervalDays != null) return BadRequest("Kun engangsopgaver kan fortrydes her.");
        task.IsDone = false;
        await _db.SaveChangesAsync();
        return Map(task);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await Find(id);
        if (task == null) return NotFound();
        _db.HouseholdTasks.Remove(task);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Hjælpere ----------
    private Task<HouseholdTask?> Find(int id) =>
        _db.HouseholdTasks.FirstOrDefaultAsync(t => t.Id == id && t.HouseholdId == User.GetHouseholdId());

    private static string? NormalizeAssignees(List<string>? names)
    {
        var clean = (names ?? new List<string>())
            .Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
        return clean.Count > 0 ? string.Join(",", clean) : null;
    }

    private static List<string> SplitAssignees(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static HouseholdTaskDto Map(HouseholdTask t)
    {
        var names = SplitAssignees(t.Assignees);
        var current = names.Count > 0 ? names[t.AssigneeIndex % names.Count] : null;
        return new HouseholdTaskDto(
            t.Id, t.Title, t.IntervalDays, t.NextDueDate, names, current, t.IsDone, t.LastCompletedUtc);
    }
}
