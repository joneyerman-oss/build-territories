using System.Globalization;
using CsvHelper;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Triangulate;
using NetTopologySuite.Utilities;
using Newtonsoft.Json;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Core.Export;

public sealed class ExportService
{
    public async Task ExportAssignmentsCsvAsync(string path, AssignmentResult result, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(result.AssignedBusinesses.Select(b => new
        {
            rep_id = b.AssignedRepId,
            entity_id = b.Source.Name,
            b.Source.Address,
            b.Source.City,
            b.Source.County,
            b.Source.State,
            b.Source.Zip,
            building_type = b.Source.BuildingTypeBucket,
            score = b.Score,
            distance_proxy = b.DistanceProxyMiles
        }), cancellationToken);
    }

    public async Task ExportSummaryCsvAsync(string path, AssignmentResult result, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(result.RepMetrics, cancellationToken);
    }

    public async Task ExportRunLogJsonAsync(string path, AssignmentResult result, CancellationToken cancellationToken)
    {
        var payload = JsonConvert.SerializeObject(new
        {
            result.RunId,
            AssignedBusinesses = result.AssignedBusinesses.Select(b => new
            {
                b.Source,
                Point = new
                {
                    Latitude = b.Point.Y,
                    Longitude = b.Point.X
                },
                b.ZoneName,
                b.Score,
                b.AssignedRepId,
                b.DistanceProxyMiles
            }),
            result.RepMetrics,
            result.Overall
        }, Formatting.Indented);
        await File.WriteAllTextAsync(path, payload, cancellationToken);
    }

