using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace LogisticsAPI.Models;

public class User
{
    public int Id { get; set; }

    /// <summary>Null only for platform administrators.</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, MaxLength(160)] public string FullName { get; set; } = "";
    [Required, MaxLength(80)] public string Username { get; set; } = "";
    [MaxLength(160)] public string? Email { get; set; }
    [JsonIgnore] [Required] public string PasswordHash { get; set; } = "";
    /// <summary>Driver | Manager | Admin</summary>
    [Required, MaxLength(20)] public string Role { get; set; } = "Driver";
    [MaxLength(40)] public string? Phone { get; set; }
    [MaxLength(60)] public string? LicenseNumber { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Set while a driver still has to choose a password through an invite link.</summary>
    [JsonIgnore] [MaxLength(80)] public string? InviteToken { get; set; }
    [JsonIgnore] public DateTime? InviteExpiresAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
