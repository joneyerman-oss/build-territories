using NetTopologySuite.Geometries;
using TerritoryBuilder.Core.Assignment;
using TerritoryBuilder.Core.Models;

namespace TerritoryBuilder.Tests.Unit;

public class AssignmentTests
{
    [Fact]
    public async Task FairnessMetric_IsComputed()
    {
        var service = new InitialAssignmentService();
        var candidates = new List<BusinessCandidate>
        {
            new() { Source = new LightBoxRecord { Name = "a" }, Point = new Point(new Coordinate(0, 0)), ZoneName = "VNN", Score = 10 },
            new() { Source = new LightBoxRecord { Name = "b" }, Point = new Point(new Coordinate(1, 1)), ZoneName = "VNN", Score = 5 }
        };
        var reps = new List<RepRecord>
        {
            new() { RepId = "r1", RepName = "Rep 1", Active = true, HomeLat = 0, HomeLon = 0 },
            new() { RepId = "r2", RepName = "Rep 2", Active = true, HomeLat = 1, HomeLon = 1 }
        };

        var result = await service.AssignAsync(candidates, reps, new AssignmentOptions(), CancellationToken.None);
        Assert.True(result.Overall.FairnessIndex >= 0);
        Assert.Equal(2, result.RepMetrics.Count);
    }

    [Fact]
    public async Task SameHomeReps_UseDifferentAngularSlices()
    {
        var service = new InitialAssignmentService();
        var candidates = new List<BusinessCandidate>
        {
            new() { Source = new LightBoxRecord { Name = "n" }, Point = new Point(new Coordinate(0, 1)), ZoneName = "VNN", Score = 1 },
            new() { Source = new LightBoxRecord { Name = "e" }, Point = new Point(new Coordinate(1, 0)), ZoneName = "VNN", Score = 1 },
            new() { Source = new LightBoxRecord { Name = "s" }, Point = new Point(new Coordinate(0, -1)), ZoneName = "VNN", Score = 1 },
            new() { Source = new LightBoxRecord { Name = "w" }, Point = new Point(new Coordinate(-1, 0)), ZoneName = "VNN", Score = 1 }
        };
        var reps = new List<RepRecord>
        {
            new() { RepId = "r1", RepName = "Rep 1", Active = true, HomeLat = 0, HomeLon = 0 },
            new() { RepId = "r2", RepName = "Rep 2", Active = true, HomeLat = 0, HomeLon = 0 }
        };

        var result = await service.AssignAsync(candidates, reps, new AssignmentOptions(), CancellationToken.None);

        Assert.Equal(2, result.RepMetrics.Single(m => m.RepId == "r1").BusinessCount);
        Assert.Equal(2, result.RepMetrics.Single(m => m.RepId == "r2").BusinessCount);
    }
    [Fact]
    public async Task RepMetrics_IncludeBusinessTypeCounts_AndFairnessBandFlag()
    {
        var service = new InitialAssignmentService();
        var candidates = new List<BusinessCandidate>
        {
            new() { Source = new LightBoxRecord { Name = "s", BuildingType = "Small Business" }, Point = new Point(new Coordinate(0, 1)), ZoneName = "VNN", Score = 3 },
            new() { Source = new LightBoxRecord { Name = "m", BuildingType = "Medium Business" }, Point = new Point(new Coordinate(1, 0)), ZoneName = "VNN", Score = 6 },
            new() { Source = new LightBoxRecord { Name = "l", BuildingType = "Large Business" }, Point = new Point(new Coordinate(0, -1)), ZoneName = "VNN", Score = 10 }
        };
        var reps = new List<RepRecord>
        {
            new() { RepId = "r1", RepName = "Rep 1", Active = true, HomeLat = 0, HomeLon = 0 },
            new() { RepId = "r2", RepName = "Rep 2", Active = true, HomeLat = 0, HomeLon = 0 }
        };

        var result = await service.AssignAsync(candidates, reps, new AssignmentOptions { FairnessTolerancePercent = 5 }, CancellationToken.None);

        var first = result.RepMetrics.Single(m => m.RepId == "r1");
        var second = result.RepMetrics.Single(m => m.RepId == "r2");

        Assert.Equal(1, first.SmallBusinessCount);
        Assert.Equal(1, first.MediumBusinessCount);
        Assert.Equal(0, first.LargeBusinessCount);
        Assert.Equal(0, second.SmallBusinessCount);
        Assert.Equal(0, second.MediumBusinessCount);
        Assert.Equal(1, second.LargeBusinessCount);
        Assert.False(first.WithinFairnessTolerance);
        Assert.False(second.WithinFairnessTolerance);
    }

}
