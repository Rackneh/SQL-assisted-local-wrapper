using System.IO;
using System.Windows.Media.Imaging;

namespace DoryNative.XpSim;

/// <summary>
/// Resolves files under Assets/xp_assets (copied to output dir) and builds
/// BitmapImages from them. Paths use forward slashes like the web GUI.
/// </summary>
public static class XpAssets
{
    public static string Root => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xp_assets");

    public static string Path(string rel)
        => System.IO.Path.Combine(Root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public static bool Exists(string rel) => File.Exists(Path(rel));

    public static BitmapImage? Image(string rel)
    {
        var p = Path(rel);
        if (!File.Exists(p)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(p);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    /// <summary>Prefer the .png sibling of a .bmp/.jpg path when present.</summary>
    public static BitmapImage? ImageAuto(string rel)
    {
        var img = Image(rel);
        if (img != null) return img;
        var ext = System.IO.Path.GetExtension(rel);
        if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            return Image(rel[..^4] + ".png");
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            return Image(rel[..^4] + ".png");
        return null;
    }
}
