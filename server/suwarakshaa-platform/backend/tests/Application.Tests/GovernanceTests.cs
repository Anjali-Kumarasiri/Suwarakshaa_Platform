using System.Linq.Expressions;
using Application.Abstractions;
using Application.Services;
using Domain.Entities;
using Xunit;
namespace Application.Tests;

/// Tiny in-memory fakes so the governance rules can be tested without a database.
internal class FakeRepo<T> : IRepository<T> where T : class
{
    public List<T> Items { get; } = new();
    private readonly Func<T, long> _id;
    public FakeRepo(Func<T, long> id) => _id = id;
    public Task<T?> GetByIdAsync(long id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => _id(x) == id));
    public Task AddAsync(T entity, CancellationToken ct = default) { Items.Add(entity); return Task.CompletedTask; }
    public IQueryable<T> Query() => Items.AsQueryable();
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(predicate));
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(predicate));
}
internal class FakeUow : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default) => await work();
}

public class GovernanceTests
{
    private static (AiGovernanceService svc, FakeRepo<AiRecommendation> recs, FakeRepo<HumanDecision> dec, FakeRepo<AuditEvent> audit, FakeRepo<OutboxMessage> outbox) Build(string outputJson)
    {
        var recs = new FakeRepo<AiRecommendation>(r => r.RecommendationId);
        recs.Items.Add(new AiRecommendation { RecommendationId = 1, RequestId = 10, Type = "Classification", ModelName = "m", OutputJson = outputJson, Confidence = 0.7m });
        var dec = new FakeRepo<HumanDecision>(d => d.DecisionId); var audit = new FakeRepo<AuditEvent>(a => a.AuditId); var outbox = new FakeRepo<OutboxMessage>(o => 0);
        return (new AiGovernanceService(recs, dec, audit, outbox, new FakeUow()), recs, dec, audit, outbox);
    }

    [Fact]
    public async Task Approve_WritesDecisionAuditAndOutbox_AndKeepsOriginalOutput()
    {
        var (svc, recs, dec, audit, outbox) = Build("{\"category\":\"Food\",\"urgency\":3}");
        await svc.DecideAsync(1, DecisionAction.Approve, null, null, userId: 7, userIsCoordinator: false);
        Assert.Equal(RecommendationStatus.Approved, recs.Items[0].Status);
        Assert.Equal("{\"category\":\"Food\",\"urgency\":3}", recs.Items[0].OutputJson);   // original AI output untouched
        Assert.Single(dec.Items); Assert.Single(audit.Items); Assert.Single(outbox.Items);
    }

    [Theory]
    [InlineData(DecisionAction.Modify)]
    [InlineData(DecisionAction.Reject)]
    public async Task ModifyOrReject_RequiresReason(DecisionAction action)
    {
        var (svc, _, _, _, _) = Build("{\"urgency\":3}");
        await Assert.ThrowsAsync<ArgumentException>(() => svc.DecideAsync(1, action, null, "  ", 7, false));
    }

    [Fact]
    public async Task CriticalDecision_ByCaseManager_WaitsForSecondApproval_AndAppliesNothing()
    {
        var (svc, recs, dec, _, outbox) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        var d = await svc.DecideAsync(1, DecisionAction.Approve, null, null, userId: 7, userIsCoordinator: false);
        Assert.True(d.RequiresSecondApproval);
        Assert.Equal(RecommendationStatus.Deferred, recs.Items[0].Status);
        Assert.Empty(outbox.Items);                                         // nothing is applied or announced yet
        Assert.Single(dec.Items);
    }

    [Fact]
    public async Task SecondApproval_ByDifferentCoordinator_AppliesTheDecision()
    {
        var (svc, recs, _, audit, outbox) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        await svc.DecideAsync(1, DecisionAction.Approve, null, null, 7, false);
        await svc.SecondApprovalAsync(1, true, null, approverId: 8, approverIsCoordinator: true);
        Assert.Equal(RecommendationStatus.Approved, recs.Items[0].Status);
        Assert.Single(outbox.Items);
        Assert.Contains(audit.Items, a => a.Action == "AI_DECISION_APPROVED");
    }

    [Fact]
    public async Task SecondApproval_BySamePerson_OrNonCoordinator_IsRefused()
    {
        var (svc, _, _, _, _) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        await svc.DecideAsync(1, DecisionAction.Approve, null, null, 7, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SecondApprovalAsync(1, true, null, 7, true));        // four-eyes
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.SecondApprovalAsync(1, true, null, 9, false));     // role
    }

    [Fact]
    public async Task SecondApproval_Refused_ReturnsRecommendationToTheQueue()
    {
        var (svc, recs, _, _, outbox) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        await svc.DecideAsync(1, DecisionAction.Approve, null, null, 7, false);
        await svc.SecondApprovalAsync(1, false, "Needs a doctor's check first", 8, true);
        Assert.Equal(RecommendationStatus.PendingReview, recs.Items[0].Status);
        Assert.Empty(outbox.Items);
    }

    [Fact]
    public async Task Coordinator_CanDecideCriticalDirectly()
    {
        var (svc, recs, _, _, outbox) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        var d = await svc.DecideAsync(1, DecisionAction.Approve, null, null, 8, userIsCoordinator: true);
        Assert.False(d.RequiresSecondApproval);
        Assert.Equal(RecommendationStatus.Approved, recs.Items[0].Status);
        Assert.Single(outbox.Items);
    }

    [Fact]
    public async Task CaseManager_CannotQuietlyDowngradeACriticalCase()
    {
        // AI said 5; the case manager edits it down to 2 -> still counts as critical and needs a coordinator.
        var (svc, recs, _, _, _) = Build("{\"category\":\"Medicine\",\"urgency\":5}");
        var d = await svc.DecideAsync(1, DecisionAction.Modify, "{\"category\":\"Medicine\",\"urgency\":2}", "looks minor", 7, false);
        Assert.True(d.RequiresSecondApproval);
        Assert.Equal(RecommendationStatus.Deferred, recs.Items[0].Status);
    }

    [Fact]
    public async Task SecondDecision_IsRejected()
    {
        var (svc, _, _, _, _) = Build("{\"urgency\":3}");
        await svc.DecideAsync(1, DecisionAction.Approve, null, null, 7, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DecideAsync(1, DecisionAction.Reject, null, "changed my mind", 7, false));
    }
}
