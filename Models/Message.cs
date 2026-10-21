using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

public class Message
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    [Required, MaxLength(4000)] public string Content { get; set; } = "";
    public bool IsFromDriver { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The driver in this conversation.</summary>
    public int DriverId { get; set; }
    public User? Driver { get; set; }

    /// <summary>The company manager (dispatcher) in this conversation.</summary>
    public int ManagerId { get; set; }
    public User? Manager { get; set; }

    /// <summary>Who actually sent the message.</summary>
    public int SenderId { get; set; }
    public User? Sender { get; set; }
}
