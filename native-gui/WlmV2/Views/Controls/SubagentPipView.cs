using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WLMRevivalV2Desktop.Views.Controls
{
    /// <summary>
    /// Themeable visual targets of an "Advanced" subagent picture-in-picture panel.
    /// The host chat window keeps one of these per open subagent so it can re-apply
    /// the subagent's server-side customisation (colour scheme + banner) and the
    /// display pictures as they arrive asynchronously from the server.
    /// </summary>
    public class SubagentPipTheme
    {
        public string Email;
        public ImageBrush ColourBrush;   // header band base layer (colour-scheme background)
        public Border BannerBorder;      // custom banner, layered ABOVE the colour
        public ImageBrush BannerBrush;
        public Image SubagentDpImage;    // subagent avatar (top-left)
        public Image CallerDpImage;      // caller avatar (bottom-left, the delegating bot/person)
    }

    /// <summary>
    /// Builds the "Advanced" subagent PiP: a compact Windows Live Messenger chat
    /// window whose banner and colour come from the subagent's own server
    /// customisation. The colour scheme paints the header band; the custom banner
    /// (if any) layers on top and fades into the colour towards the right, exactly
    /// like the full chat window.
    /// </summary>
    public static class SubagentPipView
    {
        private static SolidColorBrush B(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));

        public static Border BuildAdvanced(string displayRole, string task,
                                           out StackPanel inner, out SubagentPipTheme theme)
        {
            theme = new SubagentPipTheme();

            var outer = new Border
            {
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = B(0x8F, 0xA8, 0xBE),
                Background = Brushes.White,
                Margin = new Thickness(24, 2, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 460,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromArgb(0x66, 0, 0, 0),
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };

            // Clip the contents to the rounded frame so the banner doesn't bleed.
            var clip = new Border
            {
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Background = Brushes.White
            };
            outer.Child = clip;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header band
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // body
            clip.Child = grid;

            // ----- Header band -----
            var headerBand = new Grid { Height = 60 };

            // (1) Colour layer (base): the subagent's colour-scheme background.
            theme.ColourBrush = new ImageBrush { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
            headerBand.Children.Add(new Border
            {
                Background = theme.ColourBrush,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            });

            // (2) Banner overlay (above colour): the subagent's custom banner, fading
            //     out to the right so the colour shows through past the banner edge.
            theme.BannerBrush = new ImageBrush { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Top };
            theme.BannerBorder = new Border
            {
                Background = theme.BannerBrush,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                OpacityMask = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0.0),
                        new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0.5),
                        new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0)
                    },
                    new Point(0, 0), new Point(1, 0.5))
            };
            headerBand.Children.Add(theme.BannerBorder);

            // (3) Glass sheen overlay, if the app theme defines one.
            if (Application.Current?.TryFindResource("WlmGlassSheenBrush") is Brush sheen)
                headerBand.Children.Add(new Rectangle { Fill = sheen, IsHitTestVisible = false });

            // (4) Header content: subagent DP + name/status.
            var headerContent = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var subDp = BuildDp(out var subImg, 48, SubagentTileUri(displayRole));
            theme.SubagentDpImage = subImg;
            Grid.SetColumn(subDp, 0);
            headerContent.Children.Add(subDp);

            var nameStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            nameStack.Children.Add(new TextBlock
            {
                Text = displayRole,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(0x3B, 0x3B, 0x3B)
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(task) ? "(Working…)" : task,
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = B(0x6E, 0x96, 0xBC),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 320,
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(nameStack, 1);
            headerContent.Children.Add(nameStack);

            headerBand.Children.Add(headerContent);
            Grid.SetRow(headerBand, 0);
            grid.Children.Add(headerBand);

            // ----- Body: caller DP gutter + scaled transcript mirror -----
            var body = new Grid { Background = Brushes.White };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var callerDp = BuildDp(out var callerImg, 40, DefaultTileUri());
            theme.CallerDpImage = callerImg;
            callerDp.VerticalAlignment = VerticalAlignment.Bottom;
            callerDp.HorizontalAlignment = HorizontalAlignment.Center;
            callerDp.Margin = new Thickness(6, 0, 4, 6);
            Grid.SetColumn(callerDp, 0);
            body.Children.Add(callerDp);

            var scroller = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Margin = new Thickness(2, 6, 6, 6)
            };
            inner = new StackPanel { LayoutTransform = new ScaleTransform(0.85, 0.85) };
            scroller.Content = inner;
            Grid.SetColumn(scroller, 1);
            body.Children.Add(scroller);

            Grid.SetRow(body, 1);
            grid.Children.Add(body);

            // Default colour until the subagent's real scheme is applied by the host.
            ApplyColourScheme(theme, "blue");
            return outer;
        }

        // Swap the header colour band to the pre-generated background_<scheme>.jpg.
        public static void ApplyColourScheme(SubagentPipTheme theme, string schemeName)
        {
            if (theme?.ColourBrush == null) return;
            var name = string.IsNullOrEmpty(schemeName) ? "blue" : schemeName;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri("pack://application:,,,/Assets/wlm/background_" + name + ".jpg", UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                theme.ColourBrush.ImageSource = bmp;
            }
            catch { /* unknown scheme variant - keep the current colour */ }
        }

        // Show (or clear) the subagent's custom banner above the colour band.
        public static void ApplyBanner(SubagentPipTheme theme, ImageSource banner)
        {
            if (theme?.BannerBorder == null) return;
            if (banner != null)
            {
                theme.BannerBrush.ImageSource = banner;
                theme.BannerBorder.Visibility = Visibility.Visible;
            }
            else
            {
                theme.BannerBorder.Visibility = Visibility.Collapsed;
            }
        }

        public static void SetSubagentDp(SubagentPipTheme theme, ImageSource dp)
        {
            if (dp != null && theme?.SubagentDpImage != null) theme.SubagentDpImage.Source = dp;
        }

        public static void SetCallerDp(SubagentPipTheme theme, ImageSource dp)
        {
            if (dp != null && theme?.CallerDpImage != null) theme.CallerDpImage.Source = dp;
        }

        // Builds a status-framed display-picture tile (online frame behind, avatar in
        // front) and hands back the inner Image so the host can swap the avatar later.
        private static Grid BuildDp(out Image tileImage, double size, Uri fallbackTileUri)
        {
            var grid = new Grid { Width = size, Height = size };

            var frame = new Image { Stretch = Stretch.Fill, Margin = new Thickness(-2, 0, -2, 0) };
            try { frame.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/wlm/status_frame_online_large.png")); }
            catch { /* frame is decorative; ignore if missing */ }
            Panel.SetZIndex(frame, 0);
            grid.Children.Add(frame);

            double tileSize = Math.Round(size * 0.76);
            var tile = new Border
            {
                Width = tileSize,
                Height = tileSize,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Clip = new RectangleGeometry(new Rect(0, 0, tileSize, tileSize), 2, 2)
            };
            tileImage = new Image { Stretch = Stretch.UniformToFill };
            try { tileImage.Source = new BitmapImage(fallbackTileUri); } catch { /* avatar optional */ }
            tile.Child = tileImage;
            Panel.SetZIndex(tile, 1);
            grid.Children.Add(tile);

            return grid;
        }

        private static Uri DefaultTileUri() =>
            new Uri("pack://application:,,,/Assets/wlm/usertiles/usertile_default.png");

        // Maps a subagent role to a distinct built-in display picture (used only as a
        // fallback until the subagent's real server display picture is available).
        public static Uri SubagentTileUri(string role)
        {
            string file;
            switch ((role ?? string.Empty).ToLowerInvariant())
            {
                case "coder": file = "usertile_robot.png"; break;
                case "researcher": file = "usertile_chess.png"; break;
                case "planner": file = "usertile_executive.png"; break;
                case "tester": file = "usertile_duck.png"; break;
                case "explorer": file = "usertile_surf.png"; break;
                default: file = "usertile_default.png"; break;
            }
            return new Uri("pack://application:,,,/Assets/wlm/usertiles/" + file);
        }
    }
}
