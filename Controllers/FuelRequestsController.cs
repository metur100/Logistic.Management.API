using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
namespace LogisticsAPI.Controllers;
[ApiController][Route("api/[controller]")][Authorize]
public class FuelRequestsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FuelRequestsController(AppDbContext db) => _db = db;
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var q = _db.FuelRequests.Include(f => f.Trip).AsQueryable();
        if (User.IsInRole("Driver")) q = q.Where(f => f.DriverId == CurrentUserId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(f => f.Status == status);
        return Ok(await q.OrderByDescending(f => f.RequestedAt).ToListAsync());
    }

    [HttpPost][Authorize(Roles = "Driver")]
    public async Task<IActionResult> Create(CreateFuelRequestDto dto)
    {
        var fr = new FuelRequest { TripId = dto.TripId, DriverId = CurrentUserId,
            LitersRequested = dto.LitersRequested, PumpName = dto.PumpName, Route = dto.Route, Remarks = dto.Remarks };
        _db.FuelRequests.Add(fr); await _db.SaveChangesAsync(); return Ok(fr);
    }

    [HttpPut("{id}/approve")][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Approve(int id, ApproveFuelRequestDto dto)
    {
        var fr = await _db.FuelRequests.FindAsync(id);
        if (fr == null) return NotFound();
        fr.Status = dto.Status; fr.ApprovedByUserId = dto.ApprovedByUserId; fr.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("my-requests")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyRequests()
    {
        var requests = await _db.FuelRequests
            .Include(f => f.Trip)
            .Where(f => f.DriverId == CurrentUserId)  // ← was RequestedByUserId
            .OrderByDescending(f => f.RequestedAt)
            .Select(f => new {
                f.Id,
                AmountLiters = f.LitersRequested,   // ← was AmountLiters
                f.Status,
                Reason = f.Remarks,                  // ← was Reason
                f.RequestedAt,
                ManagerNote = (string?)null,         // ← add to model if needed
                TripRoute = f.Trip != null
                    ? $"{f.Trip.OriginLocation} → {f.Trip.DestinationLocation}"
                    : null
            })
            .ToListAsync();

        return Ok(requests);
    }


}
