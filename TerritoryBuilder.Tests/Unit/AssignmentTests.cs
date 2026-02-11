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
}
