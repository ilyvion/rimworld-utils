using System.Buffers.Binary;

namespace AssetPackBaker.Tests;

public class TextureBakerTests
{
    private const int Identity = 0;
    private const int RedRedRedOne = 3 | (3 << 8) | (3 << 16) | (2 << 24);
    private const int RedZeroOneOne = 3 | (1 << 8) | (2 << 16) | (2 << 24);

    private static byte[] Pixels(params (byte R, byte G, byte B, byte A)[] pixels) =>
        [.. pixels.SelectMany(p => new[] { p.R, p.G, p.B, p.A })];

    [Fact]
    public void TrySingleChannelDetectsGray()
    {
        Assert.True(
            TextureBaker.TrySingleChannel(
                Pixels((10, 10, 10, 255), (200, 200, 200, 255)),
                out var swizzle
            )
        );
        Assert.Equal(RedRedRedOne, swizzle);
    }

    [Fact]
    public void TrySingleChannelUsesIdentityForRedOnly()
    {
        Assert.True(
            TextureBaker.TrySingleChannel(
                Pixels((10, 0, 0, 255), (200, 0, 0, 255)),
                out var swizzle
            )
        );
        Assert.Equal(Identity, swizzle);
    }

    [Fact]
    public void TrySingleChannelMapsConstantChannelsToZeroAndOne()
    {
        Assert.True(
            TextureBaker.TrySingleChannel(
                Pixels((10, 0, 255, 255), (200, 0, 255, 255)),
                out var swizzle
            )
        );
        Assert.Equal(RedZeroOneOne, swizzle);
    }

    [Fact]
    public void TrySingleChannelRejectsColor()
    {
        Assert.False(
            TextureBaker.TrySingleChannel(Pixels((10, 20, 30, 255), (40, 50, 60, 255)), out _)
        );
        Assert.False(
            TextureBaker.TrySingleChannel(Pixels((10, 7, 7, 255), (40, 7, 7, 255)), out _)
        );
    }

    [Fact]
    public void BleedTransparentPixelsSpreadsNearestVisibleColor()
    {
        // 3x1: visible red, transparent, transparent.
        var rgba = Pixels((255, 0, 0, 255), (0, 0, 255, 0), (0, 255, 0, 0));

        TextureBaker.BleedTransparentPixels(rgba, 3, 1);

        Assert.Equal(Pixels((255, 0, 0, 255), (255, 0, 0, 0), (255, 0, 0, 0)), rgba);
    }

    [Fact]
    public void BleedTransparentPixelsLeavesFullyTransparentImagesAlone()
    {
        var rgba = Pixels((1, 2, 3, 0), (4, 5, 6, 0));

        TextureBaker.BleedTransparentPixels(rgba, 2, 1);

        Assert.Equal(Pixels((1, 2, 3, 0), (4, 5, 6, 0)), rgba);
    }

