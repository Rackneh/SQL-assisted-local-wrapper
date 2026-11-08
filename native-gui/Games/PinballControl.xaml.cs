using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DoryNative.XpSim;

namespace DoryNative.Games;

/// <summary>
/// 3D Pinball Space Cadet — physics port of xp_games.js pinball engine.
/// Ball/bumpers/flippers/slingshots/plunger rendered on a WPF Canvas.
/// </summary>
public partial class PinballControl : UserControl
{
    private const double W = 560, H = 420;
    private const double BallR = 9;

    private class Bumper { public double X, Y, R; public int Score; public int Sound; public int Glow; public Ellipse Shape = null!; }

    private readonly Bumper[] _bumpers =
    {
        new() { X = 230, Y = 140, R = 24, Score = 500, Sound = 4 },
        new() { X = 330, Y = 140, R = 24, Score = 500, Sound = 5 },
        new() { X = 280, Y = 210, R = 24, Score = 1000, Sound = 14 },
    };
    private static readonly (double X, double Y, double R) Hyper = (280, 70, 30);

    private Ellipse _ball = null!;
    private Polygon _leftFlipper = null!, _rightFlipper = null!;
    private Rectangle _plunger = null!;
    private TextBlock _scoreText = null!, _ballsText = null!, _msgText = null!;
    private Image? _splash;

    private double _ballX = 525, _ballY = 360, _ballVx, _ballVy;
    private double _plungerCharge;
    private bool _plungerCharging, _ballActive, _showSplash = true, _paused;
    private int _score, _balls = 3;
    private bool _leftFlip, _rightFlip;

    private readonly DispatcherTimer _loop = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Random _rng = new();
    private int _highScore = 250000;

    public event Action? RequestClose;

    public PinballControl()
    {
        InitializeComponent();
        BuildTable();
        _loop.Tick += (_, _) => Tick();
        _loop.Start();
        Loaded += (_, _) => Focus();
        PreviewKeyDown += OnKeyDown;
        PreviewKeyUp += OnKeyUp;
    }

