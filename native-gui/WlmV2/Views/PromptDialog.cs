// PromptDialog.cs - a tiny code-only modal text prompt (WLM-styled), used for
// quick single-field input such as naming a new group. Avoids pulling in
// Microsoft.VisualBasic.Interaction just for an InputBox.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DoryNative.XpSim;

namespace WLMRevivalV2Desktop.Views
{
    public static class PromptDialog
    {
        public static string Show(Window owner, string title, string prompt, string defaultText = "")
        {
            var win = new Window
            {
                Title = title,
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(0xEA, 0xF1, 0xF8))
            };

            var root = new StackPanel { Margin = new Thickness(14) };

            root.Children.Add(new TextBlock
            {
                Text = prompt,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
            });

            var input = new TextBox
            {
                Text = defaultText ?? string.Empty,
                FontSize = 13,
                Padding = new Thickness(4, 3, 4, 3),
                Height = 26
            };
            root.Children.Add(input);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ok = new Button { Content = "OK", Width = 80, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, Height = 26, IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            win.Content = root;

            string result = null;
            ok.Click += (s, e) => { result = input.Text?.Trim(); win.DialogResult = true; };

            input.Loaded += (s, e) => { input.Focus(); input.SelectAll(); };

            var dr = win.ShowDialogSim(owner);
            if (dr == true && !string.IsNullOrEmpty(result))
                return result;
            return null;
        }
    }
}
