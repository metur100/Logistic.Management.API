using System.ComponentModel.DataAnnotations;
namespace LogisticsAPI.Models;

public class Message
{
    public int Id { get; set; }
    [Required] public int DriverId { get; set; }     
    public int? SenderId { get; set; }
    public User? Sender { get; set; }
    [Required] public string Content { get; set; } = "";
    public bool IsFromDriver { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
