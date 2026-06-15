using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

public readonly record struct BgraColor(byte B, byte G, byte R, byte A);

public readonly record struct RssiLegendBand(string Label, double MinDbm, double MaxDbm, BgraColor Color);

public static class HeatmapColorScale
{
    public const double FixedMinRssiDbm = -90;
    public const double FixedMaxRssiDbm = -30;

    public static IReadOnlyList<RssiLegendBand> RssiLegendBands { get; } =
    [
        new("Green >= -50 dBm", -50, FixedMaxRssiDbm, new BgraColor(48, 151, 33, 212)),
        new("Light Green -50 to -67 dBm", -67, -50, new BgraColor(86, 217, 126, 204)),
        new("Yellow -67 to -75 dBm", -75, -67, new BgraColor(72, 216, 255, 204)),
        new("Orange -75 to -85 dBm", -85, -75, new BgraColor(26, 140, 255, 210)),
        new("Red <= -85 dBm", FixedMinRssiDbm, -85, new BgraColor(49, 49, 224, 216))
    ];

    public static BgraColor ForRssi(double rssiDbm)
    {
        if (rssiDbm >= -50)
        {
            return RssiLegendBands[0].Color;
        }

        if (rssiDbm >= -67)
        {
            return RssiLegendBands[1].Color;
        }

        if (rssiDbm >= -75)
        {
            return RssiLegendBands[2].Color;
        }

        if (rssiDbm >= -85)
        {
            return RssiLegendBands[3].Color;
        }

        return RssiLegendBands[4].Color;
    }

    public static BgraColor ForSample(RfSamplePoint sample, HeatmapType mode)
    {
        return mode switch
        {
            HeatmapType.Snr => ForSnr(sample.SnrDb),
            HeatmapType.Interference => ForInterference(sample.InterferenceDb),
            HeatmapType.BestAp => ForBestAp(sample.ServingApId),
            HeatmapType.DeadZone => sample.RssiDbm <= -85 ? new BgraColor(40, 40, 220, 190) : new BgraColor(40, 180, 40, 44),
            HeatmapType.UserQuality => ForRssi(sample.RssiDbm),
            _ => ForRssi(sample.RssiDbm)
        };
    }

    private static BgraColor ForSnr(double snrDb)
    {
        if (snrDb >= 35) return new BgraColor(70, 200, 40, 176);
        if (snrDb <= 10) return new BgraColor(60, 60, 230, 176);
        var t = Math.Clamp((snrDb - 10.0) / 25.0, 0.0, 1.0);
        return Lerp(new BgraColor(60, 60, 230, 176), new BgraColor(70, 200, 40, 176), t);
    }

    private static BgraColor ForInterference(double interferenceDb)
    {
        var t = Math.Clamp(interferenceDb / 15.0, 0.0, 1.0);
        return Lerp(new BgraColor(80, 180, 40, 90), new BgraColor(30, 30, 230, 190), t);
    }

    private static BgraColor ForBestAp(string? apId)
    {
        if (string.IsNullOrWhiteSpace(apId))
        {
            return new BgraColor(60, 60, 60, 120);
        }

        var hash = Math.Abs(apId.GetHashCode());
        return new BgraColor(
            (byte)(80 + hash % 140),
            (byte)(80 + hash / 7 % 140),
            (byte)(80 + hash / 13 % 140),
            150);
    }

    private static BgraColor Lerp(BgraColor a, BgraColor b, double t)
    {
        return new BgraColor(
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}
