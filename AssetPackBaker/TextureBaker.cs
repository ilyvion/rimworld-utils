using System.Runtime.InteropServices;
using AssetPackBaker.Waylith;
using StbImageSharp;

namespace AssetPackBaker;

internal readonly record struct BakedTexture(
    byte[] Dds,
    int Width,
    int Height,
    int MipCount,
    TextureFormat Format,
    bool Opaque,
    int Swizzle
);

/// <summary>
/// Turns a source image into the DDS entry RimWorld expects in a mod asset pack: a full mip
/// chain, kept as raw RGBA32 when at most <see cref="RawMaxSize"/> on both sides or named
/// <c>*_Uncompressed</c>, BC4 when opaque and carrying a single channel, otherwise BC7.
/// </summary>
internal static class TextureBaker
{
    public const int RawMaxSize = 16;

    private const int BlockRowsPerStrip = 8;

    private const byte ChannelZero = 1;
    private const byte ChannelOne = 2;
    private const byte ChannelRed = 3;

    public static BakedTexture Bake(string path, bool borderMipMaps)
    {
        var bytes = File.ReadAllBytes(path);
        if (!ExtensionMatchesContent(path, bytes))
        {
            throw new InvalidDataException("the file contents do not match its extension");
        }

        var (width, height, pixels) = Decode(bytes);
        BleedTransparentPixels(pixels, width, height);
        var opaque = IsOpaque(pixels);
        var levels = MipChain.Build(pixels, width, height, borderMipMaps);

        var swizzle = 0;
        TextureFormat format;
        if (
            (width <= RawMaxSize && height <= RawMaxSize)
            || Path.GetFileNameWithoutExtension(path)
                .EndsWith("_Uncompressed", StringComparison.OrdinalIgnoreCase)
        )
        {
            format = TextureFormat.Rgba32;
        }
        else
        {
            format =
                opaque && TrySingleChannel(pixels, out swizzle)
                    ? TextureFormat.Bc4
                    : TextureFormat.Bc7;
            levels = Encode(levels, width, height, format);
        }

        var dds = DdsWriter.Write(
            width,
            height,
            format,
            levels,
            opaque ? DdsAlphaMode.Opaque : DdsAlphaMode.Straight,
            swizzle
        );
        return new BakedTexture(dds, width, height, levels.Length, format, opaque, swizzle);
    }

