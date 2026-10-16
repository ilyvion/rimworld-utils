namespace AssetPackBaker.Tests;

public class MipChainTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(16, 16, 5)]
    [InlineData(17, 16, 5)]
    [InlineData(512, 512, 10)]
    [InlineData(192, 448, 9)]
    [InlineData(1, 10, 4)]
    public void LevelCountGoesDownToOneByOne(int width, int height, int expected) =>
        Assert.Equal(expected, MipChain.LevelCount(width, height));

    [Fact]
    public void BuildHalvesEachLevelAndAveragesInLinearLight()
    {
        // Alternating black and white columns average to linear 0.5, which is sRGB 188.
        var level0 = new byte[4 * 4 * 4];
        for (var i = 0; i < 16; i++)
        {
            var value = (byte)(i % 2 == 0 ? 0 : 255);
            (level0[i * 4], level0[(i * 4) + 1], level0[(i * 4) + 2], level0[(i * 4) + 3]) = (
                value,
                value,
                value,
                255
            );
        }

        var levels = MipChain.Build(level0, 4, 4, borderMipMaps: false);

        Assert.Equal([64, 16, 4], levels.Select(l => l.Length));
        Assert.All(levels[2].Chunk(4), p => Assert.InRange(p[0], 186, 190));
        Assert.All(levels[2].Chunk(4), p => Assert.Equal(255, p[3]));
    }

    [Fact]
    public void BuildWithBorderMipMapsAveragesEdgeTexelPairs()
    {
        // 8x8, white except the top row; texel (1, 0) of the 4x4 level is the average of top-row
        // texels (2, 0) and (3, 0) alone.
        var level0 = Enumerable.Repeat((byte)255, 8 * 8 * 4).ToArray();
        level0[2 * 4] = 100;
        level0[3 * 4] = 201;

        var levels = MipChain.Build(level0, 8, 8, borderMipMaps: true);

        Assert.Equal(151, levels[1][1 * 4]);
        Assert.Equal(255, levels[1][(1 * 4) + 1]);
    }

    [Fact]
    public void BuildWithBorderMipMapsKeepsInteriorTexelsOfThePlainChain()
    {
        // 16x16, black top row on white; border texels change, but every level's interior is
        // still downsampled from the plain level above.
        var level0 = Enumerable.Repeat((byte)255, 16 * 16 * 4).ToArray();
        for (var x = 0; x < 16; x++)
        {
            (level0[x * 4], level0[(x * 4) + 1], level0[(x * 4) + 2]) = (0, 0, 0);
        }

        var plain = MipChain.Build([.. level0], 16, 16, borderMipMaps: false);
        var bordered = MipChain.Build([.. level0], 16, 16, borderMipMaps: true);

        Assert.NotEqual(plain[1][..4], bordered[1][..4]);
        for (var level = 1; level < plain.Length; level++)
        {
            var size = MipChain.LevelSize(16, level);
            for (var y = 1; y < size - 1; y++)
            {
                for (var x = 1; x < size - 1; x++)
                {
                    var i = ((y * size) + x) * 4;
                    Assert.Equal(plain[level][i..(i + 4)], bordered[level][i..(i + 4)]);
                }
            }
        }
    }
}