    private void BuildTable()
    {
        var bg = new Rectangle { Width = W, Height = H, Fill = new SolidColorBrush(Color.FromRgb(0x05, 0x08, 0x14)) };
        Table.Children.Add(bg);

        // Table border + arch
        Table.Children.Add(new Rectangle { Width = W - 36, Height = H - 36, Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)), StrokeThickness = 4 });
        var arch = new Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)), StrokeThickness = 3,
            Data = new PathGeometry(new[] { new PathFigure(new Point(40, 100), new[] { new ArcSegment(new Point(W - 40, 100), new Size(240, 240), 0, false, SweepDirection.Clockwise, true) }, false) })
        };
        Canvas.SetLeft(arch, 0); Canvas.SetTop(arch, 0);
        Table.Children.Add(arch);

        // Plunger chute divider
        Table.Children.Add(new Line { X1 = 510, Y1 = 140, X2 = 510, Y2 = 400, Stroke = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), StrokeThickness = 2 });

        // Hyperspace
        var hyper = new Ellipse { Width = Hyper.R * 2, Height = Hyper.R * 2, Fill = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)), Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), StrokeThickness = 2 };
        Canvas.SetLeft(hyper, Hyper.X - Hyper.R); Canvas.SetTop(hyper, Hyper.Y - Hyper.R);
        Table.Children.Add(hyper);
        Table.Children.Add(new TextBlock { Text = "HYPERSPACE", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)), FontWeight = FontWeights.Bold });
        Canvas.SetLeft(Table.Children[^1], Hyper.X - 34); Canvas.SetTop(Table.Children[^1], Hyper.Y + 3);

        // Bumpers
        foreach (var b in _bumpers)
        {
            b.Shape = new Ellipse
            {
                Width = b.R * 2, Height = b.R * 2,
                Fill = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5, GradientStops = { new GradientStop(Color.FromRgb(0x38,0xBD,0xF8),0), new GradientStop(Color.FromRgb(0x1E,0x3A,0x8A),1) } },
                Stroke = Brushes.White, StrokeThickness = 2
            };
            Canvas.SetLeft(b.Shape, b.X - b.R); Canvas.SetTop(b.Shape, b.Y - b.R);
            Table.Children.Add(b.Shape);
        }

        // Slingshots
        Table.Children.Add(MakeSlingshot(115, 295, true));
        Table.Children.Add(MakeSlingshot(445, 295, false));

        // Flippers (triangles)
        _leftFlipper = MakeFlipper(205, 360);
        _rightFlipper = MakeFlipper(335, 360);
        Table.Children.Add(_leftFlipper);
        Table.Children.Add(_rightFlipper);

        // Plunger
        _plunger = new Rectangle { Width = 10, Height = 30, Fill = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) };
        Canvas.SetLeft(_plunger, 528); Canvas.SetTop(_plunger, 392);
        Table.Children.Add(_plunger);

        // Ball
        _ball = new Ellipse { Width = BallR * 2, Height = BallR * 2, Fill = Brushes.White, Stroke = new SolidColorBrush(Color.FromRgb(0xBF, 0xDB, 0xFE)), StrokeThickness = 1 };
        Table.Children.Add(_ball);

        // HUD
        _scoreText = new TextBlock { Text = "SCORE: 0", FontFamily = new FontFamily("Consolas"), FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)) };
        Canvas.SetLeft(_scoreText, 20); Canvas.SetTop(_scoreText, 22);
        Table.Children.Add(_scoreText);
        _ballsText = new TextBlock { Text = "BALLS: 3", FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)) };
        Canvas.SetLeft(_ballsText, 20); Canvas.SetTop(_ballsText, 42);
        Table.Children.Add(_ballsText);
        _msgText = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center };
        Canvas.SetLeft(_msgText, 80); Canvas.SetTop(_msgText, 200); _msgText.Width = 400;
        Table.Children.Add(_msgText);

        // Splash image
        var splashPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xp_assets", "pinball", "splash.png");
        if (System.IO.File.Exists(splashPath))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.UriSource = new Uri(splashPath); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit(); bmp.Freeze();
            _splash = new Image { Source = bmp, Width = 360, Height = 250 };
            Canvas.SetLeft(_splash, (W - 360) / 2); Canvas.SetTop(_splash, 50);
            Table.Children.Add(_splash);
        }
        else _showSplash = false;
    }

    private static Polygon MakeFlipper(double cx, double cy)
    {
        var p = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
            Points = new PointCollection { new(0, 0), new(55, 6), new(10, 12) }
        };
        Canvas.SetLeft(p, cx); Canvas.SetTop(p, cy);
        return p;
    }

    private static Rectangle MakeSlingshot(double x, double y, bool left)
    {
        var r = new Rectangle { Width = 26, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)), Opacity = 0.8 };
        Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
        if (!left) Canvas.SetLeft(r, x - 26);
        return r;
    }

    private void ResetBall()
    {
        _ballActive = false;
        _ballX = 525; _ballY = 360; _ballVx = 0; _ballVy = 0;
        _showSplash = false;
        _msgText.Text = "";
        PinSound(1);
    }

    private void NewGame()
    {
        _score = 0; _balls = 3;
        _showSplash = false;
        ResetBall();
        _balls = 3;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                e.Handled = true;
                if (_showSplash) { NewGame(); return; }
                if (!_plungerCharging && !_ballActive) _plungerCharging = true;
                break;
            case Key.Z: case Key.Left:
                if (!_leftFlip) { _leftFlip = true; PinSound(12); }
                break;
            case Key.OemQuestion: case Key.Right:
                if (!_rightFlip) { _rightFlip = true; PinSound(12); }
                break;
            case Key.F2: NewGame(); break;
            case Key.F3: _paused = !_paused; break;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                if (_plungerCharging)
                {
                    _plungerCharging = false;
                    if (!_ballActive)
                    {
                        _ballActive = true;
                        double power = Math.Clamp(_plungerCharge * 0.35, 12, 24);
                        _ballVy = -power;
                        _ballVx = (_rng.NextDouble() - 0.5) * 1.5;
                        PinSound(1);
                    }
                    _plungerCharge = 0;
                }
                break;
            case Key.Z: case Key.Left: _leftFlip = false; break;
            case Key.OemQuestion: case Key.Right: _rightFlip = false; break;
        }
    }

    private void PinSound(int id)
    {
        var p = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xp_assets", "sounds", "pinball", $"sound{id}.wav");
        if (System.IO.File.Exists(p)) XpAudio.Play($"sounds/pinball/sound{id}.wav");
    }

    private void Tick()
    {
        if (_paused || _showSplash) { Draw(); return; }
        if (_plungerCharging) _plungerCharge = Math.Min(100, _plungerCharge + 3);
        if (_ballActive) Step();
        foreach (var b in _bumpers) if (b.Glow > 0) b.Glow--;
        Draw();
    }

    private void Step()
    {
        _ballVy += 0.32;
        _ballVx *= 0.996; _ballVy *= 0.996;
        _ballX += _ballVx; _ballY += _ballVy;

        if (_ballX > 500 && _ballY > 120)
        {
            if (_ballX > 535) { _ballX = 535; _ballVx = -Math.Abs(_ballVx) * 0.5; }
            if (_ballX < 515 && _ballY > 150) { _ballX = 515; _ballVx = Math.Abs(_ballVx) * 0.5; }
        }
        if (_ballY < 50) { _ballY = 50; _ballVy = Math.Abs(_ballVy) * 0.7; _ballVx += (280 - _ballX) * 0.03; }
        if (_ballX < 35) { _ballX = 35; _ballVx = Math.Abs(_ballVx) * 0.75; PinSound(17); }
        if (_ballX > 540) { _ballX = 540; _ballVx = -Math.Abs(_ballVx) * 0.75; PinSound(17); }

        foreach (var b in _bumpers)
        {
            double dx = _ballX - b.X, dy = _ballY - b.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < b.R + BallR)
            {
                double ang = Math.Atan2(dy, dx);
                double kick = 9.5;
                _ballVx = Math.Cos(ang) * kick;
                _ballVy = Math.Sin(ang) * kick;
                _score += b.Score; b.Glow = 12; PinSound(b.Sound);
            }
        }

        double hdx = _ballX - Hyper.X, hdy = _ballY - Hyper.Y;
        if (Math.Sqrt(hdx * hdx + hdy * hdy) < Hyper.R)
        {
            _score += 2500; _ballX = 140; _ballY = 160; _ballVx = 3; _ballVy = 4; PinSound(13);
        }

        // Flippers
        if (_ballX >= 180 && _ballX <= 265 && _ballY >= 340 && _ballY <= 380)
        {
            if (_leftFlip) { _ballVy = -11.5; _ballVx = 4 + (_ballX - 220) * 0.15; _score += 150; PinSound(18); }
            else { _ballVy = -Math.Abs(_ballVy) * 0.4; _ballVx += 1.2; }
        }
        if (_ballX >= 275 && _ballX <= 360 && _ballY >= 340 && _ballY <= 380)
        {
            if (_rightFlip) { _ballVy = -11.5; _ballVx = -4 + (_ballX - 320) * 0.15; _score += 150; PinSound(18); }
            else { _ballVy = -Math.Abs(_ballVy) * 0.4; _ballVx -= 1.2; }
        }

        // Slingshots
        if (_ballY > 260 && _ballY < 330)
        {
            if (_ballX > 90 && _ballX < 140 && _ballVx < 0) { _ballVx = 6.5; _ballVy -= 3; _score += 300; PinSound(16); }
            if (_ballX > 400 && _ballX < 450 && _ballVx > 0) { _ballVx = -6.5; _ballVy -= 3; _score += 300; PinSound(16); }
        }

        if (_ballY > 425)
        {
            _ballActive = false; PinSound(3); _balls--;
            if (_balls > 0) { _ballX = 525; _ballY = 360; _ballVx = 0; _ballVy = 0; }
            else
            {
                PinSound(240);
                if (_score > _highScore) _highScore = _score;
                _msgText.Text = $"GAME OVER!\nFinal Score: {_score:N0}";
                _showSplash = false;
            }
        }
    }

    private void Draw()
    {
        _scoreText.Text = $"SCORE: {_score:N0}";
        _ballsText.Text = $"BALLS: {_balls}";
        Canvas.SetLeft(_ball, _ballX - BallR); Canvas.SetTop(_ball, _ballY - BallR);
        Canvas.SetTop(_plunger, 392 - _plungerCharge * 0.3);

        // Flipper rotation
        _leftFlipper.RenderTransform = new RotateTransform(_leftFlip ? -35 : 0, 0, 0);
        _rightFlipper.RenderTransform = new RotateTransform(_rightFlip ? 35 : 0, 55, 0);
        Canvas.SetLeft(_rightFlipper, _rightFlip ? 280 : 335);

        // Bumper glow
        foreach (var b in _bumpers)
        {
            b.Shape.Opacity = b.Glow > 0 ? 1.0 : 0.85;
            b.Shape.Fill = b.Glow > 0
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
                : new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5, GradientStops = { new GradientStop(Color.FromRgb(0x38,0xBD,0xF8),0), new GradientStop(Color.FromRgb(0x1E,0x3A,0x8A),1) } };
        }

        if (_showSplash)
        {
            if (_splash != null && _splash.Parent == null) Table.Children.Add(_splash);
            _msgText.Text = "PRESS SPACE OR F2 TO LAUNCH\nSPACE: Plunger | Z: Left | /: Right";
            _msgText.Foreground = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
            Canvas.SetTop(_msgText, 330);
        }
        else if (_msgText.Text == "" || _balls > 0)
        {
            if (_splash != null) _splash.Visibility = Visibility.Collapsed;
        }
        if (!_showSplash && _splash != null) _splash.Visibility = Visibility.Collapsed;
    }

    private void NewGame_Click(object sender, RoutedEventArgs e) => NewGame();
    private void Pause_Click(object sender, RoutedEventArgs e) => _paused = !_paused;
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestClose?.Invoke();
    private void HighScores_Click(object sender, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show($"3D Pinball High Scores:\n\n1. Space Cadet Rackneh - {_highScore:N0}",
            "High Scores", MessageBoxButton.OK, MessageBoxImage.Information);
    private void About_Click(object sender, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show("3D Pinball for Windows - Space Cadet\nCinematronics / Maxis / Microsoft\nAuthentic 60-sound FX retro simulation.",
            "About 3D Pinball", MessageBoxButton.OK, MessageBoxImage.Information);
}
