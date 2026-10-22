using System.Net.Http.Headers;
using System.Text.Json;
using LogisticsAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace LogisticsAPI.Services;

public sealed record ImgBBUploadResult(string Url, string ThumbUrl, string? DeleteUrl);

/// <summary>
/// Uploads CMR / delivery-note photos to ImgBB. The API key is stored in the
/// PlatformSettings row (never in source) and can be overridden with IMGBB_API_KEY.
/// </summary>
public class ImgBBService
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<ImgBBService> _log;

    public ImgBBService(HttpClient http, AppDbContext db, IConfiguration config, ILogger<ImgBBService> log)
    {
        _http = http;
        _db = db;
        _config = config;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private async Task<string?> GetApiKeyAsync()
    {
        var setting = await _db.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        var key = setting?.ImgBBApiKey;
        if (string.IsNullOrWhiteSpace(key))
            key = _config["ImgBB:ApiKey"] ?? Environment.GetEnvironmentVariable("IMGBB_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public async Task<ImgBBUploadResult> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = await GetApiKeyAsync();
        if (key == null)
            throw new InvalidOperationException("ImgBB API key is not configured.");

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "image", fileName);

        var endpoint = $"https://api.imgbb.com/1/upload?key={Uri.EscapeDataString(key)}";
        using var response = await _http.PostAsync(endpoint, form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning("ImgBB upload failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException("ImgBB upload failed.");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data))
            throw new HttpRequestException("ImgBB returned an invalid response.");

        var url = data.TryGetProperty("url", out var u) ? u.GetString() : null;
        var thumb = data.TryGetProperty("thumb", out var t) && t.TryGetProperty("url", out var tu)
            ? tu.GetString()
            : url;
        var deleteUrl = data.TryGetProperty("delete_url", out var du) ? du.GetString() : null;

        if (string.IsNullOrWhiteSpace(url))
            throw new HttpRequestException("ImgBB returned no image URL.");

        return new ImgBBUploadResult(url!, thumb ?? url!, deleteUrl);
    }
}
