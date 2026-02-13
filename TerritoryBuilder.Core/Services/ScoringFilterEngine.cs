using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using TerritoryBuilder.Core.Models;
using TerritoryBuilder.Core.Utilities;

namespace TerritoryBuilder.Core.Services;

public sealed class ScoringFilterEngine : IScoringFilterEngine
{
    private readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public async Task<List<BusinessCandidate>> BuildCandidatesAsync(
        IAsyncEnumerable<LightBoxRecord> records,
        IReadOnlyCollection<ZoneFeature> zones,
        FilterOptions filters,
        ScoringOptions scoring,
        IReadOnlyCollection<HashSet<string>> exclusionSets,
        CancellationToken cancellationToken)
    {
        var zoneIndex = BuildZoneIndex(zones.Where(z => ShouldIncludeZone(z.ZoneName, filters)).ToList());
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<BusinessCandidate>();

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            if (!GeoUtils.IsValidCoordinate(record.Latitude, record.Longitude)) continue;
            if (!string.Equals(record.EntityCategory, "business", StringComparison.OrdinalIgnoreCase)) continue;

            var bucket = record.BuildingTypeBucket;
            if (!filters.IncludedBuildingTypes.Contains(bucket)) continue;
            if (filters.CityFilter.Count > 0 && !filters.CityFilter.Contains(record.City)) continue;
            if (filters.CountyFilter.Count > 0 && !filters.CountyFilter.Contains(record.County)) continue;

            var normalizedAddress = AddressNormalizer.Normalize(record.Address);
            if (exclusionSets.Any(s => s.Contains(normalizedAddress) || (!string.IsNullOrWhiteSpace(record.Name) && s.Contains(record.Name)))) continue;

            var dedupeKey = !string.IsNullOrWhiteSpace(record.Name)
                ? record.Name
                : $"{normalizedAddress}|{record.City}|{record.Zip}|{record.Latitude:F6}|{record.Longitude:F6}";

            if (!dedupe.Add(dedupeKey)) continue;

            var point = _geometryFactory.CreatePoint(new Coordinate(record.Longitude, record.Latitude));
            var zoneName = ResolveZone(point, zoneIndex);
            if (zoneName is null) continue;

            var baseScore = scoring.BuildingTypePoints.TryGetValue(bucket, out var score) ? score : 1;
            var totalScore = scoring.UseAddressMultiplier
                ? baseScore * (1 + (record.NumberOfAddresses * scoring.AddressMultiplierFactor))
                : baseScore;

            result.Add(new BusinessCandidate
            {
                Source = record,
                Point = point,
                ZoneName = zoneName,
                Score = totalScore
            });
        }

        return result;
    }

    public decimal CalculateTotalWeightedOpportunity(IReadOnlyCollection<BusinessCandidate> candidates) => candidates.Sum(c => c.Score);

    private static bool ShouldIncludeZone(string zoneName, FilterOptions options)
    {
        if (zoneName.Equals("VNN", StringComparison.OrdinalIgnoreCase)) return options.IncludeVnn;
        if (zoneName.Equals("NN", StringComparison.OrdinalIgnoreCase)) return options.IncludeNn;

        // Support arbitrary or unlabeled polygons in uploaded GeoJSON.
        return true;
    }

    private static STRtree<ZoneFeature> BuildZoneIndex(List<ZoneFeature> zones)
    {
        var tree = new STRtree<ZoneFeature>();
        foreach (var zone in zones)
        {
            tree.Insert(zone.Geometry.EnvelopeInternal, zone);
        }

        tree.Build();
        return tree;
    }

    private static string? ResolveZone(Point point, STRtree<ZoneFeature> zones)
    {
        foreach (var zone in zones.Query(point.EnvelopeInternal))
        {
            if (zone.Geometry.Covers(point))
            {
                return zone.ZoneName;
            }
        }

        return null;
    }
}
