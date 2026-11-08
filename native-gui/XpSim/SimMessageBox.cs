using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DoryNative.Views;

namespace DoryNative.XpSim;

/// <summary>
/// In-sim replacement for System.Windows.MessageBox. Renders an xp-chromed
/// dialog inside the simulator and blocks via a nested dispatcher frame,
/// so call sites stay synchronous like MessageBox.Show.
/// </summary>
public static class SimMessageBox
{
    public static MessageBoxResult Show(string messageBoxText)
        => Show(messageBoxText, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption)
        => Show(messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => Show(messageBoxText, caption, button, MessageBoxImage.None);

    public static MessageBoxResult Show(string messageBoxText, string caption,
        MessageBoxButton button, MessageBoxImage icon)
        => Show(null, messageBoxText, caption, button, icon);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption)
        => Show(owner, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None);

    public static MessageBoxResult Show(Window? owner, string messageBoxText, string caption,
        MessageBoxButton button, MessageBoxImage icon)
    {
        var result = MessageBoxResult.None;
        var done = new ManualResetEventSlim();

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(new TextBlock
        {
            Text = messageBoxText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            Margin = new Thickness(0, 0, 0, 14)
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        void AddBtn(string label, MessageBoxResult r, bool isDefault = false, bool isCancel = false)
        {
            var b = new Button
            {
                Content = label, MinWidth = 74, Height = 24,
                Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2),
                IsDefault = isDefault, IsCancel = isCancel
            };
            b.Click += (_, _) => { result = r; done.Set(); };
            buttons.Children.Add(b);
        }

        switch (button)
        {
            case MessageBoxButton.OK:
                AddBtn("OK", MessageBoxResult.OK, isDefault: true, isCancel: true);
                break;
            case MessageBoxButton.OKCancel:
                AddBtn("OK", MessageBoxResult.OK, isDefault: true);
                AddBtn("Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;
            case MessageBoxButton.YesNo:
                AddBtn("Yes", MessageBoxResult.Yes, isDefault: true);
                AddBtn("No", MessageBoxResult.No, isCancel: true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddBtn("Yes", MessageBoxResult.Yes, isDefault: true);
                AddBtn("No", MessageBoxResult.No);
                AddBtn("Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;
        }
        root.Children.Add(buttons);

        var shell = StudioShell.Current;
        if (shell == null)
        {
            // No sim host — fall back to a real message box
            return System.Windows.MessageBox.Show(owner, messageBoxText, caption, button, icon);
        }

        root.Measure(new Size(420, double.PositiveInfinity));
        var width = Math.Max(280, Math.Min(480, root.DesiredSize.Width + 30));
        var xp = XpDialog.ShowElement(string.IsNullOrEmpty(caption) ? "Message" : caption,
            root, width, 150);
        if (xp == null)
            return System.Windows.MessageBox.Show(owner, messageBoxText, caption, button, icon);

        // Nested frame keeps the call synchronous like MessageBox.Show
        var frame = new DispatcherFrame();
        // Poll-free: stop the frame when the dialog closes or a button fires
        var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        poll.Tick += (_, _) =>
        {
            if (done.IsSet || xp.Visibility != Visibility.Visible || !shell.Dialogs.Children.Contains(xp))
            {
                poll.Stop();
                frame.Continue = false;
            }
        };
        xp.WindowClosed += (_, _) => { done.Set(); frame.Continue = false; };
        poll.Start();
        Dispatcher.PushFrame(frame);

        return result == MessageBoxResult.None ? MessageBoxResult.Cancel : result;
    }
}
