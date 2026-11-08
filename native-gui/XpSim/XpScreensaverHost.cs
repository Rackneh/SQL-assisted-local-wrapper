using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace DoryNative.XpSim;

/// <summary>
/// Tracks user idle time on the main window and launches the configured
/// screensaver after the configured wait. Any input resets the timer.
/// </summary>
public static class XpScreensaverHost
{
    private static DispatcherTimer? _idleTimer;
    private static DateTime _lastInput = DateTime.Now;
    private static DateTime _saverShownAt = DateTime.MinValue;
    private static XpScreensaverWindow? _saver;
    private static string _mode = "pipes";
    private static int _waitMinutes = 10;
    private static bool _hooksAttached;
    private static Window? _hookedWindow;

    public static void Attach(Window desktopContentOwner, XpSetupConfig? cfg)
    {
        if (cfg != null)
            Configure(cfg.ScreenSaver, cfg.ScreenSaverWait);
        else
            Configure(_mode, _waitMinutes);

        if (_hooksAttached) return;
        _hooksAttached = true;

        // Hook input on whatever window currently hosts the desktop content.
        // Re-resolve each tick in case the host window changes.
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += (_, _) => Tick();
        _idleTimer.Start();
    }

    public static void Configure(string mode, int waitMinutes)
    {
        _mode = string.IsNullOrEmpty(mode) ? "pipes" : mode;
        _waitMinutes = Math.Clamp(waitMinutes, 1, 60);
        _lastInput = DateTime.Now; // don't fire immediately after a settings change
    }

    public static void Preview(string mode)
    {
        var m = ParseMode(mode);
        var win = new XpScreensaverWindow(m);
        win.Closed += (_, _) => { if (_saver == win) _saver = null; };
        _saver = win;
        _saverShownAt = DateTime.Now;
        XpDialog.ShowFullscreen(win);
    }

    private static void Tick()
    {
        var main = Application.Current?.MainWindow;
        if (main != null && !ReferenceEquals(main, _hookedWindow))
        {
            if (_hookedWindow != null) Unhook(_hookedWindow);
            _hookedWindow = main;
            Hook(main);
        }

        if (_saver != null)
        {
            if (_saver.IsLoaded == false) _saver = null;
            return;
        }

        if (DateTime.Now - _lastInput >= TimeSpan.FromMinutes(_waitMinutes))
            ShowSaver();
    }

    private static void Hook(Window w)
    {
        w.PreviewMouseMove += OnInput;
        w.PreviewMouseDown += OnInput;
        w.PreviewMouseWheel += OnInput;
        w.PreviewKeyDown += OnInput;
    }

    private static void Unhook(Window w)
    {
        w.PreviewMouseMove -= OnInput;
        w.PreviewMouseDown -= OnInput;
        w.PreviewMouseWheel -= OnInput;
        w.PreviewKeyDown -= OnInput;
    }

    private static void OnInput(object sender, InputEventArgs e)
    {
        _lastInput = DateTime.Now;
        // The saver is hosted as mirrored content inside the main window, so its
        // own Preview* handlers never see input — dismiss it here instead. The
        // short arm delay prevents the input that dismissed a previous state
        // from instantly killing a freshly launched saver.
        var s = _saver;
        if (s != null && (DateTime.Now - _saverShownAt).TotalMilliseconds > 800)
        {
            try { s.Close(); } catch { }
        }
    }

    private static void ShowSaver()
    {
        if (_saver != null) return;
        var win = new XpScreensaverWindow(ParseMode(_mode));
        win.Closed += (_, _) =>
        {
            if (_saver == win) _saver = null;
            _lastInput = DateTime.Now;
        };
        _saver = win;
        _saverShownAt = DateTime.Now;
        XpDialog.ShowFullscreen(win); // covers the sim area, not the whole screen
    }

    private static XpScreensaverWindow.SaverMode ParseMode(string mode) => mode switch
    {
        "starfield" => XpScreensaverWindow.SaverMode.Starfield,
        "flying_windows" => XpScreensaverWindow.SaverMode.FlyingWindows,
        "mystify" => XpScreensaverWindow.SaverMode.Mystify,
        _ => XpScreensaverWindow.SaverMode.Pipes
    };
}
