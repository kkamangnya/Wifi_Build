using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.Versioning;
using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

[SupportedOSPlatform("windows")]
public static class ExperimentReportImageExporter
{
    private const int MinimumWidthPx = 1920;
    private const int DefaultWidthPx = 2400;
    private const int HeaderHeightPx = 150;
    private const int SidePanelWidthPx = 520;
    private const int MarginPx = 48;

    public static async Task ExportConditionAsync(
        ProjectModel project,
        HeatmapResult heatmap,
        IReadOnlyList<ExperimentResultRow> rows,
        string path,
        int widthPx = DefaultWidthPx,
        CancellationToken cancellationToken = default)
    {
        var conditionRows = rows.Where(row => project.Name.Contains(row.ConditionName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (conditionRows.Count == 0)
        {
            conditionRows = rows.ToList();
        }

        var title = conditionRows.FirstOrDefault()?.ConditionName ?? project.Name;
        var subtitle = conditionRows.FirstOrDefault()?.StructureDescription ?? "Experiment heatmap";
        var bytes = RenderCondition(project, heatmap, conditionRows, title, subtitle, Math.Max(MinimumWidthPx, widthPx));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExportDifferenceAsync(
        ProjectModel beforeProject,
        HeatmapResult before,
        ProjectModel afterProject,
        HeatmapResult after,
        IReadOnlyList<ExperimentResultRow> rows,
        string path,
        int widthPx = DefaultWidthPx,
        CancellationToken cancellationToken = default)
    {
        var bytes = RenderDifference(
            beforeProject,
            before,
            afterProject,
            after,
            rows,
            "Condition 5 - Optimization Delta",
            "RSSI improvement after user-centered AP placement",
            Math.Max(MinimumWidthPx, widthPx));
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] RenderCondition(
        ProjectModel project,
        HeatmapResult heatmap,
        IReadOnlyList<ExperimentResultRow> rows,
        string title,
        string subtitle,
        int widthPx)
    {
        var layout = CreateLayout(project, widthPx);
        using var bitmap = new Bitmap(layout.ImageWidth, layout.ImageHeight, PixelFormat.Format32bppArgb);
        using var graphics = CreateGraphics(bitmap);
        using var fonts = ReportFonts.Create();

        graphics.Clear(Color.FromArgb(248, 250, 252));
        DrawHeader(graphics, fonts, title, subtitle, layout);
        DrawMapFrame(graphics, layout);
        DrawRssiHeatmap(graphics, heatmap, project.FloorPlan, layout);
        DrawOverlays(graphics, fonts, project, rows, layout);
        DrawRssiPanel(graphics, fonts, heatmap, rows, layout);
        return Encode(bitmap);
    }

    private static byte[] RenderDifference(
        ProjectModel beforeProject,
        HeatmapResult before,
        ProjectModel afterProject,
        HeatmapResult after,
        IReadOnlyList<ExperimentResultRow> rows,
        string title,
        string subtitle,
        int widthPx)
    {
        var layout = CreateLayout(afterProject, widthPx);
        using var bitmap = new Bitmap(layout.ImageWidth, layout.ImageHeight, PixelFormat.Format32bppArgb);
        using var graphics = CreateGraphics(bitmap);
        using var fonts = ReportFonts.Create();

        graphics.Clear(Color.FromArgb(248, 250, 252));
        DrawHeader(graphics, fonts, title, subtitle, layout);
        DrawMapFrame(graphics, layout);
        DrawDeltaHeatmap(graphics, before, after, afterProject.FloorPlan, layout);
        DrawOverlays(graphics, fonts, afterProject, rows, layout);
        DrawDeltaPanel(graphics, fonts, before, after, rows, layout);
        return Encode(bitmap);
    }

    private static Graphics CreateGraphics(Bitmap bitmap)
    {
        var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        return graphics;
    }

    private static ReportLayout CreateLayout(ProjectModel project, int widthPx)
    {
        var mapWidth = widthPx - SidePanelWidthPx - MarginPx * 3;
        var mapHeight = Math.Max(620, (int)Math.Round(mapWidth * project.FloorPlan.HeightCm / Math.Max(1, project.FloorPlan.WidthCm)));
        return new ReportLayout(
            widthPx,
            HeaderHeightPx + mapHeight + MarginPx,
            MarginPx,
            HeaderHeightPx,
            mapWidth,
            mapHeight,
            MarginPx * 2 + mapWidth,
            HeaderHeightPx,
            SidePanelWidthPx,
            mapHeight);
    }

    private static void DrawHeader(Graphics graphics, ReportFonts fonts, string title, string subtitle, ReportLayout layout)
    {
        using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var secondaryBrush = new SolidBrush(Color.FromArgb(71, 85, 105));
        graphics.DrawString(title, fonts.Title, titleBrush, MarginPx, 26);
        graphics.DrawString(subtitle, fonts.Subtitle, secondaryBrush, MarginPx, 86);
        graphics.DrawString(
            $"RSSI range: {HeatmapColorScale.FixedMinRssiDbm:F0} to {HeatmapColorScale.FixedMaxRssiDbm:F0} dBm",
            fonts.Caption,
            secondaryBrush,
            layout.SideX,
            92);
    }

    private static void DrawMapFrame(Graphics graphics, ReportLayout layout)
    {
        using var border = new Pen(Color.FromArgb(15, 23, 42), 4);
        using var fill = new SolidBrush(Color.FromArgb(241, 245, 249));
        graphics.FillRectangle(fill, layout.MapBounds);
        graphics.DrawRectangle(border, layout.MapBounds);
    }

    private static void DrawRssiHeatmap(Graphics graphics, HeatmapResult heatmap, FloorPlan floor, ReportLayout layout)
    {
        foreach (var sample in heatmap.Samples)
        {
            var color = ToDrawing(HeatmapColorScale.ForRssi(Math.Clamp(sample.RssiDbm, HeatmapColorScale.FixedMinRssiDbm, HeatmapColorScale.FixedMaxRssiDbm)));
            using var brush = new SolidBrush(color);
            var rect = SampleRect(sample, heatmap.CellSizeCm, floor, layout);
            graphics.FillRectangle(brush, rect);
        }
    }

    private static void DrawDeltaHeatmap(Graphics graphics, HeatmapResult before, HeatmapResult after, FloorPlan floor, ReportLayout layout)
    {
        var count = Math.Min(before.Samples.Count, after.Samples.Count);
        for (var index = 0; index < count; index++)
        {
            var oldSample = before.Samples[index];
            var newSample = after.Samples[index];
            using var brush = new SolidBrush(DeltaColor(newSample.RssiDbm - oldSample.RssiDbm));
            graphics.FillRectangle(brush, SampleRect(newSample, after.CellSizeCm, floor, layout));
        }
    }

    private static RectangleF SampleRect(RfSamplePoint sample, double cellSizeCm, FloorPlan floor, ReportLayout layout)
    {
        var halfCell = cellSizeCm / 2.0;
        var left = layout.MapX + (float)((sample.X - halfCell) / floor.WidthCm * layout.MapWidth);
        var top = layout.MapY + (float)((sample.Y - halfCell) / floor.HeightCm * layout.MapHeight);
        var right = layout.MapX + (float)((sample.X + halfCell) / floor.WidthCm * layout.MapWidth);
        var bottom = layout.MapY + (float)((sample.Y + halfCell) / floor.HeightCm * layout.MapHeight);
        return RectangleF.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
    }

    private static void DrawOverlays(Graphics graphics, ReportFonts fonts, ProjectModel project, IReadOnlyList<ExperimentResultRow> rows, ReportLayout layout)
    {
        foreach (var wall in project.FloorPlan.Walls.Where(wall => wall.IsVisible))
        {
            var material = project.MaterialOrDefault(wall.MaterialId);
            var points = GeometryMath.WallFootprint(wall).Select(point => ToPointF(point, project.FloorPlan, layout)).ToArray();
            using var fill = new SolidBrush(ParseColor(material.Color, 210, Color.FromArgb(128, 138, 150)));
            using var stroke = new Pen(Color.FromArgb(30, 41, 59), 3);
            graphics.FillPolygon(fill, points);
            graphics.DrawPolygon(stroke, points);
        }

        foreach (var obj in project.FloorPlan.Objects.Where(obj => obj.IsVisible).OrderBy(obj => obj.ZIndex))
        {
            var material = project.MaterialOrDefault(obj.Material);
            var points = GeometryMath.ObjectFootprint(obj).Select(point => ToPointF(point, project.FloorPlan, layout)).ToArray();
            using var fill = new SolidBrush(ParseColor(material.Color, 195, Color.FromArgb(148, 163, 184)));
            using var stroke = new Pen(Color.FromArgb(71, 85, 105), 2);
            graphics.FillPolygon(fill, points);
            graphics.DrawPolygon(stroke, points);
            var center = ToPointF(obj.Center, project.FloorPlan, layout);
            DrawLabel(graphics, fonts.Small, PlanObjectPreset.For(obj.Type).Name, center.X + 8, center.Y - 18, layout);
        }

        foreach (var ap in project.FloorPlan.AccessPoints.Where(ap => ap.IsVisible))
        {
            var center = ToPointF(ap.Position, project.FloorPlan, layout);
            using var coverage = new Pen(Color.FromArgb(120, 37, 99, 235), 4);
            using var apFill = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var whitePen = new Pen(Color.White, 5);
            graphics.DrawEllipse(coverage, center.X - 62, center.Y - 62, 124, 124);
            graphics.FillEllipse(apFill, center.X - 19, center.Y - 19, 38, 38);
            graphics.DrawEllipse(whitePen, center.X - 20, center.Y - 20, 40, 40);
            DrawLabel(graphics, fonts.Label, ap.Name, center.X + 28, center.Y - 15, layout);
        }

        foreach (var user in project.FloorPlan.Users.Where(user => user.IsVisible))
        {
            var row = rows.FirstOrDefault(candidate => string.Equals(candidate.UserName, user.Name, StringComparison.OrdinalIgnoreCase));
            var rssi = row?.UserRssi ?? -110;
            var center = ToPointF(user.Position, project.FloorPlan, layout);
            using var marker = new SolidBrush(ToDrawing(HeatmapColorScale.ForRssi(rssi) with { A = 255 }));
            using var whitePen = new Pen(Color.White, 5);
            graphics.FillEllipse(marker, center.X - 18, center.Y - 18, 36, 36);
            graphics.DrawEllipse(whitePen, center.X - 19, center.Y - 19, 38, 38);
            DrawLabel(graphics, fonts.Label, $"{user.Name} {rssi:F0} dBm", center.X + 28, center.Y - 22, layout);
            if (row is not null)
            {
                DrawLabel(graphics, fonts.Small, row.ConnectedAp, center.X + 28, center.Y + 8, layout);
            }
        }
    }

    private static void DrawRssiPanel(Graphics graphics, ReportFonts fonts, HeatmapResult heatmap, IReadOnlyList<ExperimentResultRow> rows, ReportLayout layout)
    {
        DrawPanelBackground(graphics, layout);
        using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bodyBrush = new SolidBrush(Color.FromArgb(51, 65, 85));
        graphics.DrawString("RSSI Legend", fonts.PanelTitle, titleBrush, layout.SideX + 26, layout.SideY + 24);

        var y = layout.SideY + 86;
        foreach (var band in HeatmapColorScale.RssiLegendBands)
        {
            using var fill = new SolidBrush(ToDrawing(band.Color with { A = 255 }));
            using var stroke = new Pen(Color.FromArgb(71, 85, 105), 1.5f);
            graphics.FillRectangle(fill, layout.SideX + 30, y, 56, 28);
            graphics.DrawRectangle(stroke, layout.SideX + 30, y, 56, 28);
            graphics.DrawString(band.Label.Replace("DBM", "dBm", StringComparison.Ordinal), fonts.Body, bodyBrush, layout.SideX + 102, y + 2);
            y += 44;
        }

        y += 18;
        graphics.DrawString("Key Metrics", fonts.PanelTitle, titleBrush, layout.SideX + 26, y);
        y += 58;
        var userRssi = rows.Count == 0 ? -110 : rows.Average(row => row.UserRssi);
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "User RSSI", $"{userRssi:F1} dBm");
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "Average RSSI", $"{heatmap.Stats.AverageRssiDbm:F1} dBm");
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "Minimum RSSI", $"{heatmap.Stats.MinimumRssiDbm:F1} dBm");
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "Dead Zone Ratio", $"{heatmap.Stats.ShadowRatio:P1}");

