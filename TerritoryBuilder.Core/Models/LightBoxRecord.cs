namespace TerritoryBuilder.Core.Models;

public sealed class LightBoxRecord
{
    public string EntityCategory { get; init; } = string.Empty;
    public string EntityCategoryId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string County { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Zip { get; init; } = string.Empty;
    public string BuildingType { get; init; } = string.Empty;
    public int NumberOfAddresses { get; init; }
    public bool OwnerCompanyFlag { get; init; }
    public Dictionary<string, string> ExtraColumns { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string BuildingTypeBucket => string.IsNullOrWhiteSpace(BuildingType) ? "(Blanks)" : BuildingType.Trim();
}
