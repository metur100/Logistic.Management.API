using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

public class Message
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public bool IsFromDriver { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // The driver in this conversation
    public int DriverId { get; set; }
    public User? Driver { get; set; }

    // The admin in this conversation  ← NEW
    public int AdminId { get; set; }
    public User? Admin { get; set; }

    // Who actually sent it
    public int SenderId { get; set; }
    public User? Sender { get; set; }
}
