using WiFiStudio.Core.Models;

namespace WiFiStudio.Rendering.Heatmaps;

public static class HeatmapPngExporter
{
    public static async Task ExportAsync(
        HeatmapResult result,
        double floorWidthCm,
        double floorHeightCm,
        string path,
        int widthPx = 1200,
        CancellationToken cancellationToken = default)
    {
        var heightPx = Math.Max(1, (int)Math.Round(widthPx * floorHeightCm / Math.Max(1, floorWidthCm)));
        var raster = HeatmapRasterizer.Rasterize(result, floorWidthCm, floorHeightCm, widthPx, heightPx);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllBytesAsync(
            path,
            PngImageEncoder.EncodeBgra(raster.Width, raster.Height, raster.BgraPixels),
            cancellationToken).ConfigureAwait(false);
    }
}