    /// <summary>
    /// Decodes an image to RGBA32. Sources with 16 bits per channel are narrowed to 8 bits with
    /// rounding, as the game does.
    /// </summary>
    internal static unsafe (int Width, int Height, byte[] Pixels) Decode(byte[] bytes)
    {
        using (MemoryStream probe = new(bytes))
        {
            if (StbImage.stbi__is_16_main(new StbImage.stbi__context(probe)) == 0)
            {
                var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
                return (image.Width, image.Height, image.Data);
            }
        }

        using MemoryStream stream = new(bytes);
        int width,
            height,
            components;
        var samples = StbImage.stbi__load_and_postprocess_16bit(
            new StbImage.stbi__context(stream),
            &width,
            &height,
            &components,
            (int)ColorComponents.RedGreenBlueAlpha
        );
        if (samples == null)
        {
            throw new InvalidDataException(StbImage.stbi__g_failure_reason);
        }
        try
        {
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)(((samples[i] * 255) + 32767) / 65535);
            }
            return (width, height, pixels);
        }
        finally
        {
            Marshal.FreeHGlobal((nint)samples);
        }
    }

    /// <summary>
    /// PNG, JPEG and PSD sources must start with their format's signature; other extensions
    /// aren't checked.
    /// </summary>
    internal static bool ExtensionMatchesContent(string path, ReadOnlySpan<byte> bytes) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => bytes.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']),
            ".jpg" or ".jpeg" => bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
            ".psd" => bytes.StartsWith("8BPS"u8),
            _ => true,
        };

    /// <summary>
    /// Copies the color of visible pixels outward into fully transparent ones, breadth-first,
    /// so filtering and block compression don't pull in whatever color hides under alpha 0.
    /// </summary>
    internal static void BleedTransparentPixels(byte[] rgba, int width, int height)
    {
        var count = width * height;
        var filled = new bool[count];
        var queue = new int[count];
        var tail = 0;
        for (var i = 0; i < count; i++)
        {
            if (rgba[(i * 4) + 3] != 0)
            {
                filled[i] = true;
                queue[tail++] = i;
            }
        }
        if (tail == 0 || tail == count)
        {
            return;
        }

        for (var head = 0; head < tail; head++)
        {
            var index = queue[head];
            var y = index / width;
            var x = index - (y * width);
            if (x > 0)
            {
                Bleed(index, index - 1);
            }
            if (x < width - 1)
            {
                Bleed(index, index + 1);
            }
            if (y > 0)
            {
                Bleed(index, index - width);
            }
            if (y < height - 1)
            {
                Bleed(index, index + width);
            }
        }

        void Bleed(int from, int to)
        {
            if (filled[to])
            {
                return;
            }
            rgba[to * 4] = rgba[from * 4];
            rgba[(to * 4) + 1] = rgba[(from * 4) + 1];
            rgba[(to * 4) + 2] = rgba[(from * 4) + 2];
            filled[to] = true;
            queue[tail++] = to;
        }
    }

    internal static bool IsOpaque(byte[] rgba)
    {
        for (var i = 3; i < rgba.Length; i += 4)
        {
            if (rgba[i] != 255)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Whether green and blue each either repeat red or hold 0 or 255 throughout, so the
    /// texture fits a single-channel BC4 texture plus a swizzle. The swizzle packs one
    /// channel selector per byte (R, G, B, A from the low byte up) and is 0 (identity) when
    /// green and blue are both 0.
    /// </summary>
    internal static bool TrySingleChannel(ReadOnlySpan<byte> rgba, out int swizzle)
    {
        swizzle = 0;
        if (rgba.Length < 4)
        {
            return false;
        }
        bool greenIsRed = true,
            blueIsRed = true,
            greenFixed = true,
            blueFixed = true;
        var firstGreen = rgba[1];
        var firstBlue = rgba[2];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            greenIsRed &= rgba[i + 1] == rgba[i];
            blueIsRed &= rgba[i + 2] == rgba[i];
            greenFixed &= rgba[i + 1] == firstGreen;
            blueFixed &= rgba[i + 2] == firstBlue;
            if (!(greenIsRed || greenFixed) || !(blueIsRed || blueFixed))
            {
                return false;
            }
        }
        if (
            ChannelFor(greenIsRed, greenFixed, firstGreen) is not { } green
            || ChannelFor(blueIsRed, blueFixed, firstBlue) is not { } blue
        )
        {
            return false;
        }
        if (green != ChannelZero || blue != ChannelZero)
        {
            swizzle = ChannelRed | (green << 8) | (blue << 16) | (ChannelOne << 24);
        }
        return true;
    }

    private static byte? ChannelFor(bool isRed, bool isFixed, byte value) =>
        isRed ? ChannelRed
        : isFixed && value == 0 ? ChannelZero
        : isFixed && value == 255 ? ChannelOne
        : null;

    /// <summary>
    /// Block-compresses every level in strips of <see cref="BlockRowsPerStrip"/> block rows,
    /// encoding the strips of all levels in parallel.
    /// </summary>
    private static byte[][] Encode(byte[][] levels, int width, int height, TextureFormat format)
    {
        var blockBytes = format == TextureFormat.Bc4 ? 8 : 16;
        var encoded = new byte[levels.Length][];
        List<(int Level, int FirstBlockRow, int BlockRowCount)> strips = [];
        for (var level = 0; level < levels.Length; level++)
        {
            var blocksWide = (MipChain.LevelSize(width, level) + 3) / 4;
            var blocksHigh = (MipChain.LevelSize(height, level) + 3) / 4;
            encoded[level] = new byte[blocksWide * blocksHigh * blockBytes];
            for (var row = 0; row < blocksHigh; row += BlockRowsPerStrip)
            {
                strips.Add((level, row, Math.Min(BlockRowsPerStrip, blocksHigh - row)));
            }
        }

        _ = Parallel.ForEach(
            strips,
            strip =>
                EncodeStrip(
                    levels[strip.Level],
                    MipChain.LevelSize(width, strip.Level),
                    MipChain.LevelSize(height, strip.Level),
                    strip.FirstBlockRow,
                    strip.BlockRowCount,
                    format,
                    encoded[strip.Level],
                    blockBytes
                )
        );
        return encoded;
    }

    private static unsafe void EncodeStrip(
        byte[] rgba,
        int width,
        int height,
        int firstBlockRow,
        int blockRowCount,
        TextureFormat format,
        byte[] output,
        int blockBytes
    )
    {
        var offset = (long)firstBlockRow * ((width + 3) / 4) * blockBytes;
        fixed (byte* source = rgba)
        fixed (byte* destination = output)
        {
            if (format == TextureFormat.Bc4)
            {
                Bc4BlockEncoder.EncodeRows(
                    source,
                    width,
                    height,
                    width * 4,
                    firstBlockRow,
                    blockRowCount,
                    destination + offset
                );
            }
            else
            {
                Bc7BlockEncoder.EncodeRows(
                    source,
                    width,
                    height,
                    width * 4,
                    firstBlockRow,
                    blockRowCount,
                    Bc7PackFlags.WaylithDefault,
                    destination + offset
                );
            }
        }
    }
}
