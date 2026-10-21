using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager")]
public class CargoController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public CargoController(AppDbContext db) => _db = db;
    private int Cid => CurrentCompanyId ?? 0;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool unassigned = false, [FromQuery] string? search = null)
    {
        var q = _db.Cargoes
            .AsNoTracking()
            .Where(c => c.CompanyId == Cid);

        if (unassigned) q = q.Where(c => c.TripId == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.Description.Contains(s) ||
                             (c.Consignor != null && c.Consignor.Contains(s)) ||
                             (c.Consignee != null && c.Consignee.Contains(s)));
        }

        var list = await q.OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Description, c.Consignor, c.Consignee, c.WeightTons, c.VolumeCbm,
                c.CargoType, c.SpecialInstructions, c.CreatedAt, c.TripId,
                tripNumber = c.Trip != null ? c.Trip.TripNumber : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var cargo = await _db.Cargoes
            .AsNoTracking()
            .Where(c => c.Id == id && c.CompanyId == Cid)
            .Select(c => new
            {
                c.Id, c.Description, c.Consignor, c.Consignee, c.WeightTons, c.VolumeCbm,
                c.CargoType, c.SpecialInstructions, c.CreatedAt, c.TripId,
                tripNumber = c.Trip != null ? c.Trip.TripNumber : null
            })
            .FirstOrDefaultAsync();

        return cargo == null ? NotFound() : Ok(cargo);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCargoDto dto)
    {
        var cargo = new Cargo
        {
            CompanyId = Cid,
            Description = dto.Description.Trim(),
            Consignor = dto.Consignor?.Trim(),
            Consignee = dto.Consignee?.Trim(),
            WeightTons = dto.WeightTons,
            VolumeCbm = dto.VolumeCbm,
            CargoType = dto.CargoType?.Trim(),
            SpecialInstructions = dto.SpecialInstructions?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.Cargoes.Add(cargo);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = cargo.Id }, cargo);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateCargoDto dto)
    {
        var cargo = await _db.Cargoes.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == Cid);
        if (cargo == null) return NotFound();

        cargo.Description = dto.Description.Trim();
        cargo.Consignor = dto.Consignor?.Trim();
        cargo.Consignee = dto.Consignee?.Trim();
        cargo.WeightTons = dto.WeightTons;
        cargo.VolumeCbm = dto.VolumeCbm;
        cargo.CargoType = dto.CargoType?.Trim();
        cargo.SpecialInstructions = dto.SpecialInstructions?.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cargo = await _db.Cargoes.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == Cid);
        if (cargo == null) return NotFound();
        if (cargo.TripId.HasValue)
            return BadRequest(new { message = "This cargo is assigned to a transport. Remove it from the transport first." });

        _db.Cargoes.Remove(cargo);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
