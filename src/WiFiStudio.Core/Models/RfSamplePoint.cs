namespace WiFiStudio.Core.Models;

public sealed class RfSamplePoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double RssiDbm { get; set; } = -110;
    public double SnrDb { get; set; }
    public double InterferenceDb { get; set; }
    public string? ServingApId { get; set; }
}
