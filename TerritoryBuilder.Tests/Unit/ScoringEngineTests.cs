using NetTopologySuite.Geometries;
using TerritoryBuilder.Core.Models;
using TerritoryBuilder.Core.Services;
using TerritoryBuilder.Core.Utilities;

namespace TerritoryBuilder.Tests.Unit;

public class ScoringEngineTests
{
    [Fact]
    public async Task BuildingTypeScoring_UsesConfiguredPoints()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord { Name = "1", Latitude = 5, Longitude = 5, BuildingType = "Large Business", Address = "1 Main" };
            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);
        Assert.Single(scored);
        Assert.Equal(10, scored[0].Score);
    }

    [Fact]
    public void ExclusionNormalization_UppercasesAndAbbreviates()
    {
        Assert.Equal("123 MAIN ST", AddressNormalizer.Normalize("123 Main Street"));
    }
}
