using System.Globalization;
using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

public static class ExperimentHeatmapPngExporter
{
    private const int DefaultWidthPx = 2200;
    private const int HeaderHeightPx = 116;
    private const int SidePanelWidthPx = 470;
    private const int MarginPx = 36;

    public static async Task ExportConditionAsync(
        ProjectModel project,
        HeatmapResult heatmap,
        IReadOnlyList<ExperimentResultRow> rows,
        string path,
        int widthPx = DefaultWidthPx,
        CancellationToken cancellationToken = default)
    {
        var conditionRows = rows.Where(row => MatchesCondition(project, row)).ToList();
        if (conditionRows.Count == 0)
        {
            conditionRows = rows.ToList();
        }

        var title = conditionRows.FirstOrDefault()?.ConditionName ?? project.Name;
        var subtitle = conditionRows.FirstOrDefault()?.StructureDescription ?? "Experiment heatmap";
        var image = RenderRssiImage(project, heatmap, conditionRows, title, subtitle, widthPx);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(path, image, cancellationToken).ConfigureAwait(false);
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
        var title = "Condition 5 - Optimization Delta";
        var subtitle = "Green shows RSSI improvement";
        var image = RenderDeltaImage(beforeProject, before, afterProject, after, rows, title, subtitle, widthPx);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(path, image, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] RenderRssiImage(
        ProjectModel project,
        HeatmapResult heatmap,
        IReadOnlyList<ExperimentResultRow> rows,
        string title,
        string subtitle,
        int widthPx)
    {
        var geometry = CreateLayout(project, widthPx);
        var canvas = new SimplePngCanvas(geometry.ImageWidth, geometry.ImageHeight, Rgb(247, 249, 252));
        DrawHeader(canvas, title, subtitle, geometry);
        DrawMapFrame(canvas, geometry);
        DrawRssiHeatmap(canvas, heatmap, project.FloorPlan, geometry);
        DrawOverlays(canvas, project, rows, geometry);
        DrawRssiSidePanel(canvas, heatmap, rows, geometry);
        return PngImageEncoder.EncodeBgra(canvas.Width, canvas.Height, canvas.Pixels);
    }

    private static byte[] RenderDeltaImage(
        ProjectModel beforeProject,
        HeatmapResult before,
        ProjectModel afterProject,
        HeatmapResult after,
        IReadOnlyList<ExperimentResultRow> rows,
        string title,
        string subtitle,
        int widthPx)
    {
        var geometry = CreateLayout(afterProject, widthPx);
        var canvas = new SimplePngCanvas(geometry.ImageWidth, geometry.ImageHeight, Rgb(247, 249, 252));
        DrawHeader(canvas, title, subtitle, geometry);
        DrawMapFrame(canvas, geometry);
        DrawDeltaHeatmap(canvas, before, after, afterProject.FloorPlan, geometry);
        DrawOverlays(canvas, afterProject, rows, geometry);
        DrawDeltaSidePanel(canvas, before, after, rows, geometry);
        return PngImageEncoder.EncodeBgra(canvas.Width, canvas.Height, canvas.Pixels);
    }

    private static ExperimentImageLayout CreateLayout(ProjectModel project, int widthPx)
    {
        widthPx = Math.Max(1500, widthPx);
        var mapWidth = widthPx - SidePanelWidthPx - MarginPx * 3;
        var mapHeight = Math.Max(520, (int)Math.Round(mapWidth * project.FloorPlan.HeightCm / Math.Max(1, project.FloorPlan.WidthCm)));
        return new ExperimentImageLayout(
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

    private static void DrawHeader(SimplePngCanvas canvas, string title, string subtitle, ExperimentImageLayout layout)
    {
        canvas.DrawText(title, MarginPx, 26, Rgb(17, 24, 39), 5);
        canvas.DrawText(subtitle, MarginPx, 78, Rgb(75, 85, 99), 3);
        canvas.DrawText($"RSSI RANGE {HeatmapColorScale.FixedMinRssiDbm:F0} TO {HeatmapColorScale.FixedMaxRssiDbm:F0} DBM", layout.SideX, 80, Rgb(75, 85, 99), 3);
    }

    private static void DrawMapFrame(SimplePngCanvas canvas, ExperimentImageLayout layout)
    {
        canvas.FillRect(layout.MapX - 4, layout.MapY - 4, layout.MapWidth + 8, layout.MapHeight + 8, Rgb(15, 23, 42));
        canvas.FillRect(layout.MapX, layout.MapY, layout.MapWidth, layout.MapHeight, Rgb(241, 245, 249));
    }

    private static void DrawRssiHeatmap(SimplePngCanvas canvas, HeatmapResult heatmap, FloorPlan floor, ExperimentImageLayout layout)
    {
        foreach (var sample in heatmap.Samples)
        {
            var color = HeatmapColorScale.ForRssi(Math.Clamp(sample.RssiDbm, HeatmapColorScale.FixedMinRssiDbm, HeatmapColorScale.FixedMaxRssiDbm));
            var halfCell = heatmap.CellSizeCm / 2.0;
            var left = layout.MapX + (int)Math.Floor((sample.X - halfCell) / floor.WidthCm * layout.MapWidth);
            var top = layout.MapY + (int)Math.Floor((sample.Y - halfCell) / floor.HeightCm * layout.MapHeight);
            var right = layout.MapX + (int)Math.Ceiling((sample.X + halfCell) / floor.WidthCm * layout.MapWidth);
            var bottom = layout.MapY + (int)Math.Ceiling((sample.Y + halfCell) / floor.HeightCm * layout.MapHeight);
            canvas.BlendRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top), color);
        }
    }

    private static void DrawDeltaHeatmap(SimplePngCanvas canvas, HeatmapResult before, HeatmapResult after, FloorPlan floor, ExperimentImageLayout layout)
    {
        var count = Math.Min(before.Samples.Count, after.Samples.Count);
        for (var index = 0; index < count; index++)
        {
            var oldSample = before.Samples[index];
            var newSample = after.Samples[index];
            var color = DeltaColor(newSample.RssiDbm - oldSample.RssiDbm);
            var halfCell = after.CellSizeCm / 2.0;
            var left = layout.MapX + (int)Math.Floor((newSample.X - halfCell) / floor.WidthCm * layout.MapWidth);
            var top = layout.MapY + (int)Math.Floor((newSample.Y - halfCell) / floor.HeightCm * layout.MapHeight);
            var right = layout.MapX + (int)Math.Ceiling((newSample.X + halfCell) / floor.WidthCm * layout.MapWidth);
            var bottom = layout.MapY + (int)Math.Ceiling((newSample.Y + halfCell) / floor.HeightCm * layout.MapHeight);
            canvas.BlendRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top), color);
        }
    }

    private static void DrawOverlays(SimplePngCanvas canvas, ProjectModel project, IReadOnlyList<ExperimentResultRow> rows, ExperimentImageLayout layout)
    {
        foreach (var wall in project.FloorPlan.Walls.Where(wall => wall.IsVisible))
        {
            var material = project.MaterialOrDefault(wall.MaterialId);
            var polygon = GeometryMath.WallFootprint(wall).Select(point => ToImage(point, project.FloorPlan, layout)).ToList();
            canvas.FillPolygon(polygon, ParseColor(material.Color, 230, Rgb(83, 94, 110, 230)));
            canvas.DrawPolygon(polygon, Rgb(17, 24, 39), 4);
        }

        foreach (var obj in project.FloorPlan.Objects.Where(obj => obj.IsVisible).OrderBy(obj => obj.ZIndex))
        {
            var material = project.MaterialOrDefault(obj.Material);
            var polygon = GeometryMath.ObjectFootprint(obj).Select(point => ToImage(point, project.FloorPlan, layout)).ToList();
            canvas.FillPolygon(polygon, ParseColor(material.Color, 208, Rgb(148, 163, 184, 208)));
            canvas.DrawPolygon(polygon, Rgb(51, 65, 85), 3);
            var center = ToImage(obj.Center, project.FloorPlan, layout);
            canvas.DrawText(PlanObjectPreset.For(obj.Type).Name, (int)center.X + 8, (int)center.Y - 12, Rgb(15, 23, 42), 2);
        }

        foreach (var ap in project.FloorPlan.AccessPoints.Where(ap => ap.IsVisible))
        {
            var center = ToImage(ap.Position, project.FloorPlan, layout);
            canvas.DrawCircle((int)center.X, (int)center.Y, 54, Rgb(37, 99, 235, 80), 4);
            canvas.FillCircle((int)center.X, (int)center.Y, 18, Rgb(37, 99, 235, 255));
            canvas.DrawCircle((int)center.X, (int)center.Y, 19, Rgb(255, 255, 255), 5);
            var labelX = LabelX(ap.Name, (int)center.X, 26, 3, layout);
            canvas.DrawText(ap.Name, labelX, (int)center.Y - 10, Rgb(15, 23, 42), 3);
        }

        foreach (var user in project.FloorPlan.Users.Where(user => user.IsVisible))
        {
            var row = rows.FirstOrDefault(candidate => string.Equals(candidate.UserName, user.Name, StringComparison.OrdinalIgnoreCase));
            var rssi = row?.UserRssi ?? -110;
            var center = ToImage(user.Position, project.FloorPlan, layout);
            canvas.FillCircle((int)center.X, (int)center.Y, 17, QualityColor(rssi, 255));
            canvas.DrawCircle((int)center.X, (int)center.Y, 18, Rgb(255, 255, 255), 5);
            var userLabel = $"{user.Name} {rssi:F0} DBM";
            var userLabelX = LabelX(userLabel, (int)center.X, 25, 3, layout);
            canvas.DrawText(userLabel, userLabelX, (int)center.Y - 20, Rgb(15, 23, 42), 3);
            if (row is not null)
            {
                canvas.DrawText(row.ConnectedAp, LabelX(row.ConnectedAp, (int)center.X, 25, 2, layout), (int)center.Y + 8, Rgb(51, 65, 85), 2);
            }
        }
    }

    private static int LabelX(string text, int centerX, int offset, int scale, ExperimentImageLayout layout)
    {
        var width = text.Length * 6 * scale;
        var right = centerX + offset + width;
        if (right < layout.MapX + layout.MapWidth - 8)
        {
            return centerX + offset;
        }

        return Math.Max(layout.MapX + 8, centerX - offset - width);
    }

    private static void DrawRssiSidePanel(SimplePngCanvas canvas, HeatmapResult heatmap, IReadOnlyList<ExperimentResultRow> rows, ExperimentImageLayout layout)
    {
        canvas.FillRect(layout.SideX, layout.SideY, layout.SideWidth, layout.SideHeight, Rgb(255, 255, 255));
        canvas.DrawRect(layout.SideX, layout.SideY, layout.SideWidth, layout.SideHeight, Rgb(203, 213, 225), 3);
        canvas.DrawText("RSSI LEGEND", layout.SideX + 24, layout.SideY + 24, Rgb(15, 23, 42), 4);

        var y = layout.SideY + 78;
        foreach (var band in HeatmapColorScale.RssiLegendBands)
        {
            canvas.FillRect(layout.SideX + 28, y, 54, 30, band.Color with { A = 255 });
            canvas.DrawRect(layout.SideX + 28, y, 54, 30, Rgb(71, 85, 105), 2);
            canvas.DrawText(band.Label, layout.SideX + 96, y + 5, Rgb(15, 23, 42), 2);
            y += 46;
        }

        y += 16;
        canvas.DrawText("KEY METRICS", layout.SideX + 24, y, Rgb(15, 23, 42), 4);
        y += 48;
        var userRssi = rows.Count == 0 ? -110 : rows.Average(row => row.UserRssi);
        DrawMetric(canvas, layout.SideX + 26, ref y, "USER RSSI", $"{userRssi:F1} DBM");
        DrawMetric(canvas, layout.SideX + 26, ref y, "AVERAGE RSSI", $"{heatmap.Stats.AverageRssiDbm:F1} DBM");
        DrawMetric(canvas, layout.SideX + 26, ref y, "MINIMUM RSSI", $"{heatmap.Stats.MinimumRssiDbm:F1} DBM");
        DrawMetric(canvas, layout.SideX + 26, ref y, "DEAD ZONE", $"{heatmap.Stats.ShadowRatio:P1}");

        y += 14;
        canvas.DrawText("USERS", layout.SideX + 24, y, Rgb(15, 23, 42), 4);
        y += 46;
        foreach (var row in rows.Take(5))
        {
            canvas.FillCircle(layout.SideX + 42, y + 12, 12, QualityColor(row.UserRssi, 255));
            canvas.DrawText($"{row.UserName} {row.UserRssi:F0} DBM", layout.SideX + 68, y, Rgb(15, 23, 42), 2);
            canvas.DrawText(row.QualityDisplay, layout.SideX + 68, y + 22, Rgb(71, 85, 105), 2);
            y += 56;
        }
    }

    private static void DrawDeltaSidePanel(SimplePngCanvas canvas, HeatmapResult before, HeatmapResult after, IReadOnlyList<ExperimentResultRow> rows, ExperimentImageLayout layout)
    {
        canvas.FillRect(layout.SideX, layout.SideY, layout.SideWidth, layout.SideHeight, Rgb(255, 255, 255));
        canvas.DrawRect(layout.SideX, layout.SideY, layout.SideWidth, layout.SideHeight, Rgb(203, 213, 225), 3);
        canvas.DrawText("DELTA LEGEND", layout.SideX + 24, layout.SideY + 24, Rgb(15, 23, 42), 4);
        var legend = new[]
        {
            ("+20 DB OR MORE", new BgraColor(65, 136, 22, 235)),
            ("+10 TO +20 DB", new BgraColor(86, 184, 34, 225)),
            ("+3 TO +10 DB", new BgraColor(134, 239, 74, 210)),
            ("-3 TO +3 DB", new BgraColor(180, 190, 203, 185)),
            ("WORSE", new BgraColor(49, 49, 224, 210))
        };
        var y = layout.SideY + 78;
        foreach (var item in legend)
        {
            canvas.FillRect(layout.SideX + 28, y, 54, 30, item.Item2 with { A = 255 });
            canvas.DrawRect(layout.SideX + 28, y, 54, 30, Rgb(71, 85, 105), 2);
            canvas.DrawText(item.Item1, layout.SideX + 96, y + 5, Rgb(15, 23, 42), 2);
            y += 46;
        }

        y += 18;
        canvas.DrawText("COMPARISON", layout.SideX + 24, y, Rgb(15, 23, 42), 4);
        y += 48;
        DrawMetric(canvas, layout.SideX + 26, ref y, "BEFORE AVG", $"{before.Stats.AverageRssiDbm:F1} DBM");
        DrawMetric(canvas, layout.SideX + 26, ref y, "AFTER AVG", $"{after.Stats.AverageRssiDbm:F1} DBM");
        DrawMetric(canvas, layout.SideX + 26, ref y, "AVG DELTA", $"{after.Stats.AverageRssiDbm - before.Stats.AverageRssiDbm:+0.0;-0.0;0.0} DB");

        foreach (var row in rows.Take(5))
        {
            DrawMetric(canvas, layout.SideX + 26, ref y, row.UserName.ToUpperInvariant(), $"{row.OptimizationDeltaDb:+0.0;-0.0;0.0} DB");
        }
    }

    private static void DrawMetric(SimplePngCanvas canvas, int x, ref int y, string label, string value)
    {
        canvas.DrawText(label, x, y, Rgb(100, 116, 139), 2);
        canvas.DrawText(value, x + 210, y - 2, Rgb(15, 23, 42), 3);
        y += 42;
    }

    private static (double X, double Y) ToImage(PlanPoint point, FloorPlan floor, ExperimentImageLayout layout) =>
        (
            layout.MapX + point.X / Math.Max(1, floor.WidthCm) * layout.MapWidth,
            layout.MapY + point.Y / Math.Max(1, floor.HeightCm) * layout.MapHeight
        );

    private static BgraColor DeltaColor(double deltaDb)
    {
        if (deltaDb >= 20) return new BgraColor(65, 136, 22, 235);
        if (deltaDb >= 10) return new BgraColor(86, 184, 34, 225);
        if (deltaDb >= 3) return new BgraColor(134, 239, 74, 210);
        if (deltaDb >= -3) return new BgraColor(180, 190, 203, 185);
        return new BgraColor(49, 49, 224, 210);
    }

    private static BgraColor QualityColor(double rssi, byte alpha) => HeatmapColorScale.ForRssi(rssi) with { A = alpha };

    private static bool MatchesCondition(ProjectModel project, ExperimentResultRow row) =>
        project.Name.Contains(row.ConditionName, StringComparison.OrdinalIgnoreCase);

    private static BgraColor Rgb(byte r, byte g, byte b, byte a = 255) => new(b, g, r, a);

    private static BgraColor ParseColor(string hex, byte alpha, BgraColor fallback)
    {
        if (hex.StartsWith('#') && hex.Length == 7
            && byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return new BgraColor(b, g, r, alpha);
        }

        return fallback;
    }

    private sealed record ExperimentImageLayout(
        int ImageWidth,
        int ImageHeight,
        int MapX,
        int MapY,
        int MapWidth,
        int MapHeight,
        int SideX,
        int SideY,
        int SideWidth,
        int SideHeight);

    private sealed class SimplePngCanvas
    {
        public SimplePngCanvas(int width, int height, BgraColor background)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 4];
            FillRect(0, 0, width, height, background);
        }

        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public void FillRect(int x, int y, int width, int height, BgraColor color)
        {
            var left = Math.Clamp(x, 0, Width);
            var top = Math.Clamp(y, 0, Height);
            var right = Math.Clamp(x + width, left, Width);
            var bottom = Math.Clamp(y + height, top, Height);
            for (var py = top; py < bottom; py++)
            {
                for (var px = left; px < right; px++)
                {
                    SetPixel(px, py, color);
                }
            }
        }

        public void BlendRect(int x, int y, int width, int height, BgraColor color)
        {
            var left = Math.Clamp(x, 0, Width);
            var top = Math.Clamp(y, 0, Height);
            var right = Math.Clamp(x + width, left, Width);
            var bottom = Math.Clamp(y + height, top, Height);
            for (var py = top; py < bottom; py++)
            {
                for (var px = left; px < right; px++)
                {
                    BlendPixel(px, py, color);
                }
            }
        }

        public void DrawRect(int x, int y, int width, int height, BgraColor color, int thickness)
        {
            FillRect(x, y, width, thickness, color);
            FillRect(x, y + height - thickness, width, thickness, color);
            FillRect(x, y, thickness, height, color);
            FillRect(x + width - thickness, y, thickness, height, color);
        }

        public void FillCircle(int centerX, int centerY, int radius, BgraColor color)
        {
            var r2 = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy <= r2)
                    {
                        BlendPixel(x, y, color);
                    }
                }
            }
        }

        public void DrawCircle(int centerX, int centerY, int radius, BgraColor color, int thickness)
        {
            var segments = 96;
            var previous = (X: centerX + radius, Y: centerY);
            for (var i = 1; i <= segments; i++)
            {
                var angle = i * Math.PI * 2.0 / segments;
                var current = (X: (int)Math.Round(centerX + Math.Cos(angle) * radius), Y: (int)Math.Round(centerY + Math.Sin(angle) * radius));
                DrawLine(previous.X, previous.Y, current.X, current.Y, color, thickness);
                previous = current;
            }
        }

        public void DrawPolygon(IReadOnlyList<(double X, double Y)> points, BgraColor color, int thickness)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                DrawLine((int)Math.Round(a.X), (int)Math.Round(a.Y), (int)Math.Round(b.X), (int)Math.Round(b.Y), color, thickness);
            }
        }

        public void FillPolygon(IReadOnlyList<(double X, double Y)> points, BgraColor color)
        {
            if (points.Count < 3)
            {
                return;
            }

            var minX = Math.Clamp((int)Math.Floor(points.Min(p => p.X)), 0, Width - 1);
            var maxX = Math.Clamp((int)Math.Ceiling(points.Max(p => p.X)), 0, Width - 1);
            var minY = Math.Clamp((int)Math.Floor(points.Min(p => p.Y)), 0, Height - 1);
            var maxY = Math.Clamp((int)Math.Ceiling(points.Max(p => p.Y)), 0, Height - 1);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (PointInPolygon(x + 0.5, y + 0.5, points))
                    {
                        BlendPixel(x, y, color);
                    }
                }
            }
        }

        public void DrawLine(int x1, int y1, int x2, int y2, BgraColor color, int thickness)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0)
            {
                FillCircle(x1, y1, Math.Max(1, thickness / 2), color);
                return;
            }

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (double)steps;
                var x = (int)Math.Round(x1 + dx * t);
                var y = (int)Math.Round(y1 + dy * t);
                FillCircle(x, y, Math.Max(1, thickness / 2), color);
            }
        }

        public void DrawText(string text, int x, int y, BgraColor color, int scale)
        {
            var cursor = x;
            foreach (var character in text.ToUpperInvariant())
            {
                if (character == '\n')
                {
                    y += 8 * scale;
                    cursor = x;
                    continue;
                }

                var glyph = Glyph(character);
                for (var gy = 0; gy < glyph.Length; gy++)
                {
                    for (var gx = 0; gx < glyph[gy].Length; gx++)
                    {
                        if (glyph[gy][gx] == '1')
                        {
                            FillRect(cursor + gx * scale, y + gy * scale, scale, scale, color);
                        }
                    }
                }

                cursor += 6 * scale;
            }
        }

        private void SetPixel(int x, int y, BgraColor color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            var offset = (y * Width + x) * 4;
            Pixels[offset] = color.B;
            Pixels[offset + 1] = color.G;
            Pixels[offset + 2] = color.R;
            Pixels[offset + 3] = color.A;
        }

        private void BlendPixel(int x, int y, BgraColor color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
            {
                return;
            }

            if (color.A == 255)
            {
                SetPixel(x, y, color);
                return;
            }

            var offset = (y * Width + x) * 4;
            var alpha = color.A / 255.0;
            Pixels[offset] = (byte)Math.Round(color.B * alpha + Pixels[offset] * (1.0 - alpha));
            Pixels[offset + 1] = (byte)Math.Round(color.G * alpha + Pixels[offset + 1] * (1.0 - alpha));
            Pixels[offset + 2] = (byte)Math.Round(color.R * alpha + Pixels[offset + 2] * (1.0 - alpha));
            Pixels[offset + 3] = 255;
        }

        private static bool PointInPolygon(double x, double y, IReadOnlyList<(double X, double Y)> polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];
                if (((pi.Y > y) != (pj.Y > y)) && x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static string[] Glyph(char character) => character switch
        {
            'A' => ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
            'B' => ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
            'C' => ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
            'D' => ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
            'E' => ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
            'F' => ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
            'G' => ["01111", "10000", "10000", "10011", "10001", "10001", "01111"],
            'H' => ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
            'I' => ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
            'J' => ["00111", "00010", "00010", "00010", "10010", "10010", "01100"],
            'K' => ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
            'L' => ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
            'M' => ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
            'N' => ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
            'O' => ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
            'P' => ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
            'Q' => ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
            'R' => ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
            'S' => ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
            'T' => ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
            'U' => ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
            'V' => ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
            'W' => ["10001", "10001", "10001", "10101", "10101", "10101", "01010"],
            'X' => ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
            'Y' => ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
            'Z' => ["11111", "00001", "00010", "00100", "01000", "10000", "11111"],
            '0' => ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
            '1' => ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
            '2' => ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
            '3' => ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
            '4' => ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
            '5' => ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
            '6' => ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
            '7' => ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
            '8' => ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
            '9' => ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
            '-' => ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
            '+' => ["00000", "00100", "00100", "11111", "00100", "00100", "00000"],
            '.' => ["00000", "00000", "00000", "00000", "00000", "01100", "01100"],
            ',' => ["00000", "00000", "00000", "00000", "00000", "01100", "01000"],
            ':' => ["00000", "01100", "01100", "00000", "01100", "01100", "00000"],
            '/' => ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
            '%' => ["11001", "11010", "00010", "00100", "01000", "01011", "10011"],
            '(' => ["00010", "00100", "01000", "01000", "01000", "00100", "00010"],
            ')' => ["01000", "00100", "00010", "00010", "00010", "00100", "01000"],
            '>' => ["10000", "01000", "00100", "00010", "00100", "01000", "10000"],
            '<' => ["00001", "00010", "00100", "01000", "00100", "00010", "00001"],
            '=' => ["00000", "11111", "00000", "11111", "00000", "00000", "00000"],
            ' ' => ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
            _ => ["11111", "10001", "00001", "00010", "00100", "00000", "00100"]
        };
    }
}
