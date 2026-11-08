using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DoryNative.XpSim;

/// <summary>
/// Fullscreen boot sequence: Phase 1 BIOS POST (Award BIOS text),
/// Phase 2 Windows XP logo + glowing progress bar, then hands off.
/// </summary>
public partial class BootSequenceWindow : Window
{
    /// <summary>Fired ~3.5s after the XP logo appears (boot sequence done).</summary>
    public Action<BootSequenceWindow>? OnLogoComplete { get; set; }

    public BootSequenceWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RunSequence();
    }

    private void RunSequence()
    {
        ShowBiosPhase();
        var t1 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2200) };
        t1.Tick += (_, _) => { t1.Stop(); ShowLogoPhase(); };
        t1.Start();
    }

    // ---------- Phase 1: BIOS POST ----------
    private void ShowBiosPhase()
    {
        PhaseHost.Children.Clear();
        PhaseHost.Background = new SolidColorBrush(Color.FromRgb(0, 0, 0));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });

        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 4, 0, 0) };
        header.Children.Add(new TextBlock
        {
            Text = "Award Modular BIOS v6.00PG, An Energy Star Ally",
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14,
            FontFamily = new FontFamily("Lucida Console")
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var sub = new TextBlock
        {
            Text = "Copyright (C) 1984-2004, Award Software, Inc.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xD0, 0xC8)),
            FontSize = 12, FontFamily = new FontFamily("Lucida Console"),
            Margin = new Thickness(16, 2, 0, 0)
        };
        Grid.SetRow(sub, 0);
        root.Children.Add(sub);

        var body = new StackPanel { Margin = new Thickness(16, 10, 0, 0) };
        void Line(string text, Color? color = null)
        {
            body.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = color.HasValue ? new SolidColorBrush(color.Value) : Brushes.White,
                FontSize = 13, FontFamily = new FontFamily("Lucida Console"),
                Margin = new Thickness(0, 1, 0, 1)
            });
        }
        Line("AMD Athlon(tm) 64 X2 Dual Core Processor 3800+");
        Line("Memory Testing: 2097152K OK");
        body.Children.Add(new Border { Height = 8 });
        Line("Primary Master  : WDC WD400BB-00DEA0 (40GB IDE)");
        Line("Primary Slave   : SONY CD-RW CRX210E");
        Line("Secondary Master: VIRTUAL JAIL SANDBOX-VM (100GB MAPPED Z:)");
        Line("Secondary Slave : NONE");
        body.Children.Add(new Border { Height = 8 });
        Line("Detecting IDE devices ... Done.", Color.FromRgb(0, 0xFF, 0xFF));
        Line("ACPI Controller v1.0 Enabled");
        Line("Booting from Local Disk (C:)...");
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = new Grid { Margin = new Thickness(16, 0, 16, 4) };
        footer.Children.Add(new TextBlock
        {
            Text = "Press DEL to enter SETUP, F11 for Boot Menu",
            Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Lucida Console"),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        footer.Children.Add(new TextBlock
        {
            Text = "11/04/2004-NF-CK804-6A61FE19C-00",
            Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Lucida Console"),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        PhaseHost.Children.Add(root);
    }

    // ---------- Phase 2: XP logo + progress ----------
    private void ShowLogoPhase()
    {
        PhaseHost.Children.Clear();
        PhaseHost.Background = new SolidColorBrush(Color.FromRgb(0x0, 0x14, 0x32)); // XP boot blue-black

        var container = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var logo = XpAssets.Image("boot/bmp_5.png");
        if (logo != null)
            container.Children.Add(new Image
            {
                Source = logo, Width = 215, Height = 147,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

        container.Children.Add(new TextBlock
        {
            Text = "Microsoft® Windows® XP",
            Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Trebuchet MS")
        });
        container.Children.Add(new TextBlock
        {
            Text = "Professional",
            Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0xD3, 0xE6)),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Trebuchet MS"),
            Margin = new Thickness(0, 2, 0, 26)
        });

        // 3-segment glowing progress bar (authentic XP boot animation)
        var barHost = new Grid { Width = 158, Height = 15, HorizontalAlignment = HorizontalAlignment.Center };
        var track = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(2),
            Height = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        barHost.Children.Add(track);

        var seg = new Border
        {
            Width = 22, Height = 9,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(3, 0, 0, 0),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.White, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.9
            }
        };
        var segImg = XpAssets.Image("boot/bmp_4.png");
        if (segImg != null)
            seg.Child = new Image { Source = segImg, Stretch = Stretch.Fill };
        barHost.Children.Add(seg);

        // Animate segment sweeping left->right like real XP boot
        seg.BeginAnimation(MarginProperty, new System.Windows.Media.Animation.ThicknessAnimation
        {
            From = new Thickness(3, 0, 0, 0),
            To = new Thickness(133, 0, 0, 0),
            Duration = TimeSpan.FromMilliseconds(2100),
            AutoReverse = true,
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        });

        container.Children.Add(barHost);
        container.Children.Add(new TextBlock
        {
            Text = "Copyright © Microsoft Corporation",
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xAD)),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0),
            FontFamily = new FontFamily("Trebuchet MS")
        });

        PhaseHost.Children.Add(container);

        var t2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
        t2.Tick += (_, _) => { t2.Stop(); OnLogoComplete?.Invoke(this); };
        t2.Start();
    }
}
