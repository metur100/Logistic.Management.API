using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[ApiController]
[Route("api/support-tickets")]
[Authorize]
public class SupportTicketsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SupportTicketsController(AppDbContext db) => _db = db;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var q = _db.SupportTickets
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!User.IsInRole("Admin"))
            q = q.Where(t => t.CreatedByUserId == CurrentUserId);

        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);

        var result = await q
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketDto(
                t.Id, t.Subject, t.Description, t.Status, t.Priority,
                t.CreatedByUserId, t.CreatedBy != null ? t.CreatedBy.FullName : null,
                t.AssignedToUserId, t.AssignedTo != null ? t.AssignedTo.FullName : null,
                t.Resolution, t.CreatedAt, t.UpdatedAt, t.ResolvedAt
            )).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.SupportTickets
            .Include(x => x.CreatedBy)
            .Include(x => x.AssignedTo)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFound();
        if (!User.IsInRole("Admin") && t.CreatedByUserId != CurrentUserId) return Forbid();

        return Ok(new SupportTicketDto(
            t.Id, t.Subject, t.Description, t.Status, t.Priority,
            t.CreatedByUserId, t.CreatedBy != null ? t.CreatedBy.FullName : null,
            t.AssignedToUserId, t.AssignedTo != null ? t.AssignedTo.FullName : null,
            t.Resolution, t.CreatedAt, t.UpdatedAt, t.ResolvedAt
        ));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupportTicketDto dto)
    {
        var ticket = new SupportTicket
        {
            Subject = dto.Subject,
            Description = dto.Description,
            Priority = string.IsNullOrEmpty(dto.Priority) ? "Medium" : dto.Priority,
            CreatedByUserId = CurrentUserId,
            Status = "Open"
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, new { ticket.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateSupportTicketDto dto)
    {
        var ticket = await _db.SupportTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.Status = dto.Status;
        if (!string.IsNullOrEmpty(dto.Priority)) ticket.Priority = dto.Priority;
        ticket.AssignedToUserId = dto.AssignedToUserId;
        ticket.Resolution = dto.Resolution;

        if ((dto.Status == "Resolved" || dto.Status == "Closed") && ticket.ResolvedAt == null)
            ticket.ResolvedAt = DateTime.UtcNow;

        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
