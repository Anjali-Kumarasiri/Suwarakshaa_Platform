using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "EmergencyCoordinator,CaseManager,SystemAdmin,LogisticsManager,ShelterAdmin")]
public class DashboardController : ControllerBase
{
    private readonly MetricsService _metrics;
    private readonly AppDbContext _db;
    public DashboardController(MetricsService metrics, AppDbContext db) { _metrics = metrics; _db = db; }

    /// Operational metrics: unresolved / overdue / escalated cases, AI override rate, shelter occupancy, resource utilisation, ...
    [HttpGet("metrics")]
    public async Task<IActionResult> Metrics(CancellationToken ct) => Ok(await _metrics.GetAsync(ct));

    /// The records behind a dashboard number. filter = unresolved | overdue | escalated | suspicious | pendingAi | aiFallback.
    /// Only summary fields are returned (no description, address or contact details).
    [HttpGet("cases")]
    public async Task<IActionResult> Cases([FromQuery] string filter, CancellationToken ct)
    {
        var done = new[] { RequestStatus.Fulfilled, RequestStatus.Closed, RequestStatus.Rejected, RequestStatus.Merged };
        var now = DateTime.UtcNow;
        if (filter is "pendingAi" or "aiFallback")
        {
            var recs = _db.AiRecommendations.AsQueryable();
            recs = filter == "pendingAi" ? recs.Where(r => r.Status == RecommendationStatus.PendingReview) : recs.Where(r => r.IsFallback);
            var rows = await recs.OrderByDescending(r => r.CreatedAt).Take(100)
                .Select(r => new { r.RecommendationId, r.RequestId, r.Type, r.Confidence, r.IsFallback, Status = r.Status.ToString(), r.CreatedAt }).ToListAsync(ct);
            return Ok(new { kind = "recommendations", items = rows });
        }
        var q = _db.AssistanceRequests.AsQueryable();
        switch (filter)
        {
            case "unresolved": q = q.Where(r => !done.Contains(r.Status)); break;
            case "overdue": q = q.Where(r => !done.Contains(r.Status) && r.DueAt != null && r.DueAt < now); break;
            case "escalated": q = q.Where(r => r.Status == RequestStatus.Escalated); break;
            case "suspicious": q = q.Where(r => r.IsFlaggedSuspicious); break;
            default: return BadRequest(new { error = "Unknown filter." });
        }
        var items = await q.OrderByDescending(r => r.FinalPriority).ThenBy(r => r.CreatedAt).Take(100)
            .Select(r => new { r.RequestId, r.ReferenceNo, r.Category, r.PeopleAffected, r.FinalPriority, Status = r.Status.ToString(), r.DueAt, r.CreatedAt }).ToListAsync(ct);
        return Ok(new { kind = "requests", items });
    }
}
