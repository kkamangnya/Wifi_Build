namespace WiFiStudio.Core.Models;

public sealed class UserSignalAnalysis
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string? ConnectedApId { get; set; }
    public string ConnectedApName { get; set; } = "None";
    public double RssiDbm { get; set; } = -110;
    public double SnrDb { get; set; }
    public string Frequency { get; set; } = "5 GHz";
    public LinkQuality Quality { get; set; } = LinkQuality.DeadZone;
    public string Recommendation { get; set; } = "";
}
