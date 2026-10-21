using System.Globalization;
using System.Text.Json;
using LogisticsAPI.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace LogisticsAPI.Services;

/// <summary>
/// Resolves a driving route between two free-text places using OpenStreetMap
/// (Nominatim/Photon for geocoding, OSRM for routing) and returns distance + duration.
/// </summary>
public class RouteService
{
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RouteService> _log;

    public RouteService(IHttpClientFactory http, IMemoryCache cache, ILogger<RouteService> log)
    {
        _http = http; _cache = cache; _log = log;
    }

    private HttpClient Client => _http.CreateClient("routing");

    public async Task<RouteDistanceDto?> CalculateAsync(string origin, string destination, CancellationToken ct = default)
    {
        origin = origin?.Trim() ?? "";
        destination = destination?.Trim() ?? "";
        if (origin.Length < 2 || destination.Length < 2) return null;

        var cacheKey = $"route::{origin.ToLowerInvariant()}::{destination.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out RouteDistanceDto? cached) && cached != null)
            return cached;

        try
        {
            var a = await GeocodeAsync(origin, ct);
            var b = await GeocodeAsync(destination, ct);
            if (a == null || b == null)
            {
                _log.LogWarning("Could not geocode route {Origin} -> {Destination}", origin, destination);
                return null;
            }

            var coords = string.Create(CultureInfo.InvariantCulture,
                $"{a.Value.Lon},{a.Value.Lat};{b.Value.Lon},{b.Value.Lat}");
            var endpoints = new[]
            {
                $"https://router.project-osrm.org/route/v1/driving/{coords}?overview=false&alternatives=false",
                $"https://routing.openstreetmap.de/routed-car/route/v1/driving/{coords}?overview=false&alternatives=false"
            };

            foreach (var url in endpoints)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.UserAgent.ParseAdd("LogistiTrack/1.0 (certidevelopment@gmail.com)");
                    using var res = await Client.SendAsync(req, ct);
                    if (!res.IsSuccessStatusCode) continue;

                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("code", out var code) || code.GetString() != "Ok") continue;

                    var route = root.GetProperty("routes")[0];
                    var meters = route.GetProperty("distance").GetDouble();
                    var seconds = route.GetProperty("duration").GetDouble();

                    var result = new RouteDistanceDto(
                        origin, destination,
                        Math.Round((decimal)(meters / 1000.0), 1),
                        (int)Math.Round(seconds / 60.0),
                        a.Value.Label, b.Value.Label,
                        a.Value.Lat, a.Value.Lon, b.Value.Lat, b.Value.Lon,
                        "osrm");

                    _cache.Set(cacheKey, result, TimeSpan.FromHours(12));
                    return result;
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Routing endpoint failed: {Url}", url);
                }
            }

            return Fallback(origin, destination, a.Value, b.Value);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Route calculation failed for {Origin} -> {Destination}", origin, destination);
            return null;
        }
    }

    private RouteDistanceDto? Fallback(string origin, string destination, GeoPoint a, GeoPoint b)
    {
        var km = Math.Round((decimal)(HaversineKm(a.Lat, a.Lon, b.Lat, b.Lon) * 1.22), 1);
        if (km <= 0) return null;
        var minutes = (int)Math.Round((double)km / 75.0 * 60.0);
        var result = new RouteDistanceDto(
            origin, destination, km, minutes, a.Label, b.Label,
            a.Lat, a.Lon, b.Lat, b.Lon, "estimate");
        return result;
    }

    private async Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct)
    {
        // Nominatim first (respectful usage: low volume + descriptive user agent).
        try
        {
            var url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q=" +
                      Uri.EscapeDataString(query);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("LogistiTrack/1.0 (certidevelopment@gmail.com)");
            req.Headers.AcceptLanguage.ParseAdd("en");
            using var res = await Client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    var lat = double.Parse(item.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture);
                    var lon = double.Parse(item.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture);
                    var label = item.TryGetProperty("display_name", out var d) ? d.GetString() ?? query : query;
                    return new GeoPoint(lat, lon, Trim(label));
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Nominatim geocode failed for {Query}", query);
        }

        // Photon as a fallback.
        try
        {
            var url = "https://photon.komoot.io/api/?limit=1&q=" + Uri.EscapeDataString(query);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("LogistiTrack/1.0 (certidevelopment@gmail.com)");
            using var res = await Client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                var features = doc.RootElement.GetProperty("features");
                if (features.GetArrayLength() > 0)
                {
                    var f = features[0];
                    var coords = f.GetProperty("geometry").GetProperty("coordinates");
                    var lon = coords[0].GetDouble();
                    var lat = coords[1].GetDouble();
                    var label = query;
                    if (f.TryGetProperty("properties", out var props))
                    {
                        var name = props.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var city = props.TryGetProperty("city", out var c) ? c.GetString() : null;
                        var country = props.TryGetProperty("country", out var co) ? co.GetString() : null;
                        label = string.Join(", ", new[] { name, city, country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    }
                    return new GeoPoint(lat, lon, Trim(string.IsNullOrWhiteSpace(label) ? query : label));
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Photon geocode failed for {Query}", query);
        }

        return null;
    }

    private static string Trim(string value) => value.Length <= 180 ? value : value[..180];

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private readonly record struct GeoPoint(double Lat, double Lon, string Label);
}
