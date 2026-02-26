using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace LogisticsAPI.Models;
public class Cargo
{
    public int Id { get; set; }
    [Required] public string Description { get; set; } = "";
    public string? Consignor { get; set; }
    public string? Consignee { get; set; }
    public decimal? WeightTons { get; set; }
    public decimal? VolumeCbm { get; set; }
    public string? CargoType { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? TripId { get; set; }
    [JsonIgnore]
    public Trip? Trip { get; set; }
}
