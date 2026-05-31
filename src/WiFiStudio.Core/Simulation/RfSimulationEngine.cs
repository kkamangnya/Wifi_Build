using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Simulation;

public sealed class RfSimulationEngine
{
    public Task<HeatmapResult> EvaluateAsync(
        ProjectModel project,
        RfSimulationSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Evaluate(project, settings, progress, cancellationToken), cancellationToken);
    }

    public HeatmapResult Evaluate(
        ProjectModel project,
        RfSimulationSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var cellSize = Math.Max(10, settings.SampleResolutionCm);
        var columns = Math.Max(1, (int)Math.Ceiling(project.FloorPlan.WidthCm / cellSize));
        var rows = Math.Max(1, (int)Math.Ceiling(project.FloorPlan.HeightCm / cellSize));
        var result = new HeatmapResult
        {
            Columns = columns,
            Rows = rows,
            CellSizeCm = cellSize,
            Settings = CloneSettings(settings)
        };

        result.Samples.Capacity = columns * rows;
        var sum = 0.0;
        var minimum = double.MaxValue;
        var covered = 0;
        var shadowed = 0;

        // This loop is intentionally simple and cancellable. A future tiled cache can
        // recalculate only dirty floor regions after localized edits.
        for (var row = 0; row < rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var column = 0; column < columns; column++)
            {
                var sample = new PlanPoint(
                    Math.Min(project.FloorPlan.WidthCm, column * cellSize + cellSize / 2.0),
                    Math.Min(project.FloorPlan.HeightCm, row * cellSize + cellSize / 2.0));

                var sampleResult = EvaluateSample(project, settings, sample);
                result.Samples.Add(sampleResult);
                sum += sampleResult.RssiDbm;
                minimum = Math.Min(minimum, sampleResult.RssiDbm);
                if (sampleResult.RssiDbm >= settings.CoverageThresholdDbm)
                {
                    covered++;
                }

                if (sampleResult.RssiDbm <= settings.ShadowThresholdDbm)
                {
                    shadowed++;
                }
            }

            progress?.Report((row + 1.0) / rows);
        }

        var sampleCount = result.Samples.Count;
        result.Stats = new CoverageStats
        {
            SampleCount = sampleCount,
            AverageRssiDbm = sampleCount > 0 ? sum / sampleCount : RfCalculator.UnusableRssiDbm,
            MinimumRssiDbm = sampleCount > 0 ? minimum : RfCalculator.UnusableRssiDbm,
            CoverageRatio = sampleCount > 0 ? (double)covered / sampleCount : 0,
            ShadowRatio = sampleCount > 0 ? (double)shadowed / sampleCount : 1,
            RecommendedBand = RecommendBand(settings, sampleCount > 0 ? sum / sampleCount : RfCalculator.UnusableRssiDbm, sampleCount > 0 ? (double)covered / sampleCount : 0, sampleCount > 0 ? (double)shadowed / sampleCount : 1)
        };

        return result;
    }

    public RfSamplePoint EvaluateSample(ProjectModel project, RfSimulationSettings settings, PlanPoint sample)
    {
        var bestRssi = RfCalculator.UnusableRssiDbm;
        string? servingApId = null;

        foreach (var ap in project.FloorPlan.AccessPoints.Where(a => a.IsEnabled && a.IsVisible))
        {
            var band = ap.Band;
            var distanceMeters = Math.Max(
                RfCalculator.MinimumStableDistanceMeters,
                GeometryMath.DistanceMeters(ap.Position, sample));
            var materialLoss = RfCalculator.MaterialLossDb(ap.Position, sample, project, band);
            var interferencePenalty = settings.InterferencePenaltyDb
                + ChannelInterferenceAnalyzer.InterferencePenaltyDb(project, ap, sample);
            var rssi = RfCalculator.RssiDbm(
                ap.TxPowerDbm + ap.AntennaGainDbi,
                distanceMeters,
                band,
                materialLoss,
                interferencePenalty);

            if (rssi > bestRssi)
            {
                bestRssi = rssi;
                servingApId = ap.Id;
            }
        }

        return new RfSamplePoint
        {
            X = sample.X,
            Y = sample.Y,
            RssiDbm = bestRssi,
            SnrDb = bestRssi - settings.NoiseFloorDbm,
            InterferenceDb = servingApId is null
                ? settings.InterferencePenaltyDb
                : settings.InterferencePenaltyDb + ChannelInterferenceAnalyzer.InterferencePenaltyDb(
                    project,
                    project.FloorPlan.AccessPoints.First(ap => ap.Id == servingApId),
                    sample),
            ServingApId = servingApId
        };
    }

    private static string RecommendBand(RfSimulationSettings settings, double averageRssi, double coverageRatio, double shadowRatio)
    {
        if (averageRssi < -76 || shadowRatio > 0.18)
        {
            return "2.4 GHz";
        }

        if (coverageRatio > 0.92 && settings.InterferencePenaltyDb <= 2)
        {
            return "6 GHz";
        }

        return "5 GHz";
    }

    private static RfSimulationSettings CloneSettings(RfSimulationSettings settings) =>
        new()
        {
            FrequencyBand = settings.FrequencyBand,
            HeatmapType = settings.HeatmapType,
            SampleResolutionCm = settings.SampleResolutionCm,
            CoverageThresholdDbm = settings.CoverageThresholdDbm,
            ShadowThresholdDbm = settings.ShadowThresholdDbm,
            NoiseFloorDbm = settings.NoiseFloorDbm,
            InterferencePenaltyDb = settings.InterferencePenaltyDb
        };
}
