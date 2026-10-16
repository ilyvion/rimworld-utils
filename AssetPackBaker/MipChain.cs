using AssetPackBaker.Waylith;

namespace AssetPackBaker;

/// <summary>
/// Builds full RGBA32 mip chains with RimWorld's Kaiser-windowed sinc filter. With border
/// mipmaps, every level is downsampled first, and then each level's outermost texel ring is
/// rebuilt from the already-rebuilt level above.
/// </summary>
internal static class MipChain
{
    /// <summary>The number of levels down to 1x1.</summary>
    public static int LevelCount(int width, int height)
    {
        var count = 1;
        for (var size = Math.Max(Math.Max(1, width), height); size > 1; size >>= 1)
        {
            count++;
        }
        return count;
    }

    public static byte[][] Build(byte[] level0, int width, int height, bool borderMipMaps)
    {
        var count = LevelCount(width, height);
        var levels = new byte[count][];
        levels[0] = level0;
        for (var level = 1; level < count; level++)
        {
            var levelWidth = LevelSize(width, level);
            var levelHeight = LevelSize(height, level);
            levels[level] = new byte[levelWidth * levelHeight * 4];
            KaiserMipFilter.Downsample(
                levels[level - 1],
                LevelSize(width, level - 1),
                LevelSize(height, level - 1),
                levels[level],
                levelWidth,
                levelHeight
            );
        }
        if (borderMipMaps)
        {
            for (var level = 1; level < count; level++)
            {
                CopyBorder(
                    levels[level - 1],
                    LevelSize(width, level - 1),
                    LevelSize(height, level - 1),
                    levels[level],
                    LevelSize(width, level),
                    LevelSize(height, level)
                );
            }
        }
        return levels;
    }

    public static int LevelSize(int size, int level) => Math.Max(1, size >> level);

    /// <summary>
    /// Rebuilds the outermost texel ring of a level from the matching edge texel pairs of the
    /// level above, so tiling edges don't blend in texels from the opposite side.
    /// </summary>
    private static void CopyBorder(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        byte[] destination,
        int width,
        int height
    )
    {
        for (var x = 0; x < width; x++)
        {
            var x0 = x * 2;
            var x1 = Math.Min(x0 + 1, sourceWidth - 1);
            AveragePair(source, sourceWidth, x0, 0, x1, 0, destination, width, x, 0);
            AveragePair(
                source,
                sourceWidth,
                x0,
                sourceHeight - 1,
                x1,
                sourceHeight - 1,
                destination,
                width,
                x,
                height - 1
            );
        }
        for (var y = 0; y < height; y++)
        {
            var y0 = y * 2;
            var y1 = Math.Min(y0 + 1, sourceHeight - 1);
            AveragePair(source, sourceWidth, 0, y0, 0, y1, destination, width, 0, y);
            AveragePair(
                source,
                sourceWidth,
                sourceWidth - 1,
                y0,
                sourceWidth - 1,
                y1,
                destination,
                width,
                width - 1,
                y
            );
        }
    }

    private static void AveragePair(
        byte[] source,
        int sourceWidth,
        int ax,
        int ay,
        int bx,
        int by,
        byte[] destination,
        int width,
        int x,
        int y
    )
    {
        var a = ((ay * sourceWidth) + ax) * 4;
        var b = ((by * sourceWidth) + bx) * 4;
        var d = ((y * width) + x) * 4;
        for (var c = 0; c < 4; c++)
        {
            destination[d + c] = (byte)((source[a + c] + source[b + c] + 1) >> 1);
        }
    }
}
