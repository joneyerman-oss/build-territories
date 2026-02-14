namespace TerritoryBuilder.Core.Models;

public sealed class AssignmentResult
{
    public string RunId { get; init; } = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
    public List<BusinessCandidate> AssignedBusinesses { get; init; } = [];
    public List<RepMetrics> RepMetrics { get; init; } = [];
    public OverallMetrics Overall { get; init; } = new();
}

public sealed class RepMetrics
{
    public string RepId { get; init; } = string.Empty;
    public string RepName { get; init; } = string.Empty;
    public decimal WeightedScore { get; init; }
    public decimal TargetScore { get; init; }
    public decimal PercentToTarget { get; init; }
    public int BusinessCount { get; init; }
    public int SmallBusinessCount { get; init; }
    public int MediumBusinessCount { get; init; }
    public int LargeBusinessCount { get; init; }
    public double AverageDistance { get; init; }
    public double MaxDistance { get; init; }
    public bool WithinFairnessTolerance { get; init; }
    public bool ContiguityPass { get; init; }
}

public sealed class OverallMetrics
{
    public decimal FairnessIndex { get; init; }
    public decimal MaxImbalancePercent { get; init; }
    public int IncludedCount { get; init; }
    public int ExcludedCount { get; init; }
}
