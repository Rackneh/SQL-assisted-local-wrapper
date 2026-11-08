using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using DoryNative.Views;
using MSNPSharp;
using WLMRevivalV2Desktop.Views;

namespace DoryNative.WlmSim;

/// <summary>
/// Hosts the V2 WLMLive windows inside the XP simulator. The real windows are
/// kept alive but parked off-screen (real HWND, so Close/etc. all work);
/// their visual content is extracted into chromeless XpWindow surfaces on the
/// desktop canvas — WLMLive draws its own titlebar/borders, so no Luna chrome.
/// Window ops (drag/min/max) are routed back to the hosting XpWindow.
/// Nothing WLM-related escapes the sim.
/// </summary>
public static class WlmSimHost
{
    private static MainWindowLive? _mainWin;
    private static XpWindow? _mainXp;
    private static readonly Dictionary<string, (ChatWindowLive Win, XpWindow Xp)> _chats = new();
    private static readonly ConditionalWeakTable<Window, XpWindow> _hosts = new();

    /// <summary>Open (or focus) the WLM main/contact-list window inside the sim.</summary>
    public static void OpenMainWindow()
    {
        if (_mainWin != null)
        {
            Raise(_mainXp);
            return;
        }
        var win = new MainWindowLive(App.Messenger);
        _mainWin = win;
        _mainXp = Host(win, "Windows Live Messenger", 880, 620);
        win.Closed += (_, _) =>
        {
            var xp = _mainXp;
            _mainXp = null; _mainWin = null;
            if (xp != null) xp.TriggerClose();
        };
    }

    /// <summary>Open a per-contact chat window inside the sim (called by MainWindowLive.OpenChat).</summary>
    public static ChatWindowLive? OpenChatWindow(WLMRevivalV2Desktop.Protocol.MessengerService messenger, Contact contact)
    {
        if (_chats.TryGetValue(contact.Mail, out var existing))
        {
            Raise(existing.Xp);
            return existing.Win;
        }
        var chat = new ChatWindowLive(messenger, contact);
        var title = $"{(string.IsNullOrEmpty(contact.Name) ? contact.Mail : contact.Name)} - Conversation";
        var xp = Host(chat, title, 620, 580);
        _chats[contact.Mail] = (chat, xp);
        chat.Closed += (_, _) =>
        {
            if (_chats.Remove(contact.Mail, out var t)) t.Xp.TriggerClose();
        };
        return chat;
    }

    /// <summary>Park a V2 window off-screen and reparent its content into a chromeless XpWindow.</summary>
    private static XpWindow Host(Window win, string title, double w, double h)
    {
        win.WindowStartupLocation = WindowStartupLocation.Manual;
        win.Left = -32000;
        win.Top = -32000;
        win.WindowState = WindowState.Normal;
        win.ShowInTaskbar = false;
        win.ShowActivated = false;
        win.Show(); // real handle, invisible — Close() and cleanup keep working

        var content = win.Content;
        var xp = new XpWindow
        {
            Title = title,
            Width = w,
            Height = h,
            Chromeless = true,
            WindowIcon = XpSim.XpAssets.ImageAuto("icons/msn.png")
        };
        _hosts.Remove(win);
        _hosts.Add(win, xp);
        xp.WindowContent = content;
        XpDesktop.Current?.RegisterWindow(xp, title);

        // XP-window close -> really close the parked V2 window (fires Closed -> cleanup)
        xp.WindowClosed += (_, _) =>
        {
            try { win.Close(); } catch { }
        };
        return xp;
    }

    // ===== Window-op routing (called by the V2 chrome instead of raw window ops) =====

    public static bool IsHosted(Window win) => _hosts.TryGetValue(win, out _);

    /// <summary>Drag the hosting XpWindow when the V2 titlebar is dragged.</summary>
    public static void DragWindow(Window win)
    {
        if (_hosts.TryGetValue(win, out var xp)) xp.BeginExternalDrag();
        else win.DragMove();
    }

    /// <summary>Minimize via the hosting XpWindow (hides it; taskbar restores).</summary>
    public static void MinimizeWindow(Window win)
    {
        if (_hosts.TryGetValue(win, out var xp)) xp.MinimizeSelf();
        else win.WindowState = WindowState.Minimized;
    }

    /// <summary>Toggle maximize on the hosting XpWindow.</summary>
    public static void ToggleMaximizeWindow(Window win)
    {
        if (_hosts.TryGetValue(win, out var xp)) xp.ToggleExternalMaximize();
        else win.WindowState = win.WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
    }

    private static void Raise(XpWindow? xp)
    {
        if (xp == null) return;
        xp.XpWindowState = WindowState.Normal;
        xp.Visibility = Visibility.Visible;
        XpDesktop.Current?.BringToFront(xp);
    }
}
