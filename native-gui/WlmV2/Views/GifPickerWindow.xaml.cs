using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WLMRevivalV2Desktop.Services;
using WLMRevivalV2Desktop.Views.Controls;

namespace WLMRevivalV2Desktop.Views
{
    public partial class GifPickerWindow : Window
    {
        public GifResult SelectedGif { get; private set; }

        private bool _showingFavorites;
        private int _requestSeq;   // guards against out-of-order async results

        public GifPickerWindow(Window owner)
        {
            InitializeComponent();

            if (owner != null)
            {
                Owner = owner;
                try
                {
                    var p = owner.PointToScreen(new Point(0, 0));
                    Left = p.X + 20;
                    Top = Math.Max(0, p.Y + owner.ActualHeight - Height - 70);
                }
                catch { WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            }

            SearchBox.TextChanged += (s, e) =>
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            Loaded += (s, e) => { LoadTrending(); SearchBox.Focus(); };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { ClosePicker(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is Border)
                try { DragMove(); } catch { }
        }

        // ===== tabs =====
        private void TrendingTab_Click(object sender, RoutedEventArgs e)
        {
            _showingFavorites = false;
            TrendingTab.IsChecked = true;
            FavoritesTab.IsChecked = false;
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) LoadTrending();
            else DoSearch(SearchBox.Text);
        }

        private void FavoritesTab_Click(object sender, RoutedEventArgs e)
        {
            _showingFavorites = true;
            TrendingTab.IsChecked = false;
            FavoritesTab.IsChecked = true;
            LoadFavorites();
        }

        // ===== search =====
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                DoSearch(SearchBox.Text);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => DoSearch(SearchBox.Text);

        private void LoadTrending() => DoSearch(string.Empty);

        private void LoadFavorites()
        {
            ResultsPanel.Children.Clear();
            var favs = GifFavorites.All();
            if (favs.Count == 0)
            {
                ShowStatus("No favourites yet.\nTap the star on any GIF to save it here.");
                return;
            }
            HideStatus();
            foreach (var g in favs)
                ResultsPanel.Children.Add(BuildThumbnail(g));
        }

        private void DoSearch(string query)
        {
            _showingFavorites = false;
            TrendingTab.IsChecked = string.IsNullOrWhiteSpace(query);
            FavoritesTab.IsChecked = false;

            ResultsPanel.Children.Clear();
            ShowStatus(string.IsNullOrWhiteSpace(query) ? "Loading trending GIFs..." : "Searching...");

            int seq = ++_requestSeq;
            Task.Run(() =>
            {
                string next; bool noKey;
                var results = GifService.Search(query, null, out next, out noKey);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (seq != _requestSeq) return; // superseded
                    if (noKey)
                    {
                        ShowStatus("GIF search isn't set up yet.\nAdd a Tenor API key on the server " +
                                   "(TENOR_API_KEY or server/tenor_key.txt) to enable it.");
                        return;
                    }
                    if (results.Count == 0)
                    {
                        ShowStatus("No GIFs found.");
                        return;
                    }
                    HideStatus();
                    ResultsPanel.Children.Clear();
                    foreach (var g in results)
                        ResultsPanel.Children.Add(BuildThumbnail(g));
                }));
            });
        }

        // ===== thumbnail =====
        private FrameworkElement BuildThumbnail(GifResult gif)
        {
            var host = new Grid
            {
                Width = 108,
                Height = 100,
                Margin = new Thickness(3)
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF4, 0xF8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xE1, 0xEE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                ToolTip = string.IsNullOrEmpty(gif.Desc) ? "Send GIF" : gif.Desc
            };

            var img = new AnimatedGifImage { Stretch = Stretch.UniformToFill };
            border.Child = img;
            img.BeginLoad(string.IsNullOrEmpty(gif.Preview) ? gif.Gif : gif.Preview);

            border.MouseLeftButtonUp += (s, e) => Select(gif);
            host.Children.Add(border);

            // Favourite star (top-right)
            var star = MakeStarButton(gif);
            host.Children.Add(star);

            return host;
        }

        private ToggleButton MakeStarButton(GifResult gif)
        {
            var star = new ToggleButton
            {
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 3, 0),
                Cursor = Cursors.Hand,
                IsChecked = GifFavorites.IsFavorite(gif.Gif),
                ToolTip = "Add to favourites",
                Focusable = false
            };
            star.Template = StarTemplate();
            star.Click += (s, e) =>
            {
                bool now = GifFavorites.Toggle(gif);
                star.IsChecked = now;
                if (_showingFavorites && !now) LoadFavorites(); // drop it from the favourites view
                e.Handled = true;
            };
            return star;
        }

        // A small circular star button: hollow star normally, gold filled when checked.
        private ControlTemplate StarTemplate()
        {
            var t = new ControlTemplate(typeof(ToggleButton));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
            border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0xB0, 0x00, 0x00, 0x00)));

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "★"); // ★
            text.SetValue(TextBlock.FontSizeProperty, 13.0);
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xCF, 0xD8, 0xE2)));
            text.Name = "Glyph";
            border.AppendChild(text);
            t.VisualTree = border;

            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new SolidColorBrush(Color.FromRgb(0xFF, 0xC4, 0x3A)), "Glyph"));
            t.Triggers.Add(on);

            return t;
        }

        private void Select(GifResult gif)
        {
            SelectedGif = gif;
            try { DialogResult = true; } catch { }
            Close();
        }

        // ===== status helpers =====
        private void ShowStatus(string text)
        {
            StatusText.Text = text;
            StatusText.Visibility = Visibility.Visible;
            ResultsPanel.Children.Clear();
        }

        private void HideStatus() => StatusText.Visibility = Visibility.Collapsed;

        private void Close_Click(object sender, MouseButtonEventArgs e) => ClosePicker();

        private void ClosePicker()
        {
            try { if (DialogResult == null) DialogResult = false; } catch { }
            Close();
        }
    }
}
