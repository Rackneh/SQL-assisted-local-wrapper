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

/// <summary>Windows XP FreeCell — 4 freecells, 4 homecells, 8 cascades.</summary>
public partial class FreeCellControl : UserControl
{
    private class Card
    {
        public int Id, Val;
        public string Suit = "";
        public bool ColorRed;
    }

    private enum LocKind { Cascade, Free, Home }
    private class Loc
    {
        public LocKind Kind;
        public int Index;
        public override string ToString() => $"{Kind}:{Index}";
    }

    private static readonly string[] Suits = { "clubs", "diamonds", "hearts", "spades" };
    private const int CardW = 71, CardH = 96, FanV = 22, GapX = 12;
    private const int FreeCount = 4, HomeCount = 4, ColCount = 8;

    private readonly Card?[] _byId = new Card?[52];
    private readonly List<Card>[] _cascades = new List<Card>[ColCount];
    private readonly List<Card> _free = new();
    private readonly List<Card>[] _homes = { new(), new(), new(), new(), new(), new(), new(), new() };
    private readonly List<Dictionary<int, string>> _undo = new();
    private int _moves;
    private Loc? _selLoc;
    private int _selStart; // index within cascade pile when a stack is selected

    public event Action? RequestClose;

    public FreeCellControl()
    {
        InitializeComponent();
        for (int i = 0; i < ColCount; i++) _cascades[i] = new List<Card>();
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
        _undo.Clear(); _moves = 0; _selLoc = null;
        foreach (var c in _cascades) c.Clear();
        _free.Clear();
        foreach (var h in _homes) h.Clear();
        var deck = new List<Card>();
        for (int val = 1; val <= 13; val++)
            for (int s = 0; s < 4; s++)
            {
                int id = (val - 1) * 4 + s + 1;
                var card = new Card { Id = id, Val = val, Suit = Suits[s], ColorRed = Suits[s] == "diamonds" || Suits[s] == "hearts" };
                _byId[id - 1] = card;
                deck.Add(card);
            }
        // FreeCell deals 8 columns of 7
        for (int col = 0; col < ColCount; col++)
            for (int row = 0; row < 7; row++)
                _cascades[col].Add(deck[col * 7 + row]);
        Render();
    }

    // ---------------- state helpers ----------------

    private Loc? FindLoc(Card card)
    {
        for (int i = 0; i < ColCount; i++)
        {
            int idx = _cascades[i].IndexOf(card);
            if (idx >= 0) return new Loc { Kind = LocKind.Cascade, Index = i };
        }
        int fi = _free.IndexOf(card);
        if (fi >= 0) return new Loc { Kind = LocKind.Free, Index = fi };
        for (int i = 0; i < HomeCount; i++)
            if (_homes[i].Contains(card)) return new Loc { Kind = LocKind.Home, Index = i };
        return null;
    }

    private List<Card> CardsAt(Loc loc, int startIdx = 0)
    {
        return loc.Kind switch
        {
            LocKind.Cascade => _cascades[loc.Index].Skip(startIdx).ToList(),
            LocKind.Free => new List<Card> { _free[loc.Index] },
            LocKind.Home => new List<Card> { _homes[loc.Index][^1] },
            _ => new List<Card>()
        };
    }

    private bool IsValidStack(List<Card> cards)
    {
        // descending by 1, alternating colors, all present
        for (int i = 1; i < cards.Count; i++)
            if (cards[i - 1].Val != cards[i].Val + 1 || cards[i - 1].ColorRed == cards[i].ColorRed)
                return false;
        return true;
    }

    private bool CanDropOnCascade(List<Card> cards, int col)
    {
        var pile = _cascades[col];
        if (cards.Count > 1 && !IsValidStack(cards)) return false;
        if (pile.Count == 0) return true; // any stack to empty cascade
        var top = pile[^1];
        return cards[0].Val == top.Val + 1 && cards[0].ColorRed != top.ColorRed;
    }

    private bool CanDropOnHome(Card card, int homeIdx)
    {
        var pile = _homes[homeIdx];
        // home idx maps to suit: homes built by suit order clubs,d,hearts,spades -> suit idx
        if (pile.Count == 0) return card.Val == 1;
        var top = pile[^1];
        return top.Suit == card.Suit && card.Val == top.Val + 1;
    }

