// ColorSchemeManager.cs - WLM-style color schemes.
//
// Each scheme is a hue rotation (in degrees) applied to:
//   - the header/banner image (a pre-generated background_<scheme>.jpg), swapped
//     into the application resource "WlmBannerImage" which the windows bind to
//     via {DynamicResource WlmBannerImage}; and
//   - the primary accent brushes, recolored live by rotating their hue the same
//     amount so the chrome stays consistent with the banner.
//
// The active scheme is persisted via AppSettings.ColorScheme and re-applied at
// startup.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WLMRevivalV2Desktop
{
    public class ColorScheme
    {
        public string Name;        // key, also the background_<name>.jpg suffix
        public string Display;     // human label
        public double HueDegrees;  // rotation applied to banner + accent brushes
        public Color Swatch;       // representative color for the picker
    }

    public static class ColorSchemeManager
    {
        // Base accent colors (the default "blue" palette, mirrors App.xaml).
        // NOTE: windows must reference these via {DynamicResource ...}; Apply()
        // replaces the brush instances, which StaticResource users never see.
        private static readonly Dictionary<string, Color> BaseBrushColors = new Dictionary<string, Color>
        {
            { "WlmPrimaryBrush",       (Color)ColorConverter.ConvertFromString("#FF2C6FB5") },
            { "WlmPrimaryDarkBrush",   (Color)ColorConverter.ConvertFromString("#FF1C4E84") },
            { "WlmAccentBrush",        (Color)ColorConverter.ConvertFromString("#FF2C82C9") },
            { "WlmBorderBrush",        (Color)ColorConverter.ConvertFromString("#FFA9C9E6") },
            { "WlmGroupHeaderBrush",   (Color)ColorConverter.ConvertFromString("#FFDCE7F2") },
        };

        // Gradient chrome brushes (toolbars / glass) - stop colors of the base
        // blue palette; rebuilt with rotated hues on every Apply().
        private static readonly Dictionary<string, string[]> BaseGradients = new Dictionary<string, string[]>
        {
            // key -> stop colors at offsets 0 / 0.5 / 0.51 / 1 (4 stops) or 0 / 1 (2 stops)
            { "WlmAeroBrush",     new[] { "#FFE6F1FB", "#FFCFE3F5", null,        "#FFBBD6EE" } },
            { "WlmGlassBrush",    new[] { "#FFEAF3FB", "#FFCFE3F6", "#FFB9D6EF", "#FFCBE0F4" } },
            { "WlmToolbarBrush",  new[] { "#FFFBFDFE", "#FFF0F6FB", null,        "#FFE4EEF7" } },
            { "WlmBodyTintBrush", new[] { "#FFF6FAFE", null,        null,        "#FFE7F1FA" } },
            { "WlmBandBrush",     new[] { "#FFDCE9F6", null,        null,        "#FFCFE2F3" } },
        };
        private static readonly double[] GradientOffsets = { 0, 0.5, 0.51, 1 };

        public static readonly List<ColorScheme> Schemes = new List<ColorScheme>
        {
            new ColorScheme { Name = "blue",   Display = "Blue",   HueDegrees = 0,   Swatch = Rgb(0x2C,0x6F,0xB5) },
            new ColorScheme { Name = "teal",   Display = "Teal",   HueDegrees = -35, Swatch = Rgb(0x1F,0x9F,0xA6) },
            new ColorScheme { Name = "green",  Display = "Green",  HueDegrees = -90, Swatch = Rgb(0x4F,0xA6,0x4F) },
            new ColorScheme { Name = "purple", Display = "Purple", HueDegrees =  55, Swatch = Rgb(0x7E,0x57,0xC2) },
            new ColorScheme { Name = "pink",   Display = "Pink",   HueDegrees =  95, Swatch = Rgb(0xC4,0x4F,0x9B) },
            new ColorScheme { Name = "red",    Display = "Red",    HueDegrees = 140, Swatch = Rgb(0xC2,0x4A,0x3B) },
            new ColorScheme { Name = "orange", Display = "Orange", HueDegrees = 165, Swatch = Rgb(0xD9,0x84,0x2B) },
        };

        public static string Current { get; private set; } = "blue";

        public static ColorScheme Get(string name)
        {
            foreach (var s in Schemes)
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return Schemes[0];
        }

        // Apply at startup (reads the saved scheme).
        public static void ApplySaved()
        {
            Apply(AppSettings.ColorScheme, persist: false);
        }

        public static void Apply(string name, bool persist = true)
        {
            var scheme = Get(name);
            Current = scheme.Name;
            var res = Application.Current?.Resources;
            if (res == null) return;

            ApplyTo(res, scheme.Name);

            if (persist) AppSettings.ColorScheme = scheme.Name;
        }

        /// <summary>
        /// Apply a colour scheme's banner image + chrome brushes to an arbitrary
        /// resource dictionary. Used to theme the whole app (Application.Resources)
        /// or a single window (Window.Resources) - the latter lets a chat window
        /// adopt the *contact's* colour, overriding the local user's scene, since
        /// window-level resources shadow application-level ones for DynamicResource
        /// lookups. Does not touch Current or persisted settings.
        /// </summary>
        public static void ApplyTo(ResourceDictionary res, string name)
        {
            if (res == null) return;
            var scheme = Get(name);

            // 1) swap the banner image
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/wlm/background_" + scheme.Name + ".jpg", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = uri;
                bmp.EndInit();
                bmp.Freeze();
                res["WlmBannerImage"] = bmp;
            }
            catch { /* missing variant - keep current banner */ }

            // 2) recolor the accent brushes. The brushes are REPLACED (not
            //    mutated): replacement is what makes {DynamicResource} bindings
            //    re-resolve, and frozen brushes can't be mutated anyway.
            foreach (var kv in BaseBrushColors)
            {
                var brush = new SolidColorBrush(RotateHue(kv.Value, scheme.HueDegrees));
                brush.Freeze();
                res[kv.Key] = brush;
            }

            // 3) rebuild the gradient chrome brushes (toolbars, glass caption)
            //    with the same hue rotation so the whole window follows the scheme.
            foreach (var kv in BaseGradients)
            {
                var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                for (int i = 0; i < kv.Value.Length; i++)
                {
                    if (kv.Value[i] == null) continue;
                    var c = (Color)ColorConverter.ConvertFromString(kv.Value[i]);
                    grad.GradientStops.Add(new GradientStop(RotateHue(c, scheme.HueDegrees), GradientOffsets[i]));
                }
                grad.Freeze();
                res[kv.Key] = grad;
            }
        }

        private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

        // Rotate a color's hue by the given degrees (HSV), preserving S and V.
        public static Color RotateHue(Color c, double degrees)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double v = max;
            double d = max - min;
            double s = max <= 0 ? 0 : d / max;
            double h = 0;
            if (d > 0)
            {
                if (max == r) h = ((g - b) / d) % 6;
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
                if (h < 0) h += 360;
            }
            h = (h + degrees) % 360;
            if (h < 0) h += 360;

            double c2 = v * s;
            double x = c2 * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c2;
            double rr = 0, gg = 0, bb = 0;
            if (h < 60) { rr = c2; gg = x; }
            else if (h < 120) { rr = x; gg = c2; }
            else if (h < 180) { gg = c2; bb = x; }
            else if (h < 240) { gg = x; bb = c2; }
            else if (h < 300) { rr = x; bb = c2; }
            else { rr = c2; bb = x; }

            return Color.FromArgb(c.A,
                (byte)Math.Round((rr + m) * 255),
                (byte)Math.Round((gg + m) * 255),
                (byte)Math.Round((bb + m) * 255));
        }
    }
}
