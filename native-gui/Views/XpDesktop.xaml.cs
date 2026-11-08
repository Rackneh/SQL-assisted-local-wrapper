using MessageBox = DoryNative.XpSim.SimMessageBox;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DoryNative.Games;
using DoryNative.Services;
using DoryNative.XpSim;

namespace DoryNative.Views;

public partial class XpDesktop : Window
{
    private readonly BackendApi _backend;
    private readonly DispatcherTimer _clockTimer;
    private int _highestZIndex = 100;
    private bool _taskbarLocked;
    private readonly ObservableCollection<TaskbarItem> _taskbarItems = new();
    private readonly XpSim.XpSetupConfig? _cfg;
    private readonly string? _workspacePath;
    private readonly Action? _onShutdown;
    private readonly Action? _onLogOff;

    /// <summary>Live reference so applets (Display Properties) can change wallpaper.</summary>
    public static XpDesktop? Current { get; private set; }

    public XpDesktop(BackendApi backend, XpSim.XpSetupConfig? cfg = null,
        Action? onShutdown = null, Action? onLogOff = null, string? workspacePath = null)
    {
        InitializeComponent();
        _backend = backend;
        _cfg = cfg;
        _onShutdown = onShutdown;
        _onLogOff = onLogOff;
        _workspacePath = workspacePath;
        Current = this;
        Closed += (_, _) => { if (Current == this) Current = null; };

        // Wallpaper (authentic XP default or saved config)
        var wpRel = string.IsNullOrEmpty(cfg?.Wallpaper) ? "bliss.jpg" : cfg!.Wallpaper;
        SetWallpaperLocal(wpRel);

        TaskbarItems.ItemsSource = _taskbarItems;

        // Icon images from authentic XP assets
        SetIcon(ImgMyComputer, "icons/my_computer.png");
        SetIcon(ImgWlm, "icons/msn.png");
        SetIcon(ImgExplorer, "icons/explorer.png");
        SetIcon(ImgNotepad, "icons/notepad.png");
        SetIcon(ImgCmd, "icons/cmd.png");
        SetIcon(ImgRecycle, "icons/recycle_empty.png");
        SetIcon(MenuImgWlm, "icons/msn.png");
        SetIcon(MenuImgExplorer, "icons/explorer.png");
        SetIcon(MenuImgNotepad, "icons/notepad.png");
        SetIcon(MenuImgCmd, "icons/cmd.png");
        SetIcon(MenuImgCalc, "icons/calc.png");
        SetIcon(MenuImgDocs, "icons/folder.png");
        SetIcon(MenuImgComputer, "icons/my_computer.png");
        SetIcon(MenuImgCpl, "icons/control_panel.png");
        SetIcon(MenuImgHelp, "icons/help.png");
        SetIcon(MenuImgRun, "icons/run.png");
        SetIcon(MenuImgLogOff, "icons/logoff.png");
        SetIcon(MenuImgTurnOff, "icons/shutdown.png");
        StartMenuAvatar.Source = XpSim.XpAssets.ImageAuto(
            string.IsNullOrEmpty(_cfg?.UserTile) ? "usertiles/dog.png" : _cfg.UserTile);
        StartMenuUser.Text = _cfg?.UserName ?? "User";
        StartButton.Tag = XpSim.XpAssets.ImageAuto("icons/xp_throbber_flag.png");

        // Desktop icon context menus — "Open" re-triggers the icon's click action
        foreach (var iconBtn in DesktopIcons.Children.OfType<Button>())
        {
            var btn = iconBtn;
            var menu = new ContextMenu();
            var open = new MenuItem { Header = "Open" };
            open.Click += (_, _) => btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            menu.Items.Add(open);
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Create Shortcut", IsEnabled = false });
            menu.Items.Add(new MenuItem { Header = "Delete", IsEnabled = false });
            menu.Items.Add(new MenuItem { Header = "Rename", IsEnabled = false });
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Properties", IsEnabled = false });
            btn.ContextMenu = menu;
        }

        // Clock timer
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += UpdateClock;
        _clockTimer.Start();
        UpdateClock(this, EventArgs.Empty);

        // Initialize windows
        Loaded += OnLoaded;

        // Screensaver idle monitoring
        XpSim.XpScreensaverHost.Attach(this, _cfg);
    }

