using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Tests.GeometryTests;

public sealed class GeometryMathTests
{
    [Fact]
    public void SegmentIntersectsWall_Detects_RotatedWallCrossing()
    {
        var wall = new WallElement
        {
            Center = new PlanPoint(500, 500),
            LengthCm = 800,
            ThicknessCm = 20,
            RotationDegrees = 45
        };

        Assert.True(GeometryMath.SegmentIntersectsWall(new PlanPoint(200, 200), new PlanPoint(800, 800), wall));
    }

    [Fact]
    public void PointInsideAnyObstacle_ReturnsTrue_ForWallFootprint()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.Walls.Add(new WallElement
        {
            Center = new PlanPoint(200, 200),
            LengthCm = 400,
            ThicknessCm = 30
        });

        Assert.True(GeometryMath.PointInsideAnyObstacle(new PlanPoint(200, 200), project));
    }
}
