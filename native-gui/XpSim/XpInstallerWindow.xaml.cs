using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DoryNative.XpSim;

/// <summary>
/// Windows XP Professional Setup — faithful port of xp_installer.js:
/// text-mode setup -> GUI billboard wizard (5 steps) -> finalizing -> OOBE.
/// </summary>
public partial class XpInstallerWindow : Window
{
    private readonly string? _workspacePath;
    private readonly Action<XpSetupConfig>? _onComplete;
    private XpSetupConfig _cfg;
    private Border? _wizardBox;

    public XpInstallerWindow(string? workspacePath, Action<XpSetupConfig>? onComplete = null)
    {
        InitializeComponent();
        _workspacePath = workspacePath;
        _onComplete = onComplete;
        _cfg = InstallStore.GetConfig(workspacePath) ?? XpSetupConfig.Defaults();
        if (string.IsNullOrEmpty(_cfg.ProductKey))
            _cfg.ProductKey = "FCKGW-RHQQ2-YXRKT-8TG6W-2B7Q8";
        Loaded += (_, _) => Start();
    }

    public void Start() => RenderTextModeSetup();

    // ================= STAGE 1: text-mode setup =================
    private void RenderTextModeSetup()
    {
        var root = new Grid { Background = Brushes.Black };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
            Child = new TextBlock
            {
                Text = "Windows XP Professional Setup", Foreground = Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
            }
        };
        Grid.SetRow(header, 0); root.Children.Add(header);

        var bodyStack = new StackPanel { Margin = new Thickness(16, 14, 16, 0) };
        TextBlock Line(string t, Color? c = null) => new()
        {
            Text = t, Foreground = c.HasValue ? new SolidColorBrush(c.Value) : Brushes.White,
            FontFamily = new FontFamily("Lucida Console"), FontSize = 13, Margin = new Thickness(0, 2, 0, 2)
        };
        bodyStack.Children.Add(Line("Setup is inspecting your computer's hardware configuration..."));
        var loadFiles = Line("Setup is loading files (Windows Executive, Kernel, HAL, NTFS, IDE Bus)...",
            Color.FromRgb(0xFE, 0xF0, 0x8A));
        bodyStack.Children.Add(loadFiles);

        // Partition selection box (hidden initially)
        var partBox = new StackPanel { Margin = new Thickness(0, 18, 0, 0), Visibility = Visibility.Collapsed };
        partBox.Children.Add(Line("The following list shows the existing partitions and unpartitioned space on this computer:"));
        var partTable = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        string wsName = System.IO.Path.GetFileName(_workspacePath ?? "Project");
        partTable.Children.Add(Line("- C: Partition1 [NTFS]    40960 MB ( 40960 MB free )"));
        partTable.Children.Add(Line($"- D: Partition2 [Host WS] 1024000 MB ( Project Volume: {wsName} )",
            Color.FromRgb(0x38, 0xBD, 0xF8)));
        partTable.Children.Add(Line("- Unpartitioned space     8 MB", Color.FromRgb(0x94, 0xA3, 0xB8)));
        partBox.Children.Add(partTable);
        partBox.Children.Add(new Border { Height = 10 });
        partBox.Children.Add(Line("• Press ENTER to install Windows XP on the selected partition."));
        partBox.Children.Add(Line("• Press C to create a partition in the unpartitioned space."));
        partBox.Children.Add(Line("• Press F3 to quit Setup without installing Windows XP."));
        partBox.Children.Add(new Border { Height = 14 });
        var contBtn = MakeTextBtn("ENTER = Continue Setup");
        partBox.Children.Add(contBtn);
        bodyStack.Children.Add(partBox);

