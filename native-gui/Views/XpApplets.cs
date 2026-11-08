using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoryNative.XpSim;

namespace DoryNative.Views;

/// <summary>Control Panel — category/classic applet launcher (desk.cpl etc.).</summary>
public class ControlPanelControl : UserControl
{
    public event Action<string>? LaunchApplet;

    private static readonly (string Name, string Icon, string Applet)[] Applets =
    {
        ("Appearance and Themes", "icons/display.png", "desk.cpl"),
        ("User Accounts", "icons/usertiles.png", "nusrmgr.cpl"),
        ("System Properties", "icons/sysdm.png", "sysdm.cpl"),
        ("Sounds and Audio Devices", "icons/audio.png", "mmsys.cpl"),
        ("Network Connections", "icons/network.png", "ncpa.cpl"),
        ("Date and Time", "icons/datetime.png", "timedate.cpl"),
        ("Add or Remove Programs", "icons/add_remove.png", "appwiz.cpl"),
        ("Security Center", "icons/security.png", "wscui.cpl"),
    };

    public ControlPanelControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new DockPanel { Margin = new Thickness(10) };

        var header = new TextBlock { Text = "Pick a category", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x99)), Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var wrap = new WrapPanel();
        foreach (var (name, icon, applet) in Applets)
        {
            var card = new Border
            {
                Width = 150, Height = 110, Margin = new Thickness(6),
                Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xAC, 0xA8, 0x99)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Cursor = System.Windows.Input.Cursors.Hand
            };
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
            var img = XpAssets.Image(icon);
            if (img != null)
                sp.Children.Add(new Image { Source = img, Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });
            sp.Children.Add(new TextBlock { Text = name, FontSize = 10, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            card.Child = sp;
            card.Tag = applet;
            card.MouseLeftButtonDown += (_, _) => LaunchApplet?.Invoke(applet);
            wrap.Children.Add(card);
        }
        var scroll = new ScrollViewer { Content = wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        Content = root;
    }
}

/// <summary>Helpers shared by applets.</summary>
internal static class XpApplet
{
    public static TextBlock H(string t, double size = 16) => new()
    {
        Text = t, FontSize = size, FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x99)), Margin = new Thickness(0, 0, 0, 8)
    };

    public static TextBlock Body(string t) => new()
    {
        Text = t, FontSize = 11, TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), Margin = new Thickness(0, 0, 0, 8)
    };

    public static Border Card(UIElement child) => new()
    {
        Child = child, Background = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xAC, 0xA8, 0x99)), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8)
    };

    public static Button Btn(string text, Action? onClick = null)
    {
        var b = new Button
        {
            Content = text, Padding = new Thickness(12, 4, 12, 4), FontSize = 11,
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x71, 0x6F, 0x64)),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        if (onClick != null) b.Click += (_, _) => onClick();
        return b;
    }
}

/// <summary>desk.cpl — Display Properties: themes, wallpaper, screen settings.</summary>
public class DisplayPropsControl : UserControl
{
    public DisplayPropsControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new DockPanel { Margin = new Thickness(12) };

        var tabs = new TabControl { Margin = new Thickness(0, 0, 0, 8) };

        // Themes tab
        var themeTab = new TabItem { Header = "Themes" };
        var themeSp = new StackPanel { Margin = new Thickness(8) };
        themeSp.Children.Add(XpApplet.H("Theme"));
        themeSp.Children.Add(XpApplet.Body("Select a visual style for your desktop."));
        var themeList = new ListBox { Height = 140, Background = Brushes.White };
        var themes = new (string Id, string Name)[]
        {
            ("luna-blue",   "Windows XP (Default / Luna Blue)"),
            ("luna-olive",  "Windows XP (Olive Green)"),
            ("luna-silver", "Windows XP (Silver)"),
            ("royale",      "Windows XP Royale (Energy Blue)"),
            ("zune",        "Windows XP Zune (Noir)"),
            ("classic",     "Windows Classic"),
        };
        foreach (var t in themes) themeList.Items.Add(t.Name);
        themeList.SelectedIndex = 0;
        themeSp.Children.Add(themeList);
        themeSp.Children.Add(XpApplet.Btn("Apply", () =>
        {
            if (themeList.SelectedIndex >= 0)
                XpDesktop.ApplyThemeGlobal(themes[themeList.SelectedIndex].Id);
        }));
        themeTab.Content = new ScrollViewer { Content = themeSp };
        tabs.Items.Add(themeTab);

