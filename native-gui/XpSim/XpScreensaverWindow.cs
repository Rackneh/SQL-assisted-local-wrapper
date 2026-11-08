using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DoryNative.XpSim;

/// <summary>
/// Fullscreen borderless screensaver. Renders one of the classic XP
/// savers (starfield / flying windows / pipes / mystify) on a Canvas at
/// ~30fps. Any input dismisses it.
/// </summary>
public class XpScreensaverWindow : Window
{
    public enum SaverMode { Starfield, FlyingWindows, Pipes, Mystify }

    private readonly SaverMode _mode;
    private readonly Canvas _canvas = new Canvas { Background = Brushes.Black };
    private readonly DispatcherTimer _timer;
    private bool _armExit = false;

    // Shared state
    private readonly Random _rng = new Random();
    private double _w, _h;

    // Starfield
    private sealed class Star { public double X, Y, Z; public Ellipse El; }
    private readonly List<Star> _stars = new();

    // Flying windows
    private sealed class Flag
    {
        public double X, Y, Z, Rot, Speed;
        public readonly Rectangle[] Quads = new Rectangle[4];
    }
    private readonly List<Flag> _flags = new();
    private static readonly Color[] FlagColors =
    {
        Color.FromRgb(0xE4, 0x5B, 0x40), Color.FromRgb(0x38, 0x8E, 0x3C),
        Color.FromRgb(0x02, 0x84, 0xC7), Color.FromRgb(0xF5, 0x9E, 0x0B)
    };

    // Pipes
    private Rectangle? _pipe, _pipeHi;
    private double _px, _py;
    private int _dir, _stepCount;
    private Color _pipeColor = Colors.Crimson;

    // Mystify
    private const int MystifyPoints = 140;
    private readonly List<Point> _trail = new();
    private readonly List<Line> _segments = new();
    private Point _head;
    private int _tick;

    [DllImport("user32.dll")] private static extern int ShowCursor(bool show);

    public XpScreensaverWindow(SaverMode mode)
    {
        _mode = mode;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.None;
        Content = _canvas;
        ShowInTaskbar = false;

        // Arm exit shortly after start so an accidental click doesn't instantly kill it
        var arm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        arm.Tick += (_, _) => { _armExit = true; arm.Stop(); };
        arm.Start();

        PreviewMouseDown += (_, _) => Dismiss();
        PreviewMouseMove += (_, _) => { if (_armExit) Dismiss(); };
        PreviewKeyDown += (_, _) => Dismiss();
        Closed += (_, _) => { _timer.Stop(); ShowCursor(true); };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Frame();
        Loaded += (_, _) =>
        {
            _w = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            _h = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            BuildScene();
            ShowCursor(false);
            _timer.Start();
        };
    }

    private void Dismiss() { _armExit = true; Close(); }

    private static readonly Color[] PipeColors =
    {
        Color.FromRgb(0xE1, 0x1D, 0x48), Color.FromRgb(0x25, 0x63, 0xEB),
        Color.FromRgb(0x16, 0xA3, 0x4A), Color.FromRgb(0xCA, 0x8A, 0x04),
        Color.FromRgb(0x93, 0x33, 0xEA), Color.FromRgb(0x06, 0xB6, 0xD4)
    };

