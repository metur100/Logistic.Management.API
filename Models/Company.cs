using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

/// <summary>A customer company (tenant). All operational data belongs to exactly one company.</summary>
public class Company
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [MaxLength(160)] public string? LegalName { get; set; }
    [MaxLength(40)] public string? TaxId { get; set; }
    [MaxLength(160)] public string? Email { get; set; }
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(240)] public string? Address { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(80)] public string? Country { get; set; } = "Bosnia and Herzegovina";
    /// <summary>Active | Suspended</summary>
    [MaxLength(20)] public string Status { get; set; } = "Active";
    public bool IsVerified { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Cargo> Cargoes { get; set; } = new List<Cargo>();
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
