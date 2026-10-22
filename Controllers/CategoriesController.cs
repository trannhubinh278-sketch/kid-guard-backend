using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public CategoriesController(ApplicationDbContext context) => _context = context;

    [Authorize(Roles = "Admin,User")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Category>>> GetAll() =>
        Ok(await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync());

    [Authorize(Roles = "Admin,User")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Category>> GetById(int id)
    {
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return category == null ? NotFound() : Ok(category);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<Category>> Create(Category category)
    {
        category.Id = 0;
        category.Name = category.Name.Trim();
        if (await _context.Categories.AnyAsync(c => c.Name == category.Name))
            return Conflict(new { message = "Tên danh mục đã tồn tại." });

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Category category)
    {
        if (id != category.Id)
            return BadRequest(new { message = "Id trên URL không khớp dữ liệu." });
        if (!await _context.Categories.AnyAsync(c => c.Id == id))
            return NotFound();

        category.Name = category.Name.Trim();
        if (await _context.Categories.AnyAsync(c => c.Name == category.Name && c.Id != id))
            return Conflict(new { message = "Tên danh mục đã tồn tại." });

        _context.Entry(category).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound();

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
