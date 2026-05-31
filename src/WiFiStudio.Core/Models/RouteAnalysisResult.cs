namespace WiFiStudio.Core.Models;

public sealed class RouteSample
{
    public double X { get; set; }
    public double Y { get; set; }
    public double RssiDbm { get; set; }
    public string? ServingApId { get; set; }
    public LinkQuality Quality { get; set; }
}

public sealed class RouteAnalysisResult
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public List<RouteSample> Samples { get; set; } = [];
    public double AverageRssiDbm { get; set; }
    public double MinimumRssiDbm { get; set; }
    public double MaximumRssiDbm { get; set; }
    public int DeadZoneSampleCount { get; set; }
    public int HandoverCount { get; set; }
    public double WorstX { get; set; }
    public double WorstY { get; set; }
    public string Recommendation { get; set; } = "";
}
