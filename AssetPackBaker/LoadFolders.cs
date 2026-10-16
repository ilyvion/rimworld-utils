using System.Globalization;
using System.Xml;

namespace AssetPackBaker;

/// <summary>
/// Picks the load folders of a mod that RimWorld 1.7+ looks for an asset pack in.
/// </summary>
internal static class LoadFolders
{
    public static readonly Version FirstAssetPackVersion = new(1, 7);

    private const string CommonFolderName = "Common";

    /// <summary>
    /// Every load folder the game can load for 1.7 or later: all folders listed for those
    /// versions in LoadFolders.xml plus the newest version below 1.7 when 1.7 itself has no
    /// entry, followed by the 'default' entries. Without a LoadFolders.xml, the same version
    /// selection applies to the mod's version-named folders, followed by Common and the mod
    /// root.
    /// </summary>
    public static List<string> All(string modRoot)
    {
        List<string> folders = [];
        void Add(string folder)
        {
            folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (!folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            {
                folders.Add(folder);
            }
        }

        var foldersForVersion = ReadLoadFoldersXml(modRoot);
        if (foldersForVersion.Count > 0)
        {
            List<(Version Version, string Key)> versions = [];
            foreach (var key in foldersForVersion.Keys)
            {
                if (TryParseVersion(key, out var version))
                {
                    versions.Add((version, key));
                }
            }
            foreach (var (_, key) in VersionsInRange(versions))
            {
                foreach (var folder in foldersForVersion[key])
                {
                    Add(Path.Combine(modRoot, folder));
                }
            }
            if (foldersForVersion.TryGetValue("default", out var defaultFolders))
            {
                foreach (var folder in defaultFolders)
                {
                    Add(Path.Combine(modRoot, folder));
                }
            }
            return folders;
        }

        List<(Version Version, string Path)> versionDirectories = [];
        foreach (var directory in new DirectoryInfo(modRoot).GetDirectories())
        {
            if (TryParseVersion(directory.Name, out var version))
            {
                versionDirectories.Add((version, directory.FullName));
            }
        }
        foreach (var (_, path) in VersionsInRange(versionDirectories))
        {
            Add(path);
        }
        var common = Path.Combine(modRoot, CommonFolderName);
        if (Directory.Exists(common))
        {
            Add(common);
        }
        Add(modRoot);
        return folders;
    }

    /// <summary>
    /// Every candidate at or above 1.7, plus the newest one below 1.7 when none is exactly 1.7.
    /// </summary>
    internal static IEnumerable<(Version Version, T Item)> VersionsInRange<T>(
        List<(Version Version, T Item)> candidates
    )
    {
        var atFloor = false;
        (Version? Version, T? Item) highestBelow = default;
        foreach (var candidate in candidates)
        {
            if (candidate.Version >= FirstAssetPackVersion)
            {
                atFloor |= candidate.Version == FirstAssetPackVersion;
                yield return candidate;
            }
            else if (highestBelow.Version == null || candidate.Version > highestBelow.Version)
            {
                highestBelow = candidate;
            }
        }
        if (!atFloor && highestBelow.Version != null)
        {
            yield return (highestBelow.Version, highestBelow.Item!);
        }
    }

    /// <summary>
    /// The folders listed per version key in LoadFolders.xml, keys lower-cased with any 'v'
    /// prefix removed, IfModActive-style conditions ignored.
    /// </summary>
    internal static Dictionary<string, List<string>> ReadLoadFoldersXml(string modRoot)
    {
        Dictionary<string, List<string>> foldersForVersion = [];
        var path = ResolveCaseInsensitiveFilePath(modRoot, "LoadFolders.xml");
        if (!File.Exists(path))
        {
            return foldersForVersion;
        }

        XmlDocument document = new();
        document.Load(path);
        foreach (var versionNode in document.DocumentElement!.ChildNodes.OfType<XmlElement>())
        {
            var key = versionNode.Name.ToLowerInvariant();
            if (key.StartsWith('v'))
            {
                key = key[1..];
            }
            if (!foldersForVersion.TryGetValue(key, out var folders))
            {
                folders = [];
                foldersForVersion.Add(key, folders);
            }
            foreach (var folderNode in versionNode.ChildNodes.OfType<XmlElement>())
            {
                var folder = folderNode.InnerText;
                folders.Add(folder is "/" or "\\" ? "" : folder.Replace('\\', '/'));
            }
        }
        return foldersForVersion;
    }

    /// <summary>
    /// Reads the first two dot-separated components as major.minor, ignoring any further ones.
    /// </summary>
    internal static bool TryParseVersion(string text, out Version version)
    {
        version = new Version();
        var parts = text.Split('.');
        if (
            parts.Length < 2
            || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var minor)
            || major < 0
            || minor < 0
        )
        {
            return false;
        }
        version = new Version(major, minor);
        return true;
    }

    private static string ResolveCaseInsensitiveFilePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        return File.Exists(path) || !Directory.Exists(directory)
            ? path
            : Directory
                .EnumerateFiles(directory)
                .FirstOrDefault(file =>
                    string.Equals(
                        Path.GetFileName(file),
                        fileName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                ?? path;
    }
}
