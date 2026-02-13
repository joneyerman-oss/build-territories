using System.Globalization;
using CsvHelper;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
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

    public async Task ExportTerritoriesGeoJsonAsync(string path, AssignmentResult result, CancellationToken cancellationToken)
    {
        var fc = new FeatureCollection();

        var assigned = result.AssignedBusinesses
            .Where(a => !string.IsNullOrWhiteSpace(a.AssignedRepId))
            .ToList();

        if (assigned.Count > 0)
        {
            var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var sites = geometryFactory.CreateMultiPoint(assigned.Select(a => a.Point).ToArray());

            var clipEnvelope = sites.EnvelopeInternal.Copy();
            clipEnvelope.ExpandBy(0.25);

            var voronoiBuilder = new VoronoiDiagramBuilder();
            voronoiBuilder.SetSites(sites);
            voronoiBuilder.ClipEnvelope = clipEnvelope;

            var diagram = voronoiBuilder.GetDiagram(geometryFactory);
            var cellsByRep = new Dictionary<string, List<Geometry>>(StringComparer.OrdinalIgnoreCase);

            foreach (var cell in diagram.Geometries.OfType<Polygon>())
            {
                var matchingCandidate = assigned
                    .FirstOrDefault(candidate => cell.Covers(candidate.Point));

                matchingCandidate ??= assigned
                    .OrderBy(candidate => candidate.Point.Distance(cell.Centroid))
                    .FirstOrDefault();

                if (matchingCandidate?.AssignedRepId is null)
                {
                    continue;
                }

                if (!cellsByRep.TryGetValue(matchingCandidate.AssignedRepId, out var repCells))
                {
                    repCells = [];
                    cellsByRep[matchingCandidate.AssignedRepId] = repCells;
                }

                repCells.Add(cell);
            }

            foreach (var (repId, cells) in cellsByRep)
            {
                var geom = NetTopologySuite.Operation.Union.CascadedPolygonUnion.Union(cells);
                var attr = new AttributesTable { { "rep_id", repId } };
                fc.Add(new Feature(geom, attr));
            }
        }

        var serializer = GeoJsonSerializer.Create();
        await using var sw = new StreamWriter(path);
        await using var jw = new JsonTextWriter(sw);
        serializer.Serialize(jw, fc);
    }
}
