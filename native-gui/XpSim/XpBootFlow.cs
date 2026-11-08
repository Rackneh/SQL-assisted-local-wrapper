using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DoryNative.Models;
using DoryNative.Services;
using DoryNative.Views;

namespace DoryNative.XpSim;

/// <summary>
/// Orchestrates the VM-like startup: BIOS/XP boot -> (installer if needed)
/// -> logon -> desktop hosted inside the StudioShell simulator area.
/// Power is driven externally (StudioShell beige-PC power buttons).
/// </summary>
public class XpBootFlow
{
    private readonly BackendApi _backend;
    private XpDesktop? _desktop;
    private BootSequenceWindow? _boot;
    private string? _workspacePath;
    private bool _poweredOn;
    private WorkspacesResponse? _prefetchedWorkspaces;
    private Task? _prefetchTask;

    /// <summary>Raised when the XP desktop is ready to be hosted (content extracted).</summary>
    public event Action<XpDesktop>? DesktopReady;

    /// <summary>Raised when the workstation power state changes (true = desktop running).</summary>
    public event Action<bool>? PowerStateChanged;

    public XpBootFlow(BackendApi backend) => _backend = backend;

    /// <summary>Power on: resolve workspace install state, run boot -> install/logon -> desktop.</summary>
    public void Boot(string? workspacePath = null)
    {
        if (_poweredOn) return;
        if (!string.IsNullOrEmpty(workspacePath)) _workspacePath = workspacePath;

        // Close any stale boot window (e.g. power cycled mid-boot) so only one
        // boot sequence can ever be in flight.
        _boot?.Close();
        _boot = new BootSequenceWindow();
        _boot.OnLogoComplete += OnBootLogoDone;
        XpStage.Show(_boot); // hosted inside the sim, not a fullscreen window

        // Prefetch workspace data off-thread — IsInstalled needs it and must
        // never block the UI thread (GetAwaiter().GetResult() deadlocks).
        _prefetchTask = Task.Run(async () =>
        {
            try
            {
                var ws = await _backend.GetWorkspacesAsync();
                _workspacePath ??= ws.ActiveWorkspace;
                _prefetchedWorkspaces = ws;
            }
            catch { }
        });
    }

    private async void OnBootLogoDone(BootSequenceWindow sender)
    {
        // Orphaned/duplicate boot windows must not spawn a second install/logon.
        if (!ReferenceEquals(sender, _boot)) { sender.Close(); return; }

        // Wait briefly for the workspace prefetch (normally already done)
        if (_prefetchTask != null)
        {
            var done = await Task.WhenAny(_prefetchTask, Task.Delay(3000));
            if (done != _prefetchTask) { /* proceed with whatever we have */ }
        }
        var ws = _workspacePath;
        bool installed = IsInstalled(ws);
        _boot?.Close();
        _boot = null;

        if (!installed)
        {
            var installer = new XpInstallerWindow(ws, cfg => ShowLogon(cfg));
            XpStage.Show(installer);
        }
        else
        {
            var cfg = InstallStore.GetConfig(ws) ?? XpSetupConfig.Defaults();
            ShowLogon(cfg);
        }
    }

    /// <summary>Mirrors xp_installer.js isInstalled(): flag, saved config, or workspace stats.</summary>
    private bool IsInstalled(string? wsPath)
    {
        if (InstallStore.IsInstalledFlag(wsPath)) return true;
        if (InstallStore.GetConfig(wsPath) != null) return true;
        var t = _prefetchedWorkspaces;
        if (t != null)
        {
            var w = t.Workspaces.FirstOrDefault(x =>
                string.Equals(x.Path, wsPath, StringComparison.OrdinalIgnoreCase) || x.IsActive);
            if (w?.Stats is { DbInitialized: true } s &&
                (s.SessionsCount > 0 || s.TasksCount > 0 || s.StepsCount > 0))
            {
                InstallStore.MarkInstalled(wsPath);
                return true;
            }
        }
        return InstallStore.KeyFor(wsPath) == "default" && InstallStore.IsInstalledFlag(null);
    }

    private void ShowLogon(XpSetupConfig cfg)
    {
        var logon = new LogonWindow(cfg, OnLoggedOn, OnTurnOffRequested);
        XpStage.Show(logon);
    }

    private void OnLoggedOn(XpSetupConfig cfg)
    {
        XpAudio.Startup();
        _desktop = new XpDesktop(_backend, cfg, RequestShutdown, OnLogOff, _workspacePath);
        _poweredOn = true;
        PowerStateChanged?.Invoke(true);
        DesktopReady?.Invoke(_desktop);
    }

    private void OnLogOff()
    {
        var cfg = InstallStore.GetConfig(_workspacePath) ?? XpSetupConfig.Defaults();
        _desktop = null;
        _poweredOn = false;
        PowerStateChanged?.Invoke(false);
        XpAudio.Logoff();
        ShowLogon(cfg);
        // Logon -> desktop again counts as powered on once logged in
    }

    private void OnTurnOffRequested()
    {
        var dlg = new TurnOffDialog();
        XpDialog.ShowDialog(dlg);
        HandleTurnOff(dlg.Action);
    }

    /// <summary>Public entry for desktop-initiated shutdown (Start > Turn Off Computer).</summary>
    public void RequestShutdown()
    {
        var dlg = new TurnOffDialog();
        XpDialog.ShowDialog(dlg);
        HandleTurnOff(dlg.Action);
    }

    private void HandleTurnOff(TurnOffAction action)
    {
        switch (action)
        {
            case TurnOffAction.StandBy:
                break;
            case TurnOffAction.TurnOff:
                XpAudio.Shutdown();
                SetPowerOff();
                ShowSafeToTurnOff();
                break;
            case TurnOffAction.Restart:
                XpAudio.Shutdown();
                SetPowerOff();
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                t.Tick += (_, _) => { t.Stop(); Boot(); };
                t.Start();
                break;
        }
    }

    private void SetPowerOff()
    {
        _boot?.Close();
        _boot = null;
        _desktop = null;
        _poweredOn = false;
        PowerStateChanged?.Invoke(false);
    }

    private void ShowSafeToTurnOff()
    {
        // Hosted as a stage inside the sim (covers the VM area, not the screen)
        var win = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Background = Brushes.Black
        };
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "It is now safe to turn off\nyour computer.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)),
            FontFamily = new FontFamily("Trebuchet MS"),
            FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center
        });
        var btn = new System.Windows.Controls.Button
        {
            Content = "Power On System", Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(14, 5, 14, 5), FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)),
            Foreground = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand
        };
        btn.Click += (_, _) => { win.Close(); Boot(); };
        panel.Children.Add(btn);
        win.Content = panel;
        XpStage.Show(win);
    }
}