    /// <summary>Persist display personalization (wallpaper/screensaver/theme) to install config.</summary>
    private void PersistConfig()
    {
        if (_cfg != null) XpSim.InstallStore.SaveConfig(_cfg, _workspacePath);
    }

    /// <summary>Called by Display Properties Screen Saver tab.</summary>
    public void SetScreensaverConfig(string mode, int waitMinutes)
    {
        if (_cfg != null)
        {
            _cfg.ScreenSaver = mode;
            _cfg.ScreenSaverWait = waitMinutes;
            PersistConfig();
        }
        XpSim.XpScreensaverHost.Configure(mode, waitMinutes);
    }

    /// <summary>Preview a screensaver mode immediately (Display Properties "Preview" button).</summary>
    public static void PreviewScreensaver(string mode) => XpSim.XpScreensaverHost.Preview(mode);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // No auto-launch — user opens apps from desktop icons / Start menu
    }

    // ===== App launchers =====

    private void LaunchWlm()
    {
        // WLM lives inside the sim: V2 WLMLive chrome hosted as XP windows.
        WlmSim.WlmSimHost.OpenMainWindow();
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void LaunchSandbox()
    {
        var xp = new XpWindow { Title = "Sandbox Controls", Width = 440, Height = 380, WindowIcon = XpIcon("folder.png") };
        xp.WindowContent = new SandboxWindow(_backend);
        RegisterWindow(xp, "Sandbox Controls");
    }

    private static System.Windows.Media.ImageSource? XpIcon(string rel)
        => XpSim.XpAssets.ImageAuto("icons/" + rel);

    private void SetIcon(System.Windows.Controls.Image img, string rel)
    {
        // Try xp_assets first, then wlm assets (msn.ico lives under Assets/wlm)
        var src = XpSim.XpAssets.Image(rel);
        if (src == null)
        {
            var p = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", rel.Replace('/', '\\'));
            if (System.IO.File.Exists(p))
            {
                try
                {
                    src = new BitmapImage(new Uri(p));
                    src.Freeze();
                }
                catch { }
            }
        }
        if (src != null) img.Source = src;
    }

    private void IconMyComputer_Click(object sender, RoutedEventArgs e) => MenuMyComputer_Click(sender, e);
    private void IconRecycle_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "Recycle Bin", Width = 560, Height = 400, WindowIcon = XpIcon("recycle.png") };
        xp.WindowContent = BuildRecycleBin();
        RegisterWindow(xp, "Recycle Bin");
    }

    private static UIElement BuildRecycleBin()
    {
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock
        {
            Text = "Recycle Bin", FontSize = 20, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x24, 0x6A)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        sp.Children.Add(new TextBlock
        {
            Text = "The Recycle Bin is empty.\n\nItems you delete in this simulator are not moved here — " +
                   "delete is not yet implemented for sandbox files.",
            FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap
        });
        return sp;
    }
    private void MenuLogOff_Click(object sender, RoutedEventArgs e)
    {
        StartMenu.Visibility = Visibility.Collapsed;
        _onLogOff?.Invoke();
    }
    private void MenuTurnOff_Click(object sender, RoutedEventArgs e)
    {
        StartMenu.Visibility = Visibility.Collapsed;
        _onShutdown?.Invoke();
    }
    private void MenuExplorerAll_Click(object sender, RoutedEventArgs e) => IconExplorer_Click(sender, e);
    private void MenuMyDocs_Click(object sender, RoutedEventArgs e) => IconExplorer_Click(sender, e);
    private void MenuMyComputer_Click(object sender, RoutedEventArgs e) => IconExplorer_Click(sender, e);
    private void IconCalc_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "Calculator", Width = 260, Height = 230, WindowIcon = XpIcon("calc.png") };
        xp.WindowContent = new CalcWindow();
        RegisterWindow(xp, "Calculator");
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void SetWallpaperLocal(string fileName)
    {
        var img = XpSim.XpAssets.ImageAuto("wallpapers/" + fileName);
        if (img != null)
            DesktopArea.Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
        if (_cfg != null) { _cfg.Wallpaper = fileName; PersistConfig(); }
    }

    /// <summary>Called by Display Properties applet.</summary>
    public static void SetWallpaperGlobal(string fileName) => Current?.SetWallpaperLocal(fileName);

    /// <summary>Resolve an image under Assets/xp_assets first, then Assets/ root (wlm assets).</summary>
    private static System.Windows.Media.Imaging.BitmapImage? ImageAny(string rel)
    {
        var img = XpSim.XpAssets.ImageAuto(rel);
        if (img != null) return img;
        var p = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", rel.Replace('/', '\\'));
        if (System.IO.File.Exists(p))
        {
            try
            {
                var b = new BitmapImage(new Uri(p));
                b.Freeze();
                return b;
            }
            catch { }
        }
        return null;
    }

    // ThemeId -> (wallpaper asset path, accent color)
    private static readonly Dictionary<string, (string Wallpaper, Color Accent)> XpThemes = new()
    {
        ["luna-blue"]   = ("wallpapers/bliss.jpg",        Color.FromRgb(0x24, 0x5E, 0xDC)),
        ["luna-olive"]  = ("wlm/scenes/0004.png",        Color.FromRgb(0x76, 0x8A, 0x4E)),
        ["luna-silver"] = ("wlm/scenes/0002.jpg",        Color.FromRgb(0x99, 0x99, 0xA8)),
        ["royale"]      = ("wlm/scenes/0007.png",        Color.FromRgb(0x09, 0x47, 0xA5)),
        ["zune"]        = ("wlm/scenes/zune_04.jpg",     Color.FromRgb(0x22, 0x22, 0x22)),
        ["classic"]     = ("wlm/background_teal.jpg",    Color.FromRgb(0x0A, 0x24, 0x6A)),
    };

    /// <summary>Display Properties Themes tab — applies wallpaper + taskbar accent.</summary>
    public void ApplyTheme(string themeId)
    {
        if (!XpThemes.TryGetValue(themeId, out var t))
            XpThemes.TryGetValue("luna-blue", out t);

        var img = ImageAny(t.Wallpaper);
        if (img != null)
            DesktopArea.Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill };

        // Taskbar + start button accent
        var accent = new SolidColorBrush(t.Accent);
        var accentDark = new SolidColorBrush(Color.FromRgb(
            (byte)(t.Accent.R * 0.6), (byte)(t.Accent.G * 0.6), (byte)(t.Accent.B * 0.6)));
        var accentLight = new SolidColorBrush(Color.FromRgb(
            (byte)Math.Min(255, t.Accent.R + 30), (byte)Math.Min(255, t.Accent.G + 30), (byte)Math.Min(255, t.Accent.B + 30)));
        Taskbar.Background = new LinearGradientBrush(accentDark.Color, accentLight.Color, 90);
        StartButton.Background = accent;

        if (_cfg != null) { _cfg.Theme = themeId; PersistConfig(); }
    }

    /// <summary>Called by Display Properties Themes tab.</summary>
    public static void ApplyThemeGlobal(string themeId) => Current?.ApplyTheme(themeId);

    private void MenuControlPanel_Click(object sender, RoutedEventArgs e) => LaunchControlPanel();

    private void LaunchControlPanel()
    {
        var xp = new XpWindow { Title = "Control Panel", Width = 660, Height = 500, WindowIcon = XpIcon("control_panel.png") };
        var cpl = new ControlPanelControl();
        cpl.LaunchApplet += applet => LaunchAppletWindow(applet);
        xp.WindowContent = cpl;
        RegisterWindow(xp, "Control Panel");
        StartMenu.Visibility = Visibility.Collapsed;
        AllProgramsFlyout.IsOpen = false;
    }

    private void LaunchAppletWindow(string applet)
    {
        UIElement? content = applet switch
        {
            "desk.cpl" => new DisplayPropsControl(),
            "nusrmgr.cpl" => new UserAccountsControl(),
            "sysdm.cpl" => new SystemPropsControl(),
            "mmsys.cpl" => new SoundsAudioControl(),
            "ncpa.cpl" => new NetConnectionsControl(),
            "timedate.cpl" => new DateTimeControl(),
            "appwiz.cpl" => new AddRemoveControl(),
            "wscui.cpl" => new SecurityCenterControl(),
            _ => null
        };
        if (content == null) return;
        var names = new Dictionary<string, string>
        {
            ["desk.cpl"] = "Display Properties", ["nusrmgr.cpl"] = "User Accounts",
            ["sysdm.cpl"] = "System Properties", ["mmsys.cpl"] = "Sounds and Audio Devices",
            ["ncpa.cpl"] = "Network Connections", ["timedate.cpl"] = "Date and Time Properties",
            ["appwiz.cpl"] = "Add or Remove Programs", ["wscui.cpl"] = "Security Center"
        };
        var xp = new XpWindow { Title = names.GetValueOrDefault(applet, applet), Width = 560, Height = 460, WindowIcon = XpIcon("desk.png") };
        xp.WindowContent = content;
        RegisterWindow(xp, names.GetValueOrDefault(applet, applet));
    }
    private void MenuHelp_Click(object sender, RoutedEventArgs e)
    {
        StartMenu.Visibility = Visibility.Collapsed;
        var xp = new XpWindow { Title = "Windows Help", Width = 520, Height = 380, WindowIcon = XpIcon("help.png") };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock
        {
            Text = "Windows XP Simulator — Help", FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x24, 0x6A)),
            Margin = new Thickness(0, 0, 0, 10)
        });
        sp.Children.Add(new TextBlock
        {
            Text = "This is a Windows XP simulator running inside the Dory studio.\n\n" +
                   "• Double-click desktop icons or use the Start menu to launch apps.\n" +
                   "• Drag windows by their title bar; use the caption buttons to min/max/close.\n" +
                   "• Windows Live Messenger lists IBA sessions as contacts — double-click to chat.\n" +
                   "• Right-click the desktop, taskbar, Start button, or icons for menus.\n" +
                   "• Turn off / restart from Start > Turn Off Computer.",
            FontSize = 12, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap
        });
        xp.WindowContent = sp;
        RegisterWindow(xp, "Windows Help");
    }
    private void MenuRun_Click(object sender, RoutedEventArgs e) { LaunchRunDialog(); StartMenu.Visibility = Visibility.Collapsed; }
    private void MenuAllPrograms_Click(object sender, RoutedEventArgs e)
    {
        AllProgramsFlyout.IsOpen = !AllProgramsFlyout.IsOpen;
    }

    private void IconSolitaire_Click(object sender, RoutedEventArgs e) => LaunchGame<SolitaireControl>("Solitaire", 660, 540, "solitaire.png");
    private void IconFreeCell_Click(object sender, RoutedEventArgs e) => LaunchGame<FreeCellControl>("FreeCell", 720, 560, "freecell.png");
    private void IconSpider_Click(object sender, RoutedEventArgs e) => LaunchGame<SpiderControl>("Spider Solitaire", 800, 620, "spider.png");
    private void IconMinesweeper_Click(object sender, RoutedEventArgs e) => LaunchGame<MinesweeperControl>("Minesweeper", 300, 380, "minesweeper.png");
    private void IconPinball_Click(object sender, RoutedEventArgs e) => LaunchGame<PinballControl>("3D Pinball Space Cadet", 480, 640, "pinball.png");
    private void IconPaint_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "untitled - Paint", Width = 640, Height = 480, WindowIcon = XpIcon("mspaint.png") };
        xp.WindowContent = new PaintControl();
        RegisterWindow(xp, "Paint");
        AllProgramsFlyout.IsOpen = false;
    }
    private void IconTaskMgr_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "Windows Task Manager", Width = 460, Height = 400, WindowIcon = XpIcon("taskmgr.png") };
        xp.WindowContent = new TaskMgrControl();
        RegisterWindow(xp, "Task Manager");
        AllProgramsFlyout.IsOpen = false;
    }
    private void IconRun_Click(object sender, RoutedEventArgs e) => LaunchRunDialog();

    private void LaunchGame<T>(string title, double w, double h, string? icon = null) where T : UIElement, new()
    {
        var xp = new XpWindow { Title = title, Width = w, Height = h, WindowIcon = icon == null ? null : XpIcon(icon) };
        var game = new T();
        if (game is SolitaireControl s) s.RequestClose += () => xp.TriggerClose();
        if (game is FreeCellControl fc) fc.RequestClose += () => xp.TriggerClose();
        if (game is SpiderControl sp) sp.RequestClose += () => xp.TriggerClose();
        if (game is MinesweeperControl m) m.RequestClose += () => xp.TriggerClose();
        if (game is PinballControl p) p.RequestClose += () => xp.TriggerClose();
        xp.WindowContent = game;
        RegisterWindow(xp, title);
        AllProgramsFlyout.IsOpen = false;
    }

    private void LaunchRunDialog()
    {
        var dlg = new Window
        {
            Title = "Run", Width = 340, Height = 150, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8)),
            FontFamily = new FontFamily("Tahoma")
        };
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = "Type the name of a program, folder, or document, and Windows will open it for you.", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        var tb = new TextBox { Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetRow(tb, 1); grid.Children.Add(tb);
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 75, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        btns.Children.Add(ok); btns.Children.Add(cancel);
        Grid.SetRow(btns, 2); grid.Children.Add(btns);
        dlg.Content = grid;
        ok.Click += (_, _) =>
        {
            var cmd = tb.Text.Trim().ToLowerInvariant();
            dlg.Close();
            switch (cmd)
            {
                case "notepad": IconNotepad_Click(null!, null!); break;
                case "calc": IconCalc_Click(null!, null!); break;
                case "mspaint": IconPaint_Click(null!, null!); break;
                case "cmd": IconCmd_Click(null!, null!); break;
                case "explorer": IconExplorer_Click(null!, null!); break;
                case "sol": case "solitaire": IconSolitaire_Click(null!, null!); break;
                case "freecell": IconFreeCell_Click(null!, null!); break;
                case "spider": IconSpider_Click(null!, null!); break;
                case "winmine": case "minesweeper": IconMinesweeper_Click(null!, null!); break;
                case "taskmgr": IconTaskMgr_Click(null!, null!); break;
                case "control": MenuControlPanel_Click(null!, null!); break;
                default:
                    DoryNative.XpSim.SimMessageBox.Show(dlg, $"Cannot find '{tb.Text}'. Make sure you typed the name correctly.",
                        "Run", MessageBoxButton.OK, MessageBoxImage.Error);
                    break;
            }
        };
        cancel.Click += (_, _) => dlg.Close();
        dlg.ShowDialogSim();
        AllProgramsFlyout.IsOpen = false;
    }

    private void IconWlm_Click(object sender, RoutedEventArgs e) => LaunchWlm();
    private void MenuWlm_Click(object sender, RoutedEventArgs e) { LaunchWlm(); StartMenu.Visibility = Visibility.Collapsed; }

    private void IconExplorer_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "Local Disk (C:)", Width = 640, Height = 420, WindowIcon = XpIcon("explorer.png") };
        xp.WindowContent = new ExplorerWindow(_backend);
        RegisterWindow(xp, "Explorer");
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void IconNotepad_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "Untitled - Notepad", Width = 560, Height = 420, WindowIcon = XpIcon("notepad.png") };
        xp.WindowContent = new NotepadWindow();
        RegisterWindow(xp, "Notepad");
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void IconCmd_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "C:\\WINDOWS\\system32\\cmd.exe", Width = 640, Height = 400, WindowIcon = XpIcon("cmd.png") };
        xp.WindowContent = new CmdWindow(_backend);
        RegisterWindow(xp, "Command Prompt");
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void IconSql_Click(object sender, RoutedEventArgs e)
    {
        var xp = new XpWindow { Title = "SQL Authority 3D", Width = 720, Height = 540, WindowIcon = XpIcon("drive.png") };
        xp.WindowContent = new SqlAuthorityWindow(_backend);
        RegisterWindow(xp, "SQL Authority");
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void MenuSandbox_Click(object sender, RoutedEventArgs e)
    {
        LaunchSandbox();
        StartMenu.Visibility = Visibility.Collapsed;
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        Clock.Text = DateTime.Now.ToString("h:mm tt");
    }

    public void RegisterWindow(XpWindow window, string title)
    {
        window.Title = title;
        window.Tag = Guid.NewGuid().ToString();

        // Position window on desktop canvas
        Canvas.SetLeft(window, 50 + _taskbarItems.Count * 30);
        Canvas.SetTop(window, 50 + _taskbarItems.Count * 30);

        // Bring to front
        BringToFront(window);

        // Add to canvas
        WindowCanvas.Children.Add(window);

        // Add to taskbar
        _taskbarItems.Add(new TaskbarItem
        {
            WindowId = window.Tag.ToString()!,
            Title = title,
            Icon = window.WindowIcon
        });

        // Wire up close handler
        window.WindowClosed += (s, _) =>
        {
            var closingWindow = s as XpWindow;
            var item = _taskbarItems.FirstOrDefault(i => i.WindowId == closingWindow?.Tag?.ToString());
            if (item != null)
            {
                _taskbarItems.Remove(item);
            }
            WindowCanvas.Children.Remove(closingWindow!);
        };
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartMenu.Visibility = StartMenu.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TaskbarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string windowId)
        {
            var window = WindowCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(c => c.Tag?.ToString() == windowId) as XpWindow;

            if (window != null)
            {
                if (window.Visibility == Visibility.Visible)
                {
                    BringToFront(window);
                }
                else
                {
                    window.XpWindowState = System.Windows.WindowState.Normal;
                    window.Visibility = Visibility.Visible;
                    BringToFront(window);
                }
            }
        }
    }

    public void BringToFront(XpWindow window)
    {
        _highestZIndex++;
        Panel.SetZIndex(window, _highestZIndex);
    }

    // ===== Context menus =====

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in VisualDescendants(child)) yield return d;
        }
    }

    private static string IconLabel(Button btn)
    {
        // Icon buttons contain a StackPanel with an Image + TextBlock label
        foreach (var child in VisualDescendants(btn))
            if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
                return tb.Text.Replace("\n", " ").Replace("&#x0a;", " ");
        return "";
    }

    private void CtxArrangeIcons_Click(object sender, RoutedEventArgs e)
    {
        // Sort desktop icons alphabetically by label (XP "Arrange Icons")
        var sorted = DesktopIcons.Children.OfType<Button>()
            .OrderBy(IconLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        DesktopIcons.Children.Clear();
        foreach (var b in sorted) DesktopIcons.Children.Add(b);
    }

    private void CtxRefresh_Click(object sender, RoutedEventArgs e)
    {
        DesktopArea.UpdateLayout();
        // Brief wallpaper flicker like a real refresh
        var brush = DesktopArea.Background;
        DesktopArea.Background = null;
        DesktopArea.Dispatcher.BeginInvoke(new Action(() => DesktopArea.Background = brush),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void CtxDesktopProperties_Click(object sender, RoutedEventArgs e) => LaunchAppletWindow("desk.cpl");
    private void CtxTaskbarProperties_Click(object sender, RoutedEventArgs e) => LaunchAppletWindow("desk.cpl");

    private List<XpWindow> OpenWindows() =>
        WindowCanvas.Children.OfType<XpWindow>().Where(w => w.Visibility == Visibility.Visible).ToList();

    private void CtxCascade_Click(object sender, RoutedEventArgs e)
    {
        int i = 0;
        foreach (var w in OpenWindows())
        {
            Canvas.SetLeft(w, 40 + i * 28);
            Canvas.SetTop(w, 30 + i * 28);
            BringToFront(w);
            i++;
        }
    }

    private void CtxTileH_Click(object sender, RoutedEventArgs e)
    {
        var wins = OpenWindows();
        if (wins.Count == 0) return;
        double w = DesktopArea.ActualWidth / wins.Count;
        double h = DesktopArea.ActualHeight;
        for (int i = 0; i < wins.Count; i++)
        {
            var win = wins[i];
            win.Width = Math.Max(200, w - 8);
            win.Height = Math.Max(150, h - 8);
            Canvas.SetLeft(win, i * w + 4);
            Canvas.SetTop(win, 4);
            BringToFront(win);
        }
    }

    private void CtxTileV_Click(object sender, RoutedEventArgs e)
    {
        var wins = OpenWindows();
        if (wins.Count == 0) return;
        double w = DesktopArea.ActualWidth;
        double h = DesktopArea.ActualHeight / wins.Count;
        for (int i = 0; i < wins.Count; i++)
        {
            var win = wins[i];
            win.Width = Math.Max(200, w - 8);
            win.Height = Math.Max(150, h - 8);
            Canvas.SetLeft(win, 4);
            Canvas.SetTop(win, i * h + 4);
            BringToFront(win);
        }
    }

    private void CtxShowDesktop_Click(object sender, RoutedEventArgs e)
    {
        // Minimize all windows; clicking a taskbar button restores them
        foreach (var w in OpenWindows()) w.Visibility = Visibility.Collapsed;
    }

    // The taskbar is fixed-position, so "Lock the Taskbar" is inherently
    // satisfied — the checkable item reflects that state.
    private void CtxLockTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi) _taskbarLocked = mi.IsChecked == true;
    }

    private void CtxStartOpen_Click(object sender, RoutedEventArgs e) => StartButton_Click(sender, e);
    private void CtxStartRun_Click(object sender, RoutedEventArgs e) => LaunchRunDialog();
}

public class TaskbarItem
{
    public string WindowId { get; set; } = "";
    public string Title { get; set; } = "";
    public System.Windows.Media.ImageSource? Icon { get; set; }
}
