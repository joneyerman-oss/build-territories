namespace TerritoryBuilder.Core.Models;

public sealed class ScoringOptions
{
    public Dictionary<string, decimal> BuildingTypePoints { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Large Business"] = 10,
        ["Medium Business"] = 6,
        ["Small Business"] = 3,
        ["Unknown"] = 1,
        ["(Blanks)"] = 1
    };

    public bool UseAddressMultiplier { get; init; }
    public decimal AddressMultiplierFactor { get; init; } = 0.1m;
}
