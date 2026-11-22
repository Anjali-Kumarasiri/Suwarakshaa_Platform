using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Api.Services;

public record Metrics(int UnresolvedCases, int OverdueCases, int EscalatedCases, int PendingAiReviews, double AiOverrideRatePct, int AiFallbackCount,
                      int TotalDecisions, double ShelterOccupancyPct, double ResourceUtilisationPct, double? AvgResolutionMinutes,
                      int PendingDuplicateReviews, int SuspiciousRequests, int NotificationDeadLetters);

/// One place that computes the operational KPIs (used by the dashboard and by the hourly snapshot job).
public class MetricsService
{
    private readonly AppDbContext _db;
    public MetricsService(AppDbContext db) => _db = db;

    public async Task<Metrics> GetAsync(CancellationToken ct = default)
    {
        var done = new[] { RequestStatus.Fulfilled, RequestStatus.Closed, RequestStatus.Rejected, RequestStatus.Merged };
        var now = DateTime.UtcNow;
        var unresolved = await _db.AssistanceRequests.CountAsync(r => !done.Contains(r.Status), ct);
        var overdue = await _db.AssistanceRequests.CountAsync(r => !done.Contains(r.Status) && r.DueAt != null && r.DueAt < now, ct);
        var escalated = await _db.AssistanceRequests.CountAsync(r => r.Status == RequestStatus.Escalated, ct);
        var pendingAi = await _db.AiRecommendations.CountAsync(r => r.Status == RecommendationStatus.PendingReview, ct);
        var decisions = await _db.HumanDecisions.CountAsync(ct);
        var overridden = await _db.HumanDecisions.CountAsync(d => d.Action == DecisionAction.Modify || d.Action == DecisionAction.Reject, ct);
        var fallback = await _db.AiRecommendations.CountAsync(r => r.IsFallback, ct);

        var cap = await _db.Shelters.SumAsync(s => (int?)s.Capacity, ct) ?? 0;
        var occ = await _db.Shelters.SumAsync(s => (int?)s.CurrentOccupancy, ct) ?? 0;
        var total = await _db.ResourceInventories.SumAsync(i => (decimal?)i.QuantityTotal, ct) ?? 0;
        var alloc = await _db.ResourceInventories.SumAsync(i => (decimal?)i.QuantityAllocated, ct) ?? 0;
        var avg = await _db.AssistanceRequests.Where(r => r.Status == RequestStatus.Fulfilled || r.Status == RequestStatus.Closed)
            .AverageAsync(r => (double?)EF.Functions.DateDiffMinute(r.CreatedAt, r.UpdatedAt), ct);

        return new Metrics(unresolved, overdue, escalated, pendingAi,
            decisions == 0 ? 0 : Math.Round(100.0 * overridden / decisions, 1), fallback, decisions,
            cap == 0 ? 0 : Math.Round(100.0 * occ / cap, 1), total == 0 ? 0 : Math.Round(100.0 * (double)(alloc / total), 1),
            avg is null ? null : Math.Round(avg.Value, 1),
            await _db.DuplicateCandidates.CountAsync(d => d.Status == "Pending", ct),
            await _db.AssistanceRequests.CountAsync(r => r.IsFlaggedSuspicious, ct),
            await _db.Notifications.CountAsync(n => n.Status == "DeadLetter", ct));
    }
}
