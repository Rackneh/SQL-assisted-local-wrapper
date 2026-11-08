using MessageBox = DoryNative.XpSim.SimMessageBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DoryNative.Games;

/// <summary>
/// Windows XP Spider Solitaire — 104 cards (2 decks), 10 tableau piles,
/// 8 home cells, stock deals one card per pile. Build descending same-suit
/// sequences; 1-suit or 2-suit mode.
/// </summary>
public partial class SpiderControl : UserControl
{
    private class Card
    {
        public int Id;          // 1..104 unique
        public int ImgId;       // 1..52 for the card face
        public int Val;
        public string Suit = "";
        public int SuitIdx;     // 0..suitCount-1 (game suit), real suit for face
    }

    private enum LocKind { Tableau, Home, Stock }
    private class Loc
    {
        public LocKind Kind;
        public int Index;
        public override string ToString() => $"{Kind}:{Index}";
    }

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };
    private const int CardW = 64, CardH = 86, FanV = 20, GapX = 10;
    private const int ColCount = 10, HomeCount = 8;

    private readonly Card?[] _byId = new Card?[104];
    private readonly List<Card>[] _tableau = new List<Card>[ColCount];
    private readonly List<Card>[] _homes = new List<Card>[HomeCount];
    private readonly List<Card> _stock = new();
    private readonly List<Dictionary<int, string>> _undo = new();
    private int _moves, _score = 500, _suitCount = 1;
    private Loc? _selLoc;
    private int _selStart;

    public event Action? RequestClose;

    public SpiderControl()
    {
        InitializeComponent();
        for (int i = 0; i < ColCount; i++) _tableau[i] = new List<Card>();
        for (int i = 0; i < HomeCount; i++) _homes[i] = new List<Card>();
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
        _undo.Clear(); _moves = 0; _score = 500; _selLoc = null; _stock.Clear();
        foreach (var t in _tableau) t.Clear();
        foreach (var h in _homes) h.Clear();

        // Build 104 cards: two copies of each of 52. Game-suit assignment:
        // 1-suit mode -> all cards are "same suit" logically (suit shown on face
        // still varies for readability, sequence rule uses SuitIdx=0 for all).
        // 2-suit mode -> alternate between two game suits.
        var deck = new List<Card>();
        for (int copy = 0; copy < 2; copy++)
            for (int val = 1; val <= 13; val++)
                for (int s = 0; s < 4; s++)
                {
                    int imgId = (val - 1) * 4 + s + 1;
                    int id = copy * 52 + imgId;
                    var card = new Card
                    {
                        Id = id, ImgId = imgId, Val = val,
                        Suit = Suits[s],
                        SuitIdx = _suitCount == 1 ? 0 : s % 2
                    };
                    _byId[id - 1] = card;
                    deck.Add(card);
                }
        // Shuffle
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        // Deal 54 cards to the tableau round-robin (cols 0-3 end with 6, rest 5);
        // the remaining 50 go to the stock.
        int di = 0, dealt = 0;
        while (dealt < 54)
        {
            for (int col = 0; col < ColCount && dealt < 54; col++, di++, dealt++)
                _tableau[col].Add(deck[di]);
        }
        while (di < deck.Count) _stock.Add(deck[di++]);
        Render();
    }

    // ---------------- helpers ----------------

    private Loc? FindLoc(Card card)
    {
        for (int i = 0; i < ColCount; i++)
            if (_tableau[i].Contains(card)) return new Loc { Kind = LocKind.Tableau, Index = i };
        for (int i = 0; i < HomeCount; i++)
            if (_homes[i].Contains(card)) return new Loc { Kind = LocKind.Home, Index = i };
        if (_stock.Contains(card)) return new Loc { Kind = LocKind.Stock, Index = 0 };
        return null;
    }

    private bool IsValidSequence(List<Card> cards)
    {
        // descending by 1, same game-suit
        for (int i = 1; i < cards.Count; i++)
            if (cards[i - 1].Val != cards[i].Val + 1 || cards[i - 1].SuitIdx != cards[i].SuitIdx)
                return false;
        return true;
    }

    private bool CanDropOnTableau(List<Card> cards, int col)
    {
        var pile = _tableau[col];
        if (pile.Count == 0) return true; // any card/sequence to empty pile
        var top = pile[^1];
        return cards[0].Val == top.Val + 1 && cards[0].SuitIdx == top.SuitIdx;
    }

    private int HomeIndexFor(Card card)
    {
        // Each home pile holds one suit (real suit). With 2 copies per suit there are
        // exactly 2 home slots per real suit.
        int suitBase = Array.IndexOf(Suits, card.Suit);
        // homes 0..7: pair per suit -> home i belongs to suit i/2? Simpler: find a home
        // pile whose top matches suit and next val, or an empty pile accepting this suit.
        for (int i = 0; i < HomeCount; i++)
        {
            var pile = _homes[i];
            if (pile.Count == 0) continue;
            var top = pile[^1];
            if (top.Suit == card.Suit && card.Val == top.Val + 1) return i;
        }
        // empty home: bind to this suit if no pile of this suit started yet? To keep it
        // simple allow empty home for any Ace; pile then binds to that suit.
        for (int i = 0; i < HomeCount; i++)
            if (_homes[i].Count == 0) return i;
        return -1;
    }

    private bool CanAutoHome(Card card)
    {
        if (card.Val == 1) return true;
        return HomeIndexFor(card) >= 0;
    }

    private void Snapshot()
    {
        var snap = new Dictionary<int, string>();
        for (int i = 0; i < 104; i++)
        {
            var c = _byId[i]!;
            var loc = FindLoc(c);
            int off = loc?.Kind == LocKind.Tableau ? _tableau[loc.Index].IndexOf(c) : 0;
            snap[i] = loc == null ? "?" : $"{loc.Kind}:{loc.Index}:{off}";
        }
        // stock order matters: store stock ids in order under key 999
        snap[999] = string.Join(",", _stock.Select(c => c.Id));
        _undo.Add(snap);
        if (_undo.Count > 200) _undo.RemoveAt(0);
    }

    private void Restore(Dictionary<int, string> snap)
    {
        foreach (var t in _tableau) t.Clear();
        foreach (var h in _homes) h.Clear();
        _stock.Clear();
        var pending = new List<(Card card, Loc loc, int off)>();
        for (int i = 0; i < 104; i++)
        {
            var parts = snap[i].Split(':');
            if (parts.Length < 3) continue;
            var kind = (LocKind)Enum.Parse(typeof(LocKind), parts[0]);
            pending.Add((_byId[i]!, new Loc { Kind = kind, Index = int.Parse(parts[1]) }, int.Parse(parts[2])));
        }
        foreach (var grp in pending.Where(p => p.loc.Kind == LocKind.Tableau).OrderBy(p => p.off))
            _tableau[grp.loc.Index].Add(grp.card);
        foreach (var grp in pending.Where(p => p.loc.Kind == LocKind.Home).OrderBy(p => p.card.Val))
            _homes[grp.loc.Index].Add(grp.card);
        if (snap.TryGetValue(999, out var stockIds) && !string.IsNullOrEmpty(stockIds))
            foreach (var idStr in stockIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                _stock.Add(_byId[int.Parse(idStr) - 1]!);
        _selLoc = null;
    }

    private void RemoveCards(List<Card> cards)
    {
        if (_selLoc == null) return;
        switch (_selLoc.Kind)
        {
            case LocKind.Tableau: _tableau[_selLoc.Index].RemoveRange(_selStart, cards.Count); break;
            case LocKind.Home: _homes[_selLoc.Index].Remove(cards[0]); break;
            case LocKind.Stock: _stock.Remove(cards[0]); break;
        }
    }

    private void TryMove(List<Card> cards, Loc dest)
    {
        if (cards.Count == 0 || _selLoc == null) return;
        bool ok = dest.Kind switch
        {
            LocKind.Tableau => CanDropOnTableau(cards, dest.Index) &&
                !(dest.Kind == _selLoc.Kind && dest.Index == _selLoc.Index),
            LocKind.Home => cards.Count == 1 && HomeIndexFor(cards[0]) == dest.Index,
            _ => false
        };
        if (!ok) return;
        Snapshot();
        RemoveCards(cards);
        switch (dest.Kind)
        {
            case LocKind.Tableau: _tableau[dest.Index].AddRange(cards); break;
            case LocKind.Home: _homes[dest.Index].Add(cards[0]); break;
        }
        _moves++;
        _score = Math.Max(0, _score - 1);
        _selLoc = null;
        CheckWin();
        Render();
    }

    private void CheckWin()
    {
        if (_homes.Sum(h => h.Count) == 104)
        {
            DoryNative.XpSim.SimMessageBox.Show("You win! All cards are in the home cells.", "Spider Solitaire",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Deal();
        }
    }

    // ---------------- input ----------------

    private void OnCardClick(Card card, LocKind kind, int pileIndex, int cardIndex)
    {
        var loc = new Loc { Kind = kind, Index = pileIndex };
        if (_selLoc != null)
        {
            if (loc.Kind == _selLoc.Kind && loc.Index == _selLoc.Index && kind != LocKind.Stock)
            {
                _selLoc = null; Render(); return;
            }
            var cards = CardsAt(_selLoc, _selStart);
            TryMove(cards, loc);
            return;
        }

        List<Card> movable; int startIdx;
        switch (kind)
        {
            case LocKind.Tableau:
                movable = _tableau[pileIndex].Skip(cardIndex).ToList();
                startIdx = cardIndex;
                if (!IsValidSequence(movable))
                {
                    movable = new List<Card> { _tableau[pileIndex][^1] };
                    startIdx = _tableau[pileIndex].Count - 1;
                }
                break;
            case LocKind.Home:
                movable = new List<Card> { _homes[pileIndex][^1] };
                startIdx = 0;
                break;
            default:
                movable = new List<Card>(); startIdx = 0; break;
        }
        if (movable.Count == 0) return;
        _selLoc = loc; _selStart = startIdx;
        Render();
    }

    private List<Card> CardsAt(Loc loc, int startIdx)
    {
        return loc.Kind switch
        {
            LocKind.Tableau => _tableau[loc.Index].Skip(startIdx).ToList(),
            LocKind.Home => new List<Card> { _homes[loc.Index][^1] },
            _ => new List<Card>()
        };
    }

    private void OnCardDoubleClick(Card card)
    {
        var loc = FindLoc(card);
        if (loc == null || loc.Kind == LocKind.Stock) return;
        // Only single top cards auto-home
        List<Card> unit; int startIdx = 0;
        if (loc.Kind == LocKind.Tableau)
        {
            int idx = _tableau[loc.Index].IndexOf(card);
            unit = _tableau[loc.Index].Skip(idx).ToList();
            startIdx = idx;
            if (unit.Count != 1) return;
        }
        else unit = new List<Card> { card };

        int h = HomeIndexFor(card);
        if (h < 0) return;
        Snapshot();
        if (loc.Kind == LocKind.Tableau) _tableau[loc.Index].RemoveAt(startIdx);
        else _homes[loc.Index].Remove(card);
        _homes[h].Add(card);
        _moves++;
        _score = Math.Max(0, _score - 1);
        CheckWin();
        Render();
    }

    // ---------------- rendering ----------------

    private void Render()
    {
        Board.Children.Clear();
        ScoreText.Text = $"Score: {_score}  |  Moves: {_moves}";

        // Top row: stock (left) + 8 home cells
        double topY = 12;
        // Stock pile
        var stockX = 12.0;
        var stockSlot = new Border
        {
            Width = CardW, Height = CardH,
            BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(4)
        };
        Canvas.SetLeft(stockSlot, stockX); Canvas.SetTop(stockSlot, topY);
        Board.Children.Add(stockSlot);
        if (_stock.Count > 0)
        {
            var back = new Image { Width = CardW, Height = CardH, Stretch = Stretch.Fill, Cursor = Cursors.Hand };
            var src = Img("cards/card_55.png"); // card back
            if (src == null) src = Img("cards/card_1.png");
            if (src != null) back.Source = src;
            Canvas.SetLeft(back, stockX); Canvas.SetTop(back, topY);
            back.MouseLeftButtonDown += Stock_Click;
            Board.Children.Add(back);
            stockSlot.Child = new TextBlock
            {
                Text = _stock.Count.ToString(), FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Home cells
        double hx = stockX + CardW + 30;
        for (int i = 0; i < HomeCount; i++)
        {
            double x = hx + i * (CardW + GapX);
            var slot = new Border
            {
                Width = CardW, Height = CardH,
                BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(4)
            };
            Canvas.SetLeft(slot, x); Canvas.SetTop(slot, topY);
            Board.Children.Add(slot);
            if (_homes[i].Count == 0)
            {
                slot.Child = new TextBlock
                {
                    Text = "A", FontSize = 20, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                };
            }
            if (_homes[i].Count > 0)
            {
                var img = CardImage($"cards/card_{_homes[i][^1].ImgId}.png", x, topY);
                WireCard(img, _homes[i][^1], LocKind.Home, i, 0);
            }
        }

        // 10 tableau columns
        double colX0 = 12;
        for (int col = 0; col < ColCount; col++)
        {
            double x = colX0 + col * (CardW + GapX);
            double baseY = topY + CardH + 24;
            var slot = new Border
            {
                Width = CardW, Height = CardH,
                BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(4)
            };
            Canvas.SetLeft(slot, x); Canvas.SetTop(slot, baseY);
            Board.Children.Add(slot);
            var pile = _tableau[col];
            for (int i = 0; i < pile.Count; i++)
            {
                double y = baseY + i * FanV;
                var img = CardImage($"cards/card_{pile[i].ImgId}.png", x, y);
                WireCard(img, pile[i], LocKind.Tableau, col, i);
                if (_selLoc != null && _selLoc.Kind == LocKind.Tableau && _selLoc.Index == col && i >= _selStart)
                    img.Opacity = 0.55;
            }
        }
    }

    private void Stock_Click(object sender, MouseButtonEventArgs e)
    {
        // Deal one card to each tableau pile — must have all piles non-empty
        if (_stock.Count < ColCount) return;
        if (_tableau.Any(t => t.Count == 0)) return;
        Snapshot();
        // XP spider deals from the TOP of stock visually; we take from index 0.
        for (int col = 0; col < ColCount; col++)
        {
            _tableau[col].Add(_stock[0]);
            _stock.RemoveAt(0);
        }
        _moves++;
        _score = Math.Max(0, _score - 1);
        Render();
    }

    private Image CardImage(string rel, double x, double y)
    {
        var img = new Image { Width = CardW, Height = CardH, Stretch = Stretch.Fill, Cursor = Cursors.Hand };
        var src = Img(rel);
        if (src != null) img.Source = src;
        Canvas.SetLeft(img, x); Canvas.SetTop(img, y);
        Board.Children.Add(img);
        return img;
    }

    private void WireCard(Image img, Card card, LocKind kind, int pileIndex, int cardIndex)
    {
        img.MouseLeftButtonDown += (_, _) => OnCardClick(card, kind, pileIndex, cardIndex);
        img.MouseLeftButtonUp += (_, e) =>
        {
            if (e.ClickCount == 2) OnCardDoubleClick(card);
        };
    }

    // ---------------- menu ----------------

    private void Deal_Click(object s, RoutedEventArgs e) => Deal();
    private void Undo_Click(object s, RoutedEventArgs e)
    {
        if (_undo.Count == 0) return;
        var snap = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        Restore(snap);
        _moves = Math.Max(0, _moves - 1);
        Render();
    }
    private void Suit1_Click(object s, RoutedEventArgs e) { _suitCount = 1; Deal(); }
    private void Suit2_Click(object s, RoutedEventArgs e) { _suitCount = 2; Deal(); }
    private void Exit_Click(object s, RoutedEventArgs e) => RequestClose?.Invoke();
    private void About_Click(object s, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show("Spider Solitaire for Windows XP\n\nBuild descending sequences of the same suit. Complete sequences move to the home cells. Deal new rows from the stock when every column has a card.",
            "About Spider Solitaire", MessageBoxButton.OK, MessageBoxImage.Information);
}
