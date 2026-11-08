using System.IO;
using System.Text.Json;

namespace DoryNative.XpSim;

/// <summary>Per-workspace Windows XP installation configuration (mirrors xp_installer.js).</summary>
public class XpSetupConfig
{
    public string UserName { get; set; } = "";
    public string Organization { get; set; } = "";
    public string ComputerName { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string ProductKey { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public string UserTile { get; set; } = "";
    public string InstalledAt { get; set; } = "";
    public string Language { get; set; } = "English (United States)";
    public string Theme { get; set; } = "blue";
    public string Wallpaper { get; set; } = "";
    public string ScreenSaver { get; set; } = "pipes";
    public int ScreenSaverWait { get; set; } = 10;

    public static XpSetupConfig Defaults() => new XpSetupConfig
    {
        UserName = "Rackneh",
        Organization = "Inference Lighting Lab",
        ComputerName = "RACKNEH-XP",
        ProductKey = "FCKGW-RHQQ2-YXRKT-8TG6W-2B7Q8",
        UserTile = "usertiles/dog.png",
        TimeZone = "(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"
    };
}

/// <summary>
/// Persists install state to %LOCALAPPDATA%/DoryNative/xp_install.json,
/// keyed by workspace path (mirrors the web GUI's localStorage scheme).
/// </summary>
public static class InstallStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoryNative");
    private static readonly string FilePath = Path.Combine(Dir, "xp_install.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static Dictionary<string, Entry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath))
                       ?? new Dictionary<string, Entry>();
        }
        catch { }
        return new Dictionary<string, Entry>();
    }

    private static void Save(Dictionary<string, Entry> data)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch { }
    }

    private class Entry
    {
        public bool Installed { get; set; }
        public XpSetupConfig? Config { get; set; }
    }

    public static string KeyFor(string? workspacePath)
        => string.IsNullOrEmpty(workspacePath) ? "default" : workspacePath;

    public static XpSetupConfig? GetConfig(string? workspacePath)
        => Load().TryGetValue(KeyFor(workspacePath), out var e) ? e.Config : null;

    public static void SaveConfig(XpSetupConfig cfg, string? workspacePath)
    {
        var d = Load();
        var k = KeyFor(workspacePath);
        if (!d.TryGetValue(k, out var e)) d[k] = e = new Entry();
        e.Config = cfg;
        Save(d);
    }

    public static bool IsInstalledFlag(string? workspacePath)
        => Load().TryGetValue(KeyFor(workspacePath), out var e) && e.Installed;

    public static void MarkInstalled(string? workspacePath, XpSetupConfig? cfg = null)
    {
        var d = Load();
        var k = KeyFor(workspacePath);
        if (!d.TryGetValue(k, out var e)) d[k] = e = new Entry();
        e.Installed = true;
        if (cfg != null) e.Config = cfg;
        Save(d);
    }

    public static void Reset(string? workspacePath)
    {
        var d = Load();
        d.Remove(KeyFor(workspacePath));
        Save(d);
    }
}
