using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Rendering.Heatmaps;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class HeatmapRasterizerTests
{
    [Fact]
    public void Rasterize_Produces_NonTransparentPixels()
    {
        var project = ProjectFactory.CreateSampleOffice();
        project.SimulationSettings.SampleResolutionCm = 400;
        var result = new RfSimulationEngine().Evaluate(project, project.SimulationSettings);

        var raster = HeatmapRasterizer.Rasterize(result, project.FloorPlan.WidthCm, project.FloorPlan.HeightCm, 100, 64);

        Assert.Equal(100 * 64 * 4, raster.BgraPixels.Length);
        Assert.Contains(raster.BgraPixels.Where((_, index) => index % 4 == 3), alpha => alpha > 0);
    }
}
