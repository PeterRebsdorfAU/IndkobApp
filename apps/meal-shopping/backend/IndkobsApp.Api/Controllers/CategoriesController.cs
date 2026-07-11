using IndkobsApp.Api.Data;
using IndkobsApp.Api.Dtos;
using IndkobsApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize] // fælles opslagsdata, men kun for indloggede
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IEnumerable<CategoryDto>> GetAll() =>
        await _db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(CategoryDto dto)
    {
        var c = new Category { Name = dto.Name.Trim(), SortOrder = dto.SortOrder };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new CategoryDto(c.Id, c.Name, c.SortOrder));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryDto dto)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        c.Name = dto.Name.Trim();
        c.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        _db.Categories.Remove(c); // ingredienser beholdes, deres CategoryId sættes til null
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
