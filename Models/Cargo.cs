using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace LogisticsAPI.Models;
public class Cargo
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    [Required, MaxLength(200)] public string Description { get; set; } = "";
    [MaxLength(160)] public string? Consignor { get; set; }
    [MaxLength(160)] public string? Consignee { get; set; }
    public decimal? WeightTons { get; set; }
    public decimal? VolumeCbm { get; set; }
    [MaxLength(60)] public string? CargoType { get; set; }
    [MaxLength(1000)] public string? SpecialInstructions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? TripId { get; set; }
    [JsonIgnore]
    public Trip? Trip { get; set; }
}
