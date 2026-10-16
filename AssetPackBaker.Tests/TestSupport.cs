using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using StbImageWriteSharp;
using ZstdSharp;

namespace AssetPackBaker.Tests;

/// <summary>A temporary directory deleted on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } =
        Directory.CreateTempSubdirectory("AssetPackBaker.Tests.").FullName;

    public string File(string relativePath, byte[] content)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    public string File(string relativePath, string content) =>
        File(relativePath, Encoding.UTF8.GetBytes(content));

    public string Folder(string relativePath) =>
        Directory.CreateDirectory(System.IO.Path.Combine(Path, relativePath)).FullName;

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

internal static class Images
{
    public static byte[] Png(
        int width,
        int height,
        Func<int, int, (byte R, byte G, byte B, byte A)> pixel
    )
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                var i = ((y * width) + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (r, g, b, a);
            }
        }
        using MemoryStream stream = new();
        new ImageWriter().WritePng(rgba, width, height, ColorComponents.RedGreenBlueAlpha, stream);
        return stream.ToArray();
    }

    /// <summary>A PNG with 16 bits per RGBA channel.</summary>
    public static byte[] Png16(
        int width,
        int height,
        Func<int, int, (ushort R, ushort G, ushort B, ushort A)> pixel
    )
    {
        using MemoryStream raw = new();
        Span<byte> samples = stackalloc byte[8];
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter: none
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                BinaryPrimitives.WriteUInt16BigEndian(samples, r);
                BinaryPrimitives.WriteUInt16BigEndian(samples[2..], g);
                BinaryPrimitives.WriteUInt16BigEndian(samples[4..], b);
                BinaryPrimitives.WriteUInt16BigEndian(samples[6..], a);
                raw.Write(samples);
            }
        }
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            raw.WriteTo(zlib);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 16; // bit depth
        header[9] = 6; // color type: RGBA

        using MemoryStream png = new();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(field, data.Length);
        png.Write(field);
        png.Write(type);
        png.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(field, Crc32.HashToUInt32([.. type, .. data]));
        png.Write(field);
    }
}

/// <summary>Reads the entries of an asset pack written by <see cref="AssetPackWriter"/>.</summary>
internal sealed class PackReader
{
    internal sealed record Entry(ushort Method, uint Crc, byte[] Content);

    public Dictionary<string, Entry> Entries { get; } = new(StringComparer.Ordinal);

    public PackReader(string path)
    {
        using Decompressor decompressor = new();
        var data = File.ReadAllBytes(path);
        var end = data.Length - 22;
        Assert.Equal(0x06054B50u, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(end)));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(end + 10));
        var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(end + 16));
        for (var i = 0; i < count; i++)
        {
            var header = data.AsSpan(offset);
            Assert.Equal(0x02014B50u, BinaryPrimitives.ReadUInt32LittleEndian(header));
            var method = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
            var compressed = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
            int extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            var localOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(header[42..]);
            var name = Encoding.UTF8.GetString(header.Slice(46, nameLength));

            var local = data.AsSpan(localOffset);
            Assert.Equal(0x04034B50u, BinaryPrimitives.ReadUInt32LittleEndian(local));
            int localName = BinaryPrimitives.ReadUInt16LittleEndian(local[26..]);
            int localExtra = BinaryPrimitives.ReadUInt16LittleEndian(local[28..]);
            var payload = local.Slice(30 + localName + localExtra, compressed).ToArray();
            var content = method == 93 ? decompressor.Unwrap(payload).ToArray() : payload;
            Assert.Equal(length, content.Length);
            Entries.Add(name, new Entry(method, crc, content));
            offset += 46 + nameLength + extraLength;
        }
    }

    /// <summary>Entry name to content hash.</summary>
    public Dictionary<string, string> Manifest =>
        Lines(AssetPackWriter.ManifestEntryName)
            .ToDictionary(
                line => line[..line.LastIndexOf(' ')],
                line => line[(line.LastIndexOf(' ') + 1)..]
            );

    /// <summary>Texture entry name to "width height mips format opaque swizzle".</summary>
    public Dictionary<string, string> TextureInfo =>
        Lines(AssetPackWriter.TextureInfoEntryName)
            .Select(line => line.Split(' '))
            .ToDictionary(
                parts => string.Join(' ', parts[..^6]),
                parts => string.Join(' ', parts[^6..])
            );

    private string[] Lines(string entry) =>
        Encoding
            .UTF8.GetString(Entries[entry].Content)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
