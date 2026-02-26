using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;
public class User
{
    public int Id { get; set; }
    [Required] public string FullName { get; set; } = "";
    [Required] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required] public string Role { get; set; } = "Driver"; // Driver | Manager | Admin
    public string? Phone { get; set; }
    public string? LicenseNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
