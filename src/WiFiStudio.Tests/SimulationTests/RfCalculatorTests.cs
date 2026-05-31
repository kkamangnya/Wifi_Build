using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;

namespace WiFiStudio.Tests.SimulationTests;

public sealed class RfCalculatorTests
{
    [Fact]
    public void Fspl_Clamps_NearZeroDistance_ToStableOneMeter()
    {
        var nearZero = RfCalculator.FsplDb(0.001, FrequencyBand.Ghz24);
        var oneMeter = RfCalculator.FsplDb(1.0, FrequencyBand.Ghz24);

        Assert.Equal(oneMeter, nearZero, precision: 6);
        Assert.InRange(oneMeter, 39.5, 40.8);
    }

    [Fact]
    public void MaterialLoss_Increases_When_LinkCrossesConcreteWall()
    {
        var project = ProjectFactory.CreateNewProject();
        project.FloorPlan.Walls.Add(new WallElement
        {
            Center = new PlanPoint(500, 500),
            LengthCm = 1000,
            ThicknessCm = 20,
            RotationDegrees = 90,
            MaterialId = "concrete"
        });

        var loss = RfCalculator.MaterialLossDb(new PlanPoint(100, 500), new PlanPoint(900, 500), project, FrequencyBand.Ghz5);

        Assert.True(loss >= 15.0);
    }

    [Fact]
    public void Rssi_Uses_TxPower_Fspl_MaterialLoss_AndInterferencePenalty()
    {
        var rssi = RfCalculator.RssiDbm(18, 10, FrequencyBand.Ghz5, materialLossDb: 6, interferencePenaltyDb: 3);
        var expected = 18 - RfCalculator.FsplDb(10, FrequencyBand.Ghz5) - 6 - 3;

        Assert.Equal(expected, rssi, precision: 6);
    }
}
