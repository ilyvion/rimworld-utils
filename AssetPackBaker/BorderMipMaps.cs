using System.Xml;

namespace AssetPackBaker;

/// <summary>
/// Textures a load folder's Defs mark with <c>&lt;borderMipMaps&gt;true&lt;/borderMipMaps&gt;</c>
/// next to their <c>&lt;texPath&gt;</c>.
/// </summary>
internal sealed class BorderMipMaps
{
    private readonly List<string> suffixes;

    private BorderMipMaps(List<string> suffixes)
    {
        this.suffixes = suffixes;
    }

    public static BorderMipMaps For(string loadFolder)
    {
        List<string> suffixes = [];
        var defs = Path.Combine(loadFolder, "Defs");
        if (Directory.Exists(defs))
        {
            foreach (
                var file in Directory.EnumerateFiles(defs, "*.xml", SearchOption.AllDirectories)
            )
            {
                XmlDocument document = new();
                try
                {
                    document.Load(file);
                }
                catch (XmlException)
                {
                    continue;
                }
                foreach (XmlElement element in document.SelectNodes("//borderMipMaps")!)
                {
                    if (
                        !string.Equals(
                            element.InnerText.Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }
                    var texPath = element.ParentNode?["texPath"]?.InnerText.Trim();
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        suffixes.Add("/Textures/" + texPath.Replace('\\', '/').Trim('/'));
                    }
                }
            }
        }
        return new BorderMipMaps(suffixes);
    }

    /// <summary>
    /// Whether the texture at <paramref name="fullPath"/>, minus its extension, ends with the
    /// path of a texture marked for border mip maps.
    /// </summary>
    public bool Wants(string fullPath)
    {
        if (suffixes.Count == 0)
        {
            return false;
        }
        var path = fullPath.Replace('\\', '/');
        path = path[..^Path.GetExtension(path).Length];
        return suffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
