using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using LogisticsAPI.Data;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MessagesController(AppDbContext db) => _db = db;

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/messages — driver gets own thread; manager gets by driverId
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? driverId)
    {
        IQueryable<Message> q = _db.Messages
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt);

        if (User.IsInRole("Driver"))
        {
            q = q.Where(m => m.DriverId == CurrentUserId);
        }
        else if (driverId.HasValue)
        {
            q = q.Where(m => m.DriverId == driverId.Value);
        }

        var msgs = await q.Select(m => new {
            m.Id,
            m.Content,
            m.IsFromDriver,
            m.CreatedAt,
            m.IsRead,
            SenderName = m.Sender != null ? m.Sender.FullName : null
        }).ToListAsync();

        return Ok(msgs);
    }

    // GET /api/messages/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount([FromQuery] string? role)
    {
        int count;
        if (User.IsInRole("Driver"))
        {
            // Count messages sent TO this driver (not from driver, not read)
            count = await _db.Messages
                .Where(m => m.DriverId == CurrentUserId && !m.IsFromDriver && !m.IsRead)
                .CountAsync();
        }
        else
        {
            // Count messages from drivers not yet read by management
            count = await _db.Messages
                .Where(m => m.IsFromDriver && !m.IsRead)
                .CountAsync();
        }
        return Ok(new { count });
    }

    // POST /api/messages
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
    {
        Message msg;
        if (User.IsInRole("Driver"))
        {
            msg = new Message
            {
                DriverId = CurrentUserId,
                SenderId = CurrentUserId,
                Content = dto.Content,
                IsFromDriver = true
            };
        }
        else
        {
            if (dto.ToDriverId == null)
                return BadRequest(new { message = "toDriverId required" });

            msg = new Message
            {
                DriverId = dto.ToDriverId.Value,
                SenderId = CurrentUserId,
                Content = dto.Content,
                IsFromDriver = false
            };
        }

        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();
        return Ok(new { msg.Id });
    }

    // POST /api/messages/mark-read
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromQuery] int? driverId)
    {
        IQueryable<Message> q = _db.Messages;

        if (User.IsInRole("Driver"))
        {
            q = q.Where(m => m.DriverId == CurrentUserId && !m.IsFromDriver && !m.IsRead);
        }
        else if (driverId.HasValue)
        {
            q = q.Where(m => m.DriverId == driverId.Value && m.IsFromDriver && !m.IsRead);
        }

        await q.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        return Ok();
    }
}

public record SendMessageDto(string Content, int? ToDriverId);