    [Theory]
    [InlineData("a.png", new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, true)]
    [InlineData("a.png", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, false)]
    [InlineData("a.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData("a.PSD", new byte[] { (byte)'8', (byte)'B', (byte)'P', (byte)'S' }, true)]
    [InlineData("a.tga", new byte[] { 0, 0, 2, 0 }, true)]
    public void ExtensionMatchesContentChecksSignatures(string path, byte[] bytes, bool expected) =>
        Assert.Equal(expected, TextureBaker.ExtensionMatchesContent(path, bytes));

    [Fact]
    public void BakeKeepsSmallTexturesRawWithFullMipChain()
    {
        using TempDirectory dir = new();
        var path = dir.File("small.png", Images.Png(16, 8, (_, _) => (10, 20, 30, 255)));

        var texture = TextureBaker.Bake(path, borderMipMaps: false);

        Assert.Equal(
            (16, 8, 5, TextureFormat.Rgba32, true, 0),
            (
                texture.Width,
                texture.Height,
                texture.MipCount,
                texture.Format,
                texture.Opaque,
                texture.Swizzle
            )
        );
        Assert.Equal(
            DdsWriter.HeaderLength + (((16 * 8) + (8 * 4) + (4 * 2) + 2 + 1) * 4),
            texture.Dds.Length
        );
        // A uniform image stays uniform through the mip chain.
        Assert.All(
            texture.Dds.Skip(DdsWriter.HeaderLength).Chunk(4),
            p => Assert.Equal(new byte[] { 10, 20, 30, 255 }, p)
        );
    }

    [Fact]
    public void BakeUsesBc7ForTransparentTextures()
    {
        using TempDirectory dir = new();
        var path = dir.File(
            "icon.png",
            Images.Png(32, 32, (x, y) => (200, 50, 50, (byte)(x < 16 ? 255 : 0)))
        );

        var texture = TextureBaker.Bake(path, borderMipMaps: false);

        Assert.Equal(
            (TextureFormat.Bc7, false, 6),
            (texture.Format, texture.Opaque, texture.MipCount)
        );
        Assert.Equal(98u, BinaryPrimitives.ReadUInt32LittleEndian(texture.Dds.AsSpan(128)));
        Assert.Equal(
            (uint)DdsAlphaMode.Straight,
            BinaryPrimitives.ReadUInt32LittleEndian(texture.Dds.AsSpan(144))
        );
        Assert.Equal(DdsWriter.HeaderLength + (16 * (64 + 16 + 4 + 1 + 1 + 1)), texture.Dds.Length);
    }

    [Fact]
    public void BakeUsesBc4WithSwizzleForOpaqueGrayTextures()
    {
        using TempDirectory dir = new();
        var path = dir.File(
            "mask.png",
            Images.Png(32, 20, (x, y) => ((byte)(x * 8), (byte)(x * 8), (byte)(x * 8), 255))
        );

        var texture = TextureBaker.Bake(path, borderMipMaps: false);

        Assert.Equal(
            (TextureFormat.Bc4, true, RedRedRedOne),
            (texture.Format, texture.Opaque, texture.Swizzle)
        );
        Assert.Equal(80u, BinaryPrimitives.ReadUInt32LittleEndian(texture.Dds.AsSpan(128)));
        Assert.Equal(RedRedRedOne, BinaryPrimitives.ReadInt32LittleEndian(texture.Dds.AsSpan(32)));
        Assert.Equal(
            (uint)DdsAlphaMode.Opaque,
            BinaryPrimitives.ReadUInt32LittleEndian(texture.Dds.AsSpan(144))
        );
    }

    [Fact]
    public void BakeKeepsUncompressedSuffixRaw()
    {
        using TempDirectory dir = new();
        var path = dir.File("big_Uncompressed.png", Images.Png(64, 64, (x, y) => (1, 2, 3, 255)));

        Assert.Equal(TextureFormat.Rgba32, TextureBaker.Bake(path, borderMipMaps: false).Format);
    }

    [Fact]
    public void DecodeRoundsSixteenBitSamples()
    {
        // 0x00FF and 0x01C0 round to 1 and 2; dropping the low byte would give 0 and 1.
        var png = Images.Png16(1, 1, (_, _) => (0x00FF, 0x01C0, 0xFFFF, 0x8000));

        var (width, height, pixels) = TextureBaker.Decode(png);

        Assert.Equal((1, 1), (width, height));
        Assert.Equal(new byte[] { 1, 2, 255, 128 }, pixels);
    }

    [Fact]
    public void BakeEncodesBc7CloseToSource()
    {
        using TempDirectory dir = new();
        // Each 4x4 block's colors lie on a line in RGBA space, which one BC7 subset can follow.
        static (byte, byte, byte, byte) Pixel(int x, int y)
        {
            var t = (x * 4) + (y * 2);
            return ((byte)t, (byte)(t / 2), (byte)(255 - t), (byte)(128 + (t / 4)));
        }
        var path = dir.File("gradient.png", Images.Png(32, 32, Pixel));

        var texture = TextureBaker.Bake(path, borderMipMaps: false);

        Assert.Equal(TextureFormat.Bc7, texture.Format);
        AssertMip0CloseToSource(texture, 16, Pixel, Waylith.Bc7BlockEncoder.DecodeBc7Block);
    }

    [Fact]
    public void BakeEncodesBc4CloseToSource()
    {
        using TempDirectory dir = new();
        static (byte, byte, byte, byte) Pixel(int x, int y)
        {
            return ((byte)((x * 5) + (y * 3)), 0, 0, 255);
        }
        var path = dir.File("mask.png", Images.Png(32, 32, Pixel));

        var texture = TextureBaker.Bake(path, borderMipMaps: false);

        Assert.Equal(TextureFormat.Bc4, texture.Format);
        AssertMip0CloseToSource(
            texture,
            8,
            (x, y) => (Pixel(x, y).Item1, 0, 0, 255),
            Waylith.Bc7BlockEncoder.DecodeBc4Block
        );
    }

    private delegate void BlockDecoder(ReadOnlySpan<byte> block, Span<byte> rgba);

    private static void AssertMip0CloseToSource(
        BakedTexture texture,
        int blockBytes,
        Func<int, int, (byte R, byte G, byte B, byte A)> source,
        BlockDecoder decode
    )
    {
        var blocksWide = texture.Width / 4;
        var decoded = new byte[64];
        for (var by = 0; by < texture.Height / 4; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                var offset = DdsWriter.HeaderLength + (((by * blocksWide) + bx) * blockBytes);
                decode(texture.Dds.AsSpan(offset, blockBytes), decoded);
                for (var i = 0; i < 16; i++)
                {
                    var (r, g, b, a) = source((bx * 4) + (i % 4), (by * 4) + (i / 4));
                    byte[] expected = [r, g, b, a];
                    for (var c = 0; c < 4; c++)
                    {
                        Assert.InRange(decoded[(i * 4) + c], expected[c] - 4, expected[c] + 4);
                    }
                }
            }
        }
    }

    [Fact]
    public void BakeRejectsMismatchedContent()
    {
        using TempDirectory dir = new();
        var path = dir.File("fake.png", [0xFF, 0xD8, 0xFF, 0xE0, 0, 0]);

        _ = Assert.Throws<InvalidDataException>(() =>
            TextureBaker.Bake(path, borderMipMaps: false)
        );
    }
}
