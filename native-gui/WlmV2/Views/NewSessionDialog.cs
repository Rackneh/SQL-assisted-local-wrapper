// NewSessionDialog.cs - "New IBA Session" modal matching the port-5000 GUI's
// newSessionModal: session name + optional persona/task goal.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WLMRevivalV2Desktop.Views
{
    public class NewSessionDialog : Window
    {
        public string SessionName { get; private set; } = "";
        public string Description { get; private set; } = "";

        public NewSessionDialog(Window owner)
        {
            Title = "New Session";
            Width = 360;
            Height = 220;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = owner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xE9, 0xD8));
            FontFamily = new FontFamily("Tahoma");

            var root = new StackPanel { Margin = new Thickness(14) };

            root.Children.Add(MakeLabel("Session Name"));
            var nameBox = new TextBox { Height = 24, Margin = new Thickness(0, 2, 0, 10) };
            root.Children.Add(nameBox);

            root.Children.Add(MakeLabel("Persona / Task Goal (Optional)"));
            var descBox = new TextBox { Height = 24, Margin = new Thickness(0, 2, 0, 12) };
            root.Children.Add(descBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var ok = new Button { Content = "Create", Width = 80, Height = 26, IsDefault = true, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "Cancel", Width = 80, Height = 26, IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            Content = root;

            ok.Click += (s, e) =>
            {
                SessionName = nameBox.Text?.Trim() ?? "";
                Description = descBox.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(SessionName))
                {
                    DoryNative.XpSim.SimMessageBox.Show(this, "Please enter a session name.", "New Session",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                DialogResult = true;
            };

            nameBox.Loaded += (s, e) => nameBox.Focus();
        }

        private static TextBlock MakeLabel(string text) => new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40))
        };
    }
}
