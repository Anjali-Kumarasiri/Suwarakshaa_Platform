using Api.Extensions;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/ai-recommendations")]
[Authorize(Roles = "EmergencyCoordinator,CaseManager")]
public class AiRecommendationsController : ControllerBase
{
    private readonly AiGovernanceService _gov;
    private readonly AppDbContext _db;
    public AiRecommendationsController(AiGovernanceService gov, AppDbContext db) { _gov = gov; _db = db; }

    public record SecondApprovalRequest(bool Approve, string? Reason);
    public record DecisionRequest(DecisionAction Action, string? FinalValueJson, string? Reason);

    /// GET api/ai-recommendations?status=PendingReview
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status = "PendingReview", CancellationToken ct = default)
    {
        var q = _db.AiRecommendations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RecommendationStatus>(status, true, out var s)) return BadRequest(new { error = "Unknown status." });
            q = q.Where(r => r.Status == s);
        }
        var items = await (from r in q
                           from rq in _db.AssistanceRequests.Where(x => x.RequestId == r.RequestId)
                           orderby r.CreatedAt
                           select new
                           {
                               r.RecommendationId, r.RequestId, r.Type, r.OutputJson, r.Confidence, r.Rationale, r.IsFallback,
                               Status = r.Status, r.CreatedAt,
                               Description = rq.Description, rq.Category, rq.PeopleAffected
                           }).Take(100).ToListAsync(ct);
        return Ok(items);
    }

    /// POST api/ai-recommendations/12/decision  { "action":"Modify", "finalValueJson":"{\"category\":\"Medicine\",\"urgency\":4}", "reason":"..." }
    [HttpPost("{id:long}/decision")]
    public async Task<IActionResult> Decide(long id, DecisionRequest body, CancellationToken ct)
    {
        try
        {
            var d = await _gov.DecideAsync(id, body.Action, body.FinalValueJson, body.Reason,
                                           User.GetUserId(), User.IsInRole("EmergencyCoordinator"), ct);
            return Ok(new
            {
                d.DecisionId, Action = d.Action.ToString(), d.DecidedAt, requiresSecondApproval = d.RequiresSecondApproval,
                message = d.RequiresSecondApproval ? "Critical decision recorded. It will take effect after a different Emergency Coordinator approves it." : "Decision applied."
            });
        }
        catch (KeyNotFoundException e)        { return NotFound(new { error = e.Message }); }
        catch (ArgumentException e)           { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e)   { return Conflict(new { error = e.Message }); }
    }

    /// Critical decisions proposed by case managers, waiting for a coordinator.
    [HttpGet("pending-approval"), Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> PendingApproval(CancellationToken ct) =>
        Ok(await (from d in _db.HumanDecisions.Where(x => x.RequiresSecondApproval && x.SecondApprovedAt == null)
                  join r in _db.AiRecommendations on d.RecommendationId equals r.RecommendationId
                  join u in _db.Users on d.DecidedBy equals u.UserId
                  select new { d.RecommendationId, r.RequestId, ProposedAction = d.Action, d.FinalValueJson, d.Reason, ProposedBy = u.FullName, d.DecidedAt, AiOutput = r.OutputJson, r.Confidence }).ToListAsync(ct));

    /// Four-eyes: a DIFFERENT coordinator approves or refuses the proposed critical decision.
    [HttpPost("{id:long}/second-approval"), Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> SecondApproval(long id, SecondApprovalRequest body, CancellationToken ct)
    {
        try
        {
            var d = await _gov.SecondApprovalAsync(id, body.Approve, body.Reason, User.GetUserId(), User.IsInRole("EmergencyCoordinator"), ct);
            return Ok(new { d.DecisionId, approved = body.Approve });
        }
        catch (KeyNotFoundException e)        { return NotFound(new { error = e.Message }); }
        catch (ArgumentException e)           { return BadRequest(new { error = e.Message }); }
        catch (UnauthorizedAccessException e) { return StatusCode(403, new { error = e.Message }); }
        catch (InvalidOperationException e)   { return Conflict(new { error = e.Message }); }
    }

    /// Post-incident review of AI-assisted decisions: what the AI said, what the human did, and why.
    [HttpGet("review"), Authorize(Roles = "EmergencyCoordinator,CaseManager,SystemAdmin")]
    public async Task<IActionResult> Review([FromQuery(Name = "from")] DateTime? fromDate, [FromQuery(Name = "to")] DateTime? toDate, [FromQuery] bool onlyOverrides = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200); page = Math.Max(page, 1);
        var q = from d in _db.HumanDecisions
                join r in _db.AiRecommendations on d.RecommendationId equals r.RecommendationId
                join u in _db.Users on d.DecidedBy equals u.UserId
                select new { d, r, u };
        if (fromDate is not null) q = q.Where(x => x.d.DecidedAt >= fromDate.Value.ToUniversalTime());
        if (toDate is not null) q = q.Where(x => x.d.DecidedAt <= toDate.Value.ToUniversalTime());
        if (onlyOverrides) q = q.Where(x => x.d.Action == DecisionAction.Modify || x.d.Action == DecisionAction.Reject);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.d.DecidedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.r.RecommendationId, x.r.RequestId, x.r.Type, AiOutput = x.r.OutputJson, x.r.Confidence, x.r.Rationale, x.r.IsFallback,
                Action = x.d.Action, FinalValue = x.d.FinalValueJson, x.d.Reason, DecidedBy = x.u.FullName, x.d.DecidedAt,
                x.d.RequiresSecondApproval, x.d.SecondApprovedBy, x.d.SecondApprovedAt
            }).ToListAsync(ct);
        var byAction = await _db.HumanDecisions.GroupBy(d => d.Action).Select(g => new { Action = g.Key, Count = g.Count() }).ToListAsync(ct);
        return Ok(new { total, page, pageSize, summary = byAction, items });
    }
}
