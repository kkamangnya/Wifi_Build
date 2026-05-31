using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Simulation;

namespace WiFiStudio.Core.Optimization;

public sealed class ApPlacementOptimizer
{
    private const double MinApDistanceCm = 450.0;
    private readonly RfSimulationEngine _engine = new();

    public Task<OptimizationResult> RecommendAsync(
        ProjectModel project,
        int desiredNewAccessPointCount,
        int maxResults,
        CancellationToken cancellationToken,
        OptimizationMode mode = OptimizationMode.Balanced)
    {
        return Task.Run(() => Recommend(project, desiredNewAccessPointCount, maxResults, cancellationToken, mode), cancellationToken);
    }

    public Task<OptimizationResult> RecommendLayoutAsync(
        ProjectModel project,
        int accessPointCount,
        CancellationToken cancellationToken,
        OptimizationMode mode = OptimizationMode.Balanced)
    {
        return Task.Run(() => RecommendLayout(project, accessPointCount, cancellationToken, mode), cancellationToken);
    }

    public OptimizationResult Recommend(
        ProjectModel project,
        int desiredNewAccessPointCount = 1,
        int maxResults = 3,
        CancellationToken cancellationToken = default,
        OptimizationMode mode = OptimizationMode.Balanced)
    {
        desiredNewAccessPointCount = Math.Max(1, desiredNewAccessPointCount);
        maxResults = Math.Max(1, maxResults);

        var settings = project.SimulationSettings;
        var candidateStep = CandidateStepCm(project);
        var minApDistanceCm = 350.0;
        var recommendations = new List<AccessPointRecommendation>();

        for (var y = candidateStep / 2.0; y < project.FloorPlan.HeightCm; y += candidateStep)
        {
            for (var x = candidateStep / 2.0; x < project.FloorPlan.WidthCm; x += candidateStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var point = GeometryMath.Snap(new PlanPoint(x, y), project.FloorPlan.GridSizeCm);
                if (!IsCandidateAllowed(project, point, minApDistanceCm))
                {
                    continue;
                }

                var recommendedApCount = RecommendApCount(project, desiredNewAccessPointCount, mode);
                var recommendedTxPower = RecommendTxPower(project, point, mode);
                var recommendedChannel = RecommendChannel(project);
                var candidateProject = CloneForCandidate(project, point, desiredNewAccessPointCount, recommendedTxPower, recommendedChannel);
                var heatmap = _engine.Evaluate(candidateProject, settings, cancellationToken: cancellationToken);
                var deltas = BuildUserDeltas(project, candidateProject);
                var score = ScoreCandidate(project, point, heatmap, deltas, mode);
                recommendations.Add(new AccessPointRecommendation
                {
                    Position = point,
                    Score = score,
                    ExpectedCoverageRatio = heatmap.Stats.CoverageRatio,
                    ExpectedAverageRssiDbm = heatmap.Stats.AverageRssiDbm,
                    RecommendedApCount = recommendedApCount,
                    RecommendedTxPowerDbm = recommendedTxPower,
                    RecommendedChannel = recommendedChannel,
                    UserDeltas = deltas,
                    Reasons =
                    [
                        $"Expected coverage {heatmap.Stats.CoverageRatio:P0}",
                        $"Average RSSI {heatmap.Stats.AverageRssiDbm:F1} dBm",
                        $"At least {minApDistanceCm / 100.0:F1} m from existing APs",
                        mode == OptimizationMode.UserQuality ? "Weighted toward user RSSI and dead zone recovery" : "Balanced area and user quality score"
                    ]
                });
            }
        }

        var ordered = recommendations
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.ExpectedCoverageRatio)
            .Take(maxResults)
            .ToList();

