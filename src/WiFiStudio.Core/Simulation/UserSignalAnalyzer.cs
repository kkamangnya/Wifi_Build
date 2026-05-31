using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Simulation;

public sealed class UserSignalAnalyzer
{
    private readonly RfSimulationEngine _engine = new();

    public UserSignalAnalysis Analyze(ProjectModel project, UserLocation user)
    {
        var sample = _engine.EvaluateSample(project, project.SimulationSettings, user.Position);
        var ap = project.FloorPlan.AccessPoints.FirstOrDefault(a => a.Id == sample.ServingApId);
        var quality = Classify(sample.RssiDbm);
        return new UserSignalAnalysis
        {
            UserId = user.Id,
            UserName = user.Name,
            X = user.Position.X,
            Y = user.Position.Y,
            ConnectedApId = ap?.Id,
            ConnectedApName = ap?.Name ?? "None",
            RssiDbm = sample.RssiDbm,
            SnrDb = sample.SnrDb,
            Frequency = FormatBand(ap?.Band ?? project.SimulationSettings.FrequencyBand),
            Quality = quality,
            Recommendation = RecommendAction(sample.RssiDbm, sample.SnrDb, ap)
        };
    }

    public static LinkQuality Classify(double rssiDbm)
    {
        if (rssiDbm >= -50)
        {
            return LinkQuality.Excellent;
        }

        if (rssiDbm >= -67)
        {
            return LinkQuality.Good;
        }

        if (rssiDbm >= -75)
        {
            return LinkQuality.Fair;
        }

        if (rssiDbm >= -85)
        {
            return LinkQuality.Poor;
        }

        return LinkQuality.DeadZone;
    }

    public static string RecommendAction(double rssiDbm, double snrDb, AccessPoint? ap)
    {
        if (rssiDbm <= -85)
        {
            return "Add AP or move AP closer; check major obstacles.";
        }

        if (rssiDbm <= -75)
        {
            return "Increase Tx power, move AP, or reduce obstacle loss.";
        }

        if (snrDb < 20)
        {
            return "Change channel or reduce interference.";
        }

        if (ap is not null && ap.TxPowerDbm < 16)
        {
            return "Tx power can be increased if coverage remains weak.";
        }

        return "No immediate action required.";
    }

    private static string FormatBand(FrequencyBand band) => band switch
    {
        FrequencyBand.Ghz24 => "2.4 GHz",
        FrequencyBand.Ghz6 => "6 GHz",
        _ => "5 GHz"
    };
}