    private void Snapshot()
    {
        var snap = new Dictionary<int, string>();
        for (int i = 0; i < 52; i++)
        {
            var c = _byId[i]!;
            var loc = FindLoc(c);
            snap[i] = loc == null ? "?" : $"{loc.Kind}:{loc.Index}:{LocOffset(c, loc)}";
        }
        _undo.Add(snap);
        if (_undo.Count > 200) _undo.RemoveAt(0);
    }

    private int LocOffset(Card c, Loc loc)
    {
        if (loc.Kind == LocKind.Cascade) return _cascades[loc.Index].IndexOf(c);
        return 0;
    }

    private void Restore(Dictionary<int, string> snap)
    {
        foreach (var c in _cascades) c.Clear();
        _free.Clear();
        foreach (var h in _homes) h.Clear();
        var pending = new List<(Card card, Loc loc, int off)>();
        for (int i = 0; i < 52; i++)
        {
            var parts = snap[i].Split(':');
            if (parts.Length < 3) continue;
            var kind = (LocKind)Enum.Parse(typeof(LocKind), parts[0]);
            var loc = new Loc { Kind = kind, Index = int.Parse(parts[1]) };
            pending.Add((_byId[i]!, loc, int.Parse(parts[2])));
        }
        // Cascades need ordered insertion — sort by offset
        foreach (var grp in pending.Where(p => p.loc.Kind == LocKind.Cascade).OrderBy(p => p.off))
            _cascades[grp.loc.Index].Add(grp.card);
        foreach (var p in pending.Where(p => p.loc.Kind == LocKind.Free))
            _free.Add(p.card);
        // Homes: append in val order per home pile
        foreach (var grp in pending.Where(p => p.loc.Kind == LocKind.Home).OrderBy(p => p.card.Val))
            _homes[grp.loc.Index].Add(grp.card);
        _selLoc = null;
    }

    private void TryMove(List<Card> cards, Loc dest)
    {
        if (cards.Count == 0 || _selLoc == null) return;
        bool ok = dest.Kind switch
        {
            LocKind.Cascade => CanDropOnCascade(cards, dest.Index),
            LocKind.Free => cards.Count == 1 && _free.Count < FreeCount,
            LocKind.Home => cards.Count == 1 && CanDropOnHome(cards[0], dest.Index),
            _ => false
        };
        if (!ok) return;

        // Don't allow moving a stack onto itself / same pile no-op
        if (dest.Kind == _selLoc.Kind && dest.Index == _selLoc.Index) return;
        if (_selLoc.Kind == LocKind.Cascade && dest.Kind == LocKind.Cascade &&
            _selStart == 0 && ReferenceEquals(_cascades[_selLoc.Index], _cascades[dest.Index]))
            return;

        Snapshot();
        RemoveCards(cards);
        switch (dest.Kind)
        {
            case LocKind.Cascade: _cascades[dest.Index].AddRange(cards); break;
            case LocKind.Free: _free.Add(cards[0]); break;
            case LocKind.Home: _homes[dest.Index].Add(cards[0]); break;
        }
        _moves++;
        _selLoc = null;
        CheckWin();
        Render();
    }

    private void RemoveCards(List<Card> cards)
    {
        if (_selLoc == null) return;
        switch (_selLoc.Kind)
        {
            case LocKind.Cascade: _cascades[_selLoc.Index].RemoveRange(_selStart, cards.Count); break;
            case LocKind.Free: _free.RemoveAt(_selLoc.Index); break;
            case LocKind.Home: _homes[_selLoc.Index].RemoveAt(_homes[_selLoc.Index].Count - 1); break;
        }
    }

