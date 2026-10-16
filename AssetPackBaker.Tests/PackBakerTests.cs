using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace AssetPackBaker.Tests;

public class PackBakerTests
{
    [Fact]
    public void BakeWritesTexturesSoundsManifestAndTextureInfo()
    {
        using TempDirectory folder = new();
        var gray = Images.Png(32, 32, (x, _) => ((byte)x, (byte)x, (byte)x, 255));
        _ = folder.File("Textures/Things/A.png", gray);
        _ = folder.File("Textures/Things/B.png", gray);
        _ = folder.File(
            "Textures/UI/Icon.png",
            Images.Png(64, 64, (x, _) => (9, 99, 199, (byte)(x * 4)))
        );
        _ = folder.File("Textures/UI/Tiny.png", Images.Png(8, 8, (_, _) => (1, 2, 3, 255)));
        _ = folder.File("Textures/UI/Notes.txt", "not a texture");
        _ = folder.File("Sounds/Hit.ogg", [1, 2, 3, 4]);
        _ = folder.File("Sounds/Sub/Ding.wav", [5, 6, 7, 8, 5, 6, 7, 8]);
        _ = folder.File("Sounds/Hit.ogg.meta", "ignored");
        _ = folder.File("Assets.zip.stamps", "stale");

        var result = PackBaker.Bake(folder.Path);

        Assert.True(result.Succeeded);
        Assert.Equal((6, 1), (result.Entries, result.Deduplicated));
        Assert.False(File.Exists(Path.Combine(folder.Path, "Assets.zip.stamps")));
        Assert.False(File.Exists(Path.Combine(folder.Path, "Assets.zip.tmp")));

        PackReader pack = new(Path.Combine(folder.Path, "Assets.zip"));
        // B duplicates A's content, so only the ordinally first name gets an entry.
        Assert.Equal(
            [
                "Sounds/Hit.ogg",
                "Sounds/Sub/Ding.wav",
                "Textures/Things/A.png.dds",
                "Textures/UI/Icon.png.dds",
                "Textures/UI/Tiny.png.dds",
                "bake.manifest",
                "textures.info",
            ],
            pack.Entries.Keys
        );
        Assert.Equal(0, pack.Entries["Sounds/Hit.ogg"].Method);
        Assert.Equal(93, pack.Entries["Sounds/Sub/Ding.wav"].Method);
        Assert.Equal(93, pack.Entries["Textures/UI/Icon.png.dds"].Method);
        Assert.Equal(
            new byte[] { 5, 6, 7, 8, 5, 6, 7, 8 },
            pack.Entries["Sounds/Sub/Ding.wav"].Content
        );
        foreach (var entry in pack.Entries.Values)
        {
            Assert.Equal(
                BinaryPrimitives.ReadUInt32LittleEndian(Crc32.Hash(entry.Content)),
                entry.Crc
            );
        }

        var manifest = pack.Manifest;
        Assert.Equal(6, manifest.Count);
        Assert.Equal(manifest["Textures/Things/A.png.dds"], manifest["Textures/Things/B.png.dds"]);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(pack.Entries["Textures/UI/Icon.png.dds"].Content)),
            manifest["Textures/UI/Icon.png.dds"]
        );

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Textures/Things/A.png.dds"] =
                    $"32 32 6 26 1 {3 | (3 << 8) | (3 << 16) | (2 << 24)}",
                ["Textures/Things/B.png.dds"] =
                    $"32 32 6 26 1 {3 | (3 << 8) | (3 << 16) | (2 << 24)}",
                ["Textures/UI/Icon.png.dds"] = "64 64 7 25 0 0",
                ["Textures/UI/Tiny.png.dds"] = "8 8 4 4 1 0",
            },
            pack.TextureInfo
        );
    }

    [Fact]
    public void BakeIsDeterministic()
    {
        using TempDirectory folder = new();
        _ = folder.File(
            "Textures/A.png",
            Images.Png(48, 40, (x, y) => ((byte)(x * 5), (byte)(y * 6), 7, (byte)(x + y)))
        );
        _ = folder.File("Textures/B.png", Images.Png(24, 24, (x, y) => ((byte)(x * y), 0, 0, 255)));
        var pack = Path.Combine(folder.Path, "Assets.zip");

        _ = PackBaker.Bake(folder.Path);
        var first = File.ReadAllBytes(pack);
        _ = PackBaker.Bake(folder.Path);

        Assert.Equal(first, File.ReadAllBytes(pack));
    }

    [Fact]
    public void BakeFailsWithoutWritingAPackWhenATextureIsBroken()
    {
        using TempDirectory folder = new();
        _ = folder.File("Textures/Good.png", Images.Png(4, 4, (_, _) => (1, 1, 1, 255)));
        _ = folder.File("Textures/Broken.png", [0x89, (byte)'P', (byte)'N', (byte)'G', 0, 0]);

        var result = PackBaker.Bake(folder.Path);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Broken.png", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(folder.Path, "Assets.zip")));
    }

    [Fact]
    public void TextureSourcesSkipPsdShadowedBySibling()
    {
        using TempDirectory folder = new();
        _ = folder.File("Textures/A.psd", "8BPS");
        _ = folder.File("Textures/A.png", "");
        _ = folder.File("Textures/B.psd", "8BPS");
        _ = folder.File("Textures/C.obj", "");

        var sources = PackBaker.TextureSources(folder.Path).Select(Path.GetFileName).Order();

        Assert.Equal(["A.png", "B.psd"], sources);
    }

    [Fact]
    public void BakeAppliesBorderMipMapsFromDefs()
    {
        using TempDirectory folder = new();
        _ = folder.File(
            "Defs/Terrain.xml",
            """
            <Defs>
                <ThingDef>
                    <graphicData><texPath>Things\Wall</texPath><borderMipMaps>true</borderMipMaps></graphicData>
                </ThingDef>
                <ThingDef>
                    <graphicData><texPath>Things/Other</texPath><borderMipMaps>false</borderMipMaps></graphicData>
                </ThingDef>
            </Defs>
            """
        );

        var border = BorderMipMaps.For(folder.Path);

        Assert.True(border.Wants(Path.Combine(folder.Path, "Textures", "Things", "Wall.png")));
        Assert.False(border.Wants(Path.Combine(folder.Path, "Textures", "Things", "Other.png")));
        Assert.False(border.Wants(Path.Combine(folder.Path, "Textures", "Things", "Wall2.png")));
    }
}
