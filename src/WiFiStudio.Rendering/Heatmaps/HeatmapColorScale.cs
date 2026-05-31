using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

public readonly record struct BgraColor(byte B, byte G, byte R, byte A);

public static class HeatmapColorScale
{
    public static BgraColor ForRssi(double rssiDbm)
    {
        if (rssiDbm >= -55)
        {
            return new BgraColor(63, 201, 39, 176);
        }

        if (rssiDbm <= -90)
        {
            return new BgraColor(60, 76, 231, 168);
        }

        var t = Math.Clamp((rssiDbm + 90.0) / 35.0, 0.0, 1.0);
        var weak = new BgraColor(60, 76, 231, 176);
        var mid = new BgraColor(76, 201, 242, 168);
        var strong = new BgraColor(63, 201, 39, 176);

        var a = t < 0.5 ? weak : mid;
        var b = t < 0.5 ? mid : strong;
        var local = t < 0.5 ? t * 2.0 : (t - 0.5) * 2.0;
        return Lerp(a, b, local);
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
