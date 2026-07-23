using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Forbruger-siden af ordre-flowet: send ugens indkøbsliste til en butik og
/// følg status (butikken pakker via StoreController).
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ShoppingListService _shoppingList;
    private readonly IConfiguration _cfg;

    public OrdersController(AppDbContext db, ShoppingListService shoppingList, IConfiguration cfg)
    {
        _db = db;
        _shoppingList = shoppingList;
        _cfg = cfg;
    }

    /// <summary>Butikker man kan sende til (fra konfiguration).</summary>
    [HttpGet("stores")]
    public IEnumerable<StoreDto> Stores() => OrderMapping.StoreNames(_cfg).Select(n => new StoreDto(n));

    [HttpGet]
    public async Task<IEnumerable<OrderDto>> GetMine()
    {
        var hid = User.GetHouseholdId();
        var orders = await _db.Orders
            .Where(o => o.HouseholdId == hid)
            .Include(o => o.Lines)
            .OrderByDescending(o => o.CreatedUtc)
            .ToListAsync();
        return orders.Select(OrderMapping.Map);
    }

    /// <summary>Send en uges indkøbsliste (skal-købes-linjer) som ordre til en butik.</summary>
    [HttpPost("from-week/{weekId:int}")]
    public async Task<ActionResult<OrderDto>> CreateFromWeek(int weekId, CreateOrderDto dto)
    {
        var hid = User.GetHouseholdId();
        var week = await _db.Weeks.FirstOrDefaultAsync(w => w.Id == weekId && w.HouseholdId == hid);
        if (week == null) return NotFound();

        if (string.IsNullOrWhiteSpace(dto.StoreName) ||
            !OrderMapping.StoreNames(_cfg).Contains(dto.StoreName))
            return BadRequest("Ukendt butik.");

        var list = await _shoppingList.BuildAsync(weekId);
        if (list == null) return NotFound();

        var household = await _db.Households.FindAsync(hid);

        var order = new Order
        {
            HouseholdId = hid,
            HouseholdName = household?.Name ?? "Husstand",
            StoreName = dto.StoreName,
            Note = dto.Note?.Trim(),
            Status = OrderStatus.Modtaget
        };
        // Snapshot: kun det der reelt skal købes (mængde > 0), med kategori til pakke-rækkefølge.
        foreach (var group in list.Groups)
            foreach (var line in group.Lines.Where(l => l.Quantity > 0))
                order.Lines.Add(new OrderLine
                {
                    Name = line.Name,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    CategoryName = group.CategoryName
                });
        if (order.Lines.Count == 0) return BadRequest("Indkøbslisten er tom — intet at sende.");

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return OrderMapping.Map(order);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancel(int id)
    {
        var hid = User.GetHouseholdId();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.HouseholdId == hid);
        if (order == null) return NotFound();
        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Fælles mapping + butiksliste for ordre-endpoints.</summary>
public static class OrderMapping
{
    public static string[] StoreNames(IConfiguration cfg) =>
        cfg.GetSection("Stores:Names").Get<string[]>() ?? new[] { "Lokal Købmand" };

    public static OrderDto Map(Order o) => new(
        o.Id, o.HouseholdName, o.StoreName, o.Status.ToString(), o.Note,
        o.CreatedUtc.ToString("o"), o.ReadyUtc?.ToString("o"),
        o.Lines
            .OrderBy(l => l.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => new OrderLineDto(l.Id, l.Name, l.Quantity, l.Unit, l.CategoryName, l.IsPacked, l.NotAvailable))
            .ToList());
}
