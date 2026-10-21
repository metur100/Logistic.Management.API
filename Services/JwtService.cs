using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using LogisticsAPI.Models;

namespace LogisticsAPI.Services;

public class JwtService
{
    private readonly IConfiguration _cfg;
    private readonly JwtKeyProvider _keys;

    public JwtService(IConfiguration cfg, JwtKeyProvider keys)
    {
        _cfg = cfg; _keys = keys;
    }

    public string GenerateToken(User user)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_keys.Key ?? throw new InvalidOperationException("JWT signing key is not initialized."));
        var key = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.CompanyId.HasValue)
        {
            claims.Add(new Claim("companyId", user.CompanyId.Value.ToString()));
            claims.Add(new Claim("companyName", user.Company?.Name ?? ""));
        }
        if (!string.IsNullOrWhiteSpace(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"], audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_cfg["Jwt:ExpiresInMinutes"]!)),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string NewInviteToken() => Guid.NewGuid().ToString("N");
}
