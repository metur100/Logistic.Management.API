using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using LogisticsAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LogisticsAPI.Services;

public class EmailService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<EmailService> _log;

    public EmailService(AppDbContext db, IConfiguration cfg, ILogger<EmailService> log)
    {
        _db = db; _cfg = cfg; _log = log;
    }

    public async Task<(bool Sent, string? Error)> SendAsync(string to, string subject, string html)
    {
        try
        {
            var smtp = await _db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync();
            if (smtp == null || string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.Username))
                return (false, "Email is not configured yet.");

            using var message = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, string.IsNullOrWhiteSpace(smtp.FromName) ? "LogistiTrack" : smtp.FromName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = html,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                DeliveryNotificationOptions = DeliveryNotificationOptions.None
            };
            message.To.Add(new MailAddress(to));

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtp.Username, smtp.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };

            await client.SendMailAsync(message);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Sending email to {To} failed", to);
            return (false, ex.Message);
        }
    }

    public async Task<(bool Sent, string? Error)> SendInviteAsync(
        string to, string driverName, string companyName, string username, string inviteLink, DateTime expiresAt)
    {
        var subject = $"Your driver account for {companyName}";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;line-height:1.25;color:#111a24;">You're invited to drive for {H(companyName)}</h1>
            <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#475467;">
              Hi {H(driverName)}, your company created a LogistiTrack driver account for you.
              Choose your password and you can start receiving transports.
            </p>
            <table role="presentation" style="margin:0 0 22px;font-size:14px;color:#111a24;">
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Username</td><td style="font-weight:700;">{H(username)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Company</td><td style="font-weight:700;">{H(companyName)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Link expires</td><td style="font-weight:700;">{expiresAt:dd MMM yyyy HH:mm} UTC</td></tr>
            </table>
            <a href="{H(inviteLink)}" style="display:inline-block;background:#111a24;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;padding:13px 22px;border-radius:8px;">
              Set my password
            </a>
            <p style="margin:24px 0 0;font-size:12px;line-height:1.6;color:#98a2b3;">
              If the button does not work, copy this link into your browser:<br>
              <a href="{H(inviteLink)}" style="color:#e8590c;word-break:break-all;">{H(inviteLink)}</a>
            </p>
            <p style="margin:18px 0 0;font-size:12px;color:#98a2b3;">
              You received this email because {H(companyName)} uses LogistiTrack for transport operations.
            </p>
            """);
        return await SendAsync(to, subject, html);
    }

    public async Task<(bool Sent, string? Error)> SendPasswordResetAsync(
        string to, string driverName, string companyName, string username, string link, DateTime expiresAt)
    {
        var subject = $"Reset your LogistiTrack password · {companyName}";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;line-height:1.25;color:#111a24;">Set a new password</h1>
            <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#475467;">
              Hi {H(driverName)}, {H(companyName)} requested a new password for your LogistiTrack driver account
              (<strong>{H(username)}</strong>). The link is valid until {expiresAt:dd MMM yyyy HH:mm} UTC.
            </p>
            <a href="{H(link)}" style="display:inline-block;background:#111a24;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;padding:13px 22px;border-radius:8px;">
              Choose a new password
            </a>
            <p style="margin:24px 0 0;font-size:12px;color:#98a2b3;word-break:break-all;">{H(link)}</p>
            """);
        return await SendAsync(to, subject, html);
    }

    public async Task<(bool Sent, string? Error)> SendTestAsync(string to)
    {
        var appName = _cfg["App:Name"] ?? "LogistiTrack";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;color:#111a24;">{H(appName)} email is working</h1>
            <p style="margin:0;font-size:15px;line-height:1.6;color:#475467;">
              This is a test message from your LogistiTrack platform. Driver invitations and password links
              will use this sender identity.
            </p>
            """);
        return await SendAsync(to, $"{appName} · SMTP test", html);
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

    private string Template(string content) => $$"""
        <!doctype html>
        <html><body style="margin:0;padding:0;background:#f4f5f3;">
        <div style="padding:32px 16px;">
          <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e4e6e3;border-radius:14px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;">
            <div style="background:#111a24;padding:22px 28px;">
              <div style="font-size:18px;font-weight:800;letter-spacing:-0.02em;color:#ffffff;">
                Logisti<span style="color:#f08c4a;">Track</span>
              </div>
              <div style="font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:#98a2b3;margin-top:4px;">
                Transport operations
              </div>
            </div>
            <div style="padding:30px 28px 34px;">
              {{content}}
            </div>
            <div style="border-top:1px solid #e4e6e3;padding:16px 28px;font-size:11px;color:#98a2b3;">
              Sent by LogistiTrack on behalf of {{H(_cfg["App:Name"] ?? "your transport company")}}.
            </div>
          </div>
        </div>
        </body></html>
        """;
}
