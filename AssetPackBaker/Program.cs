using System.Diagnostics;

namespace AssetPackBaker;

/// <summary>
/// Bakes the asset packs (<c>Assets.zip</c>) RimWorld 1.7+ loads a mod's textures and sounds
/// from, the way the game's <c>-BakeModAssetPacks</c> option and Workshop upload do, without
/// needing the game.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var modRoots = args.SelectMany(arg =>
                arg.Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            .ToList();
        if (modRoots.Count == 0)
        {
            Console.Error.WriteLine("Usage: AssetPackBaker <mod folder> [<mod folder>...]");
            return 2;
        }

        var failed = false;
        foreach (var modRoot in modRoots)
        {
            try
            {
                failed |= !BakeMod(Path.GetFullPath(modRoot));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{modRoot}: {e}");
                failed = true;
            }
        }
        return failed ? 1 : 0;
    }

    private static bool BakeMod(string modRoot)
    {
        if (!Directory.Exists(modRoot))
        {
            Console.Error.WriteLine($"Mod folder not found: {modRoot}");
            return false;
        }

        var loadFolders = LoadFolders.All(modRoot).Where(PackBaker.HasBakeableContent).ToList();
        if (loadFolders.Count == 0)
        {
            Console.WriteLine(
                $"{modRoot} has no load folder with textures or sounds, nothing to bake"
            );
            return true;
        }

        var succeeded = true;
        foreach (var loadFolder in loadFolders)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = PackBaker.Bake(loadFolder, Console.WriteLine);
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"{loadFolder}: {error}");
            }
            Console.WriteLine(
                $"{loadFolder}: {result.Entries} entries ({result.Deduplicated} deduplicated, "
                    + $"{result.Errors.Count} failed) in {stopwatch.Elapsed.TotalSeconds:F1}s"
            );
            succeeded &= result.Succeeded;
        }
        return succeeded;
    }
}
