using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Data;
using LogisticsAPI.Models;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager,Driver")]
public class MessagesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public MessagesController(AppDbContext db) => _db = db;
    private int Cid => CurrentCompanyId ?? 0;

    /// <summary>List of people the signed-in user can chat with, with last message and unread count.</summary>
    [HttpGet("threads")]
    public async Task<IActionResult> Threads()
    {
        var peerRole = IsDriver ? "Manager" : "Driver";

        var peers = await _db.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == Cid && u.IsActive && u.Role == peerRole)
            .Select(u => new { u.Id, u.FullName, u.Role, u.Email, u.Phone })
            .ToListAsync();

        var messages = await _db.Messages
            .AsNoTracking()
            .Where(m => m.CompanyId == Cid &&
                        (IsDriver ? m.DriverId == CurrentUserId : m.ManagerId == CurrentUserId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var threads = peers.Select(p =>
        {
            var thread = messages.Where(m => (IsDriver ? m.ManagerId : m.DriverId) == p.Id).ToList();
            var last = thread.LastOrDefault();
            var unread = thread.Count(m => !m.IsRead && (IsDriver ? !m.IsFromDriver : m.IsFromDriver));
            return new
            {
                userId = p.Id,
                fullName = p.FullName,
                role = p.Role,
                email = p.Email,
                phone = p.Phone,
                lastMessage = last?.Content,
                lastMessageAt = last?.CreatedAt,
                unread
            };
        }).OrderByDescending(t => t.lastMessageAt ?? DateTime.MinValue).ToList();

        return Ok(threads);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int withUserId)
    {
        var peer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == withUserId && u.CompanyId == Cid && u.Role != "Admin");
        if (peer == null) return NotFound(new { message = "Conversation partner not found." });

        var q = _db.Messages.AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.CompanyId == Cid);

        q = IsDriver
            ? q.Where(m => m.DriverId == CurrentUserId && m.ManagerId == withUserId)
            : q.Where(m => m.ManagerId == CurrentUserId && m.DriverId == withUserId);

        var list = await q.OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.IsFromDriver,
                m.IsRead,
                m.CreatedAt,
                m.SenderId,
                senderName = m.Sender != null ? m.Sender.FullName : null
            }).ToListAsync();

        return Ok(list);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var count = IsDriver
            ? await _db.Messages.CountAsync(m => m.CompanyId == Cid && m.DriverId == CurrentUserId && !m.IsFromDriver && !m.IsRead)
            : await _db.Messages.CountAsync(m => m.CompanyId == Cid && m.ManagerId == CurrentUserId && m.IsFromDriver && !m.IsRead);
        return Ok(new { count });
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
    {
        var content = dto.Content?.Trim() ?? "";
        if (content.Length == 0) return BadRequest(new { message = "Write a message first." });
        if (content.Length > 4000) return BadRequest(new { message = "Message is too long." });

        var peerRole = IsDriver ? "Manager" : "Driver";
        var peer = await _db.Users.FirstOrDefaultAsync(u =>
            u.Id == dto.ToUserId && u.CompanyId == Cid && u.IsActive && u.Role == peerRole);
        if (peer == null) return BadRequest(new { message = "Conversation partner not found." });

        var message = new Message
        {
            CompanyId = Cid,
            Content = content,
            IsFromDriver = IsDriver,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            SenderId = CurrentUserId,
            DriverId = IsDriver ? CurrentUserId : peer.Id,
            ManagerId = IsDriver ? peer.Id : CurrentUserId
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        return Ok(new { message.Id, message.CreatedAt });
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkRead([FromQuery] int withUserId)
    {
        if (IsDriver)
        {
            await _db.Messages
                .Where(m => m.CompanyId == Cid && m.DriverId == CurrentUserId &&
                            m.ManagerId == withUserId && !m.IsFromDriver && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        }
        else
        {
            await _db.Messages
                .Where(m => m.CompanyId == Cid && m.ManagerId == CurrentUserId &&
                            m.DriverId == withUserId && m.IsFromDriver && !m.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        }
        return Ok();
    }
}

public record SendMessageDto(string Content, int ToUserId);
