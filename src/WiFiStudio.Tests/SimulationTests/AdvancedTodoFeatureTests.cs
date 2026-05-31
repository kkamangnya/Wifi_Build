using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Rendering.Heatmaps;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class AdvancedTodoFeatureTests
{
    [Fact]
    public void DoorAndWindow_Add_PartialMaterialLoss()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.Doors.Add(new DoorElement
        {
            Center = new PlanPoint(500, 500),
            WidthCm = 120,
            RotationDegrees = 90,
            MaterialId = "wood"
        });
        project.FloorPlan.Windows.Add(new WindowElement
        {
            Center = new PlanPoint(700, 500),
            WidthCm = 120,
            RotationDegrees = 90,
            MaterialId = "glass"
        });

        var partialLoss = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz5);

        project.FloorPlan.Walls.Add(new WallElement
        {
            Center = new PlanPoint(600, 500),
            LengthCm = 900,
            ThicknessCm = 12,
            RotationDegrees = 90,
            MaterialId = "concrete"
        });
        var wallLoss = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz5);

        Assert.InRange(partialLoss, 1.0, 8.0);
        Assert.True(wallLoss > partialLoss);
    }

    [Fact]
    public void CoChannelInterference_Reduces_SampleRssi()
    {
        var project = ProjectFactory.CreateNewProject();
        project.SimulationSettings.InterferencePenaltyDb = 0;
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Id = "ap1", Position = new PlanPoint(300, 300), Channel = 36, Band = FrequencyBand.Ghz5, TxPowerDbm = 18 });
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Id = "ap2", Position = new PlanPoint(420, 300), Channel = 36, Band = FrequencyBand.Ghz5, TxPowerDbm = 18 });
        var engine = new RfSimulationEngine();

        var coChannel = engine.EvaluateSample(project, project.SimulationSettings, new PlanPoint(320, 300));
        project.FloorPlan.AccessPoints[1].Channel = 149;
        var separated = engine.EvaluateSample(project, project.SimulationSettings, new PlanPoint(320, 300));

        Assert.True(coChannel.InterferenceDb > separated.InterferenceDb);
        Assert.True(separated.RssiDbm > coChannel.RssiDbm);
    }

    [Fact]
    public void HeatmapCache_Invalidates_RegionTiles()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.WidthCm = 500;
        project.FloorPlan.HeightCm = 500;
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Position = new PlanPoint(250, 250) });
        project.SimulationSettings.SampleResolutionCm = 250;
        var result = new RfSimulationEngine().Evaluate(project, project.SimulationSettings);
        var cache = new HeatmapCache();

        cache.Store(project, result);
        Assert.True(cache.TryGet(project, out _));

        cache.InvalidateRegion(new PlanRect(0, 0, 500, 500));

        Assert.False(cache.TryGet(project, out _));
    }

    [Fact]
    public async Task Exporters_Write_CsvSvgPdfAndPng()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.WidthCm = 500;
        project.FloorPlan.HeightCm = 400;
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Position = new PlanPoint(250, 200) });
        project.SimulationSettings.SampleResolutionCm = 250;
        var result = new RfSimulationEngine().Evaluate(project, project.SimulationSettings);
        var directory = Path.Combine(Path.GetTempPath(), "wifi-studio-tests", Guid.NewGuid().ToString("N"));

        var csv = Path.Combine(directory, "analysis.csv");
        var svg = Path.Combine(directory, "plan.svg");
        var pdf = Path.Combine(directory, "report.pdf");
        var png = Path.Combine(directory, "heatmap.png");
        await ProjectExportService.ExportCsvAsync(result, csv);
        await ProjectExportService.ExportSvgAsync(project, svg);
        await ProjectExportService.ExportPdfReportAsync(project, result, pdf);
        await HeatmapPngExporter.ExportAsync(result, project.FloorPlan.WidthCm, project.FloorPlan.HeightCm, png, widthPx: 64);

        Assert.StartsWith("x_cm,y_cm", await File.ReadAllTextAsync(csv));
        Assert.Contains("<svg", await File.ReadAllTextAsync(svg));
        Assert.True(new FileInfo(pdf).Length > 100);
        Assert.True(new FileInfo(png).Length > 40);
    }

    [Fact]
    public void ProjectSerializer_RoundTrips_MultiFloorState()
    {
        var project = ProjectFactory.CreateNewProject();
        project.Floors =
        [
            project.FloorPlan,
            new FloorPlan { Name = "Second Floor", WidthCm = 1200, HeightCm = 900 }
        ];
        project.ActiveFloorIndex = 1;

        var roundTrip = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(project));

        Assert.Equal("Second Floor", roundTrip.FloorPlan.Name);
        Assert.Equal(2, roundTrip.Floors.Count);
        Assert.Equal(1, roundTrip.ActiveFloorIndex);
    }
}
