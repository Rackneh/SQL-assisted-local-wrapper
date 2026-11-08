using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WLMRevivalV2Desktop.Views.Controls
{
    /// <summary>
    /// Wraps a contact's content row (avatar + name) with that contact's banner
    /// and colour-scheme behind it, WLM 2009 style:
    ///   - the banner image sits behind the whole row and fades to transparent
    ///     towards the right edge (the blue line in the design marks the fade);
    ///   - a faint tint of the contact's colour scheme fills behind the row so
    ///     contacts who set only a colour (no banner) still read as "themed".
    /// Used by both Live skins (MainWindowLive and MainWindowLiveNoBanner) so the
    /// rendering stays identical and lives in one place.
    /// </summary>
    public static class ContactRowDecorator
    {
        // Angled fade mask: opaque on the left, transparent on the right, with the
        // fade boundary slanted (the gradient vector points right-and-down, so the
        // iso-opacity edge is angled - matching the slanted edge in the design).
        // 0.0 .. 0.60 stays fully opaque, then fades out by the right edge.
        private static LinearGradientBrush MakeFadeMask(byte leftAlpha)
        {
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(leftAlpha, 0xFF, 0xFF, 0xFF), 0.0),
                    new GradientStop(Color.FromArgb(leftAlpha, 0xFF, 0xFF, 0xFF), 0.60),
                    new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0)
                }
            };
        }

        /// <summary>
        /// Returns the element to use as a ListBoxItem's content: either the bare
        /// content row, or the row layered over the contact's banner / colour.
        /// </summary>
        public static FrameworkElement Decorate(FrameworkElement contentRow, string email)
        {
            var style = ContactStyleManager.GetStyle(email);
            if (style == null)
                return contentRow;

            // Use only the already-cached banner image so we never block the UI
            // thread on a download. If it isn't cached yet, kick off a background
            // load that raises StyleChanged to re-render this row when ready.
            ImageSource banner = null;
            if (style.HasBanner)
            {
                banner = ContactStyleManager.GetCachedBannerImage(style.BannerUrl);
                if (banner == null)
                    ContactStyleManager.EnsureBannerImageAsync(style.BannerUrl, email, style);
            }

            // Resolve the contact's colour-scheme swatch (e.g. "blue" -> #2C6FB5).
            bool hasColour = !string.IsNullOrEmpty(style.ColorScheme);
            if (banner == null && !hasColour)
                return contentRow;

            var grid = new Grid { ClipToBounds = true };

            // 1) Colour-scheme tint (faded right). Drawn first so the banner, when
            //    present, sits on top of it.
            if (hasColour)
            {
                var swatch = ColorSchemeManager.Get(style.ColorScheme).Swatch;
                // Keep it subtle - this is a background wash, not a solid block.
                var tint = Color.FromArgb(0x44, swatch.R, swatch.G, swatch.B);
                var tintBorder = new Border
                {
                    Background = new SolidColorBrush(tint),
                    OpacityMask = MakeFadeMask(0xFF),
                    IsHitTestVisible = false
                };
                grid.Children.Add(tintBorder);
            }

            // 2) Banner image behind the row, fading to transparent on the right.
            if (banner != null)
            {
                var bannerBorder = new Border
                {
                    Background = new ImageBrush(banner)
                    {
                        Stretch = Stretch.UniformToFill,
                        AlignmentY = AlignmentY.Center
                    },
                    OpacityMask = MakeFadeMask(0xFF),
                    IsHitTestVisible = false
                };
                grid.Children.Add(bannerBorder);
            }

            // 3) The actual contact content on top.
            grid.Children.Add(contentRow);
            return grid;
        }
    }
}
