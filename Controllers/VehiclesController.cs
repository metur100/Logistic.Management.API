using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
namespace LogisticsAPI.Controllers;
[ApiController][Route("api/[controller]")][Authorize]
public class VehiclesController : ControllerBase
{
    private readonly AppDbContext _db;
    public VehiclesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _db.Vehicles.Where(v => v.IsActive).ToListAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var v = await _db.Vehicles.FindAsync(id);
        return v == null ? NotFound() : Ok(v);
    }

    [HttpPost][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create(CreateVehicleDto dto)
    {
        var v = new Vehicle { RegistrationNumber = dto.RegistrationNumber, FleetName = dto.FleetName,
            Type = dto.Type, Capacity = dto.Capacity, OwnershipType = dto.OwnershipType,
            OwnerName = dto.OwnerName, OwnerPhone = dto.OwnerPhone };
        _db.Vehicles.Add(v); await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = v.Id }, v);
    }

    [HttpPut("{id}")][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateVehicleDto dto)
    {
        var v = await _db.Vehicles.FindAsync(id);
        if (v == null) return NotFound();
        v.RegistrationNumber = dto.RegistrationNumber; v.FleetName = dto.FleetName;
        v.Type = dto.Type; v.Capacity = dto.Capacity; v.OwnershipType = dto.OwnershipType;
        v.OwnerName = dto.OwnerName; v.OwnerPhone = dto.OwnerPhone;
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpDelete("{id}")][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await _db.Vehicles.FindAsync(id);
        if (v == null) return NotFound();
        v.IsActive = false; await _db.SaveChangesAsync(); return NoContent();
    }
}
