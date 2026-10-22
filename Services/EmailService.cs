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
        var subject = $"Vaš vozački račun za {companyName}";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;line-height:1.25;color:#111a24;">Pozvani ste da vozite za {H(companyName)}</h1>
            <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#475467;">
              Zdravo {H(driverName)}, vaša firma je kreirala LogistiTrack vozački račun za vas.
              Odaberite lozinku i možete početi primati transporte.
            </p>
            <table role="presentation" style="margin:0 0 22px;font-size:14px;color:#111a24;">
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Korisničko ime</td><td style="font-weight:700;">{H(username)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Firma</td><td style="font-weight:700;">{H(companyName)}</td></tr>
              <tr><td style="padding:4px 16px 4px 0;color:#667085;">Link istječe</td><td style="font-weight:700;">{expiresAt:dd MMM yyyy HH:mm} UTC</td></tr>
            </table>
            <a href="{H(inviteLink)}" style="display:inline-block;background:#111a24;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;padding:13px 22px;border-radius:8px;">
              Postavi moju lozinku
            </a>
            <p style="margin:24px 0 0;font-size:12px;line-height:1.6;color:#98a2b3;">
              Ako dugme ne radi, kopirajte ovaj link u preglednik:<br>
              <a href="{H(inviteLink)}" style="color:#e8590c;word-break:break-all;">{H(inviteLink)}</a>
            </p>
            <p style="margin:18px 0 0;font-size:12px;color:#98a2b3;">
              Primili ste ovaj email jer {H(companyName)} koristi LogistiTrack za transportne operacije.
            </p>
            """);
        return await SendAsync(to, subject, html);
    }

    public async Task<(bool Sent, string? Error)> SendPasswordResetAsync(
        string to, string driverName, string companyName, string username, string link, DateTime expiresAt)
    {
        var subject = $"Resetujte LogistiTrack lozinku · {companyName}";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;line-height:1.25;color:#111a24;">Postavite novu lozinku</h1>
            <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#475467;">
              Zdravo {H(driverName)}, {H(companyName)} je zatražila novu lozinku za vaš LogistiTrack vozački račun
              (<strong>{H(username)}</strong>). Link vrijedi do {expiresAt:dd MMM yyyy HH:mm} UTC.
            </p>
            <a href="{H(link)}" style="display:inline-block;background:#111a24;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;padding:13px 22px;border-radius:8px;">
              Odaberite novu lozinku
            </a>
            <p style="margin:24px 0 0;font-size:12px;color:#98a2b3;word-break:break-all;">{H(link)}</p>
            """);
        return await SendAsync(to, subject, html);
    }

    public async Task<(bool Sent, string? Error)> SendTestAsync(string to)
    {
        var appName = _cfg["App:Name"] ?? "LogistiTrack";
        var html = Template($"""
            <h1 style="margin:0 0 12px;font-size:24px;color:#111a24;">{H(appName)} email radi</h1>
            <p style="margin:0;font-size:15px;line-height:1.6;color:#475467;">
              Ovo je testna poruka sa vaše LogistiTrack platforme. Pozivnice vozačima i linkovi za lozinku
              koristit će ovaj identitet pošiljaoca.
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
                Transportne operacije
              </div>
            </div>
            <div style="padding:30px 28px 34px;">
              {{content}}
            </div>
            <div style="border-top:1px solid #e4e6e3;padding:16px 28px;font-size:11px;color:#98a2b3;">
              Poslao LogistiTrack u ime {{H(_cfg["App:Name"] ?? "vaše transportne firme")}}.
            </div>
          </div>
        </div>
        </body></html>
        """;
}
