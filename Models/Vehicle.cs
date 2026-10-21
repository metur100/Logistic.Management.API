using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class Vehicle
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    [Required, MaxLength(40)] public string RegistrationNumber { get; set; } = "";
    [MaxLength(100)] public string? FleetName { get; set; }
    [MaxLength(60)] public string? Type { get; set; }
    public decimal? Capacity { get; set; }
    [MaxLength(40)] public string? OwnershipType { get; set; }
    [MaxLength(120)] public string? OwnerName { get; set; }
    [MaxLength(40)] public string? OwnerPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
