using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.DTOs;
using LogisticsAPI.Services;
namespace LogisticsAPI.Controllers;
[ApiController][Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    public AuthController(AppDbContext db, JwtService jwt) { _db = db; _jwt = jwt; }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });
        var token = _jwt.GenerateToken(user);
        return Ok(new LoginResponse(token, user.Role, user.Id, user.FullName));
    }
}
