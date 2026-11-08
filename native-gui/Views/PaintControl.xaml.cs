using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DoryNative.Views;

/// <summary>Windows XP Paint — pencil/brush/eraser/shapes/fill on a canvas.</summary>
public partial class PaintControl : UserControl
{
    private enum Tool { Pencil, Brush, Eraser, Line, Rect, Ellipse, Fill }
    private Tool _tool = Tool.Pencil;
    private Color _color = Colors.Black;
    private Point _start;
    private Shape? _preview;
    private Polyline? _stroke;

    private static readonly Color[] PaletteColors =
    {
        Colors.Black, Colors.White, Colors.Red, Colors.Green, Colors.Blue, Colors.Yellow,
        Colors.Orange, Colors.Purple, Colors.Pink, Colors.Brown, Colors.Gray, Colors.Cyan,
        Colors.Magenta, Colors.Lime, Colors.Navy, Colors.Teal, Colors.Maroon, Colors.Olive,
        Colors.Silver, Colors.Gold, Colors.Coral, Colors.SkyBlue, Colors.Violet, Colors.Turquoise,
        Colors.Indigo, Colors.Plum, Colors.Tan
    };

    public PaintControl()
    {
        InitializeComponent();
        Palette.ItemsSource = PaletteColors.Select(c => new { Color = c, Brush = new SolidColorBrush(c) }).ToList();
        PaintCanvas.MouseLeftButtonDown += Canvas_Down;
        PaintCanvas.MouseMove += Canvas_Move;
        PaintCanvas.MouseLeftButtonUp += Canvas_Up;
    }

    private void Canvas_Down(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(PaintCanvas);
        if (_tool == Tool.Fill)
        {
            FloodFill((int)_start.X, (int)_start.Y, _color);
            return;
        }
        if (_tool is Tool.Line or Tool.Rect or Tool.Ellipse)
        {
            _preview = _tool switch
            {
                Tool.Line => new Line { Stroke = new SolidColorBrush(_color), StrokeThickness = 2 },
                Tool.Rect => new Rectangle { Stroke = new SolidColorBrush(_color), StrokeThickness = 2 },
                _ => new Ellipse { Stroke = new SolidColorBrush(_color), StrokeThickness = 2 }
            };
            PaintCanvas.Children.Add(_preview);
        }
        else
        {
            _stroke = new Polyline
            {
                Stroke = _tool == Tool.Eraser ? Brushes.White : new SolidColorBrush(_color),
                StrokeThickness = _tool == Tool.Brush ? 6 : _tool == Tool.Eraser ? 14 : 2,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            _stroke.Points.Add(_start);
            PaintCanvas.Children.Add(_stroke);
        }
        PaintCanvas.CaptureMouse();
    }

    private void Canvas_Move(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(PaintCanvas);
        if (_preview != null)
        {
            if (_preview is Line l) { l.X1 = _start.X; l.Y1 = _start.Y; l.X2 = p.X; l.Y2 = p.Y; }
            else if (_preview is Rectangle r)
            {
                r.Width = Math.Abs(p.X - _start.X); r.Height = Math.Abs(p.Y - _start.Y);
                Canvas.SetLeft(r, Math.Min(p.X, _start.X)); Canvas.SetTop(r, Math.Min(p.Y, _start.Y));
            }
            else if (_preview is Ellipse el)
            {
                el.Width = Math.Abs(p.X - _start.X) * 2; el.Height = Math.Abs(p.Y - _start.Y) * 2;
                Canvas.SetLeft(el, Math.Min(p.X, _start.X) - Math.Abs(p.X - _start.X));
                Canvas.SetTop(el, Math.Min(p.Y, _start.Y) - Math.Abs(p.Y - _start.Y));
            }
        }
        else if (_stroke != null)
        {
            _stroke.Points.Add(p);
        }
    }

    private void Canvas_Up(object sender, MouseButtonEventArgs e)
    {
        _preview = null;
        _stroke = null;
        PaintCanvas.ReleaseMouseCapture();
    }

    private void FloodFill(int x, int y, Color target)
    {
        // Flood-fill approximation: paint a filled circle at the click point.
        var fill = new Ellipse { Fill = new SolidColorBrush(_color), Width = 40, Height = 40 };
        Canvas.SetLeft(fill, x - 20); Canvas.SetTop(fill, y - 20);
        PaintCanvas.Children.Add(fill);
    }

    private void ToolPencil_Click(object sender, RoutedEventArgs e) => _tool = Tool.Pencil;
    private void ToolBrush_Click(object sender, RoutedEventArgs e) => _tool = Tool.Brush;
    private void ToolEraser_Click(object sender, RoutedEventArgs e) => _tool = Tool.Eraser;
    private void ToolLine_Click(object sender, RoutedEventArgs e) => _tool = Tool.Line;
    private void ToolRect_Click(object sender, RoutedEventArgs e) => _tool = Tool.Rect;
    private void ToolEllipse_Click(object sender, RoutedEventArgs e) => _tool = Tool.Ellipse;
    private void ToolFill_Click(object sender, RoutedEventArgs e) => _tool = Tool.Fill;
    private void Clear_Click(object sender, RoutedEventArgs e) => PaintCanvas.Children.Clear();

    private void PalettePick_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Color c }) _color = c;
    }
}
