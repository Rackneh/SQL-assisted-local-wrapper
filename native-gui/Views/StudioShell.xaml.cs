using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DoryNative.Models;
using DoryNative.Services;
using DoryNative.XpSim;

namespace DoryNative.Views;

/// <summary>
/// Port-5000 studio chrome: collapsible left sidebar + session panel,
/// top status bar, bottom live console — surrounding the XP simulator.
/// </summary>
public partial class StudioShell : Window
{
    private readonly BackendApi _backend;
    private readonly XpBootFlow _bootFlow;
    private readonly DispatcherTimer _pollTimer;
    private int _logSince;
    private bool _autoscroll = true;
    private bool _pcBooted;
    private WorkspaceInfo? _activeWorkspace;
    private string _activeSession = "default";
    private int _loops = 2;
    private string _kvMode = "per_loop";

    public StudioShell(BackendApi backend, XpBootFlow bootFlow)
    {
        InitializeComponent();
        _backend = backend;
        _bootFlow = bootFlow;
        _bootFlow.DesktopReady += OnDesktopReady;
        _bootFlow.PowerStateChanged += OnPowerStateChanged;
        Current = this;
        Closed += (_, _) => { if (Current == this) Current = null; };
        Loaded += (_, _) => DialogOwner.Show();

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _pollTimer.Start();
        Loaded += async (_, _) => await PollAsync();
    }

    // ---------------- data ----------------

    private bool _syncingWorkspaces;