    private void BuildScene()
    {
        _canvas.Children.Clear();
        _stars.Clear(); _flags.Clear(); _segments.Clear(); _trail.Clear();

        switch (_mode)
        {
            case SaverMode.Starfield:
            {
                for (int i = 0; i < 300; i++)
                {
                    var el = new Ellipse { Fill = Brushes.White, Width = 2, Height = 2 };
                    _canvas.Children.Add(el);
                    _stars.Add(new Star
                    {
                        X = (_rng.NextDouble() - 0.5) * _w,
                        Y = (_rng.NextDouble() - 0.5) * _h,
                        Z = _rng.NextDouble() * _w,
                        El = el
                    });
                }
                break;
            }
            case SaverMode.FlyingWindows:
            {
                for (int i = 0; i < 24; i++)
                {
                    var f = new Flag
                    {
                        X = (_rng.NextDouble() - 0.5) * _w * 1.5,
                        Y = (_rng.NextDouble() - 0.5) * _h * 1.5,
                        Z = _rng.NextDouble() * 800 + 100,
                        Rot = _rng.NextDouble() * Math.PI * 2,
                        Speed = _rng.NextDouble() * 3 + 2
                    };
                    for (int q = 0; q < 4; q++)
                    {
                        var r = new Rectangle { Fill = new SolidColorBrush(FlagColors[q]), IsHitTestVisible = false };
                        _canvas.Children.Add(r);
                        f.Quads[q] = r;
                    }
                    _flags.Add(f);
                }
                break;
            }
            case SaverMode.Pipes:
            {
                _pipe = new Rectangle { Fill = new SolidColorBrush(_pipeColor), IsHitTestVisible = false };
                _pipeHi = new Rectangle { Fill = Brushes.White, IsHitTestVisible = false };
                _canvas.Children.Add(_pipe);
                _canvas.Children.Add(_pipeHi);
                _px = _w / 2; _py = _h / 2; _dir = _rng.Next(4); _stepCount = 0;
                break;
            }
            case SaverMode.Mystify:
            {
                _head = new Point(_w / 2, _h / 2);
                for (int i = 0; i < MystifyPoints - 1; i++)
                {
                    var ln = new Line { StrokeThickness = 2, IsHitTestVisible = false };
                    _canvas.Children.Add(ln);
                    _segments.Add(ln);
                }
                break;
            }
        }
    }

    private void Frame()
    {
        switch (_mode)
        {
            case SaverMode.Starfield: FrameStars(); break;
            case SaverMode.FlyingWindows: FrameFlags(); break;
            case SaverMode.Pipes: FramePipes(); break;
            case SaverMode.Mystify: FrameMystify(); break;
        }
    }

    private void FrameStars()
    {
        double cx = _w / 2, cy = _h / 2;
        foreach (var s in _stars)
        {
            s.Z -= 4;
            if (s.Z <= 0)
            {
                s.Z = _w;
                s.X = (_rng.NextDouble() - 0.5) * _w;
                s.Y = (_rng.NextDouble() - 0.5) * _h;
            }
            double k = 200 / s.Z;
            double px = cx + s.X * k, py = cy + s.Y * k;
            double sz = Math.Max(1, (1 - s.Z / _w) * 3);
            bool vis = px >= 0 && px <= _w && py >= 0 && py <= _h;
            s.El.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            if (vis)
            {
                Canvas.SetLeft(s.El, px - sz / 2);
                Canvas.SetTop(s.El, py - sz / 2);
                s.El.Width = s.El.Height = sz;
            }
        }
    }

    private void FrameFlags()
    {
        double cx = _w / 2, cy = _h / 2;
        foreach (var f in _flags)
        {
            f.Z -= f.Speed;
            f.Rot += 0.02;
            if (f.Z <= 10)
            {
                f.Z = 800;
                f.X = (_rng.NextDouble() - 0.5) * _w * 1.5;
                f.Y = (_rng.NextDouble() - 0.5) * _h * 1.5;
            }
            double scale = 300 / f.Z;
            double px = cx + f.X * scale, py = cy + f.Y * scale;
            double sz = 40 * scale;
            bool vis = px >= -50 && px <= _w + 50 && py >= -50 && py <= _h + 50;
            foreach (var r in f.Quads) r.Visibility = vis ? Visibility.Visible : Visibility.Collapsed;
            if (vis) LayoutFlagQuads(f, px, py, sz, f.Rot);
        }
    }

