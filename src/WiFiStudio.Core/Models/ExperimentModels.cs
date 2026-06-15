using System.Text.Json.Serialization;

namespace WiFiStudio.Core.Models;

public enum ExperimentConditionKind
{
    OpenArea,
    WallDense,
    ConcreteWall,
    MetalFurniture,
    UserOptimized
}

public sealed class ExperimentPreset
{
    public string Id { get; set; } = "";
    public string ConditionName { get; set; } = "";
    public string StructureDescription { get; set; } = "";
    public ExperimentConditionKind Kind { get; set; }
}

public sealed class ExperimentProgress
{
    public int ConditionIndex { get; set; }
    public int ConditionCount { get; set; }
    public string ConditionName { get; set; } = "";
    public string Stage { get; set; } = "";
}

public sealed class ExperimentResultRow
{
    public string ConditionId { get; set; } = "";
    public string ConditionName { get; set; } = "";
    public string StructureDescription { get; set; } = "";
    public string ApPosition { get; set; } = "";
    public string UserPosition { get; set; } = "";
    public string UserName { get; set; } = "";
    public double UserRssi { get; set; }
    public double AverageRssi { get; set; }
    public double MinimumRssi { get; set; }
    public double DeadZoneRatio { get; set; }
    public string ConnectedAp { get; set; } = "None";
    public double BeforeOptimizationRssi { get; set; }
    public double AfterOptimizationRssi { get; set; }
    public double OptimizationDeltaDb { get; set; }
    public string AnalysisNote { get; set; } = "";

    [JsonIgnore]
    public string UserPositionDisplay => $"{UserName} {UserPosition}";

    [JsonIgnore]
    public string UserRssiDisplay => $"{UserRssi:F1} dBm";

    [JsonIgnore]
    public string AverageRssiDisplay => $"{AverageRssi:F1} dBm";

    [JsonIgnore]
    public string MinimumRssiDisplay => $"{MinimumRssi:F1} dBm";

    [JsonIgnore]
    public string DeadZoneRatioDisplay => $"{DeadZoneRatio:P1}";

    [JsonIgnore]
    public string OptimizationDeltaDisplay => $"{OptimizationDeltaDb:+0.0;-0.0;0.0} dB";

    [JsonIgnore]
    public string QualityDisplay => UserRssi switch
    {
        >= -50 => "Excellent",
        >= -67 => "Good",
        >= -75 => "Fair",
        >= -85 => "Poor",
        _ => "Dead Zone"
    };

    [JsonIgnore]
    public string BeforeOptimizationRssiDisplay => $"{BeforeOptimizationRssi:F1} dBm";

    [JsonIgnore]
    public string AfterOptimizationRssiDisplay => $"{AfterOptimizationRssi:F1} dBm";
}

public sealed class ExperimentRunResult
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ExperimentResultRow> Rows { get; set; } = [];
    public string Summary { get; set; } = "";
    public double FloorWidthCm { get; set; }
    public double FloorHeightCm { get; set; }

    [JsonIgnore]
    public Dictionary<string, HeatmapResult> Heatmaps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public Dictionary<string, ProjectModel> DisplayProjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public Dictionary<string, ProjectModel> BaselineProjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public Dictionary<string, ProjectModel> OptimizedProjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public Dictionary<string, HeatmapResult> BaselineHeatmaps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public Dictionary<string, HeatmapResult> OptimizedHeatmaps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