        y += 18;
        graphics.DrawString("Users", fonts.PanelTitle, titleBrush, layout.SideX + 26, y);
        y += 52;
        foreach (var row in rows.Take(5))
        {
            using var marker = new SolidBrush(ToDrawing(HeatmapColorScale.ForRssi(row.UserRssi) with { A = 255 }));
            graphics.FillEllipse(marker, layout.SideX + 34, y + 6, 22, 22);
            graphics.DrawString($"{row.UserName}  {row.UserRssi:F1} dBm", fonts.BodyBold, titleBrush, layout.SideX + 68, y);
            graphics.DrawString($"{row.QualityDisplay} / {row.ConnectedAp}", fonts.Small, bodyBrush, layout.SideX + 68, y + 25);
            y += 60;
        }
    }

    private static void DrawDeltaPanel(Graphics graphics, ReportFonts fonts, HeatmapResult before, HeatmapResult after, IReadOnlyList<ExperimentResultRow> rows, ReportLayout layout)
    {
        DrawPanelBackground(graphics, layout);
        using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var bodyBrush = new SolidBrush(Color.FromArgb(51, 65, 85));
        graphics.DrawString("Delta Legend", fonts.PanelTitle, titleBrush, layout.SideX + 26, layout.SideY + 24);

        var y = layout.SideY + 86;
        foreach (var (label, color) in DeltaLegend())
        {
            using var fill = new SolidBrush(color);
            using var stroke = new Pen(Color.FromArgb(71, 85, 105), 1.5f);
            graphics.FillRectangle(fill, layout.SideX + 30, y, 56, 28);
            graphics.DrawRectangle(stroke, layout.SideX + 30, y, 56, 28);
            graphics.DrawString(label, fonts.Body, bodyBrush, layout.SideX + 102, y + 2);
            y += 44;
        }

        y += 20;
        graphics.DrawString("Before / After", fonts.PanelTitle, titleBrush, layout.SideX + 26, y);
        y += 58;
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "Before Avg", $"{before.Stats.AverageRssiDbm:F1} dBm");
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "After Avg", $"{after.Stats.AverageRssiDbm:F1} dBm");
        DrawMetric(graphics, fonts, layout.SideX + 30, ref y, "Average Delta", $"{after.Stats.AverageRssiDbm - before.Stats.AverageRssiDbm:+0.0;-0.0;0.0} dB");

        y += 14;
        foreach (var row in rows.Take(5))
        {
            DrawMetric(graphics, fonts, layout.SideX + 30, ref y, row.UserName, $"{row.OptimizationDeltaDb:+0.0;-0.0;0.0} dB");
        }
    }

    private static void DrawPanelBackground(Graphics graphics, ReportLayout layout)
    {
        using var fill = new SolidBrush(Color.White);
        using var border = new Pen(Color.FromArgb(203, 213, 225), 2);
        graphics.FillRectangle(fill, layout.SideBounds);
        graphics.DrawRectangle(border, layout.SideBounds);
    }

    private static void DrawMetric(Graphics graphics, ReportFonts fonts, int x, ref int y, string label, string value)
    {
        using var labelBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        using var valueBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        graphics.DrawString(label, fonts.Body, labelBrush, x, y);
        graphics.DrawString(value, fonts.BodyBold, valueBrush, x + 225, y - 2);
        y += 42;
    }

    private static void DrawLabel(Graphics graphics, Font font, string text, float desiredX, float y, ReportLayout layout)
    {
        using var brush = new SolidBrush(Color.FromArgb(15, 23, 42));
        var size = graphics.MeasureString(text, font);
        var x = desiredX + size.Width < layout.MapX + layout.MapWidth - 10
            ? desiredX
            : Math.Max(layout.MapX + 10, desiredX - size.Width - 56);
        graphics.DrawString(text, font, brush, x, y);
    }

    private static PointF ToPointF(PlanPoint point, FloorPlan floor, ReportLayout layout) =>
        new(
            layout.MapX + (float)(point.X / Math.Max(1, floor.WidthCm) * layout.MapWidth),
            layout.MapY + (float)(point.Y / Math.Max(1, floor.HeightCm) * layout.MapHeight));

    private static Color ParseColor(string hex, byte alpha, Color fallback)
    {
        if (hex.StartsWith('#') && hex.Length == 7
            && byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return Color.FromArgb(alpha, r, g, b);
        }

        return Color.FromArgb(alpha, fallback);
    }

    private static Color ToDrawing(BgraColor color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Color DeltaColor(double deltaDb)
    {
        if (deltaDb >= 20) return Color.FromArgb(232, 22, 101, 52);
        if (deltaDb >= 10) return Color.FromArgb(224, 34, 197, 94);
        if (deltaDb >= 3) return Color.FromArgb(216, 134, 239, 172);
        if (deltaDb >= -3) return Color.FromArgb(200, 203, 213, 225);
        return Color.FromArgb(220, 220, 38, 38);
    }

    private static IReadOnlyList<(string Label, Color Color)> DeltaLegend() =>
    [
        ("+20 dB or more", DeltaColor(22)),
        ("+10 to +20 dB", DeltaColor(12)),
        ("+3 to +10 dB", DeltaColor(5)),
        ("-3 to +3 dB", DeltaColor(0)),
        ("Worse", DeltaColor(-5))
    ];

    private static byte[] Encode(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private sealed record ReportLayout(
        int ImageWidth,
        int ImageHeight,
        int MapX,
        int MapY,
        int MapWidth,
        int MapHeight,
        int SideX,
        int SideY,
        int SideWidth,
        int SideHeight)
    {
        public Rectangle MapBounds => new(MapX, MapY, MapWidth, MapHeight);
        public Rectangle SideBounds => new(SideX, SideY, SideWidth, SideHeight);
    }

    private sealed class ReportFonts : IDisposable
    {
        private readonly List<Font> _fonts = [];

        private ReportFonts(string family)
        {
            Title = Add(new Font(family, 34, FontStyle.Bold, GraphicsUnit.Pixel));
            Subtitle = Add(new Font(family, 19, FontStyle.Regular, GraphicsUnit.Pixel));
            PanelTitle = Add(new Font(family, 28, FontStyle.Bold, GraphicsUnit.Pixel));
            Body = Add(new Font(family, 17, FontStyle.Regular, GraphicsUnit.Pixel));
            BodyBold = Add(new Font(family, 18, FontStyle.Bold, GraphicsUnit.Pixel));
            Label = Add(new Font(family, 18, FontStyle.Bold, GraphicsUnit.Pixel));
            Small = Add(new Font(family, 14, FontStyle.Regular, GraphicsUnit.Pixel));
            Caption = Add(new Font(family, 16, FontStyle.Regular, GraphicsUnit.Pixel));
        }

        public Font Title { get; }
        public Font Subtitle { get; }
        public Font PanelTitle { get; }
        public Font Body { get; }
        public Font BodyBold { get; }
        public Font Label { get; }
        public Font Small { get; }
        public Font Caption { get; }

        public static ReportFonts Create() => new(ResolveFontFamily());

        public void Dispose()
        {
            foreach (var font in _fonts)
            {
                font.Dispose();
            }
        }

        private Font Add(Font font)
        {
            _fonts.Add(font);
            return font;
        }

        private static string ResolveFontFamily()
        {
            using var installed = new InstalledFontCollection();
            var names = installed.Families.Select(family => family.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (names.Contains("Noto Sans KR"))
            {
                return "Noto Sans KR";
            }

            if (names.Contains("Segoe UI"))
            {
                return "Segoe UI";
            }

            return "Arial";
        }
    }
}
