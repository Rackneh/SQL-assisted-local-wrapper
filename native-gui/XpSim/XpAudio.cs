using System.IO;
using System.Media;

namespace DoryNative.XpSim;

/// <summary>Plays XP wav assets fire-and-forget (System.Media.SoundPlayer).</summary>
public static class XpAudio
{
    private static SoundPlayer? _player;

    public static void Play(string rel)
    {
        var p = XpAssets.Path(rel);
        if (!File.Exists(p)) return;
        try
        {
            _player?.Stop();
            _player = new SoundPlayer(p);
            _player.Play(); // async, doesn't block UI
        }
        catch { }
    }

    public static void Startup() => Play("sounds/startup.wav");
    public static void Shutdown() => Play("sounds/shutdown.wav");
    public static void Logon() => Play("sounds/logon.wav");
    public static void Logoff() => Play("sounds/logoff.wav");
    public static void Ding() => Play("sounds/ding.wav");
    public static void Error() => Play("sounds/error.wav");
    public static void Exclamation() => Play("sounds/exclamation.wav");
    public static void Tada() => Play("sounds/tada.wav");
    public static void Click() => Play("sounds/click.wav");
    public static void Menu() => Play("sounds/menu.wav");
    public static void Minimize() => Play("sounds/minimize.wav");
    public static void Restore() => Play("sounds/restore.wav");
    public static void Recycle() => Play("sounds/recycle.wav");
    public static void Balloon() => Play("sounds/balloon.wav");
    public static void Notify() => Play("sounds/notify.wav");
    public static void HardwareInsert() => Play("sounds/hardware_insert.wav");
    public static void HardwareRemove() => Play("sounds/hardware_remove.wav");
}
