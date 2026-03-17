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

    // GET /api/messages
    // Driver: ?adminId=X  → their thread with that admin
    // Admin:  ?driverId=X → their thread with that driver
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? driverId, [FromQuery] int? adminId)
    {
        IQueryable<Message> q = _db.Messages
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt);

        if (User.IsInRole("Driver"))
        {
            if (adminId == null)
                return BadRequest(new { message = "adminId required" });

            q = q.Where(m => m.DriverId == CurrentUserId && m.AdminId == adminId.Value);
        }
        else // Admin / Manager
        {
            if (driverId == null)
                return BadRequest(new { message = "driverId required" });

            q = q.Where(m => m.AdminId == CurrentUserId && m.DriverId == driverId.Value);
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
    // Driver: counts unread from any admin
    // Admin:  counts unread from any driver (only in their own threads)
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        int count;
        if (User.IsInRole("Driver"))
        {
            count = await _db.Messages
                .Where(m => m.DriverId == CurrentUserId && !m.IsFromDriver && !m.IsRead)
                .CountAsync();
        }
        else
        {
            count = await _db.Messages
                .Where(m => m.AdminId == CurrentUserId && m.IsFromDriver && !m.IsRead)
                .CountAsync();
        }
        return Ok(new { count });
    }

    // GET /api/messages/my-threads
    // Driver: returns list of admins they have threads with (or all admins)
    // Admin:  returns list of drivers they have threads with
    [HttpGet("my-threads")]
    public async Task<IActionResult> MyThreads()
    {
        if (User.IsInRole("Driver"))
        {
            // Return all admins available to chat with
            var admins = await _db.Users
                .Where(u => u.Role == "Admin" || u.Role == "Manager")
                .Select(u => new { u.Id, u.FullName, u.Role })
                .ToListAsync();
            return Ok(admins);
        }
        else
        {
            // Return drivers who have exchanged messages with this admin
            // + all drivers (so admin can initiate)
            var drivers = await _db.Users
                .Where(u => u.Role == "Driver")
                .Select(u => new {
                    u.Id,
                    u.FullName,
                    UnreadCount = _db.Messages
                        .Count(m => m.AdminId == CurrentUserId
                                 && m.DriverId == u.Id
                                 && m.IsFromDriver
                                 && !m.IsRead)
                })
                .ToListAsync();
            return Ok(drivers);
        }
    }

    // POST /api/messages
    // Driver sends: { content, toAdminId }
    // Admin sends:  { content, toDriverId }
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
    {
        Message msg;

        if (User.IsInRole("Driver"))
        {
            if (dto.ToAdminId == null)
                return BadRequest(new { message = "toAdminId required" });

            msg = new Message
            {
                DriverId = CurrentUserId,
                AdminId = dto.ToAdminId.Value,
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
                AdminId = CurrentUserId,
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
    // Driver: ?adminId=X  → marks messages from that admin as read
    // Admin:  ?driverId=X → marks messages from that driver as read
    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromQuery] int? driverId, [FromQuery] int? adminId)
    {
        IQueryable<Message> q = _db.Messages;

        if (User.IsInRole("Driver"))
        {
            if (adminId == null) return BadRequest();
            q = q.Where(m => m.DriverId == CurrentUserId
                           && m.AdminId == adminId.Value
                           && !m.IsFromDriver
                           && !m.IsRead);
        }
        else
        {
            if (driverId == null) return BadRequest();
            q = q.Where(m => m.AdminId == CurrentUserId
                           && m.DriverId == driverId.Value
                           && m.IsFromDriver
                           && !m.IsRead);
        }

        await q.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        return Ok();
    }
}

public record SendMessageDto(string Content, int? ToDriverId, int? ToAdminId);