        // Desktop tab (wallpaper)
        var deskTab = new TabItem { Header = "Desktop" };
        var deskSp = new StackPanel { Margin = new Thickness(8) };
        deskSp.Children.Add(XpApplet.H("Background"));
        deskSp.Children.Add(XpApplet.Body("Choose a background image for your desktop."));
        var wpList = new ListBox { Height = 160, Background = Brushes.White };
        var wallpapers = new List<string>();
        var wpDir = Path.Combine(AppContext.BaseDirectory, "Assets", "xp_assets", "wallpapers");
        if (Directory.Exists(wpDir))
            wallpapers.AddRange(Directory.GetFiles(wpDir, "*.jpg").Concat(Directory.GetFiles(wpDir, "*.png")).Concat(Directory.GetFiles(wpDir, "*.bmp")));
        if (wallpapers.Count == 0) wallpapers.Add("bliss.jpg");
        foreach (var w in wallpapers)
            wpList.Items.Add(Path.GetFileName(w));
        wpList.SelectedIndex = 0;
        deskSp.Children.Add(wpList);
        var applyWp = XpApplet.Btn("Apply", () =>
        {
            var sel = wpList.SelectedItem as string;
            if (sel != null) XpDesktop.SetWallpaperGlobal(sel);
        });
        deskSp.Children.Add(applyWp);
        deskTab.Content = new ScrollViewer { Content = deskSp };
        tabs.Items.Add(deskTab);

        // Screen Settings tab
        var scrTab = new TabItem { Header = "Screen Settings" };
        var scrSp = new StackPanel { Margin = new Thickness(8) };
        scrSp.Children.Add(XpApplet.H("Resolution"));
        scrSp.Children.Add(XpApplet.Body("Drag the slider to change screen resolution. (Simulated)"));
        var slider = new Slider { Minimum = 800, Maximum = 1920, Value = 1440, TickFrequency = 40, IsSnapToTickEnabled = true, Width = 300 };
        scrSp.Children.Add(slider);
        scrSp.Children.Add(XpApplet.Body($"Current: {(int)slider.Value} x 900"));
        scrTab.Content = new ScrollViewer { Content = scrSp };
        tabs.Items.Add(scrTab);

        // Screen Saver tab
        var ssTab = new TabItem { Header = "Screen Saver" };
        var ssSp = new StackPanel { Margin = new Thickness(8) };
        ssSp.Children.Add(XpApplet.H("Screen saver"));
        ssSp.Children.Add(XpApplet.Body("Choose a screen saver and wait time."));
        var savers = new (string Id, string Name)[]
        {
            ("pipes",          "Pipes"),
            ("starfield",      "Starfield Simulation"),
            ("flying_windows", "3D Flying Windows"),
            ("mystify",        "Mystify"),
        };
        var saverList = new ListBox { Height = 120, Background = Brushes.White };
        foreach (var s in savers) saverList.Items.Add(s.Name);
        saverList.SelectedIndex = 0;
        ssSp.Children.Add(saverList);

        var waitRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        waitRow.Children.Add(new TextBlock { Text = "Wait:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), FontSize = 11 });
        var waitBox = new ComboBox { Width = 70, SelectedIndex = 2 };
        foreach (var m in new[] { "1", "5", "10", "30" }) waitBox.Items.Add(m);
        waitRow.Children.Add(waitBox);
        waitRow.Children.Add(new TextBlock { Text = " minutes", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), FontSize = 11 });
        ssSp.Children.Add(waitRow);

        var ssBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        ssBtns.Children.Add(XpApplet.Btn("Preview", () =>
        {
            if (saverList.SelectedIndex >= 0)
                XpDesktop.PreviewScreensaver(savers[saverList.SelectedIndex].Id);
        }));
        ssBtns.Children.Add(XpApplet.Btn("Settings", () =>
            DoryNative.XpSim.SimMessageBox.Show("This screensaver uses its default settings.", "Screen Saver Settings", MessageBoxButton.OK, MessageBoxImage.Information)));
        ssSp.Children.Add(ssBtns);
        ssTab.Content = new ScrollViewer { Content = ssSp };
        tabs.Items.Add(ssTab);

        root.Children.Add(tabs);

        void ApplyAll()
        {
            if (themeList.SelectedIndex >= 0)
                XpDesktop.ApplyThemeGlobal(themes[themeList.SelectedIndex].Id);
            if (wpList.SelectedItem is string wp)
                XpDesktop.SetWallpaperGlobal(wp);
            if (saverList.SelectedIndex >= 0)
            {
                int wait = int.TryParse(waitBox.SelectedItem as string, out var wm) ? wm : 10;
                XpDesktop.Current?.SetScreensaverConfig(savers[saverList.SelectedIndex].Id, wait);
            }
        }
        void CloseSelf() => FindParent<XpWindow>(this)?.TriggerClose();

        var okRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        okRow.Children.Add(XpApplet.Btn("OK", () => { ApplyAll(); CloseSelf(); }));
        okRow.Children.Add(XpApplet.Btn("Cancel", CloseSelf));
        okRow.Children.Add(XpApplet.Btn("Apply", ApplyAll));
        DockPanel.SetDock(okRow, Dock.Bottom);
        root.Children.Add(okRow);
        Content = root;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var p = VisualTreeHelper.GetParent(child);
        while (p != null)
        {
            if (p is T t) return t;
            p = VisualTreeHelper.GetParent(p);
        }
        return null;
    }
}

