using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class RfSimulationEngineTests
{
    [Fact]
    public void Evaluate_Produces_Stats_And_ServingAp()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.WidthCm = 500;
        project.FloorPlan.HeightCm = 500;
        project.FloorPlan.AccessPoints.Add(new AccessPoint
        {
            Id = "ap-1",
            Position = new PlanPoint(250, 250),
            TxPowerDbm = 18
        });
        project.SimulationSettings.SampleResolutionCm = 250;

        var result = new RfSimulationEngine().Evaluate(project, project.SimulationSettings);

        Assert.Equal(4, result.Samples.Count);
        Assert.Equal("ap-1", result.Samples[0].ServingApId);
        Assert.True(result.Stats.AverageRssiDbm > -80);
        Assert.InRange(result.Stats.CoverageRatio, 0.0, 1.0);
    }
}
