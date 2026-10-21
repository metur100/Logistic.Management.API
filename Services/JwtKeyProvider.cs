using System.Security.Cryptography;
using LogisticsAPI.Data;
using LogisticsAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticsAPI.Services;

/// <summary>Holds the JWT signing key loaded from the database at startup.</summary>
public class JwtKeyProvider
{
    public string? Key { get; private set; }

    public async Task InitializeAsync(AppDbContext db, ILogger log)
    {
        var setting = await db.PlatformSettings.FirstOrDefaultAsync();
        if (setting == null || string.IsNullOrWhiteSpace(setting.JwtKey))
        {
            setting ??= new PlatformSetting();
            setting.JwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            setting.CreatedAt = DateTime.UtcNow;
            if (setting.Id == 0) db.PlatformSettings.Add(setting);
            await db.SaveChangesAsync();
            log.LogInformation("Generated a new platform JWT signing key.");
        }

        Key = setting.JwtKey;
    }
}
