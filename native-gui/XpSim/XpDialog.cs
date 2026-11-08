using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DoryNative.Views;

namespace DoryNative.XpSim;

/// <summary>
/// Hosts boot/install/logon stage windows and mirrors dialogs inside the
/// StudioShell simulator area. Real Window objects are parked off-screen
/// (real HWND, so Loaded/timers/DialogResult/Close all work); only their
/// content is ever visible, inside the sim.
/// </summary>
public static class XpStage
{
    private static Window? _stageWindow;
    private static UIElement? _stageContent;

    /// <summary>Show a fullscreen stage (BIOS/XP logo/installer/logon) inside the sim.</summary>
    public static void Show(Window win)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = -32000;
        win.Top = -32000;
        win.WindowState = WindowState.Normal; // Maximized + ShowActivated=false throws
        win.ShowInTaskbar = false;
        win.ShowActivated = false;
        win.Closed += (_, _) => ClearStage(win);
        win.Show(); // off-screen but presented — Loaded fires, timers run

        var content = win.Content as UIElement;
        _stageWindow = win;
        _stageContent = content;
        var shell = StudioShell.Current;
        shell?.SetOffOverlay(false); // stages only appear while booting
        if (shell != null && content != null)
            shell.Stage.Content = content;
    }

    private static void ClearStage(Window win)
    {
        var shell = StudioShell.Current;
        if (shell != null && _stageContent != null &&
            ReferenceEquals(shell.Stage.Content, _stageContent))
            shell.Stage.Content = null;
        if (ReferenceEquals(_stageWindow, win))
        {
            _stageWindow = null;
            _stageContent = null;
        }
    }
}

/// <summary>
/// Mirrors Window dialogs into the sim's dialog layer. The real window is
/// parked off-screen (ShowDialog still blocks and DialogResult still works);
/// an XpWindow on the dialog canvas shows its content.
/// </summary>
public static class XpDialog
{
    private static int _zCounter;

    /// <summary>Show a non-modal window's content inside the sim.</summary>
    public static void Show(Window win)
    {
        var shell = StudioShell.Current;
        if (shell == null) { win.Show(); return; }

        Park(win);
        win.Show(); // realize size off-screen
        var xp = Wrap(win, win.ActualWidth > 0 ? win.ActualWidth : win.Width,
            win.ActualHeight > 0 ? win.ActualHeight : win.Height);
        if (xp == null) return;
        win.Closed += (_, _) => xp.TriggerClose();
        xp.WindowClosed += (_, _) => { try { win.Close(); } catch { } };
    }

    /// <summary>Show a window's content covering the entire sim area (screensaver).</summary>
    public static void ShowFullscreen(Window win)
    {
        var shell = StudioShell.Current;
        if (shell == null) { win.Show(); return; }

        Park(win);
        win.Show();
        var content = win.Content as UIElement;
        if (content == null) return;

        var layerW = shell.Dialogs.ActualWidth;
        var layerH = shell.Dialogs.ActualHeight;
        if (layerW <= 0) layerW = shell.ActualWidth;
        if (layerH <= 0) layerH = shell.ActualHeight;

        var xp = new XpWindow
        {
            Title = win.Title ?? "Screen Saver",
            Width = layerW,
            Height = layerH,
            Chromeless = true,
            IsDialog = true,
            WindowIcon = null
        };
        Canvas.SetLeft(xp, 0);
        Canvas.SetTop(xp, 0);
        Panel.SetZIndex(xp, 500);
        xp.WindowContent = content;
        shell.Dialogs.Children.Add(xp);
        xp.WindowClosed += (_, _) => shell.Dialogs.Children.Remove(xp);

        win.Closed += (_, _) => xp.TriggerClose();
        xp.WindowClosed += (_, _) => { try { win.Close(); } catch { } };
    }

