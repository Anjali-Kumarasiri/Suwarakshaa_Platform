using Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Api.Controllers;

[ApiController]
[Route("api/geo")]
[Authorize]
public class GeoController : ControllerBase
{
    private readonly IGeocodingService _geo; private readonly IRoutingService _routing;
    public GeoController(IGeocodingService geo, IRoutingService routing) { _geo = geo; _routing = routing; }

    /// GET api/geo/geocode?address=Negombo%20Sri%20Lanka  -> { latitude, longitude, formattedAddress }
    [HttpGet("geocode")]
    public async Task<IActionResult> Geocode([FromQuery] string address, CancellationToken ct)
    {
        var p = await _geo.GeocodeAsync(address ?? "", ct);
        return p is null ? NotFound(new { error = "Address not found or map service unavailable." }) : Ok(p);
    }

    /// GET api/geo/reverse?lat=6.93&lon=79.86 -> readable address
    [HttpGet("reverse")]
    public async Task<IActionResult> Reverse([FromQuery] decimal lat, [FromQuery] decimal lon, CancellationToken ct)
    {
        if (lat is < -90 or > 90 || lon is < -180 or > 180) return BadRequest(new { error = "Invalid coordinates." });
        var p = await _geo.ReverseAsync(lat, lon, ct);
        return p is null ? NotFound(new { error = "No address found or map service unavailable." }) : Ok(p);
    }

    /// GET api/geo/route?fromLat=..&fromLon=..&toLat=..&toLon=.. -> distance (km) and time (minutes); estimated = true if the routing service was unavailable
    [HttpGet("route")]
    public async Task<IActionResult> Route([FromQuery] double fromLat, [FromQuery] double fromLon, [FromQuery] double toLat, [FromQuery] double toLon, CancellationToken ct)
    {
        if (new[] { fromLat, toLat }.Any(v => v is < -90 or > 90) || new[] { fromLon, toLon }.Any(v => v is < -180 or > 180)) return BadRequest(new { error = "Invalid coordinates." });
        return Ok(await _routing.RouteAsync(fromLat, fromLon, toLat, toLon, ct));
    }
}
