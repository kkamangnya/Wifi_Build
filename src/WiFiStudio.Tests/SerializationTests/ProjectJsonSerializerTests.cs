using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Tests.SerializationTests;

public sealed class ProjectJsonSerializerTests
{
    [Fact]
    public void Project_RoundTrips_WithWallsAndAccessPoints()
    {
        var project = ProjectFactory.CreateSampleOffice();

        var json = ProjectJsonSerializer.Serialize(project);
        var roundTripped = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal(project.Name, roundTripped.Name);
        Assert.Equal(project.FloorPlan.Walls.Count, roundTripped.FloorPlan.Walls.Count);
        Assert.Equal(project.FloorPlan.AccessPoints.Count, roundTripped.FloorPlan.AccessPoints.Count);
        Assert.NotEmpty(roundTripped.Materials);
    }
}
