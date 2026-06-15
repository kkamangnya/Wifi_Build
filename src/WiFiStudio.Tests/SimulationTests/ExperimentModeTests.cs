using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Rendering.Heatmaps;
using System.Runtime.Versioning;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class ExperimentModeTests
{
    [Fact]
    public void ExperimentPresets_CreateAllFiveDistinctConditions()
    {
        var source = CreateSmallProject();
        var presets = ExperimentPresetFactory.CreatePresets();
        var open = ExperimentPresetFactory.CreateCondition(source, presets[0]);
        var wallDense = ExperimentPresetFactory.CreateCondition(source, presets[1]);
        var concrete = ExperimentPresetFactory.CreateCondition(source, presets[2]);
        var metal = ExperimentPresetFactory.CreateCondition(source, presets[3]);

        Assert.Equal(5, presets.Count);
        Assert.Empty(open.FloorPlan.Walls);
        Assert.True(wallDense.FloorPlan.Walls.Count >= 4);
        Assert.Contains(concrete.FloorPlan.Walls, wall => wall.MaterialId == "concrete");
        Assert.Contains(metal.FloorPlan.Objects, obj => obj.Type is PlanObjectType.MetalShelf or PlanObjectType.ServerRack);
    }

    [Fact]
    public void ConcreteCondition_ReducesUserRssiComparedWithOpenArea()
    {
        var source = CreateSmallProject();
        var presets = ExperimentPresetFactory.CreatePresets();
        var open = ExperimentPresetFactory.CreateCondition(source, presets[0]);
        var concrete = ExperimentPresetFactory.CreateCondition(source, presets[2]);
        var analyzer = new UserSignalAnalyzer();

        var openRssi = analyzer.Analyze(open, open.FloorPlan.Users[0]).RssiDbm;
        var concreteRssi = analyzer.Analyze(concrete, concrete.FloorPlan.Users[0]).RssiDbm;

        Assert.True(concreteRssi < openRssi);
    }

    [Fact]
    public async Task ExperimentCsvExporter_WritesRequiredColumnsAndRows()
    {
        var result = new ExperimentRunResult
        {
            Rows =
            [
                new ExperimentResultRow
                {
                    ConditionName = "Condition 1",
                    StructureDescription = "Open",
                    ApPosition = "AP-01 (100, 100)",
                    UserPosition = "(500, 200) cm",
                    UserName = "User A",
                    UserRssi = -61,
                    AverageRssi = -65,
                    MinimumRssi = -80,
                    DeadZoneRatio = 0.1,
                    ConnectedAp = "AP-01",
                    AnalysisNote = "Reference condition."
                }
            ]
        };
        var path = Path.Combine(Path.GetTempPath(), "wifi-studio-tests", Guid.NewGuid().ToString("N"), "experiment.csv");

        await ExperimentCsvExporter.ExportAsync(result, path);
        var csv = await File.ReadAllTextAsync(path);

        Assert.StartsWith("ConditionName,StructureDescription,APPosition,UserPosition,UserRSSI,AverageRSSI,MinimumRSSI,DeadZoneRatio,ConnectedAP,AnalysisNote", csv);
        Assert.Contains("Condition 1", csv);
        Assert.Contains("User A", csv);
    }

    [Fact]
    public void MovingUserLocation_RefreshesMeasuredRssi()
    {
        var project = CreateSmallProject();
        var user = project.FloorPlan.Users[0];
        var analyzer = new UserSignalAnalyzer();
        user.Position = new PlanPoint(120, 200);
        var near = analyzer.Analyze(project, user);

        user.Position = new PlanPoint(570, 200);
        var far = analyzer.Analyze(project, user);

        Assert.True(near.RssiDbm > far.RssiDbm);
        Assert.Equal(570, far.X);
    }

    [Fact]
    public void ExperimentRunner_ComparesBeforeAndAfterUserOptimization()
    {
        var project = CreateSmallProject();
        var result = new ExperimentRunner().Run(project);
        var optimized = Assert.Single(result.Rows, row => row.ConditionId == "condition-5-user-optimized");

        Assert.Equal(5, result.Rows.Count);
        Assert.True(double.IsFinite(optimized.BeforeOptimizationRssi));
        Assert.True(double.IsFinite(optimized.AfterOptimizationRssi));
        Assert.Equal(optimized.AfterOptimizationRssi - optimized.BeforeOptimizationRssi, optimized.OptimizationDeltaDb, 6);
        Assert.True(optimized.AfterOptimizationRssi >= optimized.BeforeOptimizationRssi);
        Assert.Equal(5, result.Heatmaps.Count);
        Assert.True(result.BaselineHeatmaps.ContainsKey("condition-5-user-optimized"));
        Assert.True(result.OptimizedHeatmaps.ContainsKey("condition-5-user-optimized"));
        Assert.True(result.DisplayProjects.ContainsKey("condition-5-user-optimized"));
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }

    [Fact]
    public void RssiColorScale_UsesRequiredLegendBands()
    {
        Assert.Equal(-90, HeatmapColorScale.FixedMinRssiDbm);
        Assert.Equal(-30, HeatmapColorScale.FixedMaxRssiDbm);
        Assert.Equal(5, HeatmapColorScale.RssiLegendBands.Count);

        Assert.Equal(HeatmapColorScale.RssiLegendBands[0].Color, HeatmapColorScale.ForRssi(-45));
        Assert.Equal(HeatmapColorScale.RssiLegendBands[1].Color, HeatmapColorScale.ForRssi(-60));
        Assert.Equal(HeatmapColorScale.RssiLegendBands[2].Color, HeatmapColorScale.ForRssi(-70));
        Assert.Equal(HeatmapColorScale.RssiLegendBands[3].Color, HeatmapColorScale.ForRssi(-80));
        Assert.Equal(HeatmapColorScale.RssiLegendBands[4].Color, HeatmapColorScale.ForRssi(-88));
    }

    [Fact]
    public async Task ExperimentVisualization_ExportsAnnotatedImagesAndTextReport()
    {
        var project = CreateSmallProject();
        var result = new ExperimentRunner().Run(project);
        var directory = Path.Combine(Path.GetTempPath(), "wifi-studio-tests", Guid.NewGuid().ToString("N"));
        var conditionId = "condition-5-user-optimized";
        var rows = result.Rows.Where(row => row.ConditionId == conditionId).ToList();
        var annotatedPath = Path.Combine(directory, "condition-5-annotated.png");
        var deltaPath = Path.Combine(directory, "condition-5-delta.png");
        var reportPath = Path.Combine(directory, "summary.txt");

        await ExperimentHeatmapPngExporter.ExportConditionAsync(
            result.DisplayProjects[conditionId],
            result.Heatmaps[conditionId],
            rows,
            annotatedPath,
            widthPx: 1600);
        await ExperimentHeatmapPngExporter.ExportDifferenceAsync(
            result.BaselineProjects[conditionId],
            result.BaselineHeatmaps[conditionId],
            result.OptimizedProjects[conditionId],
            result.OptimizedHeatmaps[conditionId],
            rows,
            deltaPath,
            widthPx: 1600);
        await ExperimentTextReportExporter.ExportAsync(result, reportPath, [annotatedPath, deltaPath]);

        Assert.True(new FileInfo(annotatedPath).Length > 1000);
        Assert.True(new FileInfo(deltaPath).Length > 1000);
        Assert.Equal(1600, ReadPngWidth(annotatedPath));
        Assert.Equal(1600, ReadPngWidth(deltaPath));
        var report = await File.ReadAllTextAsync(reportPath);
        Assert.Contains("Condition Summary", report);
        Assert.Contains("Condition 5 Before/After", report);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ExperimentReportImageExporter_WritesSansSerifReportImagesAtMinimumWidth()
    {
        var project = CreateSmallProject();
        var result = new ExperimentRunner().Run(project);
        var directory = Path.Combine(Path.GetTempPath(), "wifi-studio-tests", Guid.NewGuid().ToString("N"));
        var conditionId = "condition-5-user-optimized";
        var rows = result.Rows.Where(row => row.ConditionId == conditionId).ToList();
        var reportPath = Path.Combine(directory, "report-condition-5.png");
        var deltaPath = Path.Combine(directory, "report-condition-5-delta.png");

        await ExperimentReportImageExporter.ExportConditionAsync(
            result.DisplayProjects[conditionId],
            result.Heatmaps[conditionId],
            rows,
            reportPath,
            widthPx: 1200);
        await ExperimentReportImageExporter.ExportDifferenceAsync(
            result.BaselineProjects[conditionId],
            result.BaselineHeatmaps[conditionId],
            result.OptimizedProjects[conditionId],
            result.OptimizedHeatmaps[conditionId],
            rows,
            deltaPath,
            widthPx: 1200);

        Assert.True(new FileInfo(reportPath).Length > 20_000);
        Assert.True(new FileInfo(deltaPath).Length > 20_000);
        Assert.True(ReadPngWidth(reportPath) >= 1920);
        Assert.True(ReadPngWidth(deltaPath) >= 1920);
    }

    private static int ReadPngWidth(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
    }

    private static ProjectModel CreateSmallProject()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.WidthCm = 600;
        project.FloorPlan.HeightCm = 400;
        project.FloorPlan.GridSizeCm = 25;
        project.SimulationSettings.SampleResolutionCm = 200;
        project.FloorPlan.AccessPoints.Add(new AccessPoint
        {
            Name = "AP-01",
            Position = new PlanPoint(75, 200),
            Band = FrequencyBand.Ghz5,
            TxPowerDbm = 18,
            Channel = 36
        });
        project.FloorPlan.Users.Add(new UserLocation
        {
            Name = "User A",
            Position = new PlanPoint(525, 200),
            Weight = 5
        });
        return project;
    }
}
