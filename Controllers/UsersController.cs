using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? role)
    {
        var q = _db.Users.Include(u => u.Company).AsQueryable();
        if (!string.IsNullOrEmpty(role)) q = q.Where(u => u.Role == role);

        var list = await q.OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.FullName, u.Username, u.Email, u.Role, u.Phone,
                u.LicenseNumber, u.IsActive, u.CreatedAt, u.LastLoginAt,
                u.CompanyId,
                companyName = u.Company != null ? u.Company.Name : null,
                invitePending = u.InviteToken != null
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var u = await _db.Users.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        return Ok(new
        {
            u.Id, u.FullName, u.Username, u.Email, u.Role, u.Phone, u.LicenseNumber,
            u.IsActive, u.CreatedAt, u.LastLoginAt, u.CompanyId,
            companyName = u.Company?.Name
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.FullName = dto.FullName.Trim();
        user.Phone = dto.Phone?.Trim();
        user.LicenseNumber = dto.LicenseNumber?.Trim();
        user.Email = dto.Email?.Trim();
        user.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/block")]
    public async Task<IActionResult> Block(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Id == CurrentUserId)
            return BadRequest(new { message = "You cannot block your own account." });
        if (user.Role == "Admin")
            return BadRequest(new { message = "Administrator accounts cannot be blocked from here." });

        user.IsActive = false;
        user.InviteToken = null;
        user.InviteExpiresAt = null;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPatch("{id:int}/unblock")]
    public async Task<IActionResult> Unblock(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "Admin" && user.Id != CurrentUserId)
            return BadRequest(new { message = "Another administrator password cannot be changed from here." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.InviteToken = null;
        user.InviteExpiresAt = null;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password reset successfully." });
    }
}
