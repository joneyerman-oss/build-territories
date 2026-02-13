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
}
