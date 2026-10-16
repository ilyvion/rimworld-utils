namespace AssetPackBaker.Tests;

public class LoadFoldersTests
{
    [Theory]
    [InlineData("1.7", true, 1, 7)]
    [InlineData("1.7.4980", true, 1, 7)]
    [InlineData("v1.7", false, 0, 0)]
    [InlineData("1", false, 0, 0)]
    [InlineData("Common", false, 0, 0)]
    [InlineData("1.-1", false, 0, 0)]
    public void TryParseVersionReadsMajorAndMinorOnly(
        string text,
        bool parsed,
        int major,
        int minor
    )
    {
        Assert.Equal(parsed, LoadFolders.TryParseVersion(text, out var version));
        if (parsed)
        {
            Assert.Equal(new Version(major, minor), version);
        }
    }

    [Theory]
    [InlineData(new[] { "1.5", "1.6" }, new[] { "1.6" })]
    [InlineData(new[] { "1.5", "1.6", "1.7" }, new[] { "1.7" })]
    [InlineData(new[] { "1.6", "1.7", "1.8" }, new[] { "1.7", "1.8" })]
    [InlineData(new[] { "1.6", "1.8" }, new[] { "1.8", "1.6" })]
    [InlineData(new string[0], new string[0])]
    public void VersionsInRangeKeepsAssetPackVersionsAndNewestOlderWithoutExactFloor(
        string[] candidates,
        string[] expected
    )
    {
        var selected = LoadFolders
            .VersionsInRange([.. candidates.Select(c => (Version.Parse(c), c))])
            .Select(v => v.Item);
        Assert.Equal(expected, selected);
    }

    [Fact]
    public void AllUsesLoadFoldersXmlForAssetPackVersionsAndDefault()
    {
        using TempDirectory mod = new();
        _ = mod.File(
            "loadfolders.xml",
            """
            <loadFolders>
                <v1.5><li>Common</li><li>1.5</li></v1.5>
                <v1.6>
                    <li>Common</li>
                    <li>1.6</li>
                    <li IfModActive="some.mod">Integrations\Some\1.6</li>
                </v1.6>
                <!-- comment -->
                <default><li>/</li></default>
            </loadFolders>
            """
        );

        var folders = LoadFolders.All(mod.Path);

        Assert.Equal(
            [
                Path.Combine(mod.Path, "Common"),
                Path.Combine(mod.Path, "1.6"),
                Path.Combine(mod.Path, "Integrations", "Some", "1.6"),
                mod.Path,
            ],
            folders
        );
    }

    [Fact]
    public void AllWithoutLoadFoldersXmlUsesVersionFoldersCommonAndRoot()
    {
        using TempDirectory mod = new();
        _ = mod.Folder("1.5");
        _ = mod.Folder("1.6");
        _ = mod.Folder("1.7");
        _ = mod.Folder("Common");
        _ = mod.Folder("Source");

        var folders = LoadFolders.All(mod.Path);

        Assert.Equal(
            [Path.Combine(mod.Path, "1.7"), Path.Combine(mod.Path, "Common"), mod.Path],
            folders
        );
    }
}
