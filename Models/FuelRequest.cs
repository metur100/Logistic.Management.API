using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class FuelRequest
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public int DriverId { get; set; }
    [Required] public decimal LitersRequested { get; set; }
    public string? PumpName { get; set; }
    public string? Route { get; set; }
    public string? Remarks { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
}
