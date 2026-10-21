using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager,Driver")]
public class IncidentsController : ApiControllerBase
{
    private static readonly string[] Types =
    {
        "Breakdown", "Accident", "Delay", "Road Closure", "Cargo Damage",
        "Weather", "Border Delay", "Other"
    };
    private static readonly string[] Severities = { "Low", "Medium", "High" };

    private readonly AppDbContext _db;
    public IncidentsController(AppDbContext db) => _db = db;
    private int Cid => CurrentCompanyId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var q = _db.Incidents
            .AsNoTracking()
            .Include(i => i.Trip)
            .Include(i => i.Reporter)
            .Where(i => i.CompanyId == Cid);

        if (IsDriver) q = q.Where(i => i.ReportedByUserId == CurrentUserId);
        if (!string.IsNullOrWhiteSpace(status) && status != "All") q = q.Where(i => i.Status == status);

        var result = await q
            .OrderByDescending(i => i.ReportedAt)
            .Select(i => new
            {
                i.Id,
                i.TripId,
                TripNumber = i.Trip != null ? i.Trip.TripNumber : null,
                Route = i.Trip != null ? i.Trip.OriginLocation + " → " + i.Trip.DestinationLocation : null,
                i.Type,
                i.Description,
                i.Severity,
                i.Status,
                i.Location,
                i.ReportedAt,
                i.ResolvedAt,
                i.Resolution,
                ReporterName = i.Reporter != null ? i.Reporter.FullName : null
            }).ToListAsync();

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIncidentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
            return BadRequest(new { message = "Describe what happened." });

        var type = Types.Contains(dto.Type) ? dto.Type : "Other";
        var severity = Severities.Contains(dto.Severity) ? dto.Severity : "Medium";

        if (dto.TripId.HasValue)
        {
            var trip = await _db.Trips.FirstOrDefaultAsync(t => t.Id == dto.TripId && t.CompanyId == Cid);
            if (trip == null) return BadRequest(new { message = "Transport not found." });
            if (IsDriver && trip.DriverId != CurrentUserId)
                return Forbid();
        }

        var incident = new Incident
        {
            CompanyId = Cid,
            TripId = dto.TripId,
            Type = type,
            Description = dto.Description.Trim(),
            Severity = severity,
            Location = dto.Location?.Trim(),
            ReportedByUserId = CurrentUserId,
            Status = "Open",
            ReportedAt = DateTime.UtcNow
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();
        return Ok(new { incident.Id });
    }

    [HttpPut("{id:int}/resolve")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveIncidentDto dto)
    {
        var incident = await _db.Incidents.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == Cid);
        if (incident == null) return NotFound();

        incident.Status = "Resolved";
        incident.Resolution = dto.Resolution?.Trim();
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
