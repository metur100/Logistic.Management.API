using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Services;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt) { _db = db; _jwt = jwt; }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var username = (req.Username ?? "").Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Username and password are required." });

        var user = await _db.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
            return Unauthorized(new { message = "Invalid username or password." });

        if (!user.IsActive)
            return StatusCode(403, new { message = "This account is disabled. Contact your company administrator." });

        if (!string.IsNullOrEmpty(user.InviteToken) || string.IsNullOrEmpty(user.PasswordHash))
            return StatusCode(403, new { message = "This account is not activated yet. Open the invitation link from your email to set a password." });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password." });

        if (user.Role != "Admin" && user.Company is { Status: not "Active" })
            return StatusCode(403, new { message = "Your company account is suspended. Contact the platform administrator." });

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);
        return Ok(new LoginResponse(
            token, user.Role, user.Id, user.FullName,
            user.CompanyId, user.Company?.Name, user.Email));
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users.Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return Unauthorized();

        return Ok(new MeResponse(
            user.Id, user.FullName, user.Username, user.Email, user.Role,
            user.Phone, user.LicenseNumber,
            user.CompanyId, user.Company?.Name, user.Company?.Status));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user == null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is not correct." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Password changed." });
    }
}
