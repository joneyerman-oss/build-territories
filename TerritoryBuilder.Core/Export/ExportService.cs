using System.Globalization;
using CsvHelper;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
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
        foreach (var group in result.AssignedBusinesses.Where(a => !string.IsNullOrWhiteSpace(a.AssignedRepId)).GroupBy(a => a.AssignedRepId!))
        {
            var geom = NetTopologySuite.Operation.Union.CascadedPolygonUnion.Union(group.Select(g => g.Point.Buffer(0.02)).ToList());
            var attr = new AttributesTable { { "rep_id", group.Key } };
            fc.Add(new Feature(geom, attr));
        }

        var serializer = GeoJsonSerializer.Create();
        await using var sw = new StreamWriter(path);
        await using var jw = new JsonTextWriter(sw);
        serializer.Serialize(jw, fc);
    }
}