        return new OptimizationResult
        {
            Recommendations = ordered,
            Mode = mode,
            Score = ordered.FirstOrDefault()?.Score ?? 0,
            Notes = ordered.Count == 0
                ? ["No valid placement candidates were found. Check obstacle placement and AP spacing constraints."]
                : ["First-pass recommendations use grid candidate search. This can be extended with continuous optimization later."]
        };
    }

    public OptimizationResult RecommendLayout(
        ProjectModel project,
        int accessPointCount,
        CancellationToken cancellationToken = default,
        OptimizationMode mode = OptimizationMode.Balanced)
    {
        accessPointCount = Math.Clamp(accessPointCount, 1, 12);
        var candidates = GenerateCandidatePoints(project).ToList();
        var selected = new List<AccessPointRecommendation>();
        var finalHeatmap = new HeatmapResult();

        for (var index = 0; index < accessPointCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccessPointRecommendation? best = null;
            HeatmapResult? bestHeatmap = null;

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selected.Any(existing => GeometryMath.DistanceCm(existing.Position, candidate) < MinApDistanceCm))
                {
                    continue;
                }

                var channel = RecommendChannel(project, index);
                var txPower = RecommendTxPower(project, candidate, mode);
                var trialRecommendations = selected
                    .Append(CreateLayoutRecommendation(project, candidate, index, accessPointCount, txPower, channel))
                    .ToList();
                var trialProject = CloneWithRecommendedLayout(project, trialRecommendations);
                var heatmap = _engine.Evaluate(trialProject, project.SimulationSettings, cancellationToken: cancellationToken);
                var deltas = BuildUserDeltas(project, trialProject);
                var score = ScoreCandidate(project, candidate, heatmap, deltas, mode) + LayoutSpacingBonus(candidate, selected);

                if (best is null || score > best.Score)
                {
                    best = CreateLayoutRecommendation(project, candidate, index, accessPointCount, txPower, channel);
                    best.Score = score;
                    best.ExpectedCoverageRatio = heatmap.Stats.CoverageRatio;
                    best.ExpectedAverageRssiDbm = heatmap.Stats.AverageRssiDbm;
                    best.UserDeltas = deltas;
                    bestHeatmap = heatmap;
                }
            }

            if (best is null)
            {
                break;
            }

            selected.Add(best);
            if (bestHeatmap is not null)
            {
                finalHeatmap = bestHeatmap;
            }
        }

        if (selected.Count > 0)
        {
            var finalProject = CloneWithRecommendedLayout(project, selected);
            finalHeatmap = _engine.Evaluate(finalProject, project.SimulationSettings, cancellationToken: cancellationToken);
            var finalDeltas = BuildUserDeltas(project, finalProject);
            foreach (var recommendation in selected)
            {
                recommendation.ExpectedCoverageRatio = finalHeatmap.Stats.CoverageRatio;
                recommendation.ExpectedAverageRssiDbm = finalHeatmap.Stats.AverageRssiDbm;
                recommendation.UserDeltas = finalDeltas;
            }
        }

        return new OptimizationResult
        {
            Recommendations = selected,
            Mode = mode,
            Score = selected.Count == 0 ? 0 : selected.Average(r => r.Score),
            Notes = selected.Count == 0
                ? ["No valid AP layout candidates were found. Check obstacle placement and spacing constraints."]
                :
                [
                    $"Optimized {selected.Count} AP position(s) with at least {MinApDistanceCm / 100.0:F1} m spacing.",
                    $"Expected coverage {finalHeatmap.Stats.CoverageRatio:P0}, average RSSI {finalHeatmap.Stats.AverageRssiDbm:F1} dBm."
                ]
        };
    }

    private static bool IsCandidateAllowed(ProjectModel project, PlanPoint point, double minApDistanceCm)
    {
        return GeometryMath.PointInsideFloor(point, project.FloorPlan)
            && !GeometryMath.PointInsideAnyObstacle(point, project)
            && project.FloorPlan.AccessPoints.All(ap => GeometryMath.DistanceCm(ap.Position, point) >= minApDistanceCm);
    }

    private static bool IsLayoutCandidateAllowed(ProjectModel project, PlanPoint point)
    {
        return GeometryMath.PointInsideFloor(point, project.FloorPlan)
            && !GeometryMath.PointInsideAnyObstacle(point, project);
    }

    private static double ScoreCandidate(ProjectModel project, PlanPoint point, HeatmapResult heatmap, IReadOnlyList<UserOptimizationDelta> deltas, OptimizationMode mode)
    {
        var averageNormalized = Math.Clamp((heatmap.Stats.AverageRssiDbm + 90.0) / 35.0, 0.0, 1.0);
        var userBonus = project.FloorPlan.Users.Sum(user =>
        {
            var distanceCm = GeometryMath.DistanceCm(user.Position, point);
            return Math.Clamp(1.0 - distanceCm / 1600.0, 0.0, 1.0) * user.Weight;
        });

        var userImprovement = deltas.Sum(d =>
        {
            var weight = project.FloorPlan.Users.FirstOrDefault(u => u.Id == d.UserId)?.Weight ?? 1.0;
            return Math.Max(0, d.ImprovementDb) * Math.Max(1.0, weight);
        });
        var deadZoneResolved = deltas.Count(d => d.DeadZoneResolved) * 15.0;
        var areaWeight = mode == OptimizationMode.UserQuality ? 35.0 : 70.0;
        var userWeight = mode == OptimizationMode.UserQuality ? 2.5 : 0.8;

        return heatmap.Stats.CoverageRatio * areaWeight
            + averageNormalized * 20.0
            + (1.0 - heatmap.Stats.ShadowRatio) * 10.0
            + Math.Min(18.0, userBonus * 2.0)
            + Math.Min(45.0, userImprovement * userWeight)
            + deadZoneResolved;
    }

    private static int RecommendApCount(ProjectModel project, int requestedCount, OptimizationMode mode)
    {
        var areaSquareMeters = project.FloorPlan.WidthCm / 100.0 * project.FloorPlan.HeightCm / 100.0;
        var areaCount = (int)Math.Ceiling(areaSquareMeters / 180.0);
        var userCount = project.FloorPlan.Users.Count == 0 ? 1 : (int)Math.Ceiling(project.FloorPlan.Users.Sum(u => Math.Max(1, u.Weight)) / 8.0);
        var estimate = mode == OptimizationMode.UserQuality ? Math.Max(areaCount, userCount) : areaCount;
        return Math.Clamp(Math.Max(requestedCount, estimate), 1, 12);
    }

    private static double RecommendTxPower(ProjectModel project, PlanPoint point, OptimizationMode mode)
    {
        if (mode == OptimizationMode.MinimizeApCount)
        {
            return 23;
        }

        var nearestUser = project.FloorPlan.Users.Count == 0
            ? 0
            : project.FloorPlan.Users.Min(user => GeometryMath.DistanceCm(user.Position, point));
        return nearestUser > 1000 ? 21 : 18;
    }

    private static int RecommendChannel(ProjectModel project)
    {
        return RecommendChannel(project, 0);
    }

    private static int RecommendChannel(ProjectModel project, int index)
    {
        if (project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24)
        {
            var channels = new[] { 1, 6, 11 };
            var ordered = channels
                .OrderBy(channel => project.FloorPlan.AccessPoints.Count(ap => ap.Band == FrequencyBand.Ghz24 && ap.Channel == channel))
                .ThenBy(channel => channel)
                .ToArray();
            return ordered[index % ordered.Length];
        }

        var candidates = project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz6
            ? new[] { 5, 21, 37, 53, 69, 85 }
            : new[] { 36, 44, 149, 157 };
        var ordered5Ghz = candidates
            .OrderBy(channel => project.FloorPlan.AccessPoints.Count(ap => ap.Band == project.SimulationSettings.FrequencyBand && Math.Abs(ap.Channel - channel) <= 4))
            .ThenBy(channel => channel)
            .ToArray();
        return ordered5Ghz[index % ordered5Ghz.Length];
    }

    private static IEnumerable<PlanPoint> GenerateCandidatePoints(ProjectModel project)
    {
        var candidateStep = CandidateStepCm(project);
        for (var y = candidateStep / 2.0; y < project.FloorPlan.HeightCm; y += candidateStep)
        {
            for (var x = candidateStep / 2.0; x < project.FloorPlan.WidthCm; x += candidateStep)
            {
                var point = GeometryMath.Snap(new PlanPoint(x, y), project.FloorPlan.GridSizeCm);
                if (IsLayoutCandidateAllowed(project, point))
                {
                    yield return point;
                }
            }
        }
    }

    private static double LayoutSpacingBonus(PlanPoint candidate, IReadOnlyList<AccessPointRecommendation> selected)
    {
        if (selected.Count == 0)
        {
            return 0;
        }

        var nearest = selected.Min(existing => GeometryMath.DistanceCm(existing.Position, candidate));
        return Math.Clamp(nearest / 100.0, 0.0, 12.0);
    }

    private static double CandidateStepCm(ProjectModel project)
    {
        var requested = Math.Max(250, project.SimulationSettings.SampleResolutionCm * 4.0);
        var floorLimited = Math.Max(125, Math.Min(project.FloorPlan.WidthCm, project.FloorPlan.HeightCm) / 2.0);
        return Math.Min(requested, floorLimited);
    }

    private static AccessPointRecommendation CreateLayoutRecommendation(
        ProjectModel project,
        PlanPoint point,
        int index,
        int apCount,
        double txPower,
        int channel)
    {
        return new AccessPointRecommendation
        {
            Position = point,
            RecommendedApCount = apCount,
            RecommendedTxPowerDbm = txPower,
            RecommendedChannel = channel,
            Reasons =
            [
                $"Maintains at least {MinApDistanceCm / 100.0:F1} m spacing from other recommended APs",
                project.FloorPlan.Users.Count > 0 ? "Weighted toward user positions and dead-zone recovery" : "Weighted toward whole-area coverage",
                $"Recommended channel {channel}"
            ]
        };
    }

    private static ProjectModel CloneWithRecommendedLayout(ProjectModel project, IReadOnlyList<AccessPointRecommendation> recommendations)
    {
        var clone = CloneForCandidateShell(project);
        clone.FloorPlan.AccessPoints.Clear();
        for (var index = 0; index < recommendations.Count; index++)
        {
            var recommendation = recommendations[index];
            clone.FloorPlan.AccessPoints.Add(new AccessPoint
            {
                Id = $"layout-{index}",
                Name = $"Recommended AP {index + 1}",
                Position = recommendation.Position,
                Band = project.SimulationSettings.FrequencyBand,
                TxPowerDbm = recommendation.RecommendedTxPowerDbm,
                Channel = recommendation.RecommendedChannel,
                BandwidthMhz = project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 20 : 40,
                AntennaGainDbi = project.FloorPlan.AccessPoints.ElementAtOrDefault(index)?.AntennaGainDbi ?? 3,
                CoverageTargetDbm = project.SimulationSettings.CoverageThresholdDbm
            });
        }

        return clone;
    }

    private static ProjectModel CloneForCandidate(ProjectModel project, PlanPoint point, int count, double txPowerDbm, int channel)
    {
        var clone = CloneForCandidateShell(project);

        for (var i = 0; i < count; i++)
        {
            clone.FloorPlan.AccessPoints.Add(new AccessPoint
            {
                Id = $"candidate-{i}",
                Name = $"Recommended AP {i + 1}",
                Position = count == 1 ? point : new PlanPoint(point.X + i * 400, point.Y),
                Band = project.SimulationSettings.FrequencyBand,
                TxPowerDbm = txPowerDbm,
                Channel = channel,
                BandwidthMhz = project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 20 : 40
            });
        }

        return clone;
    }

    private static ProjectModel CloneForCandidateShell(ProjectModel project)
    {
        return new ProjectModel
        {
            Id = project.Id,
            Name = project.Name,
            CreatedAtUtc = project.CreatedAtUtc,
            ModifiedAtUtc = project.ModifiedAtUtc,
            Materials = project.Materials,
            SimulationSettings = project.SimulationSettings,
            FloorPlan = new FloorPlan
            {
                Name = project.FloorPlan.Name,
                WidthCm = project.FloorPlan.WidthCm,
                HeightCm = project.FloorPlan.HeightCm,
                GridSizeCm = project.FloorPlan.GridSizeCm,
                Walls = project.FloorPlan.Walls,
                Doors = project.FloorPlan.Doors,
                Windows = project.FloorPlan.Windows,
                Furniture = project.FloorPlan.Furniture,
                Users = project.FloorPlan.Users,
                VirtualUsers = project.FloorPlan.VirtualUsers,
                Objects = project.FloorPlan.Objects,
                AccessPoints = [.. project.FloorPlan.AccessPoints]
            }
        };
    }

    private List<UserOptimizationDelta> BuildUserDeltas(ProjectModel before, ProjectModel after)
    {
        var analyzer = new UserSignalAnalyzer();
        return before.FloorPlan.Users.Select(user =>
        {
            var beforeAnalysis = analyzer.Analyze(before, user);
            var afterAnalysis = analyzer.Analyze(after, user);
            return new UserOptimizationDelta
            {
                UserId = user.Id,
                UserName = user.Name,
                BeforeRssiDbm = beforeAnalysis.RssiDbm,
                AfterRssiDbm = afterAnalysis.RssiDbm,
                ConnectedApName = afterAnalysis.ConnectedApName,
                BeforeQuality = beforeAnalysis.Quality,
                AfterQuality = afterAnalysis.Quality,
                Reason = beforeAnalysis.Quality == LinkQuality.DeadZone
                    ? "Dead zone recovery near user position"
                    : "Improves user-weighted RSSI"
            };
        }).ToList();
    }
}
