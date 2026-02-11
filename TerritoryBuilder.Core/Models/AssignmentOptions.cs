namespace TerritoryBuilder.Core.Models;

public sealed class AssignmentOptions
{
    public decimal FairnessTolerancePercent { get; init; } = 5;
    public decimal DriveTolerancePercent { get; init; } = 10;
    public decimal OpportunityVarianceWeight { get; init; } = 0.65m;
    public decimal DrivePenaltyWeight { get; init; } = 0.25m;
    public decimal FragmentationPenaltyWeight { get; init; } = 0.10m;
    public int MaxIterations { get; init; } = 50;
}
