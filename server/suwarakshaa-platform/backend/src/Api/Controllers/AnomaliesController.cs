using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Requests flagged as suspicious. They are still processed normally: a human decides whether they are genuine.
[ApiController]
[Route("api/anomalies")]
[Authorize(Roles = AppRoles.Staff)]
public class AnomaliesController : ControllerBase
{
    private readonly AppDbContext _db;
    public AnomaliesController(AppDbContext db) => _db = db;
    public class ReviewDto { public bool Clear { get; set; } [StringLength(500)] public string? Note { get; set; } }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var flagged = await _db.AssistanceRequests.Where(r => r.IsFlaggedSuspicious).OrderByDescending(r => r.CreatedAt).Take(100)
            .Select(r => new { r.RequestId, r.ReferenceNo, r.Category, r.Description, r.PeopleAffected, r.CreatedAt, Status = r.Status }).ToListAsync(ct);
        var ids = flagged.Select(f => f.RequestId.ToString()).ToList();
        var reasons = await _db.AuditEvents.Where(a => a.Action == "ANOMALY_FLAGGED" && ids.Contains(a.EntityId)).ToListAsync(ct);
        return Ok(flagged.Select(f => new { f.RequestId, f.ReferenceNo, f.Category, f.Description, f.PeopleAffected, f.CreatedAt, f.Status, reasons = reasons.FirstOrDefault(r => r.EntityId == f.RequestId.ToString())?.NewValue }));
    }

    [HttpPost("{requestId:long}/review")]
    public async Task<IActionResult> Review(long requestId, ReviewDto dto, CancellationToken ct)
    {
        var r = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (r is null) return NotFound();
        if (dto.Clear) r.IsFlaggedSuspicious = false;
        Workflow.Audit(_db, User.GetUserId(), dto.Clear ? "ANOMALY_CLEARED" : "ANOMALY_CONFIRMED", "AssistanceRequest", requestId.ToString(), null, dto.Note);
        await _db.SaveChangesAsync(ct);
        return Ok(new { stillFlagged = r.IsFlaggedSuspicious });
    }
}
