using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Api.Background;

/// Stores a KPI snapshot regularly so the dashboard can show history/trends (historical analytics).
public class KpiSnapshotService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes; private readonly IConfiguration _cfg; private readonly ILogger<KpiSnapshotService> _log;
    public KpiSnapshotService(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<KpiSnapshotService> log) { _scopes = scopes; _cfg = cfg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var minutes = Math.Max(1, _cfg.GetValue("Kpi:IntervalMinutes", 60));
        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var m = await scope.ServiceProvider.GetRequiredService<MetricsService>().GetAsync(ct);
                var crisis = await db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).Select(c => (int?)c.CrisisId).FirstOrDefaultAsync(ct);
                db.KpiSnapshots.Add(new KpiSnapshot
                {
                    CrisisId = crisis, UnresolvedCases = m.UnresolvedCases, AvgResponseMinutes = (decimal?)m.AvgResolutionMinutes,
                    ResourceUtilisationPct = (decimal)m.ResourceUtilisationPct, ShelterOccupancyPct = (decimal)m.ShelterOccupancyPct, AiOverrideRatePct = (decimal)m.AiOverrideRatePct
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "KPI snapshot failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(minutes), ct); } catch (OperationCanceledException) { break; }
        }
    }
}
