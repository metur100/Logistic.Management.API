namespace LogisticsAPI.Models;
public class TripStatusHistory
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public Trip Trip { get; set; } = null!;
    public string Status { get; set; } = "";
    public string? Remarks { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int? ChangedByUserId { get; set; }
}
