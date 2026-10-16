using System.Buffers.Binary;

namespace AssetPackBaker;

internal enum TextureFormat
{
    Rgba32 = 4,
    Bc7 = 25,
    Bc4 = 26,
}

internal enum DdsAlphaMode : uint
{
    Straight = 1,
    Opaque = 3,
}

/// <summary>
/// Writes the DX10-header DDS files RimWorld reads from asset packs. The game keeps the
/// channel swizzle to sample a texture with in the first reserved header field.
/// </summary>
internal static class DdsWriter
{
    public const int HeaderLength = 148;

    private const uint Magic = 0x20534444; // "DDS "
    private const uint HeaderSize = 124;
    private const uint HeaderFlags = 0x000A1007; // caps, height, width, pixel format, mip count, linear size
    private const uint PixelFormatSize = 32;
    private const uint PixelFormatFourCc = 0x4;
    private const uint FourCcDx10 = 0x30315844; // "DX10"
    private const uint Caps = 0x00401008; // complex, texture, mip map
    private const uint ResourceDimensionTexture2D = 3;

    public static byte[] Write(
        int width,
        int height,
        TextureFormat format,
        byte[][] levels,
        DdsAlphaMode alphaMode,
        int swizzle
    )
    {
        var data = new byte[HeaderLength + levels.Sum(level => level.Length)];
        var header = data.AsSpan(0, HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], HeaderFlags);
        BinaryPrimitives.WriteInt32LittleEndian(header[12..], height);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], width);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], LevelLength(width, height, format));
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], levels.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[32..], swizzle);
        BinaryPrimitives.WriteUInt32LittleEndian(header[76..], PixelFormatSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header[80..], PixelFormatFourCc);
        BinaryPrimitives.WriteUInt32LittleEndian(header[84..], FourCcDx10);
        BinaryPrimitives.WriteUInt32LittleEndian(header[108..], Caps);
        BinaryPrimitives.WriteUInt32LittleEndian(header[128..], DxgiFormat(format));
        BinaryPrimitives.WriteUInt32LittleEndian(header[132..], ResourceDimensionTexture2D);
        BinaryPrimitives.WriteUInt32LittleEndian(header[140..], 1); // array size
        BinaryPrimitives.WriteUInt32LittleEndian(header[144..], (uint)alphaMode);

        var offset = HeaderLength;
        foreach (var level in levels)
        {
            level.CopyTo(data, offset);
            offset += level.Length;
        }
        return data;
    }

    public static int LevelLength(int width, int height, TextureFormat format) =>
        format switch
        {
            TextureFormat.Rgba32 => width * height * 4,
            TextureFormat.Bc4 => (width + 3) / 4 * ((height + 3) / 4) * 8,
            TextureFormat.Bc7 => (width + 3) / 4 * ((height + 3) / 4) * 16,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

    private static uint DxgiFormat(TextureFormat format) =>
        format switch
        {
            TextureFormat.Rgba32 => 28, // R8G8B8A8_UNORM
            TextureFormat.Bc4 => 80, // BC4_UNORM
            TextureFormat.Bc7 => 98, // BC7_UNORM
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
}
