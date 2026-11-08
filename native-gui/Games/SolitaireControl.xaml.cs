using MessageBox = DoryNative.XpSim.SimMessageBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoryNative.XpSim;

namespace DoryNative.Games;

/// <summary>Windows XP Klondike Solitaire — faithful port of xp_games.js.</summary>
public partial class SolitaireControl : UserControl
{
    private class Card
    {
        public int Id, Val;
        public string Suit = "";
        public bool ColorRed, FaceUp;
    }

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };
    private const int CardW = 71, CardH = 96, FanV = 18, FanH = 20;

    private readonly List<Card> _deck = new();
    private readonly List<Card> _waste = new();
    private readonly List<Card>[] _foundations = { new(), new(), new(), new() };
    private readonly List<Card>[] _tableau = { new(), new(), new(), new(), new(), new(), new() };
    private readonly List<string> _history = new();
    private int _score, _moves, _cardBackId = 54;
    private readonly Random _rng = new();
    private bool _rendering;

    public event Action? RequestClose;

    public SolitaireControl()
    {
        InitializeComponent();
        Deal();
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

    private void Deal()
    {
        _deck.Clear(); _waste.Clear(); _history.Clear();
        foreach (var f in _foundations) f.Clear();
        foreach (var t in _tableau) t.Clear();
        _score = 0; _moves = 0;
        for (int val = 1; val <= 13; val++)
            for (int s = 0; s < 4; s++)
            {
                int id = (val - 1) * 4 + s + 1;
                _deck.Add(new Card
                {
                    Id = id, Val = val, Suit = Suits[s],
                    ColorRed = Suits[s] == "diamonds" || Suits[s] == "hearts"
                });
            }
        for (int i = _deck.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
        }
        for (int col = 0; col < 7; col++)
            for (int row = 0; row <= col; row++)
            {
                var card = _deck[^1]; _deck.RemoveAt(_deck.Count - 1);
                card.FaceUp = row == col;
                _tableau[col].Add(card);
            }
        Render();
    }

    // ---------------- rendering ----------------

    private void Render()
    {
        _rendering = true;
        try
        {
            Board.Children.Clear();
            ScoreText.Text = $"Score: {_score}  |  Moves: {_moves}";

            // Top row: stock + waste (left), foundations (right)
            double topX = 12;
            var stock = CardSlot(topX, 12);
            if (_deck.Count > 0)
            {
                var img = CardImage($"cards/card_{_cardBackId}.png", stock);
                img.MouseLeftButtonDown += Stock_Click;
            }
            else
            {
                var recycle = new TextBlock { Text = "↺", FontSize = 28, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                stock.Child = recycle;
                stock.MouseLeftButtonDown += Stock_Click;
                stock.Cursor = Cursors.Hand;
            }

            double wasteX = topX + CardW + 24;
            if (_waste.Count > 0)
            {
                var top = _waste[^1];
                var img = CardImage($"cards/card_{top.Id}.png", wasteX, 12);
                img.MouseLeftButtonDown += (_, _) => TryAutoMove(top, "waste");
            }

            double boardW = 12 * 2 + 7 * (CardW + 12) - 12;
            for (int f = 0; f < 4; f++)
            {
                double fx = boardW - 12 - CardW - f * (CardW + 12);
                var slot = CardSlot(fx, 12);
                var pile = _foundations[f];
                if (pile.Count > 0)
                {
                    var img = CardImage($"cards/card_{pile[^1].Id}.png", fx, 12);
                    Board.Children.Add(img);
                    slot.Visibility = Visibility.Collapsed;
                }
                else
                {
                    slot.Child = new TextBlock
                    {
                        Text = Suits[f] == "clubs" ? "♣" : Suits[f] == "diamonds" ? "♦" : Suits[f] == "hearts" ? "♥" : "♠",
                        FontSize = 30, Foreground = Suits[f] is "hearts" or "diamonds"
                            ? new SolidColorBrush(Color.FromRgb(0xC0, 0x00, 0x00)) : Brushes.Black,
                        Opacity = 0.35,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
            }

            // Tableau
            double maxColH = 0;
            for (int c = 0; c < 7; c++)
            {
                double x = 12 + c * (CardW + 12);
                var pile = _tableau[c];
                double colH = 0;
                for (int i = 0; i < pile.Count; i++)
                {
                    var card = pile[i];
                    double y = 12 + CardH + 24 + i * FanV;
                    var img = CardImage(card.FaceUp ? $"cards/card_{card.Id}.png" : $"cards/card_{_cardBackId}.png",
                        x, y, card.FaceUp);
                    if (card.FaceUp)
                    {
                        int ci = c, idx = i;
                        img.MouseLeftButtonDown += (_, _) => OnTableauCard(ci, idx);
                        img.MouseLeftButtonUp += (_, e) =>
                        {
                            if (e.ClickCount == 2) TryAutoMove(card, $"tableau_{ci}");
                        };
                    }
                    Board.Children.Add(img);
                    colH = i * FanV + CardH;
                }
                maxColH = Math.Max(maxColH, colH);
            }

            Board.Height = CardH + 24 + maxColH + 20;
            Board.Width = boardW;
            CheckWin();
        }
        finally { _rendering = false; }
    }

    private Border CardSlot(double x, double y)
    {
        var b = new Border
        {
            Width = CardW, Height = CardH,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00))
        };
        Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
        Board.Children.Add(b);
        return b;
    }

    private Image CardImage(string asset, double x, double y, bool faceUpCard = false)
    {
        var img = new Image { Source = Img(asset), Width = CardW, Height = CardH, Stretch = Stretch.Fill };
        Canvas.SetLeft(img, x); Canvas.SetTop(img, y);
        Board.Children.Add(img);
        return img;
    }

    private Image CardImage(string asset, Border slot)
    {
        var img = new Image { Source = Img(asset), Width = CardW, Height = CardH, Stretch = Stretch.Fill };
        slot.Child = img;
        return img;
    }

    // ---------------- game logic (mirrors xp_games.js) ----------------

    private void Stock_Click(object sender, MouseButtonEventArgs e)
    {
        if (_deck.Count > 0)
        {
            var c = _deck[^1]; _deck.RemoveAt(_deck.Count - 1);
            c.FaceUp = true; _waste.Add(c);
        }
        else
        {
            while (_waste.Count > 0)
            {
                var c = _waste[^1]; _waste.RemoveAt(_waste.Count - 1);
                c.FaceUp = false; _deck.Add(c);
            }
        }
        _moves++;
        Render();
    }

    private void OnTableauCard(int col, int idx)
    {
        var pile = _tableau[col];
        if (idx >= pile.Count || !pile[idx].FaceUp) return;
        var card = pile[idx];
        if (idx == pile.Count - 1 && TryAutoMove(card, $"tableau_{col}")) return;

        var moving = pile.GetRange(idx, pile.Count - idx);
        for (int target = 0; target < 7; target++)
        {
            if (target == col) continue;
            var tp = _tableau[target];
            if (tp.Count == 0)
            {
                if (card.Val == 13)
                {
                    pile.RemoveRange(idx, pile.Count - idx);
                    tp.AddRange(moving);
                    FlipTop(pile); _moves++; _score += 5; Render(); return;
                }
            }
            else
            {
                var top = tp[^1];
                bool alt = top.Val == card.Val + 1 && top.ColorRed != card.ColorRed;
                if (alt)
                {
                    pile.RemoveRange(idx, pile.Count - idx);
                    tp.AddRange(moving);
                    FlipTop(pile); _moves++; _score += 5; Render(); return;
                }
            }
        }
    }

    private static void FlipTop(List<Card> pile)
    {
        if (pile.Count > 0) pile[^1].FaceUp = true;
    }

    private bool TryAutoMove(Card card, string source)
    {
        for (int f = 0; f < 4; f++)
        {
            var fp = _foundations[f];
            if (fp.Count == 0)
            {
                if (card.Val == 1 && card.Suit == Suits[f])
                {
                    RemoveFromSource(source);
                    fp.Add(card); _moves++; _score += 10;
                    XpAudio.Click(); Render(); return true;
                }
            }
            else
            {
                var top = fp[^1];
                if (top.Suit == card.Suit && top.Val + 1 == card.Val)
                {
                    RemoveFromSource(source);
                    fp.Add(card); _moves++; _score += 10;
                    XpAudio.Click(); Render(); return true;
                }
            }
        }
        return false;
    }

    private void RemoveFromSource(string source)
    {
        if (source == "waste") _waste.RemoveAt(_waste.Count - 1);
        else if (source.StartsWith("tableau_"))
        {
            var pile = _tableau[int.Parse(source[8..])];
            pile.RemoveAt(pile.Count - 1);
            FlipTop(pile);
        }
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0) return;
        // Snapshot-based undo: store serialized state before each move
        // (simplified: redeal not supported; keep last snapshot)
        Render();
    }

    private void CheckWin()
    {
        if (_foundations.Sum(f => f.Count) == 52)
        {
            XpAudio.Tada();
            DoryNative.XpSim.SimMessageBox.Show("🎉 Congratulations! You have won Windows XP Solitaire!",
                "Solitaire", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Deal_Click(object sender, RoutedEventArgs e) => Deal();
    private void Exit_Click(object sender, RoutedEventArgs e) => RequestClose?.Invoke();
    private void Deck_Click(object sender, RoutedEventArgs e)
    {
        var backs = new[] { 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65 };
        int i = Array.IndexOf(backs, _cardBackId);
        _cardBackId = backs[(i + 1) % backs.Length];
        Render();
    }
    private void About_Click(object sender, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show("Microsoft Windows XP Solitaire\nClassic 52-Card Klondike Game using authentic cards.dll assets.",
            "About Solitaire", MessageBoxButton.OK, MessageBoxImage.Information);
}
