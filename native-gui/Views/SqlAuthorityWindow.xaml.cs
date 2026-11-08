using MessageBox = DoryNative.XpSim.SimMessageBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DoryNative.Models;
using DoryNative.Services;

namespace DoryNative.Views;

public partial class SqlAuthorityWindow : UserControl
{
    private readonly BackendApi _backend;
    private readonly List<SqlNode> _nodes = new();
    private readonly List<SqlEdge> _edges = new();
    private Point _dragStart;
    private bool _isDragging;
    private double _zoom = 1.0;

    private static readonly Dictionary<string, Color> NodeColors = new()
    {
        ["database"] = Color.FromRgb(0x31, 0x6A, 0xC5),
        ["table"] = Color.FromRgb(0x4C, 0xAF, 0x50),
        ["column"] = Color.FromRgb(0xFF, 0xB3, 0x00),
        ["index"] = Color.FromRgb(0x9C, 0x27, 0xB0),
        ["default"] = Color.FromRgb(0x75, 0x75, 0x75)
    };

    public SqlAuthorityWindow(BackendApi backend)
    {
        InitializeComponent();
        _backend = backend;
        Loaded += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        StatusText.Text = "Loading databases...";
        try
        {
            var dbs = await _backend.GetSqlDatabasesAsync();
            var graph = await _backend.GetSqlGraphAsync();

            _nodes.Clear();
            _edges.Clear();

            // Build nodes: databases at top, tables below
            double x = 40, y = 40;
            foreach (var db in dbs)
            {
                _nodes.Add(new SqlNode
                {
                    Id = $"db_{db.Name}",
                    Label = db.Name,
                    Type = "database",
                    Properties = new Dictionary<string, object>
                    {
                        ["tables"] = db.Tables.Count,
                        ["size"] = FormatSize(db.SizeBytes),
                        ["path"] = db.Path
                    }
                });

                // Tables under each database
                double tx = x;
                double ty = y + 90;
                foreach (var table in db.Tables)
                {
                    _nodes.Add(new SqlNode
                    {
                        Id = $"tbl_{db.Name}_{table}",
                        Label = table,
                        Type = "table",
                        Properties = new Dictionary<string, object> { ["database"] = db.Name }
                    });
                    _edges.Add(new SqlEdge { Source = $"db_{db.Name}", Target = $"tbl_{db.Name}_{table}", Label = "contains" });
                    tx += 140;
                }
                x += Math.Max(140, tx - x + 40);
                y = 40;
            }

            // Add graph nodes if API returned them
            foreach (var node in graph.Nodes)
            {
                if (_nodes.All(n => n.Id != node.Id))
                    _nodes.Add(node);
            }
            _edges.AddRange(graph.Edges);

            DbSummary.Text = $"{dbs.Count} database(s), {_nodes.Count} nodes, {_edges.Count} relationships";
            RenderGraph();
            StatusText.Text = $"Loaded {_nodes.Count} nodes";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading SQL data: {ex.Message}";
            DbSummary.Text = "Failed to load";
        }
    }

