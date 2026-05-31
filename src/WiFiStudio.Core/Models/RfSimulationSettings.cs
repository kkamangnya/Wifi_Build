namespace WiFiStudio.Core.Models;

public sealed class RfSimulationSettings
{
    public FrequencyBand FrequencyBand { get; set; } = FrequencyBand.Ghz5;
    public HeatmapType HeatmapType { get; set; } = HeatmapType.Rssi;
    public double SampleResolutionCm { get; set; } = 50;
    public double CoverageThresholdDbm { get; set; } = -67;
    public double ShadowThresholdDbm { get; set; } = -82;
    public double NoiseFloorDbm { get; set; } = -92;
    public double InterferencePenaltyDb { get; set; }
}
