using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
namespace LogisticsAPI.Controllers;
[ApiController][Route("api/[controller]")][Authorize]
public class CargoController : ControllerBase
{
    private readonly AppDbContext _db;
    public CargoController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unassigned = false)
    {
        var q = _db.Cargoes.Include(c => c.Trip).AsQueryable();
        if (unassigned) q = q.Where(c => c.TripId == null);
        return Ok(await q.ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await _db.Cargoes.Include(c => c.Trip).FirstOrDefaultAsync(c => c.Id == id);
        return c == null ? NotFound() : Ok(c);
    }

    [HttpPost][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create(CreateCargoDto dto)
    {
        var cargo = new Cargo { Description = dto.Description, Consignor = dto.Consignor,
            Consignee = dto.Consignee, WeightTons = dto.WeightTons, VolumeCbm = dto.VolumeCbm,
            CargoType = dto.CargoType, SpecialInstructions = dto.SpecialInstructions };
        _db.Cargoes.Add(cargo); await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = cargo.Id }, cargo);
    }

    [HttpPut("{id}")][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateCargoDto dto)
    {
        var cargo = await _db.Cargoes.FindAsync(id);
        if (cargo == null) return NotFound();
        cargo.Description = dto.Description; cargo.Consignor = dto.Consignor;
        cargo.Consignee = dto.Consignee; cargo.WeightTons = dto.WeightTons;
        cargo.VolumeCbm = dto.VolumeCbm; cargo.CargoType = dto.CargoType;
        cargo.SpecialInstructions = dto.SpecialInstructions;
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpDelete("{id}")][Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id)
    {
        var cargo = await _db.Cargoes.FindAsync(id);
        if (cargo == null) return NotFound();
        _db.Cargoes.Remove(cargo); await _db.SaveChangesAsync(); return NoContent();
    }
}
