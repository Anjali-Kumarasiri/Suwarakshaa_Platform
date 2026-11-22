using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace Infrastructure.External;

/// OpenStreetMap Nominatim (free, no key). Results are cached in GeocodeCache to limit external calls.
/// Limitations to document in the report: usage policy (<=1 req/s), accuracy, no SLA, address language.
public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _http; private readonly AppDbContext _db; private readonly ILogger<NominatimGeocodingService> _log;
    public NominatimGeocodingService(HttpClient http, AppDbContext db, ILogger<NominatimGeocodingService> log) { _http = http; _db = db; _log = log; }

    public record Item([property: JsonPropertyName("lat")] string Lat, [property: JsonPropertyName("lon")] string Lon,
                        [property: JsonPropertyName("display_name")] string? DisplayName);

    public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        var q = address.Trim();
        if (q.Length < 3 || q.Length > 200) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(q.ToLowerInvariant()))).ToLowerInvariant();

        var cached = await _db.GeocodeCache.FirstOrDefaultAsync(x => x.QueryHash == hash, ct);
        if (cached is not null) return new GeoPoint(cached.Latitude, cached.Longitude, cached.FormattedAddress);

        try
        {
            var items = await _http.GetFromJsonAsync<List<Item>>($"search?q={Uri.EscapeDataString(q)}&format=json&limit=1", ct);
            var first = items?.FirstOrDefault();
            if (first is null) return null;
            var lat = decimal.Parse(first.Lat, CultureInfo.InvariantCulture);
            var lon = decimal.Parse(first.Lon, CultureInfo.InvariantCulture);
            _db.GeocodeCache.Add(new GeocodeCache { QueryHash = hash, Latitude = lat, Longitude = lon, FormattedAddress = first.DisplayName });
            await _db.SaveChangesAsync(ct);
            return new GeoPoint(lat, lon, first.DisplayName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            _log.LogWarning(ex, "Geocoding unavailable; continuing without coordinates.");
            return null;
        }
    }

    /// Reverse geocoding: coordinates -> readable address (cached).
    public async Task<GeoPoint?> ReverseAsync(decimal latitude, decimal longitude, CancellationToken ct = default)
    {
        var key = FormattableString.Invariant($"rev:{latitude:F4},{longitude:F4}");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var cached = await _db.GeocodeCache.FirstOrDefaultAsync(x => x.QueryHash == hash, ct);
        if (cached is not null) return new GeoPoint(cached.Latitude, cached.Longitude, cached.FormattedAddress);
        try
        {
            var url = FormattableString.Invariant($"reverse?lat={latitude}&lon={longitude}&format=json");
            var item = await _http.GetFromJsonAsync<Item>(url, ct);
            if (item?.Lat is null || item.Lon is null) return null;
            _db.GeocodeCache.Add(new GeocodeCache { QueryHash = hash, Latitude = latitude, Longitude = longitude, FormattedAddress = item.DisplayName });
            await _db.SaveChangesAsync(ct);
            return new GeoPoint(latitude, longitude, item.DisplayName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            _log.LogWarning(ex, "Reverse geocoding unavailable.");
            return null;
        }
    }
}
