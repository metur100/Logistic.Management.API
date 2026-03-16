// Models/Incident.cs
using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

public class Incident
{
    public int Id { get; set; }
    public int? TripId { get; set; }
    public Trip? Trip { get; set; }
    [Required] public string Type { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    public string Severity { get; set; } = "Medium";   // Low | Medium | High
    public string Status { get; set; } = "Open";        // Open | Resolved
    public string? Location { get; set; }
    public string? Resolution { get; set; }
    public int? ReportedByUserId { get; set; }
    public User? Reporter { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