/// <summary>nusrmgr.cpl — User Accounts.</summary>
public class UserAccountsControl : UserControl
{
    public UserAccountsControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("User Accounts"));
        root.Children.Add(XpApplet.Body("You can change an account picture or password, create a new account, or make changes to an existing account."));

        var list = new ListBox { Height = 200, Background = Brushes.White, Margin = new Thickness(0, 0, 0, 8) };
        list.Items.Add("Rackneh (Computer administrator)");
        list.Items.Add("Dory AI Bot (Standard user)");
        root.Children.Add(list);

        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        btns.Children.Add(XpApplet.Btn("Create a new account", () =>
            DoryNative.XpSim.SimMessageBox.Show("Account creation is managed by the Windows XP installer (OOBE).", "User Accounts", MessageBoxButton.OK, MessageBoxImage.Information)));
        btns.Children.Add(XpApplet.Btn("Change my picture", () => XpAudio.Click()));
        btns.Children.Add(XpApplet.Btn("Change my password", () =>
            DoryNative.XpSim.SimMessageBox.Show("Password change is managed by the setup credentials stored in the install config.", "User Accounts", MessageBoxButton.OK, MessageBoxImage.Information)));
        root.Children.Add(btns);
        Content = new ScrollViewer { Content = root };
    }
}

/// <summary>sysdm.cpl — System Properties.</summary>
public class SystemPropsControl : UserControl
{
    public SystemPropsControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("System Properties"));

        var card = XpApplet.Card(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "System:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBlock { Text = "Microsoft Windows XP\nProfessional\nVersion 2002\nService Pack 3", FontSize = 11, Margin = new Thickness(0, 2, 0, 8) },
                new TextBlock { Text = "Computer:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBlock { Text = "Intel(R) Pentium(R) 4 CPU 3.00GHz\n1023 MB of RAM", FontSize = 11, Margin = new Thickness(0, 2, 0, 8) },
                new TextBlock { Text = "Computer name:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBlock { Text = "RACKNEH-XP\nWorkgroup: WORKGROUP", FontSize = 11 }
            }
        });
        root.Children.Add(card);
        root.Children.Add(XpApplet.Body("This simulated workstation runs the Dory LLM backend (Python/FastAPI) and the WPF XP shell."));
        Content = new ScrollViewer { Content = root };
    }
}

/// <summary>mmsys.cpl — Sounds and Audio Devices.</summary>
public class SoundsAudioControl : UserControl
{
    private static readonly (string Event, string Sound)[] Events =
    {
        ("Start Windows", "startup.wav"), ("Exit Windows", "shutdown.wav"),
        ("Logon", "logon.wav"), ("Logoff", "logoff.wav"),
        ("Critical Stop", "error.wav"), ("Exclamation", "exclamation.wav"),
        ("Asterisk", "ding.wav"), ("Question", "notify.wav"),
        ("New Mail Notification", "notify.wav"), ("Recycling Bin - Empty", "recycle.wav"),
        ("Minimize", "minimize.wav"), ("Restore Down", "restore.wav"), ("Menu Command", "menu.wav"),
        ("Device Connect", "hardware_insert.wav"), ("Device Disconnect", "hardware_remove.wav"),
        ("Windows Balloon", "balloon.wav"), ("Tada!", "tada.wav"),
    };

    public SoundsAudioControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("Sounds and Audio Devices"));
        root.Children.Add(XpApplet.Body("Program events are associated with sounds. Click Preview to hear one."));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var list = new ListBox { Background = Brushes.White, Height = 320 };
        foreach (var (ev, snd) in Events)
            list.Items.Add($"{ev}  →  {snd}");
        Grid.SetColumn(list, 0); grid.Children.Add(list);
        var btns = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        btns.Children.Add(XpApplet.Btn("▶ Preview", () =>
        {
            if (list.SelectedIndex >= 0) XpAudio.Play($"sounds/{Events[list.SelectedIndex].Sound}");
        }));
        Grid.SetColumn(btns, 1); grid.Children.Add(btns);
        root.Children.Add(grid);
        Content = new ScrollViewer { Content = root };
    }
}

