using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Simulation;

public sealed class UserRouteSimulationEngine
{
    private readonly RfSimulationEngine _engine = new();

    public RouteAnalysisResult Analyze(ProjectModel project, UserLocation user, double stepCm = 100)
    {
        var points = BuildRoutePoints(user, Math.Max(25, stepCm)).ToList();
        var samples = new List<RouteSample>();
        string? previousAp = null;
        var handovers = 0;

        foreach (var point in points)
        {
            var sample = _engine.EvaluateSample(project, project.SimulationSettings, point);
            if (previousAp is not null && sample.ServingApId is not null && sample.ServingApId != previousAp)
            {
                handovers++;
            }

            previousAp = sample.ServingApId ?? previousAp;
            samples.Add(new RouteSample
            {
                X = point.X,
                Y = point.Y,
                RssiDbm = sample.RssiDbm,
                ServingApId = sample.ServingApId,
                Quality = UserSignalAnalyzer.Classify(sample.RssiDbm)
            });
        }

        var worst = samples.OrderBy(s => s.RssiDbm).FirstOrDefault();
        return new RouteAnalysisResult
        {
            UserId = user.Id,
            UserName = user.Name,
            Samples = samples,
            AverageRssiDbm = samples.Count == 0 ? RfCalculator.UnusableRssiDbm : samples.Average(s => s.RssiDbm),
            MinimumRssiDbm = samples.Count == 0 ? RfCalculator.UnusableRssiDbm : samples.Min(s => s.RssiDbm),
            MaximumRssiDbm = samples.Count == 0 ? RfCalculator.UnusableRssiDbm : samples.Max(s => s.RssiDbm),
            DeadZoneSampleCount = samples.Count(s => s.Quality == LinkQuality.DeadZone),
            HandoverCount = handovers,
            WorstX = worst?.X ?? user.Position.X,
            WorstY = worst?.Y ?? user.Position.Y,
            Recommendation = samples.Any(s => s.Quality == LinkQuality.DeadZone)
                ? "Route contains dead zones; add AP near the worst segment or adjust AP placement."
                : "Route quality is acceptable."
        };
    }

    private static IEnumerable<PlanPoint> BuildRoutePoints(UserLocation user, double stepCm)
    {
        var points = new List<PlanPoint> { user.Position };
        points.AddRange(user.Route);
        if (points.Count == 1)
        {
            yield return user.Position;
            yield break;
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            var distance = GeometryMath.DistanceCm(start, end);
            var steps = Math.Max(1, (int)Math.Ceiling(distance / stepCm));
            for (var step = 0; step <= steps; step++)
            {
                var t = (double)step / steps;
                yield return new PlanPoint(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
            }
        }
    }
}
