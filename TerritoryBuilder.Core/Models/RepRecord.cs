namespace TerritoryBuilder.Core.Models;

public sealed class RepRecord
{
    public string RepId { get; init; } = string.Empty;
    public string RepName { get; init; } = string.Empty;
    public double HomeLat { get; init; }
    public double HomeLon { get; init; }
    public bool Active { get; init; }
}
