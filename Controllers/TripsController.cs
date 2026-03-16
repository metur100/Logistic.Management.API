using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
namespace LogisticsAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly AppDbContext _db;
    public TripsController(AppDbContext db) => _db = db;
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] int? driverId)
    {
        var q = _db.Trips.Include(t => t.Driver).Include(t => t.Vehicle).Include(t => t.CargoItems).AsQueryable();
        if (User.IsInRole("Driver")) q = q.Where(t => t.DriverId == CurrentUserId);
        else if (driverId.HasValue) q = q.Where(t => t.DriverId == driverId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);
        return Ok(await q.OrderByDescending(t => t.CreatedAt).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var trip = await _db.Trips.Include(t => t.Driver).Include(t => t.Vehicle)
            .Include(t => t.CargoItems).Include(t => t.StatusHistory)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();
        if (User.IsInRole("Driver") && trip.DriverId != CurrentUserId) return Forbid();
        return Ok(trip);
    }

    [HttpPost][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create(CreateTripDto dto)
    {
        var trip = new Trip
        {
            TripNumber = $"TRP-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DriverId = dto.DriverId, VehicleId = dto.VehicleId,
            OriginLocation = dto.OriginLocation, DestinationLocation = dto.DestinationLocation,
            DistanceKm = dto.DistanceKm, PlannedDepartureDate = dto.PlannedDepartureDate,
            ExpectedArrivalDate = dto.ExpectedArrivalDate, Remarks = dto.Remarks, Status = "Assigned"
        };
        _db.Trips.Add(trip); await _db.SaveChangesAsync();
        if (dto.CargoIds != null)
        {
            var cargoes = await _db.Cargoes.Where(c => dto.CargoIds.Contains(c.Id)).ToListAsync();
            foreach (var c in cargoes) c.TripId = trip.Id;
        }
        _db.TripStatusHistories.Add(new TripStatusHistory { TripId = trip.Id, Status = "Assigned", Remarks = "Trip created", ChangedByUserId = CurrentUserId });
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = trip.Id }, trip);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateTripDto dto)
    {
        var trip = await _db.Trips.Include(t => t.CargoItems).FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();
        trip.DriverId = dto.DriverId; trip.VehicleId = dto.VehicleId;
        trip.OriginLocation = dto.OriginLocation; trip.DestinationLocation = dto.DestinationLocation;
        trip.DistanceKm = dto.DistanceKm; trip.PlannedDepartureDate = dto.PlannedDepartureDate;
        trip.ExpectedArrivalDate = dto.ExpectedArrivalDate; trip.Remarks = dto.Remarks;
        trip.UpdatedAt = DateTime.UtcNow;
        foreach (var c in trip.CargoItems) c.TripId = null;
        if (dto.CargoIds != null)
        {
            var cargoes = await _db.Cargoes.Where(c => dto.CargoIds.Contains(c.Id)).ToListAsync();
            foreach (var c in cargoes) c.TripId = trip.Id;
        }
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTripStatusDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        if (User.IsInRole("Driver") && trip.DriverId != CurrentUserId) return Forbid();
        var allowed = new[] { "Assigned","CargoLoading","LoadingComplete","InTransit","NearDestination","Unloading","DeliveryCompleted","Cancelled" };
        if (!allowed.Contains(dto.Status)) return BadRequest(new { message = "Invalid status" });
        trip.Status = dto.Status; trip.UpdatedAt = DateTime.UtcNow;
        if (dto.Status == "InTransit") trip.ActualDepartureDate = DateTime.UtcNow;
        if (dto.Status == "DeliveryCompleted") trip.ActualArrivalDate = DateTime.UtcNow;
        if (dto.LoadingArrivalTime.HasValue) trip.LoadingArrivalTime = dto.LoadingArrivalTime;
        if (dto.LoadingEndTime.HasValue) trip.LoadingEndTime = dto.LoadingEndTime;
        if (dto.UnloadingArrivalTime.HasValue) trip.UnloadingArrivalTime = dto.UnloadingArrivalTime;
        if (dto.UnloadingEndTime.HasValue) trip.UnloadingEndTime = dto.UnloadingEndTime;
        _db.TripStatusHistories.Add(new TripStatusHistory { TripId = trip.Id, Status = dto.Status, Remarks = dto.Remarks, ChangedByUserId = CurrentUserId });
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpPut("{id}/pod")]
    public async Task<IActionResult> ReceivePod(int id, PodReceiveDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        trip.PodNumber = dto.PodNumber; trip.PodReceived = true; trip.Remarks = dto.Remarks;
        if (dto.LoadingArrivalTime.HasValue) trip.LoadingArrivalTime = dto.LoadingArrivalTime;
        if (dto.LoadingEndTime.HasValue) trip.LoadingEndTime = dto.LoadingEndTime;
        if (dto.UnloadingArrivalTime.HasValue) trip.UnloadingArrivalTime = dto.UnloadingArrivalTime;
        if (dto.UnloadingEndTime.HasValue) trip.UnloadingEndTime = dto.UnloadingEndTime;
        trip.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("my-trips")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyTrips([FromQuery] string? status)
    {
        var q = _db.Trips
            .Include(t => t.Vehicle)
            .Where(t => t.DriverId == CurrentUserId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);

        var trips = await q.OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id,
                t.TripNumber,
                t.OriginLocation,
                t.DestinationLocation,
                t.Status,
                t.DistanceKm,
                t.PlannedDepartureDate,
                t.ExpectedArrivalDate,
                t.Remarks,
                VehicleRegistration = t.Vehicle != null ? t.Vehicle.RegistrationNumber : null
            }).ToListAsync();

        return Ok(trips);
    }

    [HttpGet("my-stats")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyStats()
    {
        var trips = await _db.Trips
            .Where(t => t.DriverId == CurrentUserId)
            .ToListAsync();

        return Ok(new
        {
            TotalTrips = trips.Count,
            CompletedTrips = trips.Count(t => t.Status == "DeliveryCompleted"),
            InProgressTrips = trips.Count(t => t.Status == "InTransit" || t.Status == "CargoLoading" || t.Status == "LoadingComplete" || t.Status == "NearDestination" || t.Status == "Unloading"),
            TotalDistanceKm = trips.Where(t => t.DistanceKm.HasValue).Sum(t => t.DistanceKm)
        });
    }

    [HttpGet("my-active")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> MyActiveTrip()
    {
        var activeStatuses = new[] { "Assigned", "CargoLoading", "LoadingComplete", "InTransit", "NearDestination", "Unloading" };

        var trip = await _db.Trips
            .Include(t => t.Vehicle)
            .Where(t => t.DriverId == CurrentUserId && activeStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id,
                t.OriginLocation,
                t.DestinationLocation,
                t.Status,
                t.DistanceKm,
                VehicleRegistration = t.Vehicle != null ? t.Vehicle.RegistrationNumber : null
            })
            .FirstOrDefaultAsync();

        return Ok(trip); // returns null if no active trip — frontend handles it
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> PatchStatus(int id, UpdateTripStatusDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();
        if (User.IsInRole("Driver") && trip.DriverId != CurrentUserId) return Forbid();

        var allowed = new[] { "Assigned", "CargoLoading", "LoadingComplete", "InTransit", "NearDestination", "Unloading", "DeliveryCompleted", "Cancelled" };
        if (!allowed.Contains(dto.Status)) return BadRequest(new { message = "Invalid status" });

        trip.Status = dto.Status;
        trip.UpdatedAt = DateTime.UtcNow;
        if (dto.Status == "InTransit") trip.ActualDepartureDate = DateTime.UtcNow;
        if (dto.Status == "DeliveryCompleted") trip.ActualArrivalDate = DateTime.UtcNow;

        _db.TripStatusHistories.Add(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = dto.Status,
            Remarks = dto.Remarks,
            ChangedByUserId = CurrentUserId
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/cmr")]
    public async Task<IActionResult> UpdateCmr(int id, [FromBody] UpdateCmrDto dto)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip == null) return NotFound();

        if (User.IsInRole("Driver") && trip.DriverId != CurrentUserId)
            return Forbid();

        trip.CmrNumber = dto.CmrNumber;
        trip.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

}
