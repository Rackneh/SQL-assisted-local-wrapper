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
    public partial class DisplayPictureDialog : Window
    {
        public string SelectedPicturePath { get; private set; }

        public DisplayPictureDialog()
        {
            InitializeComponent();
            LoadDefaultPictures();
            LoadFeaturedPictures();
        }

        private void LoadDefaultPictures()
        {
            // Add default user tiles from embedded assets
            string[] defaultPictures = new string[]
            {
                "usertile_default.png",
                "wlm_people.png",
                "wlm_logo.png"
            };

            foreach (var pic in defaultPictures)
            {
                var border = new Border
                {
                    Width = 60,
                    Height = 60,
                    Margin = new Thickness(2),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Colors.White),
                    Cursor = Cursors.Hand
                };

                var img = new Image
                {
                    Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/wlm/{pic}")),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(2)
                };

                border.Child = img;
                border.MouseLeftButtonDown += (s, e) => SelectPicture($"pack://application:,,,/Assets/wlm/{pic}");
                PicturesPanel.Children.Add(border);
            }

            // Load additional usertiles from APK assets
            string usertilesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "wlm", "usertiles");

            if (Directory.Exists(usertilesPath))
            {
                var usertileFiles = Directory.GetFiles(usertilesPath, "usertile_*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".gif"))
                    .ToArray();

                foreach (var file in usertileFiles)
                {
                    try
                    {
                        var img = new Image
                        {
                            Source = new BitmapImage(new Uri(file)),
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(2)
                        };

                        var border = new Border
                        {
                            Width = 60,
                            Height = 60,
                            Margin = new Thickness(2),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                            BorderThickness = new Thickness(1),
                            Background = new SolidColorBrush(Colors.White),
                            Cursor = Cursors.Hand,
                            Child = img
                        };

                        string filePath = file;
                        border.MouseLeftButtonDown += (s, e) => SelectPicture(filePath);
                        PicturesPanel.Children.Add(border);
                    }
                    catch { /* Skip invalid images */ }
                }
            }
        }

        private void LoadFeaturedPictures()
        {
            // Load featured pictures from APK assets
            string apkDrawablePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "wlm", "usertiles");

            if (Directory.Exists(apkDrawablePath))
            {
                var featuredFiles = Directory.GetFiles(apkDrawablePath, "usertile_*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".gif"))
                    .Take(8)
                    .ToArray();

                foreach (var file in featuredFiles)
                {
                    try
                    {
                        var img = new Image
                        {
                            Source = new BitmapImage(new Uri(file)),
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(2)
                        };

                        var border = new Border
                        {
                            Width = 44,
                            Height = 44,
                            Margin = new Thickness(2),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                            BorderThickness = new Thickness(1),
                            Background = new SolidColorBrush(Colors.White),
                            Cursor = Cursors.Hand,
                            Child = img
                        };

                        string filePath = file;
                        border.MouseLeftButtonDown += (s, e) => SelectPicture(filePath);
                        FeaturedPicturesPanel.Children.Add(border);
                    }
                    catch { /* Skip invalid images */ }
                }
            }
        }

        private void SelectPicture(string picturePath)
        {
            SelectedPicturePath = picturePath;
            PreviewImage.Source = new BitmapImage(new Uri(picturePath));
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a display picture",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                SelectPicture(dlg.FileName);
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            SelectedPicturePath = "pack://application:,,,/Assets/wlm/usertile_default.png";
            PreviewImage.Source = new BitmapImage(new Uri(SelectedPicturePath));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