    private async Task PollAsync()
    {
        try
        {
            var status = await _backend.GetServerStatusAsync();
            StatusDot.Fill = status.Status == "RUNNING"
                ? new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            StatusText.Text = status.Status == "RUNNING" ? "Server Online" : $"Server {status.Status}";
            ServerPortStat.Text = $"Port: {status.Port}";

            var ws = await _backend.GetWorkspacesAsync();
            _activeSession = ws.ActiveSession;
            SyncWorkspaces(ws);

            var sess = await _backend.GetSessionsAsync();
            _activeSession = sess.ActiveSession;
            ChipSessionText.Text = $"💬 Session: {_activeSession}";

            var provider = await _backend.GetProviderAsync();
            UpdateBackendButtons(provider.Provider);
            // Badge shows what will actually serve (effective), not just the selection.
            var effective = provider.Effective ?? provider.Provider;
            ModelBadge.Text = effective == "lmstudio"
                ? (provider.LmModel ?? "Gemma (LM Studio)")
                : "Dory1.0-4B-Q5_K_M";

            var statusDict = await _backend.GetStatusAsync();
            UpdateStatusChips(statusDict);

            var tasks = await _backend.GetTasksAsync();
            // counts shown on buttons' labels live in session sidebar footer (static text ok)

            var activity = await _backend.GetActivityAsync(_logSince);
            _logSince = activity.LatestId;
            AppendLogs(activity.Logs);
            ActivityText.Text = activity.IsBusy ? activity.StatusLabel : "Idle";
            ActivityDot.Fill = activity.IsBusy
                ? new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00))
                : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            DrawerStatusTitle.Text = string.IsNullOrEmpty(activity.StatusLabel)
                ? "Engine Idle (Ready for OpenCode & Chat)" : activity.StatusLabel;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Backend offline";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            TerminalLines.Children.Add(MakeLogLine($"[poll] {ex.Message}", "#FF6B6B"));
        }
    }

    private void SyncWorkspaces(WorkspacesResponse ws)
    {
        _syncingWorkspaces = true;
        try
        {
        WorkspaceSelect.ItemsSource = null;
        WorkspaceSelect.ItemsSource = ws.Workspaces;
        WorkspaceSelect.DisplayMemberPath = "Name";
        SessionWsSelect.ItemsSource = null;
        SessionWsSelect.ItemsSource = ws.Workspaces;
        SessionWsSelect.DisplayMemberPath = "Name";

        var active = ws.Workspaces.FirstOrDefault(w => w.IsActive)
                     ?? ws.Workspaces.FirstOrDefault(w => w.Path == ws.ActiveWorkspace);
        _activeWorkspace = active;
        if (active != null)
        {
            WorkspaceSelect.SelectedItem = active;
            SessionWsSelect.SelectedItem = active;
            WorkspacePath.Text = active.Path;
            SessionWsPath.Text = active.Path;
            ChipProjectText.Text = $"📁 Project: {active.Name}";
            ActiveProjectName.Text = $"{active.Name} ({active.Path})";
        }
        else
        {
            ChipProjectText.Text = "📁 Project: none";
            ActiveProjectName.Text = "No Project Selected";
        }

        OtherProjectsGrid.ItemsSource = ws.Workspaces.Where(w => w.Path != active?.Path).ToList();
        }
        finally { _syncingWorkspaces = false; }
    }

    private void UpdateBackendButtons(string provider)
    {
        BackendDoryBtn.Background = provider == "dory" || provider == "auto"
            ? new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00)) : new SolidColorBrush(Color.FromRgb(0x1E, 0x28, 0x3D));
        BackendGemmaBtn.Background = provider == "lmstudio"
            ? new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00)) : new SolidColorBrush(Color.FromRgb(0x1E, 0x28, 0x3D));
    }

    private void UpdateStatusChips(Dictionary<string, System.Text.Json.JsonElement> status)
    {
        if (status.TryGetValue("gpu", out var gpu) && gpu.ValueKind == System.Text.Json.JsonValueKind.String)
            ChipGpuText.Text = $"🎮 {gpu.GetString()}";
        if (status.TryGetValue("loops", out var loops) && loops.TryGetInt32(out var l))
        {
            _loops = l;
            ChipLoopsText.Text = $"{l} Loops";
        }
        if (status.TryGetValue("kv_cache_mode", out var kv) && kv.ValueKind == System.Text.Json.JsonValueKind.String)
            _kvMode = kv.GetString() ?? "per_loop";
        if (status.TryGetValue("tokens_per_second", out var tps) && tps.ValueKind == System.Text.Json.JsonValueKind.Number)
            TokenSpeedStat.Text = $"{tps.GetDouble():F1} tokens/s";
    }

    private void AppendLogs(List<LogEntry> logs)
    {
        foreach (var log in logs)
        {
            var color = log.Level.ToLowerInvariant() switch
            {
                "error" => "#FF6B6B", "warn" or "warning" => "#FFA502", "debug" => "#64748B", _ => "#94A3B8"
            };
            TerminalLines.Children.Add(MakeLogLine($"[{log.Timestamp}] {log.Message}", color));
        }
        if (_autoscroll && logs.Count > 0)
            DrawerBody.ScrollToEnd();
        // cap terminal size
        while (TerminalLines.Children.Count > 500)
            TerminalLines.Children.RemoveAt(0);
    }

    private static TextBlock MakeLogLine(string text, string color) => new()
    {
        Text = text, FontFamily = new FontFamily("Consolas"), FontSize = 10,
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
        TextWrapping = TextWrapping.Wrap
    };

    // ---------------- PC power / boot ----------------

    private void SidebarPower_Click(object sender, RoutedEventArgs e) => TogglePower();
    private void ActivePower_Click(object sender, RoutedEventArgs e) => TogglePower();

    private void TogglePower()
    {
        if (_pcBooted)
        {
            _bootFlow.RequestShutdown();
        }
        else
        {
            // Hide the off-overlay while booting — stages render inside the sim
            PcOffOverlay.Visibility = Visibility.Collapsed;
            var ws = _activeWorkspace ?? WorkspaceSelect.SelectedItem as WorkspaceInfo;
            _bootFlow.Boot(ws?.Path);
        }
    }

    private void OtherProject_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is WorkspaceInfo w)
        {
            _activeWorkspace = w;
            WorkspaceSelect.SelectedItem = w;
            SessionWsSelect.SelectedItem = w;
            WorkspacePath.Text = w.Path;
            SessionWsPath.Text = w.Path;
            ChipProjectText.Text = $"📁 Project: {w.Name}";
            ActiveProjectName.Text = $"{w.Name} ({w.Path})";
            // Boot the selected workstation like the web GUI does
            PcOffOverlay.Visibility = Visibility.Collapsed;
            _bootFlow.Boot(w.Path);
        }
    }

    /// <summary>Content layer hosting boot/install/logon stages and the XP desktop.</summary>
    public ContentControl Stage => StageHost;

    /// <summary>Canvas hosting in-sim dialog windows above the stage content.</summary>
    public Canvas Dialogs => DialogLayer;

    /// <summary>Show/hide the "workstation powered off" overlay over the sim area.</summary>
    public void SetOffOverlay(bool on) => PcOffOverlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Hidden parked window used as Owner for mirrored dialogs (keeps modality off the shell).</summary>
    public Window DialogOwner { get; } = new Window
    {
        Width = 0, Height = 0, WindowStyle = WindowStyle.None,
        ShowInTaskbar = false, ShowActivated = false,
        Left = -32000, Top = -32000, Opacity = 0
    };

    public static StudioShell? Current { get; private set; }

    public void OnDesktopReady(XpDesktop desktop)
    {
        // Extract the desktop content into the simulator host (window itself stays hidden)
        var content = desktop.Content;
        StageHost.Content = content;
        PcOffOverlay.Visibility = Visibility.Collapsed;
        SetPowerVisuals(true);
    }

    public void OnPowerStateChanged(bool booted)
    {
        _pcBooted = booted;
        SetPowerVisuals(booted);
        PcStatusText.Text = booted ? "ONLINE" : "OFFLINE";
        PcStatusPill.Background = booted
            ? new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x1E))
            : new SolidColorBrush(Color.FromRgb(0x3A, 0x1D, 0x1D));
        PcStatusText.Foreground = booted
            ? new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
        ActivePcHint.Text = booted ? "Windows XP is running" : "Click to boot workstation";
        if (!booted)
        {
            StageHost.Content = null;
            PcOffOverlay.Visibility = Visibility.Visible;
        }
    }

    private void SetPowerVisuals(bool on)
    {
        var led = on ? Color.FromRgb(0x76, 0xB9, 0x00) : Color.FromRgb(0x5A, 0x52, 0x45);
        SidebarPowerLed.Fill = new SolidColorBrush(led);
    }

    // ---------------- sidebar toggles ----------------

    private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
    {
        LeftCol.Width = LeftCol.Width.Value > 1 ? new GridLength(0) : new GridLength(290);
    }

    private void ToggleSessionSidebar_Click(object sender, RoutedEventArgs e)
    {
        SessionCol.Width = SessionCol.Width.Value > 1 ? new GridLength(0) : new GridLength(270);
    }

    private void ToggleConsole_Click(object sender, RoutedEventArgs e)
    {
        ConsoleDrawer.Visibility = ConsoleDrawer.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
        if (ConsoleDrawer.Visibility == Visibility.Visible) DrawerBody.ScrollToEnd();
    }

    // ---------------- config buttons ----------------

    private async void Budget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            await _backend.SetPolicyAsync(new Dictionary<string, object> { ["iba_prompt_budget"] = tag });
            HighlightGroup(BudgetGroup, tag);
        }
    }

    private async void Backend_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            await _backend.SetProviderAsync(tag);
            UpdateBackendButtons(tag);
            TerminalLines.Children.Add(MakeLogLine($"[backend] switched to {tag} (GPU exclusive)", "#00E5FF"));
        }
    }

    private async void Loop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var n))
        {
            _loops = n;
            ChipLoopsText.Text = $"{n} Loops";
            await _backend.SetPolicyAsync(new Dictionary<string, object> { ["loops"] = n });
            HighlightGroup(LoopGroup, tag);
        }
    }

    private async void KvMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            _kvMode = tag;
            await _backend.SetPolicyAsync(new Dictionary<string, object> { ["kv_cache_mode"] = tag });
            HighlightGroup(KvModeGroup(), tag);
        }
    }

    private System.Windows.Controls.Panel KvModeGroup() => KvGroup;

    private static void HighlightGroup(System.Windows.Controls.Panel group, string activeTag)
    {
        foreach (var child in group.Children.OfType<Button>())
        {
            var on = child.Tag as string == activeTag;
            child.Background = new SolidColorBrush(on
                ? Color.FromRgb(0x76, 0xB9, 0x00) : Color.FromRgb(0x1E, 0x28, 0x3D));
            child.Foreground = new SolidColorBrush(on
                ? Color.FromRgb(0x0A, 0x0D, 0x14) : Color.FromRgb(0xF1, 0xF5, 0xF9));
        }
    }

    // ---------------- console drawer ----------------

    private void DrawerClear_Click(object sender, RoutedEventArgs e)
    {
        TerminalLines.Children.Clear();
        _logSince = 0;
        _ = _backend.ClearLogsAsync();
    }

    private void DrawerCopy_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join("\n", TerminalLines.Children.OfType<TextBlock>().Select(t => t.Text));
        try { Clipboard.SetText(text); } catch { }
    }

    private void DrawerAutoscroll_Click(object sender, RoutedEventArgs e)
    {
        _autoscroll = !_autoscroll;
        DrawerAutoscrollBtn.Content = _autoscroll ? "📜 Auto-Scroll" : "📜 Manual";
    }

    // ---------------- misc ----------------

    private async void WorkspaceSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingWorkspaces) return;
        if (WorkspaceSelect.SelectedItem is WorkspaceInfo w)
        {
            _activeWorkspace = w;
            try { await _backend.SelectWorkspaceAsync(w.Path); } catch { }
        }
    }

    private async void SessionWsSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingWorkspaces) return;
        if (SessionWsSelect.SelectedItem is WorkspaceInfo w)
        {
            _activeWorkspace = w;
            try { await _backend.SelectWorkspaceAsync(w.Path); } catch { }
        }
    }

    private void PinRule_Click(object sender, RoutedEventArgs e) { /* TODO: pin rule dialog */ }
    private void CodeMap_Click(object sender, RoutedEventArgs e) { /* TODO: code map viewer */ }
    private void TaskLog_Click(object sender, RoutedEventArgs e) { /* TODO: task log viewer */ }
    private void ClearChat_Click(object sender, RoutedEventArgs e) { /* TODO: clear chat history */ }
    private void ExportChat_Click(object sender, RoutedEventArgs e) { /* TODO: export chat */ }
}
