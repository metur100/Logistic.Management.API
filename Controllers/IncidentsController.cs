using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LogisticsAPI.Data;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public IncidentsController(AppDbContext db) => _db = db;

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var q = _db.Incidents
            .Include(i => i.Trip)
            .Include(i => i.Reporter)
            .AsQueryable();

        if (User.IsInRole("Driver"))
            q = q.Where(i => i.ReportedByUserId == CurrentUserId);

        var result = await q
            .OrderByDescending(i => i.ReportedAt)
            .Select(i => new {
                i.Id,
                i.TripId,
                TripNumber = i.Trip != null ? i.Trip.TripNumber : null,
                i.Type,
                i.Description,
                i.Severity,
                i.Status,
                i.Location,
                i.ReportedAt,
                i.ResolvedAt,
                i.Resolution,
                DriverName = i.Reporter != null ? i.Reporter.FullName : null
            }).ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIncidentDto dto)
    {
        var incident = new Incident
        {
            TripId = dto.TripId,
            Type = dto.Type,
            Description = dto.Description,
            Severity = dto.Severity,
            Location = dto.Location,
            ReportedByUserId = CurrentUserId,
            Status = "Open"
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return Ok(new { incident.Id });
    }

    [HttpPut("{id}/resolve")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveIncidentDto dto)
    {
        var incident = await _db.Incidents.FindAsync(id);
        if (incident == null) return NotFound();

        incident.Status = "Resolved";
        incident.Resolution = dto.Resolution;
        incident.ResolvedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record CreateIncidentDto(
    int? TripId, string Type, string Description,
    string Severity, string? Location
);
public record ResolveIncidentDto(string? Resolution);