    // Positions the 4 quads of a flag: quad centers are rotated around the flag
    // center, and each square rotates around its own center.
    private void LayoutFlagQuads(Flag f, double px, double py, double sz, double rot)
    {
        double half = sz / 2;
        double qw = Math.Max(1, half - 1); // slight gap like the web version
        double deg = rot * 180 / Math.PI;
        double c = Math.Cos(rot), s = Math.Sin(rot);
        var centers = new[]
        {
            new Point(-half / 2, -half / 2), new Point(half / 2, -half / 2),
            new Point(-half / 2, half / 2), new Point(half / 2, half / 2)
        };
        for (int i = 0; i < 4; i++)
        {
            var r = f.Quads[i];
            var lc = centers[i];
            double rx = lc.X * c - lc.Y * s;
            double ry = lc.X * s + lc.Y * c;
            Canvas.SetLeft(r, px + rx - qw / 2);
            Canvas.SetTop(r, py + ry - qw / 2);
            r.Width = r.Height = qw;
            r.RenderTransform = new RotateTransform(deg, qw / 2, qw / 2);
        }
    }

    private void FramePipes()
    {
        if (_pipe == null || _pipeHi == null) return;
        const double step = 4, width = 8;
        if (_stepCount % 60 == 0)
        {
            _dir = _rng.Next(4);
            _pipeColor = PipeColors[_rng.Next(PipeColors.Length)];
            _pipe.Fill = new SolidColorBrush(_pipeColor);
        }
        if (_stepCount % 600 == 0)
        {
            // Fade trail by overdrawing translucent black
            var fade = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(217, 0, 0, 0)),
                Width = _w, Height = _h, IsHitTestVisible = false
            };
            _canvas.Children.Insert(1, fade);
            while (_canvas.Children.Count > 400) _canvas.Children.RemoveAt(_canvas.Children.Count - 1);
            _px = _rng.NextDouble() * _w;
            _py = _rng.NextDouble() * _h;
        }
        Canvas.SetLeft(_pipe, _px - width / 2);
        Canvas.SetTop(_pipe, _py - width / 2);
        _pipe.Width = _pipe.Height = width;
        Canvas.SetLeft(_pipeHi, _px - width / 4);
        Canvas.SetTop(_pipeHi, _py - width / 4);
        _pipeHi.Width = _pipeHi.Height = width / 2;

        switch (_dir)
        {
            case 0: _px += step; break;
            case 1: _py += step; break;
            case 2: _px -= step; break;
            default: _py -= step; break;
        }
        if (_px < 0) _px = _w; if (_px > _w) _px = 0;
        if (_py < 0) _py = _h; if (_py > _h) _py = 0;
        _stepCount++;
    }

    private void FrameMystify()
    {
        _tick++;
        // Wander: mostly straight, occasional 90° turn (classic mystify motion)
        if (_tick % 20 == 0) _mystifyDir = _rng.Next(4);
        double spd = 3.5;
        switch (_mystifyDir)
        {
            case 0: _head.X += spd; break;
            case 1: _head.Y += spd; break;
            case 2: _head.X -= spd; break;
            default: _head.Y -= spd; break;
        }
        if (_head.X < 0 || _head.X > _w) _mystifyDir = _mystifyDir == 0 ? 2 : 0;
        if (_head.Y < 0 || _head.Y > _h) _mystifyDir = _mystifyDir == 1 ? 3 : 1;
        _head.X = Math.Clamp(_head.X, 0, _w);
        _head.Y = Math.Clamp(_head.Y, 0, _h);

        _trail.Insert(0, _head);
        if (_trail.Count > MystifyPoints) _trail.RemoveAt(_trail.Count - 1);

        // Ensure enough trail before drawing
        if (_trail.Count < 2) return;

        for (int i = 0; i < _segments.Count; i++)
        {
            var ln = _segments[i];
            if (i + 1 >= _trail.Count) { ln.Visibility = Visibility.Collapsed; continue; }
            ln.Visibility = Visibility.Visible;
            ln.X1 = _trail[i].X; ln.Y1 = _trail[i].Y;
            ln.X2 = _trail[i + 1].X; ln.Y2 = _trail[i + 1].Y;
            // Hue shifts along the trail — classic mystify rainbow
            double hue = (i * 2.0 + _tick * 0.5) % 360.0;
            ln.Stroke = new SolidColorBrush(Hsv(hue, 0.9, 1.0));
        }
    }

    private int _mystifyDir = 0;

    private static Color Hsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }
}
