using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

public sealed record HeatmapRaster(int Width, int Height, byte[] BgraPixels);

public static class HeatmapRasterizer
{
    public static HeatmapRaster Rasterize(HeatmapResult result, double floorWidthCm, double floorHeightCm, int widthPx, int heightPx)
    {
        widthPx = Math.Max(1, widthPx);
        heightPx = Math.Max(1, heightPx);
        var pixels = new byte[widthPx * heightPx * 4];

        if (result.Samples.Count == 0 || floorWidthCm <= 0 || floorHeightCm <= 0)
        {
            return new HeatmapRaster(widthPx, heightPx, pixels);
        }

        foreach (var sample in result.Samples)
        {
            var color = HeatmapColorScale.ForSample(sample, result.Settings.HeatmapType);
            var halfCell = result.CellSizeCm / 2.0;
            var left = (int)Math.Floor((sample.X - halfCell) / floorWidthCm * widthPx);
            var top = (int)Math.Floor((sample.Y - halfCell) / floorHeightCm * heightPx);
            var right = (int)Math.Ceiling((sample.X + halfCell) / floorWidthCm * widthPx);
            var bottom = (int)Math.Ceiling((sample.Y + halfCell) / floorHeightCm * heightPx);
            FillRect(pixels, widthPx, heightPx, left, top, right, bottom, color);
        }

        return new HeatmapRaster(widthPx, heightPx, pixels);
    }

    private static void FillRect(byte[] pixels, int width, int height, int left, int top, int right, int bottom, BgraColor color)
    {
        left = Math.Clamp(left, 0, width - 1);
        top = Math.Clamp(top, 0, height - 1);
        right = Math.Clamp(right, left + 1, width);
        bottom = Math.Clamp(bottom, top + 1, height);

        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * width * 4;
            for (var x = left; x < right; x++)
            {
                var offset = rowOffset + x * 4;
                pixels[offset] = color.B;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.R;
                pixels[offset + 3] = color.A;
            }
        }
    }
}
