using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Butiks-siden af ordre-flowet (DEMO). Adgang med fælles butiks-nøgle i header
/// "X-Store-Key" (config Stores:AccessKey) — ikke husstands-login. Butikken ser
/// sine ordrer, pakker linjer og markerer ordren klar.
/// I en rigtig version bliver dette til en separat app (apps/supermarket) med
/// rigtige butiks-konti og medarbejder-roller.
/// </summary>
[ApiController]
[Route("api/store")]
[AllowAnonymous]
public class StoreController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public StoreController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    private bool KeyOk()
    {
        var expected = _cfg["Stores:AccessKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return Request.Headers.TryGetValue("X-Store-Key", out var provided)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    /// <summary>Butiksliste (så demo-siden kan vise en dropdown efter nøgle er indtastet).</summary>
    [HttpGet("stores")]
    public ActionResult<IEnumerable<StoreDto>> Stores()
    {
        if (!KeyOk()) return Unauthorized();
        return Ok(OrderMapping.StoreNames(_cfg).Select(n => new StoreDto(n)));
    }

    /// <summary>Aktive ordrer for en butik (nyeste først; afhentede/annullerede skjules).</summary>
    [HttpGet("orders")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> Orders([FromQuery] string store)
    {
        if (!KeyOk()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(store)) return BadRequest("Angiv butik.");

        var orders = await _db.Orders
            .Where(o => o.StoreName == store &&
                        o.Status != OrderStatus.Afhentet && o.Status != OrderStatus.Annulleret)
            .Include(o => o.Lines)
            .OrderBy(o => o.CreatedUtc)
            .ToListAsync();
        return Ok(orders.Select(OrderMapping.Map));
    }

    /// <summary>Marker en linje pakket / ikke på lager. Sætter ordren i gang (Pakkes).</summary>
    [HttpPut("orders/{orderId:int}/lines/{lineId:int}")]
    public async Task<ActionResult<OrderDto>> PackLine(int orderId, int lineId, PackLineDto dto)
    {
        if (!KeyOk()) return Unauthorized();
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return NotFound();
        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null) return NotFound();

        line.IsPacked = dto.IsPacked;
        line.NotAvailable = dto.NotAvailable;
        if (order.Status == OrderStatus.Modtaget) order.Status = OrderStatus.Pakkes;
        await _db.SaveChangesAsync();
        return OrderMapping.Map(order);
    }

    /// <summary>Marker hele ordren klar til afhentning.</summary>
    [HttpPost("orders/{orderId:int}/ready")]
    public async Task<ActionResult<OrderDto>> Ready(int orderId)
    {
        if (!KeyOk()) return Unauthorized();
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return NotFound();
        order.Status = OrderStatus.Klar;
        order.ReadyUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return OrderMapping.Map(order);
    }

    /// <summary>Marker ordren afhentet (fjerner den fra køen).</summary>
    [HttpPost("orders/{orderId:int}/collected")]
    public async Task<ActionResult<OrderDto>> Collected(int orderId)
    {
        if (!KeyOk()) return Unauthorized();
        var order = await _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return NotFound();
        order.Status = OrderStatus.Afhentet;
        await _db.SaveChangesAsync();
        return OrderMapping.Map(order);
    }
}