    public async Task ExportTerritoriesGeoJsonAsync(
        string path,
        AssignmentResult result,
        CancellationToken cancellationToken,
        IReadOnlyCollection<ZoneFeature>? zones = null)
    {
        var fc = new FeatureCollection();

        var assigned = result.AssignedBusinesses
            .Where(a => !string.IsNullOrWhiteSpace(a.AssignedRepId))
            .ToList();

        if (assigned.Count > 0)
        {
            var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var clipGeometry = BuildClipGeometry(zones);
            var repGroups = assigned
                .GroupBy(a => a.AssignedRepId!, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (repGroups.Count == 1)
            {
                var singleRepGroup = repGroups[0];
                var territory = geometryFactory.CreateMultiPoint(singleRepGroup.Select(candidate => candidate.Point).ToArray()).ConvexHull();
                if (territory.IsEmpty)
                {
                    territory = geometryFactory.CreatePoint(singleRepGroup.First().Point.Coordinate).Buffer(0.01);
                }

                if (clipGeometry is not null)
                {
                    territory = territory.Intersection(clipGeometry);
                }

                territory = ToPolygonalGeometry(territory, geometryFactory);
                if (territory is not null && !territory.IsEmpty)
                {
                    fc.Add(new Feature(territory, new AttributesTable { { "rep_id", singleRepGroup.Key } }));
                }
            }
            else
            {
                var centroidCoordinates = repGroups
                    .Select(group => new Coordinate(
                        group.Average(candidate => candidate.Point.X),
                        group.Average(candidate => candidate.Point.Y)))
                    .ToList();

                EnsureMinimumSiteSeparation(centroidCoordinates);

                var centroidSites = repGroups
                    .Select((group, index) => new
                    {
                        RepId = group.Key,
                        Point = geometryFactory.CreatePoint(centroidCoordinates[index])
                    })
                    .ToList();

                var sites = geometryFactory.CreateMultiPoint(centroidSites.Select(site => site.Point).ToArray());
                var clipEnvelope = (clipGeometry is not null ? clipGeometry.EnvelopeInternal.Copy() : sites.EnvelopeInternal.Copy());
                clipEnvelope.ExpandBy(0.01);

                try
                {
                    var voronoiBuilder = new VoronoiDiagramBuilder();
                    voronoiBuilder.SetSites(sites);
                    voronoiBuilder.ClipEnvelope = clipEnvelope;

                    var diagram = voronoiBuilder.GetDiagram(geometryFactory);

                    foreach (var cell in diagram.Geometries.OfType<Polygon>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var territory = clipGeometry is null ? cell : cell.Intersection(clipGeometry);
                        territory = ToPolygonalGeometry(territory, geometryFactory);

                        if (territory is null || territory.IsEmpty)
                        {
                            continue;
                        }

                        var matchingSite = centroidSites
                            .OrderBy(site => site.Point.Distance(territory.Centroid))
                            .FirstOrDefault();

                        if (matchingSite?.RepId is null)
                        {
                            continue;
                        }

                        var attr = new AttributesTable { { "rep_id", matchingSite.RepId } };
                        fc.Add(new Feature(territory, attr));
                    }
                }
                catch (TopologyException)
                {
                    AppendFallbackTerritories(fc, repGroups, clipGeometry, geometryFactory);
                }
            }
        }

        var serializer = GeoJsonSerializer.Create();
        await using var sw = new StreamWriter(path);
        await using var jw = new JsonTextWriter(sw);
        serializer.Serialize(jw, fc);
    }

    private static Geometry? BuildClipGeometry(IReadOnlyCollection<ZoneFeature>? zones)
    {
        if (zones is null || zones.Count == 0)
        {
            return null;
        }

        var zoneGeometries = zones
            .Select(zone => zone.Geometry)
            .Where(geometry => geometry is not null && !geometry.IsEmpty)
            .ToArray();

        if (zoneGeometries.Length == 0)
        {
            return null;
        }

        var union = UnaryUnionOp.Union(zoneGeometries);
        return union.IsValid ? union : union.Buffer(0);
    }

    private static Geometry? ToPolygonalGeometry(Geometry geometry, GeometryFactory geometryFactory)
    {
        if (geometry.IsEmpty)
        {
            return null;
        }

        if (geometry is Polygon or MultiPolygon)
        {
            return geometry;
        }

        var polygons = PolygonExtracter.GetPolygons(geometry).Cast<Polygon>().ToArray();
        if (polygons.Length == 0)
        {
            return null;
        }

        return polygons.Length == 1 ? polygons[0] : geometryFactory.CreateMultiPolygon(polygons);
    }

    private static void EnsureMinimumSiteSeparation(IList<Coordinate> coordinates)
    {
        if (coordinates.Count < 2)
        {
            return;
        }

        var envelope = new Envelope();
        foreach (var coordinate in coordinates)
        {
            envelope.ExpandToInclude(coordinate);
        }

        var span = Math.Max(envelope.Width, envelope.Height);
        var minimumDistance = Math.Max(span * 1e-9, 1e-7);

        for (var i = 0; i < coordinates.Count; i++)
        {
            var current = coordinates[i];
            var attempt = 0;

            while (HasNeighborWithinTolerance(coordinates, i, current, minimumDistance) && attempt < 16)
            {
                attempt++;
                var angle = attempt * (Math.PI / 4d);
                var radius = minimumDistance * attempt;
                current = new Coordinate(
                    coordinates[i].X + (Math.Cos(angle) * radius),
                    coordinates[i].Y + (Math.Sin(angle) * radius));
            }

            coordinates[i] = current;
        }
    }

    private static bool HasNeighborWithinTolerance(
        IList<Coordinate> coordinates,
        int currentIndex,
        Coordinate candidate,
        double tolerance)
    {
        for (var i = 0; i < coordinates.Count; i++)
        {
            if (i == currentIndex)
            {
                continue;
            }

            if (candidate.Distance(coordinates[i]) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendFallbackTerritories(
        FeatureCollection featureCollection,
        IReadOnlyCollection<IGrouping<string, BusinessCandidate>> repGroups,
        Geometry? clipGeometry,
        GeometryFactory geometryFactory)
    {
        Geometry? consumed = null;

        foreach (var group in repGroups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var groupPoints = group.Select(candidate => candidate.Point).ToArray();
            if (groupPoints.Length == 0)
            {
                continue;
            }

            var territory = geometryFactory.CreateMultiPoint(groupPoints).ConvexHull();
            if (territory.IsEmpty)
            {
                territory = geometryFactory.CreatePoint(groupPoints[0].Coordinate).Buffer(0.01);
            }

            if (clipGeometry is not null)
            {
                territory = territory.Intersection(clipGeometry);
            }

            if (consumed is not null && !consumed.IsEmpty)
            {
                territory = territory.Difference(consumed);
            }

            territory = ToPolygonalGeometry(territory, geometryFactory);
            if (territory is null || territory.IsEmpty)
            {
                continue;
            }

            consumed = consumed is null ? territory.Copy() : consumed.Union(territory);
            featureCollection.Add(new Feature(territory, new AttributesTable { { "rep_id", group.Key } }));
        }
    }
}
