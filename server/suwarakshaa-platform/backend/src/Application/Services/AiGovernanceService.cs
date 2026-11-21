using System.Text.Json;
using Application.Abstractions;
using Domain.Entities;
namespace Application.Services;

/// Human-in-the-loop: the ONLY place an AI recommendation becomes a real decision.
/// Critical decisions (urgency 5, before or after editing) made by a non-coordinator are NOT applied: they wait for a
/// second, different person with the Emergency Coordinator role (four-eyes principle).
public class AiGovernanceService
{
    private readonly IRepository<AiRecommendation> _recs;
    private readonly IRepository<HumanDecision> _decisions;
    private readonly IRepository<AuditEvent> _audit;
    private readonly IRepository<OutboxMessage> _outbox;
    private readonly IUnitOfWork _uow;

    public AiGovernanceService(IRepository<AiRecommendation> recs, IRepository<HumanDecision> decisions,
        IRepository<AuditEvent> audit, IRepository<OutboxMessage> outbox, IUnitOfWork uow)
    { _recs = recs; _decisions = decisions; _audit = audit; _outbox = outbox; _uow = uow; }

    private static bool IsCritical(string? json) => json is not null && (json.Contains("\"urgency\":5") || json.Contains("\"urgency\": 5"));

    private static RecommendationStatus Map(DecisionAction a) => a switch
    {
        DecisionAction.Approve => RecommendationStatus.Approved,
        DecisionAction.Modify  => RecommendationStatus.Modified,
        DecisionAction.Reject  => RecommendationStatus.Rejected,
        _                      => RecommendationStatus.Deferred
    };

    public async Task<HumanDecision> DecideAsync(long recommendationId, DecisionAction action,
        string? finalValueJson, string? reason, int userId, bool userIsCoordinator, CancellationToken ct = default)
    {
        var rec = await _recs.GetByIdAsync(recommendationId, ct) ?? throw new KeyNotFoundException("Recommendation not found.");

        if (rec.Status is not (RecommendationStatus.PendingReview or RecommendationStatus.Deferred))
            throw new InvalidOperationException("Recommendation already decided.");              // blocks double-processing
        if (await _decisions.AnyAsync(d => d.RecommendationId == recommendationId && d.RequiresSecondApproval && d.SecondApprovedAt == null, ct))
            throw new InvalidOperationException("This recommendation is waiting for a second approval.");
        if (action is DecisionAction.Modify or DecisionAction.Reject && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required when modifying or rejecting.");

        bool needsSecond = (IsCritical(rec.OutputJson) || IsCritical(finalValueJson)) && !userIsCoordinator && action != DecisionAction.Defer;

        var decision = new HumanDecision
        {
            RecommendationId = rec.RecommendationId, Action = action, FinalValueJson = finalValueJson ?? rec.OutputJson,
            Reason = reason, DecidedBy = userId, DecidedAt = DateTime.UtcNow, RequiresSecondApproval = needsSecond
        };

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var old = rec.Status.ToString();
            rec.Status = needsSecond ? RecommendationStatus.Deferred : Map(action);       // nothing is applied yet when approval is pending
            await _decisions.AddAsync(decision, ct);
            await _audit.AddAsync(new AuditEvent
            {
                UserId = userId, Action = needsSecond ? "AI_DECISION_PROPOSED" : "AI_DECISION", EntityType = "AiRecommendation",
                EntityId = rec.RecommendationId.ToString(), OldValue = old,
                NewValue = JsonSerializer.Serialize(new { action = action.ToString(), rec.Confidence, rec.OutputJson, decision.FinalValueJson, reason, needsSecond })
            }, ct);
            if (!needsSecond) await _outbox.AddAsync(Event(rec, action), ct);
            await _uow.SaveChangesAsync(ct);
        }, ct);
        return decision;
    }

    /// A different Emergency Coordinator confirms (or refuses) a critical decision proposed by someone else.
    public async Task<HumanDecision> SecondApprovalAsync(long recommendationId, bool approve, string? reason, int approverId, bool approverIsCoordinator, CancellationToken ct = default)
    {
        if (!approverIsCoordinator) throw new UnauthorizedAccessException("Only an Emergency Coordinator can give second approval.");
        var decision = await _decisions.FirstOrDefaultAsync(d => d.RecommendationId == recommendationId && d.RequiresSecondApproval && d.SecondApprovedAt == null, ct)
                       ?? throw new KeyNotFoundException("No decision is waiting for approval.");
        if (decision.DecidedBy == approverId) throw new InvalidOperationException("The approver must be a different person (four-eyes principle).");
        if (!approve && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required to refuse.");
        var rec = await _recs.GetByIdAsync(recommendationId, ct) ?? throw new KeyNotFoundException("Recommendation not found.");

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            if (approve)
            {
                decision.SecondApprovedBy = approverId; decision.SecondApprovedAt = DateTime.UtcNow;
                rec.Status = Map(decision.Action);
                await _outbox.AddAsync(Event(rec, decision.Action), ct);
            }
            else
            {
                decision.RequiresSecondApproval = false;                // proposal closed; the recommendation returns to the queue
                rec.Status = RecommendationStatus.PendingReview;
            }
            await _audit.AddAsync(new AuditEvent
            {
                UserId = approverId, Action = approve ? "AI_DECISION_APPROVED" : "SECOND_APPROVAL_REFUSED", EntityType = "AiRecommendation",
                EntityId = rec.RecommendationId.ToString(), NewValue = JsonSerializer.Serialize(new { decision.DecisionId, approve, reason })
            }, ct);
            await _uow.SaveChangesAsync(ct);
        }, ct);
        return decision;
    }

    private static OutboxMessage Event(AiRecommendation rec, DecisionAction action) => new()
    {
        EventType = "AiRecommendationDecided",
        Payload = JsonSerializer.Serialize(new { rec.RecommendationId, rec.RequestId, action = action.ToString() })
    };
}
