using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using IndkobsApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

/// <summary>
/// Anonym adgang til en DELT indkøbsliste via token (ingen login).
/// Modtageren af linket kan se listen og krydse af — intet andet.
/// Tokenet giver kun adgang til præcis dén uges liste.
/// </summary>
[ApiController]
[Route("api/share")]
[AllowAnonymous]
public class ShareController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ShoppingListService _shoppingList;
    public ShareController(AppDbContext db, ShoppingListService shoppingList)
    {
        _db = db;
        _shoppingList = shoppingList;
    }

    private Task<WeekShareToken?> Find(string token) =>
        _db.WeekShareTokens.FirstOrDefaultAsync(t => t.Token == token);

    [HttpGet("{token}")]
    public async Task<ActionResult<ShoppingListDto>> GetList(string token)
    {
        var share = await Find(token);
        if (share == null) return NotFound();
        var list = await _shoppingList.BuildAsync(share.WeekId);
        return list == null ? NotFound() : list;
    }

    [HttpPut("{token}/check")]
    public async Task<IActionResult> SetCheck(string token, CheckLineDto dto)
    {
        var share = await Find(token);
        if (share == null) return NotFound();

        var check = await _db.ShoppingListChecks
            .FirstOrDefaultAsync(c => c.WeekId == share.WeekId && c.LineKey == dto.LineKey);
        if (check == null)
        {
            check = new ShoppingListCheck { WeekId = share.WeekId, LineKey = dto.LineKey, IsChecked = dto.IsChecked };
            _db.ShoppingListChecks.Add(check);
        }
        else
        {
            check.IsChecked = dto.IsChecked;
        }
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
