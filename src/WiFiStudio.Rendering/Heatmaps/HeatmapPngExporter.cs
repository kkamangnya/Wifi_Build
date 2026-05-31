using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
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
        await File.WriteAllBytesAsync(path, EncodePng(raster), cancellationToken).ConfigureAwait(false);
    }

    private static byte[] EncodePng(HeatmapRaster raster)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(output, "IHDR", BuildIhdr(raster.Width, raster.Height));
        WriteChunk(output, "IDAT", BuildImageData(raster));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var data = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4, 4), height);
        data[8] = 8;
        data[9] = 6;
        return data;
    }

    private static byte[] BuildImageData(HeatmapRaster raster)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < raster.Height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < raster.Width; x++)
            {
                var source = (y * raster.Width + x) * 4;
                raw.WriteByte(raster.BgraPixels[source + 2]);
                raw.WriteByte(raster.BgraPixels[source + 1]);
                raw.WriteByte(raster.BgraPixels[source]);
                raw.WriteByte(raster.BgraPixels[source + 3]);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            raw.Position = 0;
            raw.CopyTo(zlib);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = Crc32(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xffffffffu;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            }
        }

        return crc ^ 0xffffffffu;
    }
}
