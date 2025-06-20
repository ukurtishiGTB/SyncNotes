using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SyncNotes.Server.Data;
using SyncNotes.Shared.Models;
using Microsoft.AspNetCore.Authorization;

namespace SyncNotes.Server.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly AppDbContext _context;

    public NotesController(AppDbContext context)
    {
        _context = context;
    }
    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    [HttpGet("my-notes")]
    public async Task<IActionResult> GetMyNotes()
    {
        var userId = GetCurrentUserId(); // e.g., from JWT or HttpContext
        var notes = await _context.Notes
            .Where(n => n.OwnerId == userId || n.SharedWith.Contains(userId))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpPost]
    public async Task<IActionResult> SaveNote(Note note)
    {
        var existing = await _context.Notes.FindAsync(note.Id);
        if (existing == null)
            _context.Notes.Add(note);
        else
        {
            existing.Content = note.Content;
            existing.LastModified = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(note);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNote(string id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note == null) return NotFound();
        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();
        return NoContent();
    }
    
}