namespace Application.Strategies;

// ---------- Strategy pattern: priority scoring ----------
public record PriorityInput(int PeopleAffected, byte? UserUrgency, bool HasVulnerable, int HoursWaiting, int AiUrgency);

public interface IPriorityStrategy { int Calculate(PriorityInput input); }   // returns 1..5

public class WeightedPriorityStrategy : IPriorityStrategy
{
    public int Calculate(PriorityInput i)
    {
        double score = i.AiUrgency * 0.4
                     + (i.UserUrgency ?? 3) * 0.2
                     + Math.Min(i.PeopleAffected, 20) / 20.0 * 5 * 0.2
                     + (i.HasVulnerable ? 5 : 1) * 0.1
                     + Math.Min(i.HoursWaiting, 24) / 24.0 * 5 * 0.1;
        return Math.Clamp((int)Math.Round(score), 1, 5);
    }
}

// ---------- Strategy pattern: multi-constraint resource allocation ----------
public record Candidate(int InventoryId, decimal Available, double DistanceKm, bool IsExpiringSoon, int OrganisationId);
public record AllocationNeed(decimal Quantity, int Priority, int PreferredOrgId);

public interface IAllocationStrategy
{
    /// Returns candidates ranked best-first. NOT just "nearest".
    IReadOnlyList<Candidate> Rank(IEnumerable<Candidate> candidates, AllocationNeed need);
}

public class MultiConstraintAllocationStrategy : IAllocationStrategy
{
    public IReadOnlyList<Candidate> Rank(IEnumerable<Candidate> candidates, AllocationNeed need) =>
        candidates
            .Where(c => c.Available > 0)                                   // hard constraint
            .OrderByDescending(c => c.Available >= need.Quantity)         // can fully satisfy
            .ThenByDescending(c => c.IsExpiringSoon)                      // use perishable stock first
            .ThenByDescending(c => c.OrganisationId == need.PreferredOrgId)
            .ThenBy(c => c.DistanceKm)                                    // distance is only ONE factor
            .ToList();
}

// TODO (Member 2): ShortageResolver - when several urgent requests compete for one inventory,
// sort by Priority desc then CreatedAt asc, allocate until stock runs out, mark the rest
// 'Preempted/Pending' with a ConflictGroupId and suggest alternatives / partner requests.
