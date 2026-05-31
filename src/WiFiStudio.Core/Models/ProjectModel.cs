namespace WiFiStudio.Core.Models;

public sealed class ProjectModel
{
    public string SchemaVersion { get; set; } = "2.0";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled RF Plan";
    public string ProjectName
    {
        get => Name;
        set => Name = value;
    }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public FloorPlan FloorPlan { get; set; } = new();
    public List<FloorPlan> Floors { get; set; } = [];
    public int ActiveFloorIndex { get; set; }
    public List<MaterialProfile> Materials { get; set; } = [.. MaterialProfile.CreateDefaultLibrary()];
    public RfSimulationSettings SimulationSettings { get; set; } = new();
    public List<OptimizationResult> OptimizationResults { get; set; } = [];
    public HeatmapDisplaySettings HeatmapDisplay { get; set; } = new();
    public LayerState LayerState { get; set; } = new();

    public List<WallElement> Walls
    {
        get => FloorPlan.Walls;
        set => FloorPlan.Walls = value ?? [];
    }

    public List<PlanObject> Objects
    {
        get => FloorPlan.Objects;
        set => FloorPlan.Objects = value ?? [];
    }

    public List<AccessPoint> AccessPoints
    {
        get => FloorPlan.AccessPoints;
        set => FloorPlan.AccessPoints = value ?? [];
    }

    public List<UserLocation> Users
    {
        get => FloorPlan.Users;
        set => FloorPlan.Users = value ?? [];
    }

    public MaterialProfile MaterialOrDefault(string? materialId)
    {
        return Materials.FirstOrDefault(m => string.Equals(m.Id, materialId, StringComparison.OrdinalIgnoreCase))
            ?? Materials.FirstOrDefault(m => m.Id == "drywall")
            ?? new MaterialProfile();
    }
}

public sealed class HeatmapDisplaySettings
{
    public HeatmapType Mode { get; set; } = HeatmapType.Rssi;
    public bool IsVisible { get; set; } = true;
    public double Opacity { get; set; } = 0.82;
    public double ThresholdDbm { get; set; } = -67;
}

public sealed class LayerState
{
    public bool StructuresVisible { get; set; } = true;
    public bool ObjectsVisible { get; set; } = true;
    public bool AccessPointsVisible { get; set; } = true;
    public bool UsersVisible { get; set; } = true;
    public bool HeatmapVisible { get; set; } = true;
}
