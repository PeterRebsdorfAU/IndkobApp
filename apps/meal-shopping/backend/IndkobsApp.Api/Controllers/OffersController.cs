using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Tilbud fra Tilbudsdata.dk: fri søgning + match mod ugens indkøbsliste
/// ("hakket oksekød er på tilbud i ..."). Virker kun når API-adgang er
/// konfigureret — ellers melder /status configured=false.
/// </summary>
[ApiController]
[Route("api/offers")]
[Authorize]
public class OffersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TilbudsdataClient _client;
    private readonly ShoppingListService _shoppingList;

    public OffersController(AppDbContext db, TilbudsdataClient client, ShoppingListService shoppingList)
    {
        _db = db;
        _client = client;
        _shoppingList = shoppingList;
    }

    [HttpGet("status")]
    public OffersStatusDto Status() => new(_client.IsConfigured);

    [HttpGet("search")]
    public async Task<ActionResult<List<OfferDto>>> Search([FromQuery] string q)
    {
        if (!_client.IsConfigured) return StatusCode(503, "Tilbuds-API er ikke konfigureret.");
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("Angiv et søgeord.");
        return await _client.SearchAsync(q.Trim());
    }

    /// <summary>
    /// Match ugens indkøbsliste mod tilbudene: søger pr. vare der mangler at blive
    /// købt (max 15 varer pr. kald for at skåne API'et) og returnerer fundne tilbud.
    /// </summary>
    [HttpGet("match/{weekId:int}")]
    public async Task<ActionResult<List<OfferMatchDto>>> Match(int weekId)
    {
        if (!_client.IsConfigured) return StatusCode(503, "Tilbuds-API er ikke konfigureret.");

        var hid = User.GetHouseholdId();
        var owns = await _db.Weeks.AnyAsync(w => w.Id == weekId && w.HouseholdId == hid);
        if (!owns) return NotFound();

        var list = await _shoppingList.BuildAsync(weekId);
        if (list == null) return NotFound();

        // Kun varer der reelt mangler at blive købt.
        var names = list.Groups.SelectMany(g => g.Lines)
            .Where(l => !l.IsChecked && l.Quantity > 0)
            .Select(l => l.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15);

        var result = new List<OfferMatchDto>();
        foreach (var name in names)
        {
            var offers = await _client.SearchAsync(name);
            if (offers.Count > 0)
                result.Add(new OfferMatchDto(name, offers.Take(3).ToList()));
        }
        return result;
    }
}
