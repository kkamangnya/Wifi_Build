namespace WiFiStudio.Core.Models;

public sealed class CoverageStats
{
    public double AverageRssiDbm { get; set; } = -110;
    public double MinimumRssiDbm { get; set; } = -110;
    public double CoverageRatio { get; set; }
    public double ShadowRatio { get; set; } = 1;
    public int SampleCount { get; set; }
    public string RecommendedBand { get; set; } = "5 GHz";
}

public sealed class HeatmapResult
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int Columns { get; set; }
    public int Rows { get; set; }
    public double CellSizeCm { get; set; }
    public RfSimulationSettings Settings { get; set; } = new();
    public List<RfSamplePoint> Samples { get; set; } = [];
    public CoverageStats Stats { get; set; } = new();
}
