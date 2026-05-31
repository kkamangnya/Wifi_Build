namespace WiFiStudio.Core.Models;

public sealed class AccessPointRecommendation
{
    public string? AssignedAccessPointId { get; set; }
    public string? AssignedAccessPointName { get; set; }
    public PlanPoint Position { get; set; } = PlanPoint.Zero;
    public double Score { get; set; }
    public double ExpectedCoverageRatio { get; set; }
    public double ExpectedAverageRssiDbm { get; set; }
    public int RecommendedApCount { get; set; } = 1;
    public double RecommendedTxPowerDbm { get; set; } = 18;
    public int RecommendedChannel { get; set; } = 36;
    public List<string> Reasons { get; set; } = [];
    public bool Accepted { get; set; }
    public List<UserOptimizationDelta> UserDeltas { get; set; } = [];
}

public sealed class OptimizationResult
{
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public OptimizationMode Mode { get; set; } = OptimizationMode.Balanced;
    public double Score { get; set; }
    public List<AccessPointRecommendation> Recommendations { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

public sealed class UserOptimizationDelta
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public double BeforeRssiDbm { get; set; }
    public double AfterRssiDbm { get; set; }
    public double ImprovementDb => AfterRssiDbm - BeforeRssiDbm;
    public string? ConnectedApName { get; set; }
    public LinkQuality BeforeQuality { get; set; }
    public LinkQuality AfterQuality { get; set; }
    public bool DeadZoneResolved => BeforeQuality == LinkQuality.DeadZone && AfterQuality != LinkQuality.DeadZone;
    public string Reason { get; set; } = "";
}
