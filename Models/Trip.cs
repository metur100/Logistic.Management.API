using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class Trip
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    [Required, MaxLength(40)] public string TripNumber { get; set; } = "";
    public int? DriverId { get; set; }
    public User? Driver { get; set; }
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    [Required, MaxLength(200)] public string OriginLocation { get; set; } = "";
    [Required, MaxLength(200)] public string DestinationLocation { get; set; } = "";
    public decimal? DistanceKm { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    [MaxLength(40)] public string Status { get; set; } = "Assigned";
    public DateTime? PlannedDepartureDate { get; set; }
    public DateTime? ActualDepartureDate { get; set; }
    public DateTime? ExpectedArrivalDate { get; set; }
    public DateTime? ActualArrivalDate { get; set; }
    public DateTime? LoadingArrivalTime { get; set; }
    public DateTime? LoadingEndTime { get; set; }
    public DateTime? UnloadingArrivalTime { get; set; }
    public DateTime? UnloadingEndTime { get; set; }
    [MaxLength(2000)] public string? Remarks { get; set; }
    [MaxLength(60)] public string? PodNumber { get; set; }
    public bool PodReceived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(60)] public string? CmrNumber { get; set; }
    public ICollection<Cargo> CargoItems { get; set; } = new List<Cargo>();
    public ICollection<TripStatusHistory> StatusHistory { get; set; } = new List<TripStatusHistory>();
}
