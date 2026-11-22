using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/requests/{requestId:long}/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly AppDbContext _db;
    public FeedbackController(AppDbContext db) => _db = db;
    public class FeedbackDto { [Range(1, 5)] public byte Rating { get; set; } [StringLength(1000)] public string? Comment { get; set; } }

    /// The citizen rates the help received (only once, only after the request is fulfilled or closed).
    [HttpPost]
    public async Task<IActionResult> Create(long requestId, FeedbackDto dto, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null) return NotFound();
        if (req.SubmittedBy != User.GetUserId()) return Forbid();
        if (req.Status is not (RequestStatus.Fulfilled or RequestStatus.Closed)) return Conflict(new { error = "Feedback is possible after the help has been delivered." });
        if (await _db.Feedback.AnyAsync(f => f.RequestId == requestId, ct)) return Conflict(new { error = "Feedback was already given." });
        _db.Feedback.Add(new Feedback { RequestId = requestId, Rating = dto.Rating, Comment = TextSanitizer.Clean(dto.Comment, 1000) });
        Workflow.Audit(_db, User.GetUserId(), "FEEDBACK_GIVEN", "AssistanceRequest", requestId.ToString());
        await _db.SaveChangesAsync(ct);
        return Created("", new { message = "Thank you for your feedback." });
    }
}
