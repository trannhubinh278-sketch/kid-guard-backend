using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhuBinh_2921.Models;

namespace NhuBinh_2921.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PostsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public PostsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Post>>> GetAll() =>
        Ok(await _context.Posts.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Post>> GetById(int id)
    {
        var post = await _context.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return post == null ? NotFound() : Ok(post);
    }

    [Authorize(Roles = "Admin,User")]
    [HttpPost]
    public async Task<ActionResult<Post>> Create(Post post)
    {
        post.Id = 0;
        post.CreatedAtUtc = DateTime.UtcNow;
        post.UserName = User.Identity?.Name ?? post.UserName;
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = post.Id }, post);
    }

    [Authorize(Roles = "Admin,User")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Post post)
    {
        if (id != post.Id)
            return BadRequest(new { message = "Id trên URL không khớp dữ liệu." });
        if (!await _context.Posts.AnyAsync(p => p.Id == id))
            return NotFound();

        _context.Entry(post).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin,User")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null)
            return NotFound();

        _context.Posts.Remove(post);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
