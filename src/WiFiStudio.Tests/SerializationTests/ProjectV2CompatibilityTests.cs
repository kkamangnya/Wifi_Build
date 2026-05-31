using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Tests.SerializationTests;

public sealed class ProjectV2CompatibilityTests
{
    [Fact]
    public void Serialize_Writes_V2_Project_WithObjectsAndUsers()
    {
        var project = ProjectFactory.CreateSampleOffice();

        var json = ProjectJsonSerializer.Serialize(project);
        var roundTrip = ProjectJsonSerializer.Deserialize(json);

        Assert.Equal("2.0", roundTrip.SchemaVersion);
        Assert.NotEmpty(roundTrip.FloorPlan.Objects);
        Assert.NotEmpty(roundTrip.FloorPlan.Users);
        Assert.Contains("objects", json);
        Assert.Contains("users", json);
    }

    [Fact]
    public void Deserialize_Migrates_LegacyVirtualUsers()
    {
        const string legacyJson = """
        {
          "schemaVersion": "1.0",
          "name": "Legacy",
          "floorPlan": {
            "widthCm": 1000,
            "heightCm": 800,
            "gridSizeCm": 25,
            "walls": [],
            "doors": [],
            "windows": [],
            "furniture": [],
            "accessPoints": [],
            "virtualUsers": [
              {
                "id": "u1",
                "name": "Legacy User",
                "isVisible": true,
                "isLocked": false,
                "position": { "x": 100, "y": 200 },
                "weight": 2,
                "route": []
              }
            ]
          },
          "materials": [],
          "simulationSettings": {}
        }
        """;

        var project = ProjectJsonSerializer.Deserialize(legacyJson);

        Assert.Single(project.FloorPlan.Users);
        Assert.Equal("Legacy User", project.FloorPlan.Users[0].Name);
        Assert.NotEmpty(project.Materials);
    }
}
