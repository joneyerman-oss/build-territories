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
            yield return new LightBoxRecord { Name = "1", EntityCategory = "Business", Latitude = 5, Longitude = 5, BuildingType = "Large Business", Address = "1 Main" };
            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);
        Assert.Single(scored);
        Assert.Equal(10, scored[0].Score);
    }


    [Fact]
    public async Task BuildCandidates_IncludesOnlyBusinessEntityCategory()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord { Name = "business", EntityCategory = "Business", Latitude = 5, Longitude = 5, BuildingType = "Large Business", Address = "1 Main" };
            yield return new LightBoxRecord { Name = "non-business", EntityCategory = "Residence", Latitude = 5, Longitude = 6, BuildingType = "Large Business", Address = "2 Main" };
            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);
        Assert.Single(scored);
        Assert.Equal("business", scored[0].Source.Name);
    }

    [Fact]
    public async Task BuildCandidates_AllowsBlankEntityCategory()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord { Name = "blank-category", EntityCategory = string.Empty, Latitude = 5, Longitude = 5, BuildingType = "Large Business", Address = "1 Main" };
            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);

        Assert.Single(scored);
        Assert.Equal("blank-category", scored[0].Source.Name);
    }

    [Fact]
    public async Task BuildCandidates_AppliesAddressMultiplierWhenEnabled()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord
            {
                Name = "weighted",
                EntityCategory = "Business",
                Latitude = 5,
                Longitude = 5,
                BuildingType = "Medium Business",
                NumberOfAddresses = 3,
                Address = "3 Main"
            };

            await Task.CompletedTask;
        }

        var scoring = new ScoringOptions { UseAddressMultiplier = true, AddressMultiplierFactor = 0.1m };
        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), scoring, [], CancellationToken.None);

        Assert.Single(scored);
        Assert.Equal(7.8m, scored[0].Score);
    }

    [Fact]
    public async Task BuildCandidates_HandlesSwappedLatitudeLongitude()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(-100,30), new Coordinate(-90,30), new Coordinate(-90,35), new Coordinate(-100,35), new Coordinate(-100,30) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord
            {
                Name = "swapped",
                EntityCategory = "Business",
                Latitude = -96.79,
                Longitude = 32.81,
                BuildingType = "Large Business",
                Address = "1 Main"
            };

            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);

        Assert.Single(scored);
        Assert.Equal("VNN", scored[0].ZoneName);
    }


    [Fact]
    public async Task BuildCandidates_MapsUnrecognizedBuildingTypesToUnknownBucket()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            yield return new LightBoxRecord
            {
                Name = "household-row",
                EntityCategory = "Business",
                Latitude = 5,
                Longitude = 5,
                BuildingType = "Single Family",
                Address = "1 Main"
            };

            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);

        Assert.Single(scored);
        Assert.Equal("Unknown", scored[0].Source.BuildingTypeBucket);
        Assert.Equal(1, scored[0].Score);
    }

    [Fact]
    public async Task BuildCandidates_IncludesNearBoundaryPointWithinTolerance()
    {
        var engine = new ScoringFilterEngine();
        var zones = new List<ZoneFeature>
        {
            new() { ZoneName = "VNN", Geometry = new Polygon(new LinearRing(new[] { new Coordinate(0,0), new Coordinate(10,0), new Coordinate(10,10), new Coordinate(0,10), new Coordinate(0,0) })) }
        };

        async IAsyncEnumerable<LightBoxRecord> Records()
        {
            // Slightly outside the eastern boundary due to precision noise.
            yield return new LightBoxRecord
            {
                Name = "near-boundary",
                EntityCategory = "Business",
                Latitude = 5,
                Longitude = 10.0000005,
                BuildingType = "Large Business",
                Address = "1 Main"
            };

            await Task.CompletedTask;
        }

        var scored = await engine.BuildCandidatesAsync(Records(), zones, new FilterOptions(), new ScoringOptions(), [], CancellationToken.None);

        Assert.Single(scored);
        Assert.Equal("VNN", scored[0].ZoneName);
    }

    [Fact]
    public void ExclusionNormalization_UppercasesAndAbbreviates()
    {
        Assert.Equal("123 MAIN ST", AddressNormalizer.Normalize("123 Main Street"));
    }
}