    private void RenderGraph()
    {
        GraphCanvas.Children.Clear();

        // Layout nodes in a grid
        var nodePositions = new Dictionary<string, Point>();
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(_nodes.Count)));
        for (int i = 0; i < _nodes.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            nodePositions[_nodes[i].Id] = new Point(60 + col * 160, 60 + row * 100);
        }

        // Draw edges first (behind nodes)
        foreach (var edge in _edges)
        {
            if (!nodePositions.TryGetValue(edge.Source, out var p1)) continue;
            if (!nodePositions.TryGetValue(edge.Target, out var p2)) continue;

            var line = new Line
            {
                X1 = p1.X, Y1 = p1.Y,
                X2 = p2.X, Y2 = p2.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77)),
                StrokeThickness = 1.5,
                StrokeDashArray = edge.Label == "contains" ? null : new DoubleCollection { 4, 2 }
            };
            GraphCanvas.Children.Add(line);

            // Edge label
            var midX = (p1.X + p2.X) / 2;
            var midY = (p1.Y + p2.Y) / 2;
            var label = new TextBlock
            {
                Text = edge.Label,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0xAA)),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)),
                Padding = new Thickness(2, 0, 2, 0)
            };
            Canvas.SetLeft(label, midX - 15);
            Canvas.SetTop(label, midY - 7);
            GraphCanvas.Children.Add(label);
        }

        // Draw nodes
        foreach (var node in _nodes)
        {
            if (!nodePositions.TryGetValue(node.Id, out var pos)) continue;

            var color = NodeColors.GetValueOrDefault(node.Type, NodeColors["default"]);
            var brush = new SolidColorBrush(color);

            var border = new Border
            {
                Width = node.Type == "database" ? 140 : 120,
                Height = node.Type == "database" ? 70 : 50,
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x40)),
                BorderBrush = brush,
                BorderThickness = new Thickness(node.Type == "database" ? 2 : 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Tag = node
            };

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = node.Label,
                FontSize = node.Type == "database" ? 12 : 10,
                FontWeight = node.Type == "database" ? FontWeights.Bold : FontWeights.Normal,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            if (node.Properties != null && node.Properties.Count > 0)
            {
                var sub = node.Properties.FirstOrDefault();
                stack.Children.Add(new TextBlock
                {
                    Text = $"{sub.Key}: {sub.Value}",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0xBB)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }
            border.Child = stack;

            Canvas.SetLeft(border, pos.X);
            Canvas.SetTop(border, pos.Y);
            GraphCanvas.Children.Add(border);

            // Node click -> show details
            border.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowNodeDetails(node);
            };
        }
    }

    private void ShowNodeDetails(SqlNode node)
    {
        var info = $"Type: {node.Type}\nID: {node.Id}";
        if (node.Properties != null)
        {
            foreach (var kv in node.Properties)
                info += $"\n{kv.Key}: {kv.Value}";
        }
        StatusText.Text = info.Replace("\n", " | ");
        DoryNative.XpSim.SimMessageBox.Show(info, node.Label, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadDataAsync();

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.2);
    private void Reset_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

    private void SetZoom(double z)
    {
        _zoom = Math.Clamp(z, 0.2, 4.0);
        ZoomTransform.ScaleX = _zoom;
        ZoomTransform.ScaleY = _zoom;
    }

    private void GraphScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetZoom(_zoom + (e.Delta > 0 ? 0.1 : -0.1));
        e.Handled = true;
    }

    private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(GraphCanvas);
        _isDragging = true;
        GraphCanvas.CaptureMouse();
    }

    private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(GraphCanvas);
        GraphCanvas.RenderTransform = new TranslateTransform(
            (pos.X - _dragStart.X) / _zoom,
            (pos.Y - _dragStart.Y) / _zoom);
    }

    private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        GraphCanvas.ReleaseMouseCapture();
    }

    private void GraphCanvas_RightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Refresh", Command = null });
        ((MenuItem)menu.Items[0]).Click += (s, ev) => _ = LoadDataAsync();
        menu.Items.Add(new MenuItem { Header = "Zoom In", Command = null });
        ((MenuItem)menu.Items[1]).Click += (s, ev) => ZoomIn_Click(s, new RoutedEventArgs());
        menu.Items.Add(new MenuItem { Header = "Zoom Out", Command = null });
        ((MenuItem)menu.Items[2]).Click += (s, ev) => ZoomOut_Click(s, new RoutedEventArgs());
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Export Graph JSON", Command = null });
        ((MenuItem)menu.Items[4]).Click += (s, ev) => ExportGraph();
        menu.IsOpen = true;
    }

    private void ExportGraph()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { nodes = _nodes, edges = _edges },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "sql_graph.json" };
            if (dlg.ShowDialog() == true)
                System.IO.File.WriteAllText(dlg.FileName, json);
        }
        catch (Exception ex)
        {
            DoryNative.XpSim.SimMessageBox.Show($"Export failed: {ex.Message}");
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
