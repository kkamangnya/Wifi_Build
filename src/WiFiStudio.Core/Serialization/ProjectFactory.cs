using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class ProjectFactory
{
    public static ProjectModel CreateNewProject()
    {
        var floor = new FloorPlan
        {
            WidthCm = 2500,
            HeightCm = 1600,
            GridSizeCm = 25
        };

        return new ProjectModel
        {
            SchemaVersion = "2.0",
            Name = "Untitled RF Plan",
            FloorPlan = floor,
            Floors = [floor],
            Materials = [.. MaterialProfile.CreateDefaultLibrary()],
            SimulationSettings = new RfSimulationSettings()
        };
    }

    public static ProjectModel CreateSampleOffice()
    {
        var project = CreateNewProject();
        project.Name = "Sample Office";
        project.FloorPlan.Walls.AddRange(
        [
            CreateWall("North Wall", 1250, 40, 2500, 16, 0, "concrete"),
            CreateWall("South Wall", 1250, 1560, 2500, 16, 0, "concrete"),
            CreateWall("West Wall", 40, 800, 1600, 16, 90, "concrete"),
            CreateWall("East Wall", 2460, 800, 1600, 16, 90, "concrete"),
            CreateWall("Meeting Room Divider", 850, 650, 1000, 12, 90, "glass"),
            CreateWall("Office Divider", 1450, 850, 1200, 12, 0, "drywall")
        ]);
        project.FloorPlan.AccessPoints.Add(new AccessPoint
        {
            Name = "AP-1",
            Position = new PlanPoint(650, 500),
            Band = FrequencyBand.Ghz5,
            TxPowerDbm = 18,
            Channel = 36
        });
        project.FloorPlan.Objects.AddRange(
        [
            PlanObject.CreateDefault(PlanObjectType.ConferenceTable, new PlanPoint(650, 650)),
            PlanObject.CreateDefault(PlanObjectType.Desk, new PlanPoint(1780, 1040)),
            PlanObject.CreateDefault(PlanObjectType.Bookshelf, new PlanPoint(2140, 360)),
            PlanObject.CreateDefault(PlanObjectType.ServerRack, new PlanPoint(2120, 1230))
        ]);
        project.FloorPlan.Users.Add(new UserLocation
        {
            Name = "Focus Area",
            Position = new PlanPoint(1850, 1050),
            Weight = 3.0,
            MobilityMode = UserMobilityMode.Fixed
        });
        return project;
    }

    private static WallElement CreateWall(
        string name,
        double centerX,
        double centerY,
        double lengthCm,
        double thicknessCm,
        double rotationDegrees,
        string materialId)
    {
        return new WallElement
        {
            Name = name,
            Center = new PlanPoint(centerX, centerY),
            LengthCm = lengthCm,
            ThicknessCm = thicknessCm,
            RotationDegrees = rotationDegrees,
            MaterialId = materialId
        };
    }
}
