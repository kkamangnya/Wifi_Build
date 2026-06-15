using System.Globalization;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Optimization;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Core.Simulation;

public sealed class ExperimentRunner
{
    private readonly RfSimulationEngine _engine = new();
    private readonly UserSignalAnalyzer _userAnalyzer = new();
    private readonly ApPlacementOptimizer _optimizer = new();

    public Task<ExperimentRunResult> RunAsync(
        ProjectModel source,
        IProgress<ExperimentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Run(source, progress, cancellationToken), cancellationToken);
    }

    public ExperimentRunResult Run(
        ProjectModel source,
        IProgress<ExperimentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var presets = ExperimentPresetFactory.CreatePresets();
        var result = new ExperimentRunResult
        {
            FloorWidthCm = source.FloorPlan.WidthCm,
            FloorHeightCm = source.FloorPlan.HeightCm
        };

        for (var index = 0; index < presets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preset = presets[index];
            progress?.Report(CreateProgress(index, presets.Count, preset.ConditionName, "Preparing"));
            var condition = ExperimentPresetFactory.CreateCondition(source, preset);

            progress?.Report(CreateProgress(index, presets.Count, preset.ConditionName, "Baseline simulation"));
            var baselineHeatmap = _engine.Evaluate(condition, condition.SimulationSettings, cancellationToken: cancellationToken);
            var baselineUsers = condition.FloorPlan.Users.ToDictionary(
                user => user.Id,
                user => _userAnalyzer.Analyze(condition, user));

            progress?.Report(CreateProgress(index, presets.Count, preset.ConditionName, "User-centered optimization"));
            var optimization = _optimizer.RecommendLayout(
                condition,
                Math.Max(1, condition.FloorPlan.AccessPoints.Count),
                cancellationToken,
                OptimizationMode.UserQuality);
            var optimized = ApplyRecommendedLayout(condition, optimization);
            var optimizedHeatmap = _engine.Evaluate(optimized, optimized.SimulationSettings, cancellationToken: cancellationToken);
            var useOptimizedResult = preset.Kind == ExperimentConditionKind.UserOptimized;
            var displayedProject = useOptimizedResult ? optimized : condition;
            var displayedHeatmap = useOptimizedResult ? optimizedHeatmap : baselineHeatmap;
            result.Heatmaps[preset.Id] = displayedHeatmap;
            result.DisplayProjects[preset.Id] = displayedProject;
            result.BaselineProjects[preset.Id] = condition;
            result.OptimizedProjects[preset.Id] = optimized;
            result.BaselineHeatmaps[preset.Id] = baselineHeatmap;
            result.OptimizedHeatmaps[preset.Id] = optimizedHeatmap;

            foreach (var user in condition.FloorPlan.Users)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var before = baselineUsers[user.Id];
                var optimizedUser = optimized.FloorPlan.Users.First(candidate => candidate.Id == user.Id);
                var after = _userAnalyzer.Analyze(optimized, optimizedUser);
                var displayed = useOptimizedResult ? after : before;
                result.Rows.Add(new ExperimentResultRow
                {
                    ConditionId = preset.Id,
                    ConditionName = preset.ConditionName,
                    StructureDescription = preset.StructureDescription,
                    ApPosition = FormatApPositions(displayedProject),
                    UserPosition = $"({user.Position.X:F0}, {user.Position.Y:F0}) cm",
                    UserName = user.Name,
                    UserRssi = displayed.RssiDbm,
                    AverageRssi = displayedHeatmap.Stats.AverageRssiDbm,
                    MinimumRssi = displayedHeatmap.Stats.MinimumRssiDbm,
                    DeadZoneRatio = displayedHeatmap.Stats.ShadowRatio,
                    ConnectedAp = displayed.ConnectedApName,
                    BeforeOptimizationRssi = before.RssiDbm,
                    AfterOptimizationRssi = after.RssiDbm,
                    OptimizationDeltaDb = after.RssiDbm - before.RssiDbm,
                    AnalysisNote = BuildRowNote(preset, before, after)
                });
            }

            progress?.Report(CreateProgress(index, presets.Count, preset.ConditionName, "Completed"));
        }

        result.Summary = BuildSummary(result.Rows);
        return result;
    }

    private static ProjectModel ApplyRecommendedLayout(ProjectModel source, OptimizationResult optimization)
    {
        var clone = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(source));
        if (optimization.Recommendations.Count == 0)
        {
            return clone;
        }

        clone.FloorPlan.AccessPoints.Clear();
        for (var index = 0; index < optimization.Recommendations.Count; index++)
        {
            var recommendation = optimization.Recommendations[index];
            clone.FloorPlan.AccessPoints.Add(new AccessPoint
            {
                Name = $"Optimized AP-{index + 1:00}",
                Position = recommendation.Position,
                Band = clone.SimulationSettings.FrequencyBand,
                TxPowerDbm = recommendation.RecommendedTxPowerDbm,
                Channel = recommendation.RecommendedChannel,
                BandwidthMhz = clone.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 20 : 40
            });
        }

        return clone;
    }

    private static ExperimentProgress CreateProgress(int index, int count, string name, string stage) =>
        new()
        {
            ConditionIndex = index + 1,
            ConditionCount = count,
            ConditionName = name,
            Stage = stage
        };

    private static string FormatApPositions(ProjectModel project) =>
        string.Join("; ", project.FloorPlan.AccessPoints.Select(ap => $"{ap.Name} ({ap.Position.X:F0}, {ap.Position.Y:F0})"));

    private static string BuildRowNote(ExperimentPreset preset, UserSignalAnalysis before, UserSignalAnalysis after)
    {
        var delta = after.RssiDbm - before.RssiDbm;
        var conditionMeaning = preset.Kind switch
        {
            ExperimentConditionKind.OpenArea => "Low obstacle loss provides the reference signal level.",
            ExperimentConditionKind.WallDense => "Multiple partitions increase cumulative material loss.",
            ExperimentConditionKind.ConcreteWall => "Concrete attenuation strongly affects the signal path.",
            ExperimentConditionKind.MetalFurniture => "Metal objects create high-loss blocked paths.",
            ExperimentConditionKind.UserOptimized => "AP placement prioritizes weighted user positions.",
            _ => ""
        };
        return $"{conditionMeaning} User optimization changes RSSI by {delta:+0.0;-0.0;0.0} dB ({before.Quality} to {after.Quality}).";
    }

    private static string BuildSummary(IReadOnlyList<ExperimentResultRow> rows)
    {
        if (rows.Count == 0)
        {
            return "No experiment measurements were produced.";
        }

        var conditions = rows
            .GroupBy(row => row.ConditionName)
            .Select(group => new
            {
                Name = group.Key,
                AverageUserRssi = group.Average(row => row.UserRssi),
                AverageAreaRssi = group.Average(row => row.AverageRssi),
                DeadZoneRatio = group.Average(row => row.DeadZoneRatio),
                OptimizationGain = group.Average(row => row.OptimizationDeltaDb)
            })
            .ToList();
        var weakest = conditions.MinBy(condition => condition.AverageUserRssi)!;
        var strongest = conditions.MaxBy(condition => condition.AverageUserRssi)!;
        var mostImproved = conditions.MaxBy(condition => condition.OptimizationGain)!;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{weakest.Name} produced the lowest user RSSI ({weakest.AverageUserRssi:F1} dBm), showing the impact of structure and material attenuation. " +
            $"{strongest.Name} produced the strongest user RSSI ({strongest.AverageUserRssi:F1} dBm). " +
            $"{mostImproved.Name} had the largest average AP-optimization gain ({mostImproved.OptimizationGain:+0.0;-0.0;0.0} dB).");
    }
}
