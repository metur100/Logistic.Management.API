using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class Vehicle
{
    public int Id { get; set; }
    [Required] public string RegistrationNumber { get; set; } = "";
    public string? FleetName { get; set; }
    public string? Type { get; set; }
    public decimal? Capacity { get; set; }
    public string? OwnershipType { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