        // Format progress box (hidden)
        var progBox = new StackPanel { Margin = new Thickness(0, 20, 0, 0), Visibility = Visibility.Collapsed };
        progBox.Children.Add(Line("Please wait while Setup formats the partition..."));
        var track = new Border
        {
            Height = 14, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 0)
        };
        var fill = new Border { Background = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7)), Width = 0 };
        track.Child = fill;
        progBox.Children.Add(track);
        var fmtStatus = Line("Formatting drive C: (NTFS File System)... 0%");
        progBox.Children.Add(fmtStatus);
        bodyStack.Children.Add(progBox);
        contBtn.Click += (_, _) => ProceedToFormat();

        var scroll = new ScrollViewer { Content = bodyStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); root.Children.Add(scroll);

        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
            Child = new TextBlock
            {
                Text = "ENTER=Continue  F3=Quit", Foreground = Brushes.White, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0),
                FontFamily = new FontFamily("Lucida Console")
            }
        };
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        StageHost.Content = root;

        // Phase timings (match web: inspect 1.6s -> partition -> auto-format 3.2s)
        var t1 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        t1.Tick += (_, _) =>
        {
            t1.Stop();
            partBox.Visibility = Visibility.Visible;
            var t2 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3200) };
            t2.Tick += (_, _) => { t2.Stop(); ProceedToFormat(); };
            t2.Start();
        };
        t1.Start();

        void ProceedToFormat()
        {
            partBox.Visibility = Visibility.Collapsed;
            progBox.Visibility = Visibility.Visible;
            int pct = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            timer.Tick += (_, _) =>
            {
                pct += 10;
                if (pct > 100) pct = 100;
                fill.Width = track.ActualWidth * pct / 100.0;
                fmtStatus.Text = $"Formatting drive C: (NTFS File System)... {pct}%";
                if (pct >= 100)
                {
                    timer.Stop();
                    var t3 = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                    t3.Tick += (_, _) => { t3.Stop(); RenderGuiSetup(); };
                    t3.Start();
                }
            };
            timer.Start();
        }
    }

    private static Button MakeTextBtn(string text) => new()
    {
        Content = text, Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x7B, 0xB1)),
        Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)),
        Padding = new Thickness(12, 4, 12, 4), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 4, 0, 0), Cursor = System.Windows.Input.Cursors.Hand
    };

    // ================= STAGE 2: GUI billboard setup =================
    private void RenderGuiSetup()
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x00, 0x3A, 0x6C)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition());

        // Header: setup logo + "Setup"
        var header = new DockPanel { Margin = new Thickness(20, 8, 20, 0) };
        var logo = XpAssets.Image("setup_billboard/bmp_102.png");
        if (logo != null)
            header.Children.Add(new Image { Source = logo, Height = 38, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left });
        header.Children.Add(new TextBlock
        {
            Text = "Setup", Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(header, 0); root.Children.Add(header);

        var main = new Grid { Margin = new Thickness(20, 6, 20, 14) };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(290) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        main.ColumnDefinitions.Add(new ColumnDefinition());

        // Left billboard
        var bill = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF8, 0xFC)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(14)
        };
        var billStack = new StackPanel();
        string[] steps = { "Collecting information", "Dynamic update", "Preparing installation", "Installing Windows", "Finalizing installation" };
        for (int i = 0; i < steps.Length; i++)
        {
            var done = i < 3; var active = i == 3;
            billStack.Children.Add(new TextBlock
            {
                Text = (done ? "✔ " : active ? "➔ " : "   ") + steps[i],
                FontSize = 11, FontWeight = done || active ? FontWeights.Bold : FontWeights.Normal,
                Foreground = done ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                            : active ? new SolidColorBrush(Color.FromRgb(0x02, 0x86, 0xC7))
                            : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                Margin = new Thickness(0, 3, 0, 3)
            });
        }
        billStack.Children.Add(new Border { Height = 12 });
        var featTitle = new TextBlock
        {
            Text = "An exciting new look", FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), TextWrapping = TextWrapping.Wrap
        };
        billStack.Children.Add(featTitle);
        var featText = new TextBlock
        {
            Text = "Windows XP Professional sports a fresh visual design that makes common tasks easier and puts your most important files within easy reach.",
            FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
        };
        billStack.Children.Add(featText);
        billStack.Children.Add(new Border { Height = 12 });
        billStack.Children.Add(new TextBlock { Text = "Setup will complete in approximately:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)) });
        var timeTxt = new TextBlock
        {
            Text = "39 minutes", FontSize = 24, FontWeight = FontWeights.Black,
            Foreground = new SolidColorBrush(Color.FromRgb(0x03, 0x69, 0xA1))
        };
        billStack.Children.Add(timeTxt);
        // Animated green chaser dots
        var chase = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        for (int i = 0; i < 3; i++)
        {
            var dot = new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E))
            };
            chase.Children.Add(dot);
            var delay = i * 200;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(900))
            {
                AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(delay)
            };
            dot.BeginAnimation(OpacityProperty, anim);
        }
        billStack.Children.Add(chase);
        bill.Child = billStack;
        Grid.SetColumn(bill, 0); main.Children.Add(bill);

        // Right wizard card
        _wizardBox = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top
        };
        var wizScroll = new ScrollViewer { Content = _wizardBox, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        wizScroll.Padding = new Thickness(0);
        Grid.SetColumn(wizScroll, 2); main.Children.Add(wizScroll);

        Grid.SetRow(main, 1); root.Children.Add(main);
        StageHost.Content = root;

        // Billboard feature rotation (5s)
        var features = new (string t, string x)[]
        {
            ("An exciting new look", "Windows XP Professional sports a visual design that combines a sleek look, clean lines, and appealing colors with a task-oriented design and exceptionally streamlined navigation. The redesigned Start menu makes it easier to find important information and to access the programs you use most frequently."),
            ("The connected world", "Windows XP makes it easy to share hardware, network drives, and files securely across your home or office. With integrated Internet Connection Sharing, Windows Firewall, and remote desktop access, staying connected has never been simpler."),
            ("Rock-solid reliability", "Designed for maximum dependability and minimal restarts. Built on the proven NT 32-bit kernel architecture, Windows XP delivers bulletproof stability, protected memory architecture, and seamless system restore capabilities."),
            ("Advanced security and privacy", "Windows XP gives you peace of mind with Encrypting File System (EFS), advanced user permission controls, and automatic updates to protect against security threats.")
        };
        int fi = 0;
        var ft = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        ft.Tick += (_, _) =>
        {
            fi = (fi + 1) % features.Length;
            featTitle.Text = features[fi].t;
            featText.Text = features[fi].x;
        };
        ft.Start();

        RenderWizardStep1();
    }

    // ---------- Wizard chrome helpers ----------
    private UIElement WizardCard(string title, string subtitle, UIElement body, Action? onBack, Action? onNext, string nextText = "Next >")
    {
        var card = new StackPanel();
        card.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF6, 0xFC)),
            Padding = new Thickness(14, 10, 14, 8),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x99)) },
                    new TextBlock { Text = subtitle, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) }
                }
            }
        });
        var bodyWrap = new Border { Padding = new Thickness(14, 10, 14, 10), Child = body };
        card.Children.Add(bodyWrap);
        var foot = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF6, 0xFC)),
            Padding = new Thickness(14, 8, 14, 10)
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (onBack != null)
        {
            var bb = MakeNavBtn("< Back", false);
            bb.Click += (_, _) => onBack();
            row.Children.Add(bb);
        }
        if (onNext != null)
        {
            var nb = MakeNavBtn(nextText, true);
            nb.Margin = new Thickness(8, 0, 0, 0);
            nb.Click += (_, _) => onNext();
            row.Children.Add(nb);
        }
        foot.Child = row;
        card.Children.Add(foot);
        return card;
    }

    private static Button MakeNavBtn(string text, bool primary) => new()
    {
        Content = text,
        Background = primary ? new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)) : new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF7)),
        Foreground = primary ? Brushes.White : Brushes.Black,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)),
        Padding = new Thickness(16, 4, 16, 4), FontSize = 12, Cursor = System.Windows.Input.Cursors.Hand
    };

    private static FrameworkElement MakeField(string value = "", string placeholder = "", bool password = false, double width = 260)
    {
        if (password)
            return new PasswordBox
            {
                Width = width, Padding = new Thickness(4, 3, 4, 3), FontSize = 12,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)),
                Background = Brushes.White, Foreground = Brushes.Black,
                ToolTip = placeholder
            };
        return new TextBox
        {
            Text = value, Width = width, Padding = new Thickness(4, 3, 4, 3), FontSize = 12,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)),
            Background = Brushes.White, Foreground = Brushes.Black
        };
    }

    private static string FieldText(FrameworkElement f) => f switch
    {
        TextBox t => t.Text,
        PasswordBox p => p.Password,
        _ => ""
    };

    private void SetWizard(UIElement card) => _wizardBox!.Child = card;

    // ---------- Step 1: Regional and Language ----------
    private void RenderWizardStep1()
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "This wizard will help you install Windows XP Professional on your computer.\n\nText input languages allow you to enter text in many different languages using your keyboard.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        body.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)), BorderThickness = new Thickness(1),
            Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 6),
            Child = new TextBlock
            {
                Text = "Default text input language is set to US - Keyboard.",
                FontSize = 11, TextWrapping = TextWrapping.Wrap
            }
        });
        SetWizard(WizardCard("Regional and Language Options",
            "This page lets you select the language and regional settings for this installation.",
            body, null, RenderWizardStep2));
    }

    // ---------- Step 2: Personalize ----------
    private void RenderWizardStep2()
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "Type your full name and the name of your company or organization.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        var nameBox = MakeField(_cfg.UserName);
        var orgBox = MakeField(_cfg.Organization);
        body.Children.Add(FieldRow("Your name:", nameBox));
        body.Children.Add(FieldRow("Organization:", orgBox));
        SetWizard(WizardCard("Personalize Your Software",
            "Microsoft licenses this product to an individual user. Enter your name exactly as you would like it to appear on the Welcome screen.",
            body, RenderWizardStep1, () =>
            {
                if (FieldText(nameBox).Trim().Length > 0) _cfg.UserName = FieldText(nameBox).Trim();
                _cfg.Organization = FieldText(orgBox).Trim();
                RenderWizardStep3();
            }));
    }

    private static Border FieldRow(string label, UIElement field)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        sp.Children.Add(new TextBlock
        {
            Text = label, FontSize = 11, FontWeight = FontWeights.Bold, Width = 90,
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(field);
        return new Border { Child = sp };
    }

    // ---------- Step 3: Product Key ----------
    private void RenderWizardStep3()
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "Type the 25-character product key supplied with your Windows XP Professional package.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        var parts = (_cfg.ProductKey ?? "").Split('-');
        var boxes = new TextBox[5];
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        string[] defaults = { "FCKGW", "RHQQ2", "YXRKT", "8TG6W", "2B7Q8" };
        for (int i = 0; i < 5; i++)
        {
            var tb = (TextBox)MakeField(i < parts.Length && parts[i].Length > 0 ? parts[i] : defaults[i], width: 62);
            tb.MaxLength = 5;
            tb.CharacterCasing = System.Windows.Controls.CharacterCasing.Upper;
            tb.TextAlignment = TextAlignment.Center;
            boxes[i] = tb;
            row.Children.Add(tb);
            if (i < 4) row.Children.Add(new TextBlock { Text = " - ", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) });
        }
        var wrap = new Border { Child = row, Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(wrap);
        SetWizard(WizardCard("Product Key",
            "The product key is printed on the certificate of authenticity that came with your Windows XP Professional package.",
            body, RenderWizardStep2, () =>
            {
                _cfg.ProductKey = string.Join("-", boxes.Select(b => b.Text.Trim().ToUpperInvariant()));
                RenderWizardStep4();
            }));
    }

    // ---------- Step 4: Computer Name + Admin Password ----------
    private void RenderWizardStep4()
    {
        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = "Type a unique name for your computer, and a password for the local Administrator account.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        var compBox = MakeField(_cfg.ComputerName, width: 260);
        var pw1 = MakeField("", "Optional password", true);
        var pw2 = MakeField("", "Confirm password", true);
        // prefill password values
        if (_cfg.AdminPassword.Length > 0)
        {
            ((PasswordBox)pw1).Password = _cfg.AdminPassword;
            ((PasswordBox)pw2).Password = _cfg.AdminPassword;
        }
        body.Children.Add(FieldRow("Computer name:", compBox));
        body.Children.Add(FieldRow("Administrator password:", pw1));
        body.Children.Add(FieldRow("Confirm password:", pw2));
        SetWizard(WizardCard("Computer Name and Administrator Password",
            "Type a unique name for your computer (15 characters maximum). The Administrator account is used to manage this computer.",
            body, RenderWizardStep3, () =>
            {
                if (FieldText(compBox).Trim().Length > 0) _cfg.ComputerName = FieldText(compBox).Trim();
                _cfg.AdminPassword = ((PasswordBox)pw1).Password;
                RenderWizardStep5();
            }));
    }

    // ---------- Step 5: Date/Time ----------
    private void RenderWizardStep5()
    {
        var now = DateTime.Now;
        var body = new StackPanel();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        row.Children.Add(new TextBlock { Text = "Date:", FontSize = 11, FontWeight = FontWeights.Bold, Width = 90, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBox
        {
            Text = now.ToShortDateString(), IsReadOnly = true, Width = 110,
            Padding = new Thickness(4, 3, 4, 3), FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9))
        });
        row.Children.Add(new TextBlock { Text = "Time:", FontSize = 11, FontWeight = FontWeights.Bold, Width = 60, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBox
        {
            Text = now.ToLongTimeString(), IsReadOnly = true, Width = 110,
            Padding = new Thickness(4, 3, 4, 3), FontSize = 11,
            Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9))
        });
        body.Children.Add(row);
        var tzRow = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
        tzRow.Children.Add(new TextBlock { Text = "Time Zone:", FontSize = 12, FontWeight = FontWeights.Bold });
        var tz = new ComboBox { Width = 340, Margin = new Thickness(0, 3, 0, 0), FontSize = 11, SelectedIndex = 3 };
        foreach (var z in new[]
        {
            "(GMT-08:00) Pacific Time (US & Canada)",
            "(GMT-05:00) Eastern Time (US & Canada)",
            "(GMT+00:00) Greenwich Mean Time : Dublin, London",
            "(GMT+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"
        })
            tz.Items.Add(z);
        if (!string.IsNullOrEmpty(_cfg.TimeZone))
        {
            var idx = tz.Items.IndexOf(_cfg.TimeZone);
            if (idx >= 0) tz.SelectedIndex = idx;
        }
        tzRow.Children.Add(tz);
        body.Children.Add(tzRow);
        SetWizard(WizardCard("Date and Time Settings",
            "Set the correct date, time, and time zone for your computer.",
            body, RenderWizardStep4, () =>
            {
                _cfg.TimeZone = tz.SelectedItem?.ToString() ?? "";
                RenderFinalizingStage();
            }));
    }

    // ---------- Finalizing ----------
    private void RenderFinalizingStage()
    {
        var body = new StackPanel();
        var stepTxt = new TextBlock
        {
            Text = "Registering components...", FontSize = 12, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), Margin = new Thickness(0, 20, 0, 8)
        };
        body.Children.Add(stepTxt);
        var track = new Border
        {
            Height = 16, Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), BorderThickness = new Thickness(1)
        };
        var fill = new Border { Background = new SolidColorBrush(Color.FromRgb(0x02, 0x86, 0xC7)), Width = 0 };
        track.Child = fill;
        body.Children.Add(track);
        SetWizard(WizardCard("Finalizing Installation",
            "Please wait while Setup registers components and finalizes your system.", body, null, null));

        int progress = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        timer.Tick += (_, _) =>
        {
            progress += 8;
            if (progress == 32) stepTxt.Text = "Installing Start Menu items & Games...";
            if (progress == 64) stepTxt.Text = "Registering SQL Authority 3D & WLM Revival...";
            if (progress == 88) stepTxt.Text = "Saving settings and removing temporary files...";
            if (progress > 100) progress = 100;
            fill.Width = track.ActualWidth * progress / 100.0;
            if (progress >= 100)
            {
                timer.Stop();
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                t.Tick += (_, _) => { t.Stop(); RenderOOBE(); };
                t.Start();
            }
        };
        timer.Start();
    }

    // ---------- OOBE ----------
    private int _oobeStep = 1;
    private void RenderOOBE()
    {
        _cfg.InstalledAt = DateTime.Now.ToString("o");
        InstallStore.SaveConfig(_cfg, _workspacePath);
        _oobeStep = 1;
        RenderOobeStep();
    }

    private void RenderOobeStep()
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x00, 0x2B, 0x5C)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });

        var top = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(40, 20, 0, 0) };
        var logo = XpAssets.Image("logon/bmp_100.png");
        if (logo != null)
            top.Children.Add(new Image { Source = logo, Width = _oobeStep == 2 ? 140 : 120, Margin = new Thickness(0, 0, 16, 0) });
        top.Children.Add(new TextBlock
        {
            Text = "Welcome to Microsoft Windows",
            Foreground = Brushes.White, FontSize = _oobeStep == 2 ? 22 : 20,
            FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Trebuchet MS"),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetRow(top, 0); root.Children.Add(top);

        var center = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24, 20, 24, 20),
            Width = 600, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var card = new StackPanel();

        if (_oobeStep == 1)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var secImg = XpAssets.Image("icons/security.png");
            if (secImg != null)
                row.Children.Add(new Image { Source = secImg, Width = 48, Height = 48, Margin = new Thickness(0, 0, 16, 0) });
            var txt = new StackPanel();
            txt.Children.Add(new TextBlock { Text = "Help protect your PC", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x99)) });
            txt.Children.Add(new TextBlock
            {
                Text = "With Automatic Updates, Windows can routinely check for the latest important updates for your computer and install them automatically.",
                FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0)
            });
            var optBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)), BorderThickness = new Thickness(1),
                Padding = new Thickness(10), Margin = new Thickness(0, 14, 0, 0)
            };
            var optInner = new StackPanel();
            optBox.Child = optInner;
            optInner.Children.Add(new RadioButton
            {
                Content = "Help protect my PC by turning on Automatic Updates now (Recommended)",
                IsChecked = true, FontSize = 11, FontWeight = FontWeights.Bold
            });
            optInner.Children.Add(new RadioButton
            {
                Content = "Not right now", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)), Margin = new Thickness(0, 8, 0, 0)
            });
            txt.Children.Add(optBox);
            row.Children.Add(txt);
            card.Children.Add(row);
        }
        else
        {
            card.Children.Add(new TextBlock
            {
                Text = "Who will use this computer?", FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x99))
            });
            card.Children.Add(new TextBlock
            {
                Text = "Type the name of each person who will use this computer. A personalized user account will be created for each person. Click an avatar to choose from the authentic Windows XP account pictures.",
                FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            });

            var usersPanel = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            // Primary user with avatar picker
            var avatarBtn = new Border
            {
                Width = 48, Height = 48, CornerRadius = new CornerRadius(3), ClipToBounds = true,
                Background = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7)),
                BorderThickness = new Thickness(2), Cursor = System.Windows.Input.Cursors.Hand
            };
            var avatarImg = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill };
            avatarBtn.Child = avatarImg;
            SetOobeTile(avatarImg, _cfg.UserTile);

            var nameInput = new TextBox
            {
                Text = _cfg.UserName, Width = 230, Padding = new Thickness(4, 3, 4, 3),
                FontSize = 13, FontWeight = FontWeights.Bold,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9))
            };
            var u1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            u1.Children.Add(avatarBtn);
            var u1txt = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            u1txt.Children.Add(new TextBlock { Text = "Primary User Name:", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
            u1txt.Children.Add(nameInput);
            u1.Children.Add(u1txt);
            usersPanel.Children.Add(u1);

            // User 2: Dory AI Bot
            usersPanel.Children.Add(MakeOobeUserRow("usertiles/duck.png", "User 2 (AI Assistant):", "Dory AI Bot", null));
            // User 3: optional
            var u3 = MakeOobeUserRow("usertiles/chess.png", "User 3 (Optional):", "", "Guest / Colleague");
            usersPanel.Children.Add(u3);

            // Avatar picker grid (collapsible)
            var tilePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), BorderThickness = new Thickness(1),
                Padding = new Thickness(10), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed
            };
            var tileInner = new StackPanel();
            tilePanel.Child = tileInner;
            tileInner.Children.Add(new TextBlock { Text = "Choose an authentic Windows XP user account tile:", FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            var tileGrid = new WrapPanel();
            string[] sampleTiles = { "dog.png", "duck.png", "chess.png", "car.png", "guitar.png", "butterfly.png", "airplane.png", "astro.png", "beach.png", "cat.png", "horses.png", "palmtree.png", "soccer.png", "skater.png", "snwflake.png", "frog.png" };
            foreach (var t in sampleTiles)
            {
                var ti = new System.Windows.Controls.Image
                {
                    Width = 36, Height = 36, Margin = new Thickness(3), Cursor = System.Windows.Input.Cursors.Hand,
                    Stretch = Stretch.UniformToFill,
                    Source = XpAssets.Image("usertiles/" + t),
                    Tag = "usertiles/" + t
                };
                ti.MouseLeftButtonDown += (_, _) =>
                {
                    _cfg.UserTile = (string)ti.Tag;
                    SetOobeTile(avatarImg, _cfg.UserTile);
                    tilePanel.Visibility = Visibility.Collapsed;
                };
                tileGrid.Children.Add(ti);
            }
            tileInner.Children.Add(tileGrid);
            usersPanel.Children.Add(tilePanel);
            card.Children.Add(usersPanel);

            avatarBtn.MouseLeftButtonDown += (_, _) =>
                tilePanel.Visibility = tilePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            // Wire finish
            var finishBtn = new Button
            {
                Content = "Finish >", Padding = new Thickness(18, 5, 18, 5), FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0x02, 0x86, 0xC7)), Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)), Cursor = System.Windows.Input.Cursors.Hand
            };
            finishBtn.Click += (_, _) =>
            {
                if (nameInput.Text.Trim().Length > 0) _cfg.UserName = nameInput.Text.Trim();
                InstallStore.MarkInstalled(_workspacePath, _cfg);
                XpAudio.Tada();
                Close();
                _onComplete?.Invoke(_cfg);
            };
            var backBtn = new Button
            {
                Content = "< Back", Padding = new Thickness(16, 5, 16, 5), FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0x9D, 0xB9)), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            backBtn.Click += (_, _) => { _oobeStep = 1; RenderOobeStep(); };

            var foot = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            foot.Children.Add(new TextBlock { Text = "Need Help? Press F1", Foreground = Brushes.White, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            var footBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            footBtns.Children.Add(backBtn);
            footBtns.Children.Add(finishBtn);
            foot.Children.Add(footBtns);
            card.Children.Add(foot);
        }

        center.Child = card;
        Grid.SetRow(center, 1); root.Children.Add(center);

        var bottom = new TextBlock
        {
            Text = "Microsoft® Windows® XP Professional",
            Foreground = Brushes.White, FontSize = 11, Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 30, 0), FontFamily = new FontFamily("Trebuchet MS")
        };
        Grid.SetRow(bottom, 2); root.Children.Add(bottom);

        StageHost.Content = root;
    }

    private static void SetOobeTile(System.Windows.Controls.Image img, string tile)
    {
        var rel = tile.Contains('/') ? tile : "usertiles/" + tile;
        img.Source = XpAssets.ImageAuto(rel);
    }

    private static Border MakeOobeUserRow(string tile, string label, string value, string? placeholder)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var box = new Border
        {
            Width = 48, Height = 48, CornerRadius = new CornerRadius(3), ClipToBounds = true,
            Background = Brushes.Black, BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            BorderThickness = new Thickness(2), Margin = new Thickness(0, 0, 12, 0)
        };
        var img = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill, Source = XpAssets.Image(tile) };
        box.Child = img;
        sp.Children.Add(box);
        var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txt.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)) });
        txt.Children.Add(new TextBox
        {
            Text = value, Width = 230, Padding = new Thickness(4, 3, 4, 3), FontSize = 12,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
            ToolTip = placeholder
        });
        sp.Children.Add(txt);
        return new Border { Child = sp };
    }
}
