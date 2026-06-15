using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace WiFiStudio.Rendering.Heatmaps;

public static class PngImageEncoder
{
    public static byte[] EncodeBgra(int width, int height, byte[] bgraPixels)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "PNG dimensions must be positive.");
        }

        if (bgraPixels.Length != width * height * 4)
        {
            throw new ArgumentException("BGRA buffer size does not match the requested PNG dimensions.", nameof(bgraPixels));
        }

        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(output, "IHDR", BuildIhdr(width, height));
        WriteChunk(output, "IDAT", BuildImageData(width, height, bgraPixels));
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

    private static byte[] BuildImageData(int width, int height, byte[] bgraPixels)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                var source = (y * width + x) * 4;
                raw.WriteByte(bgraPixels[source + 2]);
                raw.WriteByte(bgraPixels[source + 1]);
                raw.WriteByte(bgraPixels[source]);
                raw.WriteByte(bgraPixels[source + 3]);
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
