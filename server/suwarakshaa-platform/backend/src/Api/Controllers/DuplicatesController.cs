using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Possible duplicate requests are FLAGGED for a human, never auto-deleted or auto-merged.
[ApiController]
[Route("api/duplicates")]
[Authorize(Roles = AppRoles.Staff)]
public class DuplicatesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DuplicatesController(AppDbContext db) => _db = db;
    public class ResolveDto { [Required] public string Decision { get; set; } = ""; }   // Confirmed | NotDuplicate

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "Pending", CancellationToken ct = default) =>
        Ok(await (from d in _db.DuplicateCandidates.Where(x => x.Status == status)
                  join a in _db.AssistanceRequests on d.RequestId equals a.RequestId
                  join b in _db.AssistanceRequests on d.OtherRequestId equals b.RequestId
                  orderby d.SimilarityScore descending
                  select new { d.DuplicateId, d.SimilarityScore, d.Status, A = new { a.RequestId, a.ReferenceNo, a.Description }, B = new { b.RequestId, b.ReferenceNo, b.Description } })
                 .Take(100).ToListAsync(ct));

    [HttpPost("{id:long}/resolve")]
    public async Task<IActionResult> Resolve(long id, ResolveDto dto, CancellationToken ct)
    {
        var d = await _db.DuplicateCandidates.FindAsync(new object[] { id }, ct);
        if (d is null) return NotFound();
        if (d.Status != "Pending") return Conflict(new { error = "Already reviewed." });
        var uid = User.GetUserId();
        d.ReviewedBy = uid; d.ReviewedAt = DateTime.UtcNow;

        if (dto.Decision == "NotDuplicate") d.Status = "NotDuplicate";
        else if (dto.Decision == "Confirmed")
        {
            d.Status = "ConfirmedDuplicate";
            var newer = await _db.AssistanceRequests.FindAsync(new object[] { Math.Max(d.RequestId, d.OtherRequestId) }, ct);
            if (newer is not null && newer.Status is RequestStatus.Submitted or RequestStatus.UnderReview)
            {
                var from = newer.Status.ToString();
                newer.ChangeStatus(RequestStatus.Merged);
                d.Status = "Merged";
                _db.RequestHistory.Add(new RequestHistory { RequestId = newer.RequestId, FromStatus = from, ToStatus = "Merged", ChangedBy = uid, Note = "Merged as duplicate" });
                Workflow.Notify(_db, newer.RequestId, "REQUEST_MERGED", "Your request was merged", "We found you already asked for the same help. We are handling it under your earlier request.");
            }
        }
        else return BadRequest(new { error = "Decision must be Confirmed or NotDuplicate." });

        Workflow.Audit(_db, uid, "DUPLICATE_" + d.Status.ToUpperInvariant(), "DuplicateCandidate", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { d.Status });
    }
}
