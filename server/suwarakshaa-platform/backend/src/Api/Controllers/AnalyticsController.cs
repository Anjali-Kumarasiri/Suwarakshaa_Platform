using System.Text.Json;
using Api.Services;
using Application.Abstractions;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Reporting and analytics: forecasting, AI crisis summary, coverage analysis, KPI history, feedback, workload, shortages.
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "EmergencyCoordinator,CaseManager,LogisticsManager,ShelterAdmin,SystemAdmin")]
public class AnalyticsController : ControllerBase
{
    private static readonly RequestStatus[] Done = { RequestStatus.Fulfilled, RequestStatus.Closed, RequestStatus.Rejected, RequestStatus.Merged };
    private readonly AppDbContext _db; private readonly MetricsService _metrics; private readonly IAiAssistantClient _ai;
    public AnalyticsController(AppDbContext db, MetricsService metrics, IAiAssistantClient ai) { _db = db; _metrics = metrics; _ai = ai; }

    /// Demand forecast for a request category: linear trend over the last 14 days (statistical baseline, confidence stated).
    [HttpGet("forecast")]
    public async Task<IActionResult> Forecast([FromQuery] string category = "Food", [FromQuery] int days = 3, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-13);
        var perDay = await _db.AssistanceRequests.Where(r => r.Category == category && r.CreatedAt >= from)
            .GroupBy(r => r.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct);
        var series = Enumerable.Range(0, 14).Select(i => perDay.FirstOrDefault(x => x.Day == from.AddDays(i).Date)?.Count ?? 0).ToList();
        var f = DemandForecaster.Forecast(series, days);
        return Ok(new { category, history = series, f.Points, f.TrendPerDay, f.Confidence, method = "linear trend over last 14 days" });
    }

    /// AI-generated crisis briefing for coordinators. Only AGGREGATE numbers are sent to the AI (no personal data).
    [HttpGet("summary"), Authorize(Roles = AppRoles.Staff)]
    public async Task<IActionResult> Summary([FromQuery] string language = "en", CancellationToken ct = default)
    {
        language = new[] { "en", "si", "ta" }.Contains(language) ? language : "en";
        var m = await _metrics.GetAsync(ct);
        var byCategory = await _db.AssistanceRequests.Where(r => !Done.Contains(r.Status)).GroupBy(r => r.Category).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var lowStock = await _db.ResourceInventories.CountAsync(i => i.QuantityTotal - i.QuantityAllocated <= i.ReorderLevel, ct);
        var json = JsonSerializer.Serialize(new
        {
            m.UnresolvedCases, m.OverdueCases, m.EscalatedCases, m.PendingAiReviews, m.ShelterOccupancyPct, m.ResourceUtilisationPct, m.AiOverrideRatePct,
            LowStockLocations = lowStock, OpenRequestsByCategory = byCategory.ToDictionary(x => x.Key, x => x.Count)
        });
        var s = await _ai.SummarizeAsync(json, language, ct);
        return Ok(new { summary = s.Text, usedFallback = s.IsFallback, generatedAt = DateTime.UtcNow, note = "Draft for coordinators: verify before acting." });
    }

    /// Service-coverage analysis: how many open requests with a location are within reach of an open shelter.
    [HttpGet("coverage")]
    public async Task<IActionResult> Coverage([FromQuery] double radiusKm = 10, CancellationToken ct = default)
    {
        radiusKm = Math.Clamp(radiusKm, 1, 200);
        var shelters = await (from s in _db.Shelters.Where(s => s.Status != "Closed")
                              from sa in _db.ServiceAreas.Where(x => x.ShelterId == s.ShelterId).DefaultIfEmpty()
                              select new { s.ShelterId, s.Name, s.Latitude, s.Longitude, Radius = (decimal?)sa.RadiusKm }).ToListAsync(ct);
        var reqs = await _db.AssistanceRequests.Where(r => !Done.Contains(r.Status) && r.Latitude != null && r.Longitude != null)
            .Select(r => new { r.RequestId, r.ReferenceNo, r.AreaId, Lat = r.Latitude!.Value, Lon = r.Longitude!.Value }).Take(5000).ToListAsync(ct);

        var rows = reqs.Select(r =>
        {
            var best = shelters.Select(s => new { s.Name, Km = GeoMath.HaversineKm((double)r.Lat, (double)r.Lon, (double)s.Latitude, (double)s.Longitude), Reach = (double)(s.Radius ?? (decimal)radiusKm) })
                               .OrderBy(x => x.Km).FirstOrDefault();
            return new { r.RequestId, r.ReferenceNo, r.AreaId, NearestShelter = best?.Name, DistanceKm = best is null ? (double?)null : Math.Round(best.Km, 1), Covered = best is not null && best.Km <= best.Reach };
        }).ToList();

        var areas = await _db.OperationalAreas.ToDictionaryAsync(a => a.AreaId, a => a.Name, ct);
        return Ok(new
        {
            openRequestsWithLocation = rows.Count, covered = rows.Count(x => x.Covered),
            coveragePct = rows.Count == 0 ? 100 : Math.Round(100.0 * rows.Count(x => x.Covered) / rows.Count, 1),
            byArea = rows.GroupBy(x => x.AreaId).Select(g => new { area = g.Key is int id && areas.ContainsKey(id) ? areas[id] : "Outside defined areas", requests = g.Count(), uncovered = g.Count(x => !x.Covered) }),
            uncovered = rows.Where(x => !x.Covered).Take(50)
        });
    }

    /// Historical KPIs (hourly snapshots) for trend charts.
    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis([FromQuery] int days = 7, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 90));
        return Ok(await _db.KpiSnapshots.Where(k => k.CapturedAt >= since).OrderBy(k => k.CapturedAt).Take(2000).ToListAsync(ct));
    }

    [HttpGet("feedback")]
    public async Task<IActionResult> FeedbackSummary(CancellationToken ct)
    {
        var rows = await _db.Feedback.GroupBy(f => f.Rating).Select(g => new { Rating = (int)g.Key, Count = g.Count() }).ToListAsync(ct);
        var total = rows.Sum(r => r.Count);
        return Ok(new { responses = total, averageRating = total == 0 ? (double?)null : Math.Round(rows.Sum(r => r.Rating * r.Count) / (double)total, 2), distribution = rows.OrderBy(r => r.Rating) });
    }

    [HttpGet("workload")]
    public async Task<IActionResult> Workload(CancellationToken ct)
    {
        var byStatus = await _db.AssistanceRequests.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var byOrg = await (from r in _db.AssistanceRequests.Where(r => r.AssignedOrgId != null && !Done.Contains(r.Status))
                           join o in _db.Organisations on r.AssignedOrgId equals o.OrganisationId
                           group r by o.Name into g select new { Organisation = g.Key, Open = g.Count() }).ToListAsync(ct);
        var byUser = await (from r in _db.AssistanceRequests.Where(r => r.AssignedTo != null && !Done.Contains(r.Status))
                            join u in _db.Users on r.AssignedTo equals u.UserId
                            group r by u.FullName into g select new { Staff = g.Key, Open = g.Count() }).ToListAsync(ct);
        return Ok(new { byStatus, byOrganisation = byOrg, byStaff = byUser });
    }

    /// Resource shortages at a glance: low stock, requests that could not be fully served, open partner requests.
    [HttpGet("shortages")]
    public async Task<IActionResult> Shortages(CancellationToken ct)
    {
        var low = await (from i in _db.ResourceInventories join t in _db.ResourceTypes on i.ResourceTypeId equals t.ResourceTypeId
                         where i.QuantityTotal - i.QuantityAllocated <= i.ReorderLevel
                         select new { i.InventoryId, Resource = t.Name, i.LocationName, Available = i.QuantityTotal - i.QuantityAllocated, i.ReorderLevel }).ToListAsync(ct);
        var unmet = await _db.ResourceAllocations.Where(a => a.Status == "Preempted").GroupBy(a => a.InventoryId).Select(g => new { InventoryId = g.Key, Requests = g.Count(), UnmetQuantity = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        var partners = await _db.PartnerAssistanceRequests.CountAsync(p => p.Status == "Pending", ct);
        return Ok(new { lowStock = low, unmetDemand = unmet, pendingPartnerRequests = partners });
    }
}
