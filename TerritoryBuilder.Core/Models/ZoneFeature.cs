using NetTopologySuite.Geometries;

namespace TerritoryBuilder.Core.Models;

public sealed class ZoneFeature
{
    public string ZoneName { get; init; } = string.Empty;
    public Geometry Geometry { get; init; } = default!;
}
