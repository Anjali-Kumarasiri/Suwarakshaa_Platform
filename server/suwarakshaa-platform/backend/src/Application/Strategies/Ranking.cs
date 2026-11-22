namespace Application.Strategies;

public static class GeoMath
{
    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = Rad(lat2 - lat1), dLon = Rad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
    private static double Rad(double d) => d * Math.PI / 180.0;
}

public static class TextSimilarity
{
    private static readonly char[] Sep = { ' ', ',', '.', '!', '?', ';', ':', '\n', '\r', '\t', '-', '(', ')', '"' };
    /// Jaccard similarity of word sets (0..1). Simple, explainable duplicate-detection heuristic.
    public static double Jaccard(string a, string b)
    {
        var A = Tokens(a); var B = Tokens(b);
        if (A.Count == 0 || B.Count == 0) return 0;
        return (double)A.Intersect(B).Count() / A.Union(B).Count();
    }
    private static HashSet<string> Tokens(string s) =>
        s.ToLowerInvariant().Split(Sep, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 2).ToHashSet();
}

public record ShelterCandidate(int ShelterId, string Name, double DistanceKm, int SpareCapacity, bool IsAccessible);

public static class ShelterRanker
{
    /// Capacity is a hard constraint; accessibility matters when needed; then nearest; then most spare room.
    public static List<ShelterCandidate> Rank(IEnumerable<ShelterCandidate> c, int people, bool needAccessible) =>
        c.Where(x => x.SpareCapacity >= people)
         .OrderByDescending(x => !needAccessible || x.IsAccessible)
         .ThenBy(x => x.DistanceKm)
         .ThenByDescending(x => x.SpareCapacity)
         .ToList();
}

public record ShortageDemand(long RequestId, byte Priority, DateTime CreatedAt, decimal Quantity);
public record ShortagePlanItem(long RequestId, decimal Granted, decimal Unmet)
{ public bool FullyServed => Unmet == 0; }

public static class ShortageResolver
{
    /// Several requests compete for limited stock: highest priority first, then oldest first.
    /// Nobody is silently dropped: unmet quantities are reported so staff can find alternatives or ask partners.
    public static List<ShortagePlanItem> Resolve(decimal available, IEnumerable<ShortageDemand> demands)
    {
        var plan = new List<ShortagePlanItem>();
        var left = Math.Max(available, 0);
        foreach (var d in demands.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt))
        {
            var grant = Math.Min(left, d.Quantity);
            left -= grant;
            plan.Add(new ShortagePlanItem(d.RequestId, grant, d.Quantity - grant));
        }
        return plan;
    }
}
