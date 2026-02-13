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
}
