using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager")]
public class VehiclesController : ApiControllerBase
{
    private static readonly string[] ActiveStatuses =
    {
        "Assigned", "CargoLoading", "LoadingComplete", "InTransit",
        "NearDestination", "Unloading", "UnloadingComplete"
    };

    private readonly AppDbContext _db;
    public VehiclesController(AppDbContext db) => _db = db;
    private int Cid => CurrentCompanyId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.CompanyId == Cid)
            .OrderByDescending(v => v.IsActive).ThenBy(v => v.RegistrationNumber)
            .Select(v => new
            {
                v.Id, v.RegistrationNumber, v.FleetName, v.Type, v.Capacity,
                v.OwnershipType, v.OwnerName, v.OwnerPhone, v.IsActive, v.CreatedAt,
                activeTrips = v.Trips.Count(t => ActiveStatuses.Contains(t.Status)),
                totalTrips = v.Trips.Count()
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == Cid);
        return v == null ? NotFound() : Ok(v);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateVehicleDto dto)
    {
        var reg = dto.RegistrationNumber.Trim().ToUpperInvariant();
        if (await _db.Vehicles.AnyAsync(v => v.CompanyId == Cid && v.RegistrationNumber == reg))
            return BadRequest(new { message = "A vehicle with that registration already exists." });

        var v = new Vehicle
        {
            CompanyId = Cid,
            RegistrationNumber = reg,
            FleetName = dto.FleetName?.Trim(),
            Type = dto.Type?.Trim(),
            Capacity = dto.Capacity,
            OwnershipType = dto.OwnershipType?.Trim(),
            OwnerName = dto.OwnerName?.Trim(),
            OwnerPhone = dto.OwnerPhone?.Trim()
        };
        _db.Vehicles.Add(v);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = v.Id }, v);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateVehicleDto dto)
    {
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == Cid);
        if (v == null) return NotFound();

        var reg = dto.RegistrationNumber.Trim().ToUpperInvariant();
        if (await _db.Vehicles.AnyAsync(x => x.CompanyId == Cid && x.Id != id && x.RegistrationNumber == reg))
            return BadRequest(new { message = "A vehicle with that registration already exists." });

        v.RegistrationNumber = reg;
        v.FleetName = dto.FleetName?.Trim();
        v.Type = dto.Type?.Trim();
        v.Capacity = dto.Capacity;
        v.OwnershipType = dto.OwnershipType?.Trim();
        v.OwnerName = dto.OwnerName?.Trim();
        v.OwnerPhone = dto.OwnerPhone?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == Cid);
        if (v == null) return NotFound();
        if (await _db.Trips.AnyAsync(t => t.CompanyId == Cid && t.VehicleId == id && ActiveStatuses.Contains(t.Status)))
            return BadRequest(new { message = "This vehicle is assigned to an active transport." });

        v.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == Cid);
        if (v == null) return NotFound();
        v.IsActive = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
