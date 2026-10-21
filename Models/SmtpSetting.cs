using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

/// <summary>Single-row SMTP configuration managed from the admin panel.</summary>
public class SmtpSetting
{
    public int Id { get; set; }
    [MaxLength(160)] public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    [MaxLength(160)] public string Username { get; set; } = "";
    [MaxLength(300)] public string Password { get; set; } = "";
    [MaxLength(160)] public string FromAddress { get; set; } = "";
    [MaxLength(120)] public string FromName { get; set; } = "LogistiTrack";
    [MaxLength(160)] public string? ReportsTo { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
