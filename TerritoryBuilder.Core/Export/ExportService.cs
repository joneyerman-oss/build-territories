using System.Globalization;
using CsvHelper;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Triangulate;
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
                var centroidSites = repGroups
                    .Select(group => new
                    {
                        RepId = group.Key,
                        Point = geometryFactory.CreatePoint(new Coordinate(
                            group.Average(candidate => candidate.Point.X),
                            group.Average(candidate => candidate.Point.Y)))
                    })
                    .ToList();

                var sites = geometryFactory.CreateMultiPoint(centroidSites.Select(site => site.Point).ToArray());
                var clipEnvelope = (clipGeometry is not null ? clipGeometry.EnvelopeInternal.Copy() : sites.EnvelopeInternal.Copy());
                clipEnvelope.ExpandBy(0.01);

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
}
