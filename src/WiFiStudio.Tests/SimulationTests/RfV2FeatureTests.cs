using WiFiStudio.Core.Models;
using WiFiStudio.Core.Optimization;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Core.Geometry;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class RfV2FeatureTests
{
    [Fact]
    public void WallMaterialLoss_Uses_FrequencyMultiplier()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.Walls.Add(new WallElement
        {
            Center = new PlanPoint(500, 500),
            LengthCm = 900,
            ThicknessCm = 10,
            RotationDegrees = 90,
            MaterialId = "metal"
        });

        var loss24 = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz24);
        var loss6 = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz6);

        Assert.True(loss6 > loss24);
    }

    [Fact]
    public void ObjectLoss_Uses_ObjectAttenuation()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.Objects.Add(PlanObject.CreateDefault(PlanObjectType.ServerRack, new PlanPoint(500, 500)));

        var loss = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz5);

        Assert.True(loss >= 12);
    }

    [Fact]
    public void UserSignalAnalysis_Returns_Rssi_Snr_And_BestAp()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Id = "near", Name = "Near AP", Position = new PlanPoint(100, 100), TxPowerDbm = 18 });
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Id = "far", Name = "Far AP", Position = new PlanPoint(2000, 1400), TxPowerDbm = 18 });
        var user = new UserLocation { Name = "User A", Position = new PlanPoint(160, 120), Weight = 3 };
        project.FloorPlan.Users.Add(user);

        var analysis = new UserSignalAnalyzer().Analyze(project, user);

        Assert.Equal("Near AP", analysis.ConnectedApName);
        Assert.True(analysis.RssiDbm > -67);
        Assert.True(analysis.SnrDb > 0);
    }

    [Fact]
    public void Simulation_Changes_After_ApMove()
    {
        var project = ProjectFactory.CreateNewProject();
        var ap = new AccessPoint { Position = new PlanPoint(100, 100), TxPowerDbm = 18 };
        project.FloorPlan.AccessPoints.Add(ap);
        var engine = new RfSimulationEngine();
        var before = engine.EvaluateSample(project, project.SimulationSettings, new PlanPoint(120, 120)).RssiDbm;

        ap.Position = new PlanPoint(2000, 1400);
        var after = engine.EvaluateSample(project, project.SimulationSettings, new PlanPoint(120, 120)).RssiDbm;

        Assert.True(before > after);
    }

    [Fact]
    public void UserFocusedOptimization_Reports_BeforeAfterDeltas()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Name = "Existing AP", Position = new PlanPoint(100, 100), TxPowerDbm = 12 });
        project.FloorPlan.Users.Add(new UserLocation { Name = "Dead Zone User", Position = new PlanPoint(2200, 1400), Weight = 5 });
        project.SimulationSettings.SampleResolutionCm = 250;

        var result = new ApPlacementOptimizer().Recommend(project, maxResults: 1, mode: OptimizationMode.UserQuality);

        var recommendation = Assert.Single(result.Recommendations);
        Assert.NotEmpty(recommendation.UserDeltas);
        Assert.True(recommendation.UserDeltas[0].AfterRssiDbm >= recommendation.UserDeltas[0].BeforeRssiDbm);
    }

    [Fact]
    public void MultiApLayout_Recommends_Spaced_OptimalPositions()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.WidthCm = 2000;
        project.FloorPlan.HeightCm = 1000;
        project.SimulationSettings.SampleResolutionCm = 500;
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Name = "AP-01", Position = new PlanPoint(100, 100), TxPowerDbm = 18 });
        project.FloorPlan.AccessPoints.Add(new AccessPoint { Name = "AP-02", Position = new PlanPoint(150, 100), TxPowerDbm = 18 });
        project.FloorPlan.Users.Add(new UserLocation { Name = "Left user", Position = new PlanPoint(250, 500), Weight = 3 });
        project.FloorPlan.Users.Add(new UserLocation { Name = "Right user", Position = new PlanPoint(1750, 500), Weight = 3 });

        var result = new ApPlacementOptimizer().RecommendLayout(project, accessPointCount: 2, mode: OptimizationMode.UserQuality);

        Assert.Equal(2, result.Recommendations.Count);
        Assert.True(GeometryMath.DistanceCm(result.Recommendations[0].Position, result.Recommendations[1].Position) >= 450);
        Assert.All(result.Recommendations, recommendation =>
            Assert.True(GeometryMath.PointInsideFloor(recommendation.Position, project.FloorPlan)));
    }
}
