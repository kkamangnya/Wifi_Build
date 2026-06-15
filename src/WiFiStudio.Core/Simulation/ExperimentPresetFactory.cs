using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Core.Simulation;

public static class ExperimentPresetFactory
{
    public static IReadOnlyList<ExperimentPreset> CreatePresets() =>
    [
        new()
        {
            Id = "condition-1-open-area",
            ConditionName = "Condition 1 - Open Area",
            StructureDescription = "Almost no obstacles",
            Kind = ExperimentConditionKind.OpenArea
        },
        new()
        {
            Id = "condition-2-wall-dense",
            ConditionName = "Condition 2 - Wall Dense",
            StructureDescription = "Multiple drywall partitions",
            Kind = ExperimentConditionKind.WallDense
        },
        new()
        {
            Id = "condition-3-concrete",
            ConditionName = "Condition 3 - Concrete Wall",
            StructureDescription = "Concrete wall across the main signal path",
            Kind = ExperimentConditionKind.ConcreteWall
        },
        new()
        {
            Id = "condition-4-metal",
            ConditionName = "Condition 4 - Metal Furniture",
            StructureDescription = "Metal shelves and server racks",
            Kind = ExperimentConditionKind.MetalFurniture
        },
        new()
        {
            Id = "condition-5-user-optimized",
            ConditionName = "Condition 5 - User Optimized",
            StructureDescription = "AP placement optimized for weighted user locations",
            Kind = ExperimentConditionKind.UserOptimized
        }
    ];

    public static ProjectModel CreateCondition(ProjectModel source, ExperimentPreset preset)
    {
        var project = Clone(source);
        project.Name = $"{source.Name} - {preset.ConditionName}";
        project.OptimizationResults.Clear();
        EnsureAccessPointAndUser(project);

        switch (preset.Kind)
        {
            case ExperimentConditionKind.OpenArea:
                project.FloorPlan.Walls.Clear();
                project.FloorPlan.Doors.Clear();
                project.FloorPlan.Windows.Clear();
                project.FloorPlan.Furniture.Clear();
                project.FloorPlan.Objects.Clear();
                break;
            case ExperimentConditionKind.WallDense:
                AddWallDenseLayout(project);
                break;
            case ExperimentConditionKind.ConcreteWall:
                AddConcreteWall(project);
                break;
            case ExperimentConditionKind.MetalFurniture:
                AddMetalFurniture(project);
                break;
            case ExperimentConditionKind.UserOptimized:
                break;
        }

        return project;
    }

    private static void EnsureAccessPointAndUser(ProjectModel project)
    {
        if (project.FloorPlan.AccessPoints.Count == 0)
        {
            project.FloorPlan.AccessPoints.Add(new AccessPoint
            {
                Name = "Experiment AP-01",
                Position = new PlanPoint(project.FloorPlan.WidthCm * 0.25, project.FloorPlan.HeightCm * 0.5),
                Band = project.SimulationSettings.FrequencyBand
            });
        }

        if (project.FloorPlan.Users.Count == 0)
        {
            project.FloorPlan.Users.Add(new UserLocation
            {
                Name = "Measurement User",
                Position = new PlanPoint(project.FloorPlan.WidthCm * 0.75, project.FloorPlan.HeightCm * 0.5),
                Weight = 3
            });
        }
    }

    private static void AddWallDenseLayout(ProjectModel project)
    {
        var width = project.FloorPlan.WidthCm;
        var height = project.FloorPlan.HeightCm;
        project.FloorPlan.Walls.AddRange(
        [
            CreateWall("Experiment drywall A", width * 0.35, height * 0.35, height * 0.65, 90, "drywall"),
            CreateWall("Experiment drywall B", width * 0.55, height * 0.65, height * 0.65, 90, "drywall"),
            CreateWall("Experiment drywall C", width * 0.72, height * 0.42, width * 0.34, 0, "drywall"),
            CreateWall("Experiment drywall D", width * 0.28, height * 0.72, width * 0.32, 0, "drywall")
        ]);
    }

    private static void AddConcreteWall(ProjectModel project)
    {
        project.FloorPlan.Walls.Add(CreateWall(
            "Experiment concrete barrier",
            project.FloorPlan.WidthCm * 0.5,
            project.FloorPlan.HeightCm * 0.5,
            project.FloorPlan.HeightCm * 0.82,
            90,
            "concrete"));
    }

    private static void AddMetalFurniture(ProjectModel project)
    {
        var width = project.FloorPlan.WidthCm;
        var height = project.FloorPlan.HeightCm;
        project.FloorPlan.Objects.AddRange(
        [
            PlanObject.CreateDefault(PlanObjectType.MetalShelf, new PlanPoint(width * 0.44, height * 0.35)),
            PlanObject.CreateDefault(PlanObjectType.ServerRack, new PlanPoint(width * 0.52, height * 0.55)),
            PlanObject.CreateDefault(PlanObjectType.MetalShelf, new PlanPoint(width * 0.62, height * 0.72))
        ]);
    }

    private static WallElement CreateWall(string name, double x, double y, double length, double rotation, string materialId) =>
        new()
        {
            Name = name,
            Center = new PlanPoint(x, y),
            LengthCm = Math.Max(100, length),
            ThicknessCm = materialId == "concrete" ? 20 : 12,
            RotationDegrees = rotation,
            MaterialId = materialId,
            BlocksSignal = true
        };

    private static ProjectModel Clone(ProjectModel project) =>
        ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));
}
