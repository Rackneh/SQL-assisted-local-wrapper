using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WLMRevivalV2Desktop.Views
{
    public partial class WinksPicker : Window
    {
        public string SelectedWink { get; private set; }
        private Window _ownerWindow;

        public WinksPicker(Window owner)
        {
            _ownerWindow = owner;
            InitializeComponent();
            LoadWinks();
            
            // Position relative to owner
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.Manual;
                
                // Calculate position: bottom-left of chat window, above the toolbar
                var screenPos = owner.PointToScreen(new System.Windows.Point(0, 0));
                Left = screenPos.X + 10;
                Top = screenPos.Y + owner.ActualHeight - Height - 80;
            }
            
            // Must be shown as dialog for DialogResult to work
            // The caller should use ShowDialog() not Show()
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Deactivated += OnWindowDeactivated;
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            // Only close if no wink was selected (DialogResult not set)
            if (DialogResult == null)
            {
                if (IsModal())
                {
                    DialogResult = false;
                }
                else
                {
                    Close();
                }
            }
        }

        private void LoadWinks()
        {
            string winksPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "wlm", "winks");

            if (!Directory.Exists(winksPath))
            {
                LoadPlaceholderWinks();
                return;
            }

            var winkFiles = Directory.GetFiles(winksPath, "*.mct")
                .OrderBy(f => f)
                .ToArray();

            if (winkFiles.Length == 0)
            {
                LoadPlaceholderWinks();
                return;
            }

            foreach (var file in winkFiles)
            {
                try
                {
                    string baseName = Path.GetFileNameWithoutExtension(file);
                    string thumbPath = FindThumbnail(winksPath, baseName);

                    FrameworkElement child;
                    if (thumbPath != null)
                    {
                        child = new Image
                        {
                            Source = new BitmapImage(new Uri(thumbPath)),
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(4)
                        };
                    }
                    else
                    {
                        child = new TextBlock
                        {
                            Text = baseName.Replace("_", " "),
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        };
                    }

                    var border = new Border
                    {
                        Style = (Style)FindResource("WinkItem"),
                        Child = child,
                        ToolTip = baseName.Replace("_", " ")
                    };

                    border.MouseLeftButtonDown += (s, e) => SelectWink(baseName);
                    FeaturedWinksPanel.Children.Add(border);
                }
                catch { /* Skip invalid winks */ }
            }
        }

        private static string FindThumbnail(string winksPath, string baseName)
        {
            foreach (var ext in new[] { ".png", ".jpg" })
            {
                string path = Path.Combine(winksPath, baseName + ext);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private void LoadPlaceholderWinks()
        {
            string[] winks = new string[] { "Wink 1", "Wink 2", "Wink 3", "Wink 4", "Wink 5" };
            
            foreach (var wink in winks)
            {
                var border = new Border
                {
                    Style = (Style)FindResource("WinkItem"),
                    Child = new TextBlock
                    {
                        Text = "[W]",
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                border.MouseLeftButtonDown += (s, e) => SelectWink(wink);
                FeaturedWinksPanel.Children.Add(border);
            }
        }

        private void SelectWink(string wink)
        {
            SelectedWink = wink;
            // Only set DialogResult if shown as dialog
            if (IsModal())
            {
                DialogResult = true;
            }
            Close();
        }

        private bool IsModal()
        {
            // Check if window was shown as dialog by reflection
            var field = typeof(Window).GetField("_showingAsDialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null && (bool)field.GetValue(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (IsModal())
            {
                DialogResult = false;
            }
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
