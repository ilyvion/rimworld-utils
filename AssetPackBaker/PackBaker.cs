using System.Collections.Concurrent;

namespace AssetPackBaker;

internal sealed record BakeResult(int Entries, int Deduplicated, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Bakes one load folder's <c>Textures</c> and <c>Sounds</c> into <c>Assets.zip</c> in that
/// load folder.
/// </summary>
internal static class PackBaker
{
    public const string PackFileName = "Assets.zip";

    private const string TexturesFolder = "Textures";
    private const string SoundsFolder = "Sounds";

    private static readonly string[] TextureExtensions = [".png", ".jpg", ".jpeg", ".tga", ".psd"];

    private static readonly string[] PsdShadowingExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".bmp",
        ".dds",
    ];

    private static readonly string[] SoundExtensions =
    [
        ".wav",
        ".mp3",
        ".ogg",
        ".xm",
        ".it",
        ".mod",
        ".s3m",
    ];

    /// <summary>Raw files in these formats are stored without further compression.</summary>
    private static readonly string[] CompressedExtensions =
    [
        ".ogg",
        ".mp3",
        ".opus",
        ".jpg",
        ".jpeg",
        ".png",
        ".zip",
        ".zst",
    ];

    /// <summary>
    /// Sidecar files the game writes next to a pack it bakes locally; they describe that
    /// local bake and must not ship.
    /// </summary>
    private static readonly string[] SidecarSuffixes = [".stamps", ".roles", ".fingerprint"];

    public static bool HasBakeableContent(string loadFolder) =>
        Directory.Exists(Path.Combine(loadFolder, TexturesFolder))
        || Directory.Exists(Path.Combine(loadFolder, SoundsFolder));

    public static BakeResult Bake(string loadFolder, Action<string>? log = null)
    {
        loadFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(loadFolder));
        var borderMipMaps = BorderMipMaps.For(loadFolder);
        var textures = TextureSources(loadFolder, log);
        var sounds = SoundSources(loadFolder);

        AssetPackWriter writer = new();
        ConcurrentQueue<string> errors = new();
        List<(string Path, bool IsTexture)> sources =
        [
            .. textures.Select(path => (path, true)),
            .. sounds.Select(path => (path, false)),
        ];
        _ = Parallel.ForEach(
            sources,
            source =>
            {
                var name = Path.GetRelativePath(loadFolder, source.Path).Replace('\\', '/');
                try
                {
                    if (source.IsTexture)
                    {
                        writer.AddTexture(
                            name + ".dds",
                            TextureBaker.Bake(source.Path, borderMipMaps.Wants(source.Path))
                        );
                    }
                    else
                    {
                        writer.AddRaw(
                            name,
                            File.ReadAllBytes(source.Path),
                            CompressedExtensions.Contains(
                                Path.GetExtension(source.Path).ToLowerInvariant()
                            )
                        );
                    }
                }
                catch (Exception e)
                {
                    errors.Enqueue($"{source.Path}: {e.Message}");
                }
            }
        );

        if (!errors.IsEmpty)
        {
            return new BakeResult(writer.Count, writer.Deduplicated, [.. errors.Order()]);
        }

        var output = Path.Combine(loadFolder, PackFileName);
        var temporary = output + ".tmp";
        try
        {
            writer.Write(temporary);
            File.Move(temporary, output, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
        foreach (var suffix in SidecarSuffixes)
        {
            File.Delete(output + suffix);
        }
        return new BakeResult(writer.Count, writer.Deduplicated, []);
    }

    /// <summary>
    /// Image files under <c>Textures</c>, skipping symbolic links and any PSD with a sibling of
    /// the same name in another image format.
    /// </summary>
    internal static List<string> TextureSources(string loadFolder, Action<string>? log = null)
    {
        var root = Path.Combine(loadFolder, TexturesFolder);
        if (!Directory.Exists(root))
        {
            return [];
        }
        var files = Directory
            .EnumerateFiles(
                root,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                }
            )
            .Select(Path.GetFullPath)
            .ToList();
        HashSet<string> present = new(files, StringComparer.Ordinal);

        List<string> sources = [];
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension == ".obj")
            {
                log?.Invoke($"{file}: meshes are not baked; the game loads it from the file");
                continue;
            }
            if (!TextureExtensions.Contains(extension))
            {
                continue;
            }
            if (
                extension == ".psd"
                && PsdShadowingExtensions.Any(sibling =>
                    present.Contains(Path.ChangeExtension(file, sibling))
                )
            )
            {
                continue;
            }
            sources.Add(file);
        }
        return sources;
    }

    internal static List<string> SoundSources(string loadFolder)
    {
        var root = Path.Combine(loadFolder, SoundsFolder);
        return !Directory.Exists(root)
            ? []
            :
            [
                .. Directory
                    .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Where(file =>
                        SoundExtensions.Contains(
                            Path.GetExtension(file),
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    .Select(Path.GetFullPath),
            ];
    }
}
