using NetTopologySuite.Geometries;
using Newtonsoft.Json.Linq;
using TerritoryBuilder.Core.Export;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Tests.Unit;

public class ExportServiceTests
{
    [Fact]
    public async Task ExportRunLogJsonAsync_WritesSerializablePointShape()
    {
        var service = new ExportService();
        var result = new AssignmentResult
        {
            AssignedBusinesses =
            [
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "Acme" },
                    Point = new Point(new Coordinate(-97.7431, 30.2672)),
                    ZoneName = "VNN",
                    Score = 7,
                    AssignedRepId = "rep-1",
                    DistanceProxyMiles = 1.25
                }
            ]
        };

        var path = Path.Combine(Path.GetTempPath(), $"run-log-{Guid.NewGuid():N}.json");

        try
        {
            await service.ExportRunLogJsonAsync(path, result, CancellationToken.None);

            var json = await File.ReadAllTextAsync(path);
            var payload = JObject.Parse(json);

            Assert.Equal("Acme", payload["AssignedBusinesses"]?[0]?["Source"]?["Name"]?.Value<string>());
            Assert.Equal(30.2672, payload["AssignedBusinesses"]?[0]?["Point"]?["Latitude"]?.Value<double>());
            Assert.Equal(-97.7431, payload["AssignedBusinesses"]?[0]?["Point"]?["Longitude"]?.Value<double>());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
    [Fact]
    public async Task ExportTerritoriesGeoJsonAsync_CreatesNonOverlappingTerritories()
    {
        var service = new ExportService();
        var result = new AssignmentResult
        {
            AssignedBusinesses =
            [
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "A1" },
                    Point = new Point(new Coordinate(-97.75, 30.26)),
                    AssignedRepId = "rep-a"
                },
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "A2" },
                    Point = new Point(new Coordinate(-97.74, 30.27)),
                    AssignedRepId = "rep-a"
                },
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "B1" },
                    Point = new Point(new Coordinate(-97.70, 30.26)),
                    AssignedRepId = "rep-b"
                },
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "B2" },
                    Point = new Point(new Coordinate(-97.69, 30.27)),
                    AssignedRepId = "rep-b"
                }
            ]
        };

        var path = Path.Combine(Path.GetTempPath(), $"territories-{Guid.NewGuid():N}.geojson");

        try
        {
            await service.ExportTerritoriesGeoJsonAsync(path, result, CancellationToken.None);

            var json = await File.ReadAllTextAsync(path);
            var payload = JObject.Parse(json);
            var features = payload["features"]?.Children<JObject>().ToList() ?? [];

            Assert.Equal(2, features.Count);

            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geometries = features
                .Select(feature => feature["geometry"]?.ToString())
                .Where(geometryJson => !string.IsNullOrWhiteSpace(geometryJson))
                .Select(geometryJson => reader.Read<Geometry>(geometryJson!))
                .ToList();

            Assert.Equal(2, geometries.Count);
            Assert.False(geometries[0].Overlaps(geometries[1]));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExportTerritoriesGeoJsonAsync_ClipsTerritoriesToZoneGeometry()
    {
        var service = new ExportService();
        var result = new AssignmentResult
        {
            AssignedBusinesses =
            [
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "A1" },
                    Point = new Point(new Coordinate(-97.75, 30.26)),
                    AssignedRepId = "rep-a"
                },
                new BusinessCandidate
                {
                    Source = new LightBoxRecord { Name = "B1" },
                    Point = new Point(new Coordinate(-97.70, 30.26)),
                    AssignedRepId = "rep-b"
                }
            ]
        };

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var zonePolygon = geometryFactory.CreatePolygon(
        [
            new Coordinate(-97.76, 30.25),
            new Coordinate(-97.72, 30.25),
            new Coordinate(-97.72, 30.28),
            new Coordinate(-97.76, 30.28),
            new Coordinate(-97.76, 30.25)
        ]);
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "z1", Geometry = zonePolygon }
        };

        var path = Path.Combine(Path.GetTempPath(), $"territories-{Guid.NewGuid():N}.geojson");

        try
        {
            await service.ExportTerritoriesGeoJsonAsync(path, result, CancellationToken.None, zones);

            var json = await File.ReadAllTextAsync(path);
            var payload = JObject.Parse(json);
            var features = payload["features"]?.Children<JObject>().ToList() ?? [];

            Assert.NotEmpty(features);

            var reader = new NetTopologySuite.IO.GeoJsonReader();
            var geometries = features
                .Select(feature => feature["geometry"]?.ToString())
                .Where(geometryJson => !string.IsNullOrWhiteSpace(geometryJson))
                .Select(geometryJson => reader.Read<Geometry>(geometryJson!))
                .ToList();

            Assert.All(geometries, geometry => Assert.True(zonePolygon.Covers(geometry)));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

}
