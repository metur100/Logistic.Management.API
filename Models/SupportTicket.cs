using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class SupportTicket
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Open";       // Open | InProgress | Resolved | Closed
    [MaxLength(20)] public string Priority { get; set; } = "Medium";   // Low | Medium | High
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public int? AssignedToUserId { get; set; }
    public User? AssignedTo { get; set; }
    [MaxLength(2000)] public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
