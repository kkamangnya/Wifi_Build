using System.Globalization;
using System.Text;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class ProjectExportService
{
    public static async Task ExportCsvAsync(HeatmapResult result, string path, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("x_cm,y_cm,rssi_dbm,snr_db,interference_db,serving_ap_id");
        foreach (var sample in result.Samples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(C(sample.X)).Append(',')
                .Append(C(sample.Y)).Append(',')
                .Append(C(sample.RssiDbm)).Append(',')
                .Append(C(sample.SnrDb)).Append(',')
                .Append(C(sample.InterferenceDb)).Append(',')
                .Append(EscapeCsv(sample.ServingApId ?? ""))
                .AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExportSvgAsync(ProjectModel project, string path, CancellationToken cancellationToken = default)
    {
        var scale = 0.1;
        var builder = new StringBuilder();
        builder.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{C(project.FloorPlan.WidthCm * scale)}" height="{C(project.FloorPlan.HeightCm * scale)}" viewBox="0 0 {C(project.FloorPlan.WidthCm)} {C(project.FloorPlan.HeightCm)}">""");
        builder.AppendLine("""<rect width="100%" height="100%" fill="#111827"/>""");
        foreach (var wall in project.FloorPlan.Walls.Where(w => w.IsVisible))
        {
            var material = project.MaterialOrDefault(wall.MaterialId);
            builder.AppendLine($"""<rect x="{C(wall.Center.X - wall.LengthCm / 2)}" y="{C(wall.Center.Y - wall.ThicknessCm / 2)}" width="{C(wall.LengthCm)}" height="{C(wall.ThicknessCm)}" fill="{Xml(material.Color)}" stroke="#f9fafb" stroke-width="3" transform="rotate({C(wall.RotationDegrees)} {C(wall.Center.X)} {C(wall.Center.Y)})"/>""");
        }

        foreach (var obj in project.FloorPlan.Objects.Where(o => o.IsVisible).OrderBy(o => o.ZIndex))
        {
            var material = project.MaterialOrDefault(obj.Material);
            builder.AppendLine($"""<rect x="{C(obj.X - obj.Width / 2)}" y="{C(obj.Y - obj.Height / 2)}" width="{C(obj.Width)}" height="{C(obj.Height)}" rx="8" fill="{Xml(material.Color)}" stroke="#d1d5db" stroke-width="2" transform="rotate({C(obj.Rotation)} {C(obj.X)} {C(obj.Y)})"/>""");
            builder.AppendLine($"""<text x="{C(obj.X + 12)}" y="{C(obj.Y - 12)}" fill="#f9fafb" font-size="34">{Xml(obj.Name)}</text>""");
        }

        foreach (var ap in project.FloorPlan.AccessPoints.Where(a => a.IsVisible))
        {
            builder.AppendLine($"""<circle cx="{C(ap.Position.X)}" cy="{C(ap.Position.Y)}" r="42" fill="#1e90ff" stroke="#f9fafb" stroke-width="8"/>""");
            builder.AppendLine($"""<text x="{C(ap.Position.X + 56)}" y="{C(ap.Position.Y + 10)}" fill="#f9fafb" font-size="40">{Xml(ap.Name)}</text>""");
        }

        foreach (var user in project.FloorPlan.Users.Where(u => u.IsVisible))
        {
            builder.AppendLine($"""<circle cx="{C(user.Position.X)}" cy="{C(user.Position.Y)}" r="34" fill="#22c55e" stroke="#f9fafb" stroke-width="6"/>""");
            builder.AppendLine($"""<text x="{C(user.Position.X + 44)}" y="{C(user.Position.Y - 8)}" fill="#f9fafb" font-size="34">{Xml(user.Name)}</text>""");
        }

        builder.AppendLine("</svg>");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExportPdfReportAsync(ProjectModel project, HeatmapResult? heatmap, string path, CancellationToken cancellationToken = default)
    {
        var lines = new List<string>
        {
            "WiFi Studio Pro RF Report",
            $"Project: {project.Name}",
            $"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}",
            $"Floor: {project.FloorPlan.WidthCm / 100.0:F1} m x {project.FloorPlan.HeightCm / 100.0:F1} m",
            $"Walls: {project.FloorPlan.Walls.Count}, Objects: {project.FloorPlan.Objects.Count}, APs: {project.FloorPlan.AccessPoints.Count}, Users: {project.FloorPlan.Users.Count}"
        };

        if (heatmap is not null)
        {
            lines.Add($"Coverage: {heatmap.Stats.CoverageRatio:P1}");
            lines.Add($"Average RSSI: {heatmap.Stats.AverageRssiDbm:F1} dBm");
            lines.Add($"Minimum RSSI: {heatmap.Stats.MinimumRssiDbm:F1} dBm");
            lines.Add($"Shadow ratio: {heatmap.Stats.ShadowRatio:P1}");
        }

        foreach (var recommendation in project.OptimizationResults.FirstOrDefault()?.Recommendations.Take(3) ?? [])
        {
            lines.Add($"Recommendation: ({recommendation.Position.X:F0}, {recommendation.Position.Y:F0}) cm, score {recommendation.Score:F1}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(path, BuildSimplePdf(lines), cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder("BT /F1 18 Tf 50 790 Td ");
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdf(line)).Append(") Tj 0 -26 Td ");
        }

        content.Append("ET");
        var stream = Encoding.ASCII.GetBytes(content.ToString());
        var objects = new List<string>
        {
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n",
            "4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n",
            $"5 0 obj << /Length {stream.Length} >> stream\n{Encoding.ASCII.GetString(stream)}\nendstream endobj\n"
        };

        var output = new MemoryStream();
        using var writer = new StreamWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            writer.Flush();
            offsets.Add(output.Position);
            writer.Write(obj);
        }

        writer.Flush();
        var xref = output.Position;
        writer.Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            writer.Write($"{offset:0000000000} 00000 n \n");
        }

        writer.Write($"trailer << /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        writer.Flush();
        return output.ToArray();
    }

    private static string C(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value) => value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? "";

    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
