using TerritoryBuilder.Core.Models;
using TerritoryBuilder.Core.Utilities;

namespace TerritoryBuilder.Core.Assignment;

public sealed class InitialAssignmentService : IAssignmentService
{
    public Task<AssignmentResult> AssignAsync(
        IReadOnlyCollection<BusinessCandidate> candidates,
        IReadOnlyCollection<RepRecord> reps,
        AssignmentOptions options,
        CancellationToken cancellationToken)
    {
        var activeReps = reps.Where(r => r.Active).ToList();
        if (activeReps.Count == 0) throw new InvalidOperationException("No active reps available.");
        if (candidates.Count == 0) throw new InvalidOperationException("No candidates available after filtering.");

        var total = candidates.Sum(c => c.Score);
        var target = total / activeReps.Count;

        var repLoad = activeReps.ToDictionary(r => r.RepId, _ => 0m, StringComparer.OrdinalIgnoreCase);

        foreach (var c in candidates.OrderByDescending(c => c.Score))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ranked = activeReps
                .Select(r =>
                {
                    var dist = GeoUtils.HaversineMiles(new(r.HomeLon, r.HomeLat), c.Point.Coordinate);
                    var loadPenalty = (double)Math.Max(0, repLoad[r.RepId] - target);
                    return new { Rep = r, Dist = dist, Objective = (dist * (double)options.DrivePenaltyWeight) + (loadPenalty * (double)options.OpportunityVarianceWeight) };
                })
                .OrderBy(x => x.Objective)
                .First();

            c.AssignedRepId = ranked.Rep.RepId;
            c.DistanceProxyMiles = ranked.Dist;
            repLoad[ranked.Rep.RepId] += c.Score;
        }

        var repMetrics = activeReps.Select(r =>
        {
            var mine = candidates.Where(c => c.AssignedRepId == r.RepId).ToList();
            var weighted = mine.Sum(c => c.Score);
            var pct = target == 0 ? 0 : ((weighted - target) / target) * 100;
            return new RepMetrics
            {
                RepId = r.RepId,
                RepName = r.RepName,
                WeightedScore = weighted,
                TargetScore = target,
                PercentToTarget = pct,
                BusinessCount = mine.Count,
                AverageDistance = mine.Count == 0 ? 0 : mine.Average(c => c.DistanceProxyMiles),
                MaxDistance = mine.Count == 0 ? 0 : mine.Max(c => c.DistanceProxyMiles),
                ContiguityPass = true
            };
        }).ToList();

        var mean = repMetrics.Average(m => m.WeightedScore);
        var stddev = (decimal)Math.Sqrt(repMetrics.Average(m => Math.Pow((double)(m.WeightedScore - mean), 2)));

        var overall = new OverallMetrics
        {
            FairnessIndex = mean == 0 ? 0 : stddev / mean,
            MaxImbalancePercent = repMetrics.Max(m => Math.Abs(m.PercentToTarget)),
            IncludedCount = candidates.Count,
            ExcludedCount = 0
        };

        return Task.FromResult(new AssignmentResult
        {
            AssignedBusinesses = candidates.ToList(),
            RepMetrics = repMetrics,
            Overall = overall
        });
    }
}
