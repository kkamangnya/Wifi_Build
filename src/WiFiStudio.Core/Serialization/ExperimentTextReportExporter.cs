using System.Globalization;
using System.Text;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class ExperimentTextReportExporter
{
    public static async Task ExportAsync(
        ExperimentRunResult result,
        string path,
        IEnumerable<string>? imagePaths = null,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WiFi Studio Pro - Experiment Mode Summary");
        builder.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine();
        builder.AppendLine(result.Summary);
        builder.AppendLine();
        builder.AppendLine("Condition Summary");
        builder.AppendLine("ConditionName | UserRSSI | Quality | AverageRSSI | MinimumRSSI | DeadZoneRatio | ConnectedAP");
        foreach (var row in result.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{row.ConditionName} | {row.UserRssi:F1} dBm | {row.QualityDisplay} | {row.AverageRssi:F1} dBm | {row.MinimumRssi:F1} dBm | {row.DeadZoneRatio:P1} | {row.ConnectedAp}"));
        }

        var optimizedRows = result.Rows.Where(row => row.ConditionId == "condition-5-user-optimized").ToList();
        if (optimizedRows.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Condition 5 Before/After");
            builder.AppendLine("User | Before RSSI | After RSSI | Delta | Analysis");
            foreach (var row in optimizedRows)
            {
                builder.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{row.UserName} | {row.BeforeOptimizationRssi:F1} dBm | {row.AfterOptimizationRssi:F1} dBm | {row.OptimizationDeltaDb:+0.0;-0.0;0.0} dB | {row.AnalysisNote}"));
            }
        }

        var paths = imagePaths?.Where(pathValue => !string.IsNullOrWhiteSpace(pathValue)).ToList() ?? [];
        if (paths.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Generated Images");
            foreach (var imagePath in paths)
            {
                builder.AppendLine(imagePath);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }
}
