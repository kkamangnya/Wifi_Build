using System.Globalization;
using System.Text;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class ExperimentCsvExporter
{
    public static async Task ExportAsync(
        ExperimentRunResult result,
        string path,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ConditionName,StructureDescription,APPosition,UserPosition,UserRSSI,AverageRSSI,MinimumRSSI,DeadZoneRatio,ConnectedAP,AnalysisNote");
        foreach (var row in result.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(E(row.ConditionName)).Append(',')
                .Append(E(row.StructureDescription)).Append(',')
                .Append(E(row.ApPosition)).Append(',')
                .Append(E($"{row.UserName} {row.UserPosition}")).Append(',')
                .Append(C(row.UserRssi)).Append(',')
                .Append(C(row.AverageRssi)).Append(',')
                .Append(C(row.MinimumRssi)).Append(',')
                .Append(C(row.DeadZoneRatio)).Append(',')
                .Append(E(row.ConnectedAp)).Append(',')
                .Append(E(row.AnalysisNote))
                .AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static string C(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string E(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
