using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Simulation;

public static class ChannelInterferenceAnalyzer
{
    public static double InterferencePenaltyDb(ProjectModel project, AccessPoint servingAp, PlanPoint sample)
    {
        var total = 0.0;
        foreach (var otherAp in project.FloorPlan.AccessPoints.Where(ap =>
                     ap.Id != servingAp.Id
                     && ap.IsEnabled
                     && ap.IsVisible
                     && ap.Band == servingAp.Band))
        {
            var channelWeight = ChannelOverlapWeight(servingAp, otherAp);
            if (channelWeight <= 0)
            {
                continue;
            }

            var distanceMeters = Math.Max(
                RfCalculator.MinimumStableDistanceMeters,
                GeometryMath.DistanceMeters(otherAp.Position, sample));
            var materialLoss = RfCalculator.MaterialLossDb(otherAp.Position, sample, project, otherAp.Band);
            var interferingRssi = RfCalculator.RssiDbm(
                otherAp.TxPowerDbm + otherAp.AntennaGainDbi,
                distanceMeters,
                otherAp.Band,
                materialLoss,
                0);

            if (interferingRssi <= -92)
            {
                continue;
            }

            var strengthWeight = Math.Clamp((interferingRssi + 92.0) / 30.0, 0.0, 1.0);
            total += channelWeight * strengthWeight;
        }

        return Math.Clamp(total, 0.0, 14.0);
    }

    private static double ChannelOverlapWeight(AccessPoint first, AccessPoint second)
    {
        var distance = Math.Abs(first.Channel - second.Channel);
        if (first.Band == FrequencyBand.Ghz24)
        {
            return distance switch
            {
                0 => 6.0,
                <= 2 => 4.0,
                <= 4 => 2.0,
                _ => 0.0
            };
        }

        if (distance == 0)
        {
            return 5.0;
        }

        var overlapSpan = Math.Max(first.BandwidthMhz, second.BandwidthMhz) / 20;
        return distance <= overlapSpan ? 2.0 : 0.0;
    }
}