/// <summary>ncpa.cpl — Network Connections.</summary>
public class NetConnectionsControl : UserControl
{
    public NetConnectionsControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("Network Connections"));

        var card = XpApplet.Card(new StackPanel
        {
            Children =
            {
                MakeConn("Local Area Connection", "Intel 82566DM-2 Gigabit", true),
                MakeConn("Wireless Network Connection", "Not present", false),
                MakeConn("1394 Connection", "Not present", false)
            }
        });
        root.Children.Add(card);
        root.Children.Add(XpApplet.Body("The simulator bridges to the host machine's localhost networking. WLM connects via the backend on 127.0.0.1."));
        Content = new ScrollViewer { Content = root };
    }

    private static UIElement MakeConn(string name, string desc, bool connected)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        var img = XpAssets.Image(connected ? "icons/network.png" : "icons/network_drive.png");
        if (img != null) sp.Children.Add(new Image { Source = img, Width = 24, Height = 24, Margin = new Thickness(0, 0, 8, 0) });
        sp.Children.Add(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = name, FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBlock { Text = desc + (connected ? "  —  Connected, 100.0 Mbps" : "  —  Disabled"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) }
            }
        });
        return sp;
    }
}

/// <summary>timedate.cpl — Date and Time.</summary>
public class DateTimeControl : UserControl
{
    public DateTimeControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("Date and Time Properties"));

        var card = XpApplet.Card(new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Date:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new Calendar { Width = 220, Margin = new Thickness(0, 4, 0, 10) },
                new TextBlock { Text = "Time:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBox { Text = DateTime.Now.ToString("h:mm:ss tt"), IsReadOnly = true, Width = 120, Margin = new Thickness(0, 4, 0, 10) },
                new TextBlock { Text = "Time zone:", FontWeight = FontWeights.Bold, FontSize = 11 },
                new TextBox { Text = "(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna", IsReadOnly = true, Width = 380 }
            }
        });
        root.Children.Add(card);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(XpApplet.Btn("OK", () => { }));
        row.Children.Add(XpApplet.Btn("Cancel", () => { }));
        row.Children.Add(XpApplet.Btn("Apply", () => { }));
        root.Children.Add(row);
        Content = new ScrollViewer { Content = root };
    }
}

/// <summary>appwiz.cpl — Add or Remove Programs.</summary>
public class AddRemoveControl : UserControl
{
    public AddRemoveControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("Add or Remove Programs"));
        root.Children.Add(XpApplet.Body("Currently installed programs on this simulated workstation:"));

        var list = new ListBox { Background = Brushes.White, Height = 280 };
        foreach (var app in new[] { "Windows Live Messenger 2009", "Windows XP Simulator (Dory LLM)", "Notepad", "Calculator", "Paint", "Solitaire", "Minesweeper", "3D Pinball Space Cadet", "Command Prompt", "Windows Explorer", "Task Manager", "Control Panel", "SQL Authority 3D", "Sandbox Controls" })
            list.Items.Add(app);
        root.Children.Add(list);
        Content = new ScrollViewer { Content = root };
    }
}

/// <summary>wscui.cpl — Security Center.</summary>
public class SecurityCenterControl : UserControl
{
    public SecurityCenterControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        var root = new StackPanel { Margin = new Thickness(12) };
        root.Children.Add(XpApplet.H("Security Center"));

        var sp = new StackPanel();
        sp.Children.Add(MakeStatus("Firewall", "ON", true));
        sp.Children.Add(MakeStatus("Automatic Updates", "ON", true));
        sp.Children.Add(MakeStatus("Virus Protection", "N/A (simulated)", false));
        root.Children.Add(XpApplet.Card(sp));
        root.Children.Add(XpApplet.Body("Security Center monitors the security essentials for your computer: firewall, automatic updates, and virus protection."));
        Content = new ScrollViewer { Content = root };
    }

    private static UIElement MakeStatus(string name, string status, bool ok)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        sp.Children.Add(new TextBlock { Text = name, Width = 160, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        sp.Children.Add(new Border
        {
            Background = ok ? new SolidColorBrush(Color.FromRgb(0xDC, 0xF8, 0xC6)) : new SolidColorBrush(Color.FromRgb(0xFC, 0xE4, 0xD6)),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 1, 8, 1),
            Child = new TextBlock { Text = status, FontSize = 10, Foreground = ok ? new SolidColorBrush(Color.FromRgb(0x27, 0x6E, 0x1B)) : new SolidColorBrush(Color.FromRgb(0x9C, 0x42, 0x1C)) }
        });
        return sp;
    }
}
