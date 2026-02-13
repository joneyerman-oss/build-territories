namespace TerritoryBuilder.Core.Models;

public sealed class FilterOptions
{
    public HashSet<string> IncludedBuildingTypes { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Large Business", "Medium Business", "Small Business", "Unknown", "(Blanks)"
    };

    public HashSet<string> CityFilter { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CountyFilter { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
