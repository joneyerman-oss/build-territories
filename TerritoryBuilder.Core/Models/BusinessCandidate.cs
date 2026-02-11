using NetTopologySuite.Geometries;

namespace TerritoryBuilder.Core.Models;

public sealed class BusinessCandidate
{
    public required LightBoxRecord Source { get; init; }
    public required Point Point { get; init; }
    public required string ZoneName { get; init; }
    public decimal Score { get; set; }
    public string? AssignedRepId { get; set; }
    public double DistanceProxyMiles { get; set; }
}
