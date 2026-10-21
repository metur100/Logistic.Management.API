using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[Route("api/support-tickets")]
[Authorize]
public class SupportTicketsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public SupportTicketsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var q = _db.SupportTickets
            .AsNoTracking()
            .Include(t => t.Company)
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!IsAdmin)
        {
            var cid = CurrentCompanyId ?? 0;
            q = q.Where(t => t.CompanyId == cid);
            if (IsDriver) q = q.Where(t => t.CreatedByUserId == CurrentUserId);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            q = q.Where(t => t.Status == status);

        var result = await q
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketDto(
                t.Id, t.Subject, t.Description, t.Status, t.Priority,
                t.CompanyId, t.Company != null ? t.Company.Name : null,
                t.CreatedByUserId, t.CreatedBy != null ? t.CreatedBy.FullName : null,
                t.AssignedToUserId, t.AssignedTo != null ? t.AssignedTo.FullName : null,
                t.Resolution, t.CreatedAt, t.UpdatedAt, t.ResolvedAt
            )).ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.SupportTickets
            .AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.CreatedBy)
            .Include(x => x.AssignedTo)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (t == null) return NotFound();
        if (!IsAdmin && t.CompanyId != (CurrentCompanyId ?? 0)) return Forbid();
        if (IsDriver && t.CreatedByUserId != CurrentUserId) return Forbid();

        return Ok(new SupportTicketDto(
            t.Id, t.Subject, t.Description, t.Status, t.Priority,
            t.CompanyId, t.Company?.Name,
            t.CreatedByUserId, t.CreatedBy?.FullName,
            t.AssignedToUserId, t.AssignedTo?.FullName,
            t.Resolution, t.CreatedAt, t.UpdatedAt, t.ResolvedAt
        ));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSupportTicketDto dto)
    {
        if (IsAdmin)
            return BadRequest(new { message = "Administrator accounts cannot create company support tickets." });

        var priority = dto.Priority is "Low" or "High" ? dto.Priority : "Medium";
        var ticket = new SupportTicket
        {
            CompanyId = CurrentCompanyId ?? 0,
            Subject = dto.Subject.Trim(),
            Description = dto.Description.Trim(),
            Priority = priority,
            CreatedByUserId = CurrentUserId,
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, new { ticket.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateSupportTicketDto dto)
    {
        var ticket = await _db.SupportTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        if (dto.Status is not ("Open" or "InProgress" or "Resolved" or "Closed"))
            return BadRequest(new { message = "Invalid ticket status." });

        ticket.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Priority)) ticket.Priority = dto.Priority;
        ticket.AssignedToUserId = dto.AssignedToUserId ?? CurrentUserId;
        ticket.Resolution = dto.Resolution?.Trim();
        ticket.UpdatedAt = DateTime.UtcNow;

        if ((dto.Status == "Resolved" || dto.Status == "Closed") && ticket.ResolvedAt == null)
            ticket.ResolvedAt = DateTime.UtcNow;
        if (dto.Status is "Open" or "InProgress")
            ticket.ResolvedAt = null;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
