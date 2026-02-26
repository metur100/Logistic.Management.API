using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class Trip
{
    public int Id { get; set; }
    public string TripNumber { get; set; } = "";
    public int? DriverId { get; set; }
    public User? Driver { get; set; }
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    [Required] public string OriginLocation { get; set; } = "";
    [Required] public string DestinationLocation { get; set; } = "";
    public decimal? DistanceKm { get; set; }
    public string Status { get; set; } = "Assigned";
    public DateTime? PlannedDepartureDate { get; set; }
    public DateTime? ActualDepartureDate { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public DateTime? ActualArrivalDate { get; set; }
    public DateTime? LoadingArrivalTime { get; set; }
    public DateTime? LoadingEndTime { get; set; }
    public DateTime? UnloadingArrivalTime { get; set; }
    public DateTime? UnloadingEndTime { get; set; }
    public string? Remarks { get; set; }
    public string? PodNumber { get; set; }
    public bool PodReceived { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Cargo> CargoItems { get; set; } = new List<Cargo>();
    public ICollection<TripStatusHistory> StatusHistory { get; set; } = new List<TripStatusHistory>();
    public ICollection<FuelRequest> FuelRequests { get; set; } = new List<FuelRequest>();
}
