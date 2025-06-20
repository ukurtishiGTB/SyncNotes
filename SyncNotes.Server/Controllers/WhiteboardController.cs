using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SyncNotes.Server.Data;
using SyncNotes.Shared.Models;

[ApiController]
[Route("api/[controller]")]
public class WhiteboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public WhiteboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetElements(string userId)
    {
        var elements = await _context.WhiteboardElements
            .Where(w => w.UserId == userId)
            .Include(w => w.Points)
            .ToListAsync();

        return Ok(elements);
    }

    [HttpPost]
    public async Task<IActionResult> SaveElement(WhiteboardElement element)
    {
        var existing = await _context.WhiteboardElements
            .Include(w => w.Points)
            .FirstOrDefaultAsync(w => w.Id == element.Id);

        if (existing == null)
        {
            _context.WhiteboardElements.Add(element);
        }
        else
        {
            existing.Color = element.Color;
            existing.StrokeWidth = element.StrokeWidth;
            existing.Text = element.Text;
            existing.Points = element.Points;
            existing.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(element);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteElement(string id)
    {
        var element = await _context.WhiteboardElements.FindAsync(id);
        if (element == null) return NotFound();

        _context.WhiteboardElements.Remove(element);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("clear/{userId}")]
    public async Task<IActionResult> ClearWhiteboard(string userId)
    {
        var elements = _context.WhiteboardElements.Where(w => w.UserId == userId);
        _context.WhiteboardElements.RemoveRange(elements);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}