    private void CheckWin()
    {
        if (_homes.Sum(h => h.Count) == 52)
        {
            DoryNative.XpSim.SimMessageBox.Show("You win! All cards are in the home cells.", "FreeCell",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Deal();
        }
    }

    // ---------------- input ----------------

    private void OnCardClick(Card card, LocKind kind, int pileIndex, int cardIndex)
    {
        var loc = new Loc { Kind = kind, Index = pileIndex };

        // If something selected, this click is a drop attempt on this pile
        if (_selLoc != null)
        {
            // Clicking the same pile/card deselects or re-selects deeper
            if (loc.Kind == _selLoc.Kind && loc.Index == _selLoc.Index)
            {
                _selLoc = null;
                Render();
                return;
            }
            var cards = CardsAt(_selLoc, _selStart);
            TryMove(cards, loc);
            return;
        }

        // Auto-move to home on double click is handled via tag; here single click selects
        List<Card> movable;
        int startIdx;
        switch (kind)
        {
            case LocKind.Cascade:
                movable = _cascades[pileIndex].Skip(cardIndex).ToList();
                startIdx = cardIndex;
                if (!IsValidStack(movable)) { movable = new List<Card> { _cascades[pileIndex][^1] }; startIdx = _cascades[pileIndex].Count - 1; }
                break;
            case LocKind.Free:
                movable = new List<Card> { _free[pileIndex] };
                startIdx = 0;
                break;
            default:
                movable = new List<Card> { _homes[pileIndex][^1] };
                startIdx = 0;
                break;
        }
        _selLoc = loc;
        _selStart = startIdx;
        Render();
    }

    private void OnCardDoubleClick(Card card)
    {
        // Try to send the topmost movable unit containing this card to a home cell
        var loc = FindLoc(card);
        if (loc == null) return;
        List<Card> unit;
        int startIdx = 0;
        if (loc.Kind == LocKind.Cascade)
        {
            int idx = _cascades[loc.Index].IndexOf(card);
            unit = _cascades[loc.Index].Skip(idx).ToList();
            startIdx = idx;
            if (unit.Count != 1) return; // only single cards auto-home
        }
        else unit = new List<Card> { card };

        for (int h = 0; h < HomeCount; h++)
            if (CanDropOnHome(card, h))
            {
                Snapshot();
                if (loc.Kind == LocKind.Cascade) _cascades[loc.Index].RemoveRange(startIdx, 1);
                else if (loc.Kind == LocKind.Free) _free.Remove(card);
                else _homes[loc.Index].Remove(card);
                _homes[h].Add(card);
                _moves++;
                CheckWin();
                Render();
                return;
            }
    }

    // ---------------- rendering ----------------

    private void Render()
    {
        Board.Children.Clear();
        ScoreText.Text = $"Score: {_homes.Sum(h => h.Count) * 10 - _moves}  |  Moves: {_moves}";

        double topY = 12;
        // Top row: 4 freecells then 4 homecells
        for (int i = 0; i < FreeCount; i++)
        {
            double x = 12 + i * (CardW + GapX);
            var slot = Slot(x, topY);
            if (_free.Count > i)
            {
                var img = CardImage($"cards/card_{_free[i].Id}.png", x, topY);
                WireCard(img, _free[i], LocKind.Free, i, 0);
            }
        }
        double homeStartX = 12 + (FreeCount + 1) * (CardW + GapX);
        for (int i = 0; i < HomeCount; i++)
        {
            double x = homeStartX + i * (CardW + GapX);
            var slot = Slot(x, topY);
            // Suit indicator on empty home
            if (_homes[i].Count == 0)
            {
                slot.Child = new TextBlock
                {
                    Text = Suits[i][0].ToString().ToUpper(), FontSize = 20, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                };
            }
            if (_homes[i].Count > 0)
            {
                var img = CardImage($"cards/card_{_homes[i][^1].Id}.png", x, topY);
                WireCard(img, _homes[i][^1], LocKind.Home, i, 0);
            }
        }

        // 8 cascades
        double colX0 = 12;
        for (int col = 0; col < ColCount; col++)
        {
            double x = colX0 + col * (CardW + GapX);
            var pile = _cascades[col];
            // empty slot marker
            var slot = Slot(x, 12 + CardH + 24);
            for (int i = 0; i < pile.Count; i++)
            {
                double y = 12 + CardH + 24 + i * FanV;
                var img = CardImage($"cards/card_{pile[i].Id}.png", x, y);
                WireCard(img, pile[i], LocKind.Cascade, col, i);
                // highlight selected stack
                if (_selLoc != null && _selLoc.Kind == LocKind.Cascade && _selLoc.Index == col && i >= _selStart)
                    img.Opacity = 0.55;
            }
        }
    }

    private Border Slot(double x, double y) => new Border()
    {
        Width = CardW, Height = CardH,
        BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
        Background = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
        CornerRadius = new CornerRadius(4)
    }.Also(b => { Canvas.SetLeft(b, x); Canvas.SetTop(b, y); Board.Children.Add(b); });

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
    private void Exit_Click(object s, RoutedEventArgs e) => RequestClose?.Invoke();
    private void About_Click(object s, RoutedEventArgs e) =>
        DoryNative.XpSim.SimMessageBox.Show("FreeCell for Windows XP\n\nBuild alternating-color sequences down in the columns. Fill the home cells A through K by suit.",
            "About FreeCell", MessageBoxButton.OK, MessageBoxImage.Information);
}

internal static class FreeCellExtensions
{
    public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
}