    /// <summary>ShowDialog equivalent — blocks until the dialog closes.</summary>
    public static bool? ShowDialog(Window win, Window? owner = null)
    {
        var shell = StudioShell.Current;
        if (shell == null)
        {
            if (owner != null) win.Owner = owner;
            return win.ShowDialog();
        }

        Park(win, owner);

        // Measure desired size before showing (real window stays hidden)
        var content = win.Content as UIElement;
        if (content != null)
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        var chromeless = win.WindowStyle == WindowStyle.None;
        var chromePad = chromeless ? 0 : 8;
        var w = win.Width > 0 ? win.Width
            : (content?.DesiredSize.Width ?? 360) + chromePad;
        var h = win.Height > 0 ? win.Height
            : (content?.DesiredSize.Height ?? 160) + chromePad;

        var xp = Wrap(win, w, h);
        if (xp == null) return win.ShowDialog();

        // The grid now lives in the sim's XpWindow; clear it here so the parked
        // window's template doesn't steal the element back when ShowDialog realizes it.
        win.Content = null;

        win.Closed += (_, _) => xp.TriggerClose();
        xp.WindowClosed += (_, _) => { try { win.Close(); } catch { } };

        // WPF ShowDialog disables every top-level window in the thread — including
        // the StudioShell that hosts the in-sim dialog. Re-enable it once the
        // dialog has been shown so the sim stays interactive.
        var shellHwnd = new WindowInteropHelper(shell).Handle;
        void Reenable()
        {
            try { EnableWindow(shellHwnd, true); } catch { }
        }
        win.Loaded += (_, _) =>
        {
            Reenable();
            // WPF may apply the modal disable after Loaded — re-assert briefly.
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            int n = 0;
            t.Tick += (_, _) => { Reenable(); if (++n >= 6) t.Stop(); };
            t.Start();
        };

        try { return win.ShowDialog(); }
        finally
        {
            try { xp.TriggerClose(); } catch { }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    private static void Park(Window win, Window? owner = null)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = -32000;
        win.Top = -32000;
        win.WindowState = WindowState.Normal;
        win.ShowInTaskbar = false;
        win.ShowActivated = false;
        var shell = StudioShell.Current;
        // Owner must be the StudioShell itself: Win32 ShowDialog modality disables
        // every top-level window in the thread except the dialog and its owner.
        // Owning to a parked/off-screen window would disable the shell — and the
        // sim dialog lives inside the shell's tree — freezing the whole app.
        win.Owner = shell ?? owner ?? shell?.DialogOwner;
    }

    private static XpWindow? Wrap(Window win, double w, double h)
    {
        var shell = StudioShell.Current;
        if (shell == null) return null;
        var content = win.Content as UIElement;
        if (content == null) return null;

        var chromeless = win.WindowStyle == WindowStyle.None;

        var xp = new XpWindow
        {
            Title = string.IsNullOrEmpty(win.Title) ? "Dialog" : win.Title,
            Width = Math.Max(220, Math.Min(w, shell.Dialogs.ActualWidth > 0 ? shell.Dialogs.ActualWidth - 40 : w)),
            Height = Math.Max(120, Math.Min(h, shell.Dialogs.ActualHeight > 0 ? shell.Dialogs.ActualHeight - 40 : h)),
            Chromeless = chromeless,
            IsDialog = true,
            WindowIcon = XpAssets.ImageAuto("icons/dialog.png") ?? XpAssets.ImageAuto("icons/explorer.png")
        };

        // Center in the dialog layer
        var layerW = shell.Dialogs.ActualWidth;
        var layerH = shell.Dialogs.ActualHeight;
        if (layerW <= 0) layerW = shell.ActualWidth;
        if (layerH <= 0) layerH = shell.ActualHeight;
        Canvas.SetLeft(xp, Math.Max(8, (layerW - xp.Width) / 2));
        Canvas.SetTop(xp, Math.Max(8, (layerH - xp.Height) / 2));
        Panel.SetZIndex(xp, 1000 + _zCounter++);

        xp.WindowContent = content;
        shell.Dialogs.Children.Add(xp);
        xp.WindowClosed += (_, _) => shell.Dialogs.Children.Remove(xp);
        return xp;
    }

    /// <summary>
    /// Host arbitrary UIElement content as an xp window in the dialog layer.
    /// Returns the XpWindow; caller closes it via TriggerClose().
    /// </summary>
    public static XpWindow? ShowElement(string title, UIElement content, double width, double height,
        bool chromeless = false)
    {
        var shell = StudioShell.Current;
        if (shell == null) return null;

        var xp = new XpWindow
        {
            Title = title,
            Width = Math.Max(200, Math.Min(width, shell.Dialogs.ActualWidth > 0 ? shell.Dialogs.ActualWidth - 20 : width)),
            Height = Math.Max(100, Math.Min(height, shell.Dialogs.ActualHeight > 0 ? shell.Dialogs.ActualHeight - 20 : height)),
            Chromeless = chromeless,
            IsDialog = true
        };
        var layerW = shell.Dialogs.ActualWidth > 0 ? shell.Dialogs.ActualWidth : shell.ActualWidth;
        var layerH = shell.Dialogs.ActualHeight > 0 ? shell.Dialogs.ActualHeight : shell.ActualHeight;
        Canvas.SetLeft(xp, Math.Max(4, (layerW - xp.Width) / 2));
        Canvas.SetTop(xp, Math.Max(4, (layerH - xp.Height) / 2));
        Panel.SetZIndex(xp, 2000 + _zCounter++);
        xp.WindowContent = content;
        shell.Dialogs.Children.Add(xp);
        xp.WindowClosed += (_, _) => shell.Dialogs.Children.Remove(xp);
        return xp;
    }
}

/// <summary>Extension methods so call sites read naturally.</summary>
public static class SimWindowExtensions
{
    public static void ShowSim(this Window win) => XpDialog.Show(win);
    public static bool? ShowDialogSim(this Window win) => XpDialog.ShowDialog(win);
    public static bool? ShowDialogSim(this Window win, Window? owner) => XpDialog.ShowDialog(win, owner);
}
