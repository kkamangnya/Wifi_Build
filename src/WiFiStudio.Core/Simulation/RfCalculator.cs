using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Simulation;

public static class RfCalculator
{
    public const double MinimumStableDistanceMeters = 1.0;
    public const double UnusableRssiDbm = -110.0;

    public static double FrequencyMhz(FrequencyBand band) => band switch
    {
        FrequencyBand.Ghz24 => 2412,
        FrequencyBand.Ghz5 => 5180,
        FrequencyBand.Ghz6 => 5955,
        _ => 5180
    };

    public static double FsplDb(double distanceMeters, FrequencyBand band)
    {
        var clampedDistance = Math.Max(MinimumStableDistanceMeters, distanceMeters);
        var distanceKm = clampedDistance / 1000.0;
        return 32.44 + 20.0 * Math.Log10(distanceKm) + 20.0 * Math.Log10(FrequencyMhz(band));
    }

    public static double RssiDbm(double txPowerDbm, double distanceMeters, FrequencyBand band, double materialLossDb, double interferencePenaltyDb)
    {
        return txPowerDbm - FsplDb(distanceMeters, band) - materialLossDb - interferencePenaltyDb;
    }

    public static double MaterialLossDb(PlanPoint from, PlanPoint to, ProjectModel project, FrequencyBand band)
    {
        var frequencyScale = FrequencyLossMultiplier(band);
        var loss = 0.0;

        foreach (var wall in project.FloorPlan.Walls.Where(w => w.IsVisible))
        {
            if (!GeometryMath.SegmentIntersectsWall(from, to, wall))
            {
                continue;
            }

            var material = project.MaterialOrDefault(wall.MaterialId);
            var baseLoss = wall.OverrideAttenuationDb.GetValueOrDefault(material.BaseAttenuationDb);
            var thicknessScale = Math.Max(0.7, wall.ThicknessCm / 10.0);
            loss += baseLoss * material.MultiplierFor(band) * thicknessScale;
            if (material.IsConductive)
            {
                loss += 4.0 * frequencyScale;
            }
        }

        foreach (var door in project.FloorPlan.Doors.Where(d => d.IsVisible))
        {
            if (GeometryMath.SegmentIntersectsDoor(from, to, door))
            {
                var material = project.MaterialOrDefault(door.MaterialId);
                loss += material.BaseAttenuationDb * material.MultiplierFor(band) * 0.55;
            }
        }

        foreach (var window in project.FloorPlan.Windows.Where(w => w.IsVisible))
        {
            if (GeometryMath.SegmentIntersectsWindow(from, to, window))
            {
                var material = project.MaterialOrDefault(window.MaterialId);
                loss += material.BaseAttenuationDb * material.MultiplierFor(band) * 0.45;
            }
        }

        foreach (var furniture in project.FloorPlan.Furniture.Where(f => f.IsVisible))
        {
            if (GeometryMath.SegmentIntersectsFurniture(from, to, furniture))
            {
                loss += furniture.AttenuationDb * frequencyScale;
            }
        }

        foreach (var planObject in project.FloorPlan.Objects.Where(o => o.IsVisible && o.BlocksSignal))
        {
            if (GeometryMath.SegmentIntersectsObject(from, to, planObject))
            {
                var material = project.MaterialOrDefault(planObject.Material);
                loss += planObject.AttenuationDb * material.MultiplierFor(band);
                if (material.IsConductive)
                {
                    loss += 2.0 * frequencyScale;
                }
            }
        }

        return loss;
    }

    public static double FrequencyLossMultiplier(FrequencyBand band) => band switch
    {
        FrequencyBand.Ghz24 => 0.86,
        FrequencyBand.Ghz5 => 1.0,
        FrequencyBand.Ghz6 => 1.14,
        _ => 1.0
    };
}
