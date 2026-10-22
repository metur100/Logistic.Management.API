using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

/// <summary>Platform-wide secret settings generated at first startup (never committed to source control).</summary>
public class PlatformSetting
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string JwtKey { get; set; } = "";
    [MaxLength(200)] public string? ImgBBApiKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
