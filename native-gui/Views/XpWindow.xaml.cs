using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DoryNative.Views;

public partial class XpWindow : UserControl
{
    private Point _dragStart;
    private bool _isDragging;
    private System.Windows.WindowState _xpWindowState = System.Windows.WindowState.Normal;
    private Rect _restoreBounds;

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(XpWindow),
            new PropertyMetadata("Window", OnTitleChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty XpWindowStateProperty =
        DependencyProperty.Register(nameof(XpWindowState), typeof(System.Windows.WindowState), typeof(XpWindow),
            new PropertyMetadata(System.Windows.WindowState.Normal, OnXpWindowStateChanged));

    public System.Windows.WindowState XpWindowState
    {
        get => _xpWindowState;
        set
        {
            _xpWindowState = value;
            UpdateWindowState();
        }
    }

    /// <summary>
    /// App content hosted inside the window chrome. NOTE: do NOT use the
    /// UserControl's own Content property — assigning it replaces the whole
    /// visual tree (including the titlebar/frame). Use this instead.
    /// </summary>
    public static readonly DependencyProperty WindowContentProperty =
        DependencyProperty.Register(nameof(WindowContent), typeof(object), typeof(XpWindow),
            new PropertyMetadata(null, OnWindowContentChanged));

    public object? WindowContent
    {
        get => GetValue(WindowContentProperty);
        set => SetValue(WindowContentProperty, value);
    }

    /// <summary>Optional app icon shown in the titlebar and taskbar button.</summary>
    public static readonly DependencyProperty WindowIconProperty =
        DependencyProperty.Register(nameof(WindowIcon), typeof(System.Windows.Media.ImageSource), typeof(XpWindow),
            new PropertyMetadata(null));

    public System.Windows.Media.ImageSource? WindowIcon
    {
        get => (System.Windows.Media.ImageSource?)GetValue(WindowIconProperty);
        set => SetValue(WindowIconProperty, value);
    }

    /// <summary>
    /// When true, the Luna frame/titlebar/caption buttons are hidden and the
    /// content fills the whole window — for apps (like WLMLive) that draw
    /// their own chrome. Taskbar button, resize thumbs, and external
    /// drag/min/max helpers remain available.
    /// </summary>
    public static readonly DependencyProperty ChromelessProperty =
        DependencyProperty.Register(nameof(Chromeless), typeof(bool), typeof(XpWindow),
            new PropertyMetadata(false, OnChromelessChanged));

    public bool Chromeless
    {
        get => (bool)GetValue(ChromelessProperty);
        set => SetValue(ChromelessProperty, value);
    }

    /// <summary>
    /// Dialog mode: only a close caption button (no min/max), no resize thumbs.
    /// </summary>
    public static readonly DependencyProperty IsDialogProperty =
        DependencyProperty.Register(nameof(IsDialog), typeof(bool), typeof(XpWindow),
            new PropertyMetadata(false, OnDialogModeChanged));

    public bool IsDialog
    {
        get => (bool)GetValue(IsDialogProperty);
        set => SetValue(IsDialogProperty, value);
    }

    private static void OnDialogModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpWindow w) w.ApplyDialogMode();
    }

    private void ApplyDialogMode()
    {
        if (CaptionButtons == null) return;
        CaptionButtons.Children.Clear();
        if (IsDialog)
        {
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_close.png", "closebtn", CloseBtn_Click));
            ResizeSE.Visibility = Visibility.Collapsed;
            ResizeE.Visibility = Visibility.Collapsed;
            ResizeS.Visibility = Visibility.Collapsed;
        }
        else
        {
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_minimize.png", "cap", MinBtn_Click));
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_maximize.png", "cap", MaxBtn_Click));
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_close.png", "closebtn", CloseBtn_Click));
            ResizeSE.Visibility = Visibility.Visible;
            ResizeE.Visibility = Visibility.Visible;
            ResizeS.Visibility = Visibility.Visible;
        }
    }

    private static void OnChromelessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpWindow w) w.ApplyChromeless();
    }

    private void ApplyChromeless()
    {
        if (Chromeless)
        {
            TitleBar.Visibility = Visibility.Collapsed;
            FrameLeftImg.Visibility = Visibility.Collapsed;
            FrameRightImg.Visibility = Visibility.Collapsed;
            FrameBottomImg.Visibility = Visibility.Collapsed;
            Grid.SetRow(ContentHost, 0);
            Grid.SetRowSpan(ContentHost, 2);
            ContentHost.Background = Brushes.Transparent;
        }
        else
        {
            TitleBar.Visibility = Visibility.Visible;
            FrameLeftImg.Visibility = Visibility.Visible;
            FrameRightImg.Visibility = Visibility.Visible;
            FrameBottomImg.Visibility = Visibility.Visible;
            Grid.SetRow(ContentHost, 1);
            Grid.SetRowSpan(ContentHost, 1);
            ContentHost.Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
        }
    }

    private static void OnWindowContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpWindow w) w.ContentPresenter.Content = e.NewValue;
    }

    public event EventHandler? WindowClosed;

    public void TriggerClose()
    {
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    public XpWindow()
    {
        InitializeComponent();
        TitleText.Text = Title;
        WindowIconImg.Source = WindowIcon;
        ApplyLunaChrome();
        PreviewMouseMove += (_, _) => OnExternalDragMove();
        // Raise this window when any part of it is clicked (window depth/focus).
        PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (XpDesktop.Current != null) XpDesktop.Current.BringToFront(this);
        };
        PreviewMouseLeftButtonUp += (_, _) =>
        {
            if (_externalDrag) { _externalDrag = false; ReleaseMouseCapture(); }
        };
    }

    /// <summary>Load authentic Luna bitmaps for frame, titlebar, and caption buttons.</summary>
    private void ApplyLunaChrome()
    {
        try
        {
            TbLeftImg.Source = Luna("luna/tb_left.png");
            TbCenterImg.Source = Luna("luna/tb_center.png");
            TbRightImg.Source = Luna("luna/tb_right.png");
            FrameLeftImg.Source = Luna("luna/frame_left.png");
            FrameRightImg.Source = Luna("luna/frame_right.png");
            FrameBottomImg.Source = Luna("luna/frame_bottom.png");

            // Caption buttons: min, max/restore, close — with Luna glyphs + state bgs
            CaptionButtons.Children.Clear();
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_minimize.png", "cap", MinBtn_Click));
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_maximize.png", "cap", MaxBtn_Click));
            CaptionButtons.Children.Add(MakeCaptionBtn("glyph_close.png", "closebtn", CloseBtn_Click));
        }
        catch { /* fall back to flat colors if assets missing */ }
    }

    private static System.Windows.Media.Imaging.BitmapImage? Luna(string rel)
    {
        var img = XpSim.XpAssets.Image(rel);
        return img;
    }

    private FrameworkElement MakeCaptionBtn(string glyph, string bgPrefix, RoutedEventHandler onClick)
    {
        var host = new Grid { Width = 21, Height = 21, Margin = new Thickness(1, 0, 1, 0) };
        var bg = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        var glyphImg = new Image
        {
            Width = 13, Height = 13, Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = Luna($"luna/{glyph}")
        };
        host.Children.Add(bg);
        host.Children.Add(glyphImg);

        void SetState(string s) => bg.Source = Luna($"luna/{bgPrefix}_{s}.png");
        SetState("normal");

        host.MouseEnter += (_, _) => SetState("hot");
        host.MouseLeave += (_, _) => SetState("normal");
        host.MouseLeftButtonDown += (_, _) => SetState("push");
        host.MouseLeftButtonUp += (_, _) => SetState("hot");
        host.MouseLeftButtonDown += (_, e) => onClick(host, e);
        host.Cursor = Cursors.Hand;
        return host;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == WindowIconProperty && WindowIconImg != null)
            WindowIconImg.Source = WindowIcon;
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpWindow window)
        {
            window.TitleText.Text = (string)e.NewValue;
        }
    }

    private static void OnXpWindowStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is XpWindow window)
        {
            window.UpdateWindowState();
        }
    }

    private bool _externalDrag;
    private bool _isMaxApplied;
    private Point _extDragOffset;

    /// <summary>
    /// Begin a drag initiated by hosted-app chrome (e.g. WLMLive's own
    /// titlebar). Captures the mouse on this control; movement is tracked
    /// in OnExternalDragMove until the button is released.
    /// </summary>
    public void BeginExternalDrag()
    {
        _externalDrag = true;
        _extDragOffset = Mouse.GetPosition(this);
        CaptureMouse();
    }

    private void OnExternalDragMove()
    {
        if (!_externalDrag) return;
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            _externalDrag = false;
            ReleaseMouseCapture();
            return;
        }
        var pos = Mouse.GetPosition(Parent as UIElement ?? this);
        Canvas.SetLeft(this, pos.X - _extDragOffset.X);
        Canvas.SetTop(this, pos.Y - _extDragOffset.Y);
    }

    /// <summary>Minimize like the Luna caption button does.</summary>
    public void MinimizeSelf()
    {
        XpWindowState = System.Windows.WindowState.Minimized;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Toggle maximize/restore against the parent canvas bounds.</summary>
    public void ToggleExternalMaximize()
    {
        XpWindowState = _xpWindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Maximized;
    }

    private void UpdateWindowState()
    {
        var parent = Parent as FrameworkElement;
        if (_xpWindowState == System.Windows.WindowState.Maximized && parent != null)
        {
            if (!_isMaxApplied)
            {
                _isMaxApplied = true;
                _restoreBounds = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
                Canvas.SetLeft(this, 0);
                Canvas.SetTop(this, 0);
                Width = parent.ActualWidth;
                Height = parent.ActualHeight;
            }
        }
        else if (_xpWindowState == System.Windows.WindowState.Normal && _isMaxApplied)
        {
            _isMaxApplied = false;
            if (_restoreBounds != Rect.Empty)
            {
                Canvas.SetLeft(this, _restoreBounds.X);
                Canvas.SetTop(this, _restoreBounds.Y);
                Width = _restoreBounds.Width;
                Height = _restoreBounds.Height;
                _restoreBounds = Rect.Empty;
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaxBtn_Click(sender, e);
            return;
        }

        _isDragging = true;
        _dragStart = e.GetPosition(this);
        TitleBar.CaptureMouse();
        e.Handled = true;
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var pos = e.GetPosition(Parent as UIElement);
            Canvas.SetLeft(this, pos.X - _dragStart.X);
            Canvas.SetTop(this, pos.Y - _dragStart.Y);
        }
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        TitleBar.ReleaseMouseCapture();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
    {
        XpWindowState = System.Windows.WindowState.Minimized;
        Visibility = Visibility.Collapsed;
    }

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
        XpWindowState = XpWindowState == System.Windows.WindowState.Maximized
            ? System.Windows.WindowState.Normal
            : System.Windows.WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        TriggerClose();
    }

    private void ResizeSE_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(200, Width + e.HorizontalChange);
        Height = Math.Max(150, Height + e.VerticalChange);
    }

    private void ResizeE_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(200, Width + e.HorizontalChange);
    }

    private void ResizeS_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Height = Math.Max(150, Height + e.VerticalChange);
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        Visibility = Visibility.Collapsed;
    }

    public void Activate()
    {
        // Bring to front logic handled by parent
    }

    public void CloseWindow()
    {
        WindowClosed?.Invoke(this, EventArgs.Empty);
    }
}
