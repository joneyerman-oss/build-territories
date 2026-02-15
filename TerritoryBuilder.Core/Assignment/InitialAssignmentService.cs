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

        if (ShouldUseAngularSlicing(activeReps))
        {
            AssignByAngularSlices(candidates, activeReps, target, repLoad, cancellationToken);
            return Task.FromResult(BuildResult(candidates, activeReps, target, options.FairnessTolerancePercent));
        }

        var orderedCandidates = candidates.OrderByDescending(c => c.Score).ToList();

        foreach (var c in orderedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RepRecord? bestRep = null;
            double bestDistance = 0;
            var bestObjective = double.MaxValue;
            foreach (var rep in activeReps)
            {
                var dist = GeoUtils.HaversineMiles(new(rep.HomeLon, rep.HomeLat), c.Point.Coordinate);
                var loadPenalty = (double)Math.Max(0, repLoad[rep.RepId] - target);
                var objective = (dist * (double)options.DrivePenaltyWeight) + (loadPenalty * (double)options.OpportunityVarianceWeight);

                if (objective >= bestObjective) continue;

                bestRep = rep;
                bestDistance = dist;
                bestObjective = objective;
            }

            if (bestRep is null)
            {
                throw new InvalidOperationException("Failed to rank reps for assignment.");
            }

            c.AssignedRepId = bestRep.RepId;
            c.DistanceProxyMiles = bestDistance;
            repLoad[bestRep.RepId] += c.Score;
        }

        return Task.FromResult(BuildResult(candidates, activeReps, target, options.FairnessTolerancePercent));
    }

    private static bool ShouldUseAngularSlicing(IReadOnlyCollection<RepRecord> activeReps)
    {
        var uniqueHomes = activeReps
            .Select(r => (Lat: Math.Round(r.HomeLat, 6), Lon: Math.Round(r.HomeLon, 6)))
            .Distinct()
            .Count();

        return uniqueHomes <= 1;
    }

    private static void AssignByAngularSlices(
        IReadOnlyCollection<BusinessCandidate> candidates,
        IReadOnlyList<RepRecord> activeReps,
        decimal target,
        IDictionary<string, decimal> repLoad,
        CancellationToken cancellationToken)
    {
        var centerX = candidates.Average(c => c.Point.X);
        var centerY = candidates.Average(c => c.Point.Y);

        var ordered = candidates
            .OrderBy(c => Math.Atan2(c.Point.Y - centerY, c.Point.X - centerX))
            .ToList();

        var repIndex = 0;
        foreach (var c in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            while (repIndex < activeReps.Count - 1 && repLoad[activeReps[repIndex].RepId] >= target)
            {
                repIndex++;
            }

            var rep = activeReps[repIndex];
            c.AssignedRepId = rep.RepId;
            c.DistanceProxyMiles = GeoUtils.HaversineMiles(new(rep.HomeLon, rep.HomeLat), c.Point.Coordinate);
            repLoad[rep.RepId] += c.Score;
        }
    }

    private static AssignmentResult BuildResult(
        IReadOnlyCollection<BusinessCandidate> candidates,
        IReadOnlyCollection<RepRecord> activeReps,
        decimal target,
        decimal fairnessTolerancePercent)
    {
        var repCandidateLookup = candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.AssignedRepId))
            .GroupBy(c => c.AssignedRepId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var repMetrics = activeReps.Select(r =>
        {
            var mine = repCandidateLookup.TryGetValue(r.RepId, out var repCandidates)
                ? repCandidates
                : [];
            var weighted = mine.Sum(c => c.Score);
            var pct = target == 0 ? 0 : ((weighted - target) / target) * 100;
            var smallCount = mine.Count(c => string.Equals(c.Source.BuildingTypeBucket, "Small Business", StringComparison.OrdinalIgnoreCase));
            var mediumCount = mine.Count(c => string.Equals(c.Source.BuildingTypeBucket, "Medium Business", StringComparison.OrdinalIgnoreCase));
            var largeCount = mine.Count(c => string.Equals(c.Source.BuildingTypeBucket, "Large Business", StringComparison.OrdinalIgnoreCase));
            return new RepMetrics
            {
                RepId = r.RepId,
                RepName = r.RepName,
                WeightedScore = weighted,
                TargetScore = target,
                PercentToTarget = pct,
                BusinessCount = mine.Count,
                SmallBusinessCount = smallCount,
                MediumBusinessCount = mediumCount,
                LargeBusinessCount = largeCount,
                AverageDistance = mine.Count == 0 ? 0 : mine.Average(c => c.DistanceProxyMiles),
                MaxDistance = mine.Count == 0 ? 0 : mine.Max(c => c.DistanceProxyMiles),
                WithinFairnessTolerance = Math.Abs(pct) <= fairnessTolerancePercent,
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

        return new AssignmentResult
        {
            AssignedBusinesses = candidates.ToList(),
            RepMetrics = repMetrics,
            Overall = overall
        };
    }
}
