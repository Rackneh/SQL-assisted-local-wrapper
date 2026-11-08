using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DoryNative.XpSim;

namespace DoryNative.Games;

/// <summary>Windows XP Minesweeper — faithful port of winmine from xp_games.js.</summary>
public partial class MinesweeperControl : UserControl
{
    private class Cell
    {
        public bool HasMine, Revealed, Flagged, Question;
        public int Adj;
    }

    private Cell[,] _grid = new Cell[0, 0];
    private int _rows = 9, _cols = 9, _mines = 10;
    private int _flagCount, _timer;
    private bool _gameOver, _started;
    private DispatcherTimer? _timerTimer;
    private readonly Random _rng = new();

    public event Action? RequestClose;

    public MinesweeperControl()
    {
        InitializeComponent();
        FaceImg.Source = Img("winmine/face_smile.png");
        NewGame();
        PreviewMouseDown += OnPreviewMouse; // shock face
        PreviewMouseUp += (_, _) => { if (!_gameOver) SetFace("face_smile"); };
    }

    private static BitmapImage? Img(string rel)
    {
        var p = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "xp_assets", rel.Replace('/', '\\'));
        if (!System.IO.File.Exists(p)) return null;
        try
        {
            var b = new BitmapImage();
            b.BeginInit(); b.UriSource = new Uri(p); b.CacheOption = BitmapCacheOption.OnLoad; b.EndInit(); b.Freeze();
            return b;
        }
        catch { return null; }
    }

    private void SetFace(string name)
    {
        var src = Img($"winmine/{name}.png");
        if (src != null) FaceImg.Source = src;
    }

    private void NewGame()
    {
        _gameOver = false; _started = false; _flagCount = 0; _timer = 0;
        _timerTimer?.Stop();
        _grid = new Cell[_rows, _cols];
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _grid[r, c] = new Cell();

        int placed = 0;
        while (placed < _mines)
        {
            int rr = _rng.Next(_rows), cc = _rng.Next(_cols);
            if (!_grid[rr, cc].HasMine) { _grid[rr, cc].HasMine = true; placed++; }
        }
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                if (_grid[r, c].HasMine) continue;
                int n = 0;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int nr = r + dr, nc = c + dc;
                        if (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols && _grid[nr, nc].HasMine) n++;
                    }
                _grid[r, c].Adj = n;
            }
        UpdateLeds();
        SetFace("face_smile");
        Render();
    }

    private void Render()
    {
        GridHost.Children.Clear();
        GridHost.Width = _cols * 16;
        GridHost.Height = _rows * 16;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var cell = _grid[r, c];
                string tile = "winmine/tile_blank.png";
                if (cell.Revealed)
                    tile = cell.HasMine ? "winmine/tile_bomb_exploded.png"
                         : cell.Adj > 0 ? $"winmine/tile_{cell.Adj}.png"
                         : "winmine/tile_empty_pressed.png";
                else if (cell.Flagged) tile = "winmine/tile_flag.png";
                else if (cell.Question) tile = "winmine/tile_question.png";

                var img = new Image
                {
                    Source = Img(tile), Width = 16, Height = 16,
                    Stretch = Stretch.Fill
                };
                Canvas.SetLeft(img, c * 16);
                Canvas.SetTop(img, r * 16);
                var hit = new Border { Width = 16, Height = 16, Background = Brushes.Transparent,
                    Tag = (r, c), Cursor = Cursors.Arrow };
                hit.MouseLeftButtonDown += CellLeft;
                hit.MouseRightButtonDown += CellRight;
                hit.Child = img;
                GridHost.Children.Add(hit);
            }
    }

    private void CellLeft(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not (int r, int c)) return;
        var cell = _grid[r, c];
        if (_gameOver || cell.Flagged) return;
        if (!_started)
        {
            _started = true;
            _timerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timerTimer.Tick += (_, _) => { _timer = Math.Min(999, _timer + 1); UpdateLeds(); };
            _timerTimer.Start();
        }
        Reveal(r, c);
    }

    private void CellRight(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement fe || fe.Tag is not (int r, int c)) return;
        var cell = _grid[r, c];
        if (_gameOver || cell.Revealed) return;
        if (!cell.Flagged && !cell.Question) { cell.Flagged = true; _flagCount++; }
        else if (cell.Flagged) { cell.Flagged = false; cell.Question = true; _flagCount--; }
        else cell.Question = false;
        UpdateLeds();
        Render();
    }

    private void Reveal(int r, int c)
    {
        var cell = _grid[r, c];
        if (cell.Revealed || cell.Flagged) return;
        cell.Revealed = true;
        if (cell.HasMine)
        {
            _gameOver = true;
            _timerTimer?.Stop();
            SetFace("face_dead");
            XpAudio.Error();
            for (int rr = 0; rr < _rows; rr++)
                for (int cc = 0; cc < _cols; cc++)
                    if (_grid[rr, cc].HasMine) _grid[rr, cc].Revealed = true;
            Render();
            return;
        }
        if (cell.Adj == 0)
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    int nr = r + dr, nc = c + dc;
                    if (nr >= 0 && nr < _rows && nc >= 0 && nc < _cols &&
                        !_grid[nr, nc].Revealed && !_grid[nr, nc].Flagged)
                        Reveal(nr, nc);
                }
        int unrevealed = 0;
        for (int rr = 0; rr < _rows; rr++)
            for (int cc = 0; cc < _cols; cc++)
                if (!_grid[rr, cc].Revealed) unrevealed++;
        if (unrevealed == _mines)
        {
            _gameOver = true;
            _timerTimer?.Stop();
            SetFace("face_win");
            XpAudio.Tada();
        }
        Render();
    }

    private void UpdateLeds()
    {
        RenderLed(MineCounter, Math.Max(0, _mines - _flagCount));
        RenderLed(TimerCounter, _timer);
    }

    private void RenderLed(StackPanel panel, int num)
    {
        panel.Children.Clear();
        string str = Math.Min(999, Math.Max(0, num)).ToString("D3");
        foreach (var ch in str)
        {
            var img = new Image { Source = Img($"winmine/digit_{ch}.png"), Width = 13, Height = 23 };
            panel.Children.Add(img);
        }
    }

    private void OnPreviewMouse(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_gameOver) SetFace("face_shock");
    }

    private void Face_Click(object sender, RoutedEventArgs e) => NewGame();
    private void NewGame_Click(object sender, RoutedEventArgs e) => NewGame();
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestClose?.Invoke();

    private void DiffBeg_Click(object sender, RoutedEventArgs e) { _rows = 9; _cols = 9; _mines = 10; NewGame(); }
    private void DiffInt_Click(object sender, RoutedEventArgs e) { _rows = 16; _cols = 16; _mines = 40; NewGame(); }
    private void DiffExp_Click(object sender, RoutedEventArgs e) { _rows = 16; _cols = 30; _mines = 99; NewGame(); }

    private void About_Click(object sender, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show("Microsoft Windows XP Minesweeper\nClassic Windows XP Game with genuine extracted sprites.",
            "About Minesweeper", MessageBoxButton.OK, MessageBoxImage.Information);
}
