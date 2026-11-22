using System.Text.Json;
using Application.Abstractions;
using Application.Strategies;
using Microsoft.Extensions.Logging;
namespace Infrastructure.External;

/// Route distance / time from the public OSRM demo server (free, no key, no SLA: fine for a prototype).
/// If it is unreachable we return an ESTIMATE (straight-line x 1.3, 40 km/h) clearly marked Estimated = true.
public class OsrmRoutingService : IRoutingService
{
    private readonly HttpClient _http; private readonly ILogger<OsrmRoutingService> _log;
    public OsrmRoutingService(HttpClient http, ILogger<OsrmRoutingService> log) { _http = http; _log = log; }

    public async Task<RouteInfo> RouteAsync(double fromLat, double fromLon, double toLat, double toLon, CancellationToken ct = default)
    {
        try
        {
            var url = FormattableString.Invariant($"route/v1/driving/{fromLon},{fromLat};{toLon},{toLat}?overview=false");
            using var res = await _http.GetAsync(url, ct);
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var r = doc.RootElement.GetProperty("routes")[0];
            return new RouteInfo(Math.Round(r.GetProperty("distance").GetDouble() / 1000, 1), Math.Round(r.GetProperty("duration").GetDouble() / 60, 0), false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            _log.LogWarning(ex, "Routing service unavailable; using an estimate.");
            var km = GeoMath.HaversineKm(fromLat, fromLon, toLat, toLon) * 1.3;
            return new RouteInfo(Math.Round(km, 1), Math.Round(km / 40.0 * 60.0, 0), true);
        }
    }
}
