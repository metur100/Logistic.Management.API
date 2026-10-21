using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

public class Incident
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? TripId { get; set; }
    public Trip? Trip { get; set; }
    [Required, MaxLength(60)] public string Type { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(20)] public string Severity { get; set; } = "Medium";   // Low | Medium | High
    [MaxLength(20)] public string Status { get; set; } = "Open";        // Open | Resolved
    [MaxLength(240)] public string? Location { get; set; }
    [MaxLength(2000)] public string? Resolution { get; set; }
    public int? ReportedByUserId { get; set; }
    public User? Reporter { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
