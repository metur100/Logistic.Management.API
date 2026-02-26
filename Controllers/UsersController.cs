using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Models;
namespace LogisticsAPI.Controllers;
[ApiController][Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role)
    {
        var q = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(role)) q = q.Where(u => u.Role == role);
        return Ok(await q.Select(u => new { u.Id, u.FullName, u.Username, u.Role, u.Phone, u.LicenseNumber, u.IsActive, u.CreatedAt }).ToListAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        return Ok(new { u.Id, u.FullName, u.Username, u.Role, u.Phone, u.LicenseNumber, u.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return BadRequest(new { message = "Username already exists" });
        var user = new User { FullName = dto.FullName, Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role, Phone = dto.Phone, LicenseNumber = dto.LicenseNumber };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.FullName, user.Role });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.FullName = dto.FullName; user.Phone = dto.Phone;
        user.LicenseNumber = dto.LicenseNumber; user.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsActive = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
