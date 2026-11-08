using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WLMRevivalV2Desktop.Views
{
    public partial class SceneDialog : Window
    {
        // Path (relative to Assets/wlm) of the chosen scene, e.g. "scenes/Robot.jpg".
        // null  = no scene chosen in this dialog session (keep current);
        // ""    = the "(Default)" tile - clear the scene and show the scheme banner.
        public string SelectedScene { get; private set; }
        public Color SelectedColor { get; private set; }
        public string SelectedScheme { get; private set; }

        private readonly List<Border> _sceneBorders = new List<Border>();
        private readonly List<Border> _schemeBorders = new List<Border>();

        public SceneDialog()
        {
            InitializeComponent();
            LoadScenes();
            LoadColorSchemes();
        }

        private void LoadScenes()
        {
            var searchDirs = new List<string>();

            string localDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "wlm", "scenes");
            if (Directory.Exists(localDir)) searchDirs.Add(localDir);

            string sharedDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "shared", "assets", "scenes");
            if (Directory.Exists(sharedDir)) searchDirs.Add(sharedDir);

            // "(Default)" tile - clears any scene so the color-scheme banner shows.
            // Without this, a chosen scene permanently covered the banner and made
            // color-scheme changes look like they did nothing.
            try
            {
                var def = new Border
                {
                    Style = (Style)FindResource("SceneItem"),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(
                            "pack://application:,,,/Assets/wlm/background.jpg", UriKind.Absolute)),
                        Stretch = Stretch.UniformToFill
                    },
                    ToolTip = "Default (no scene)"
                };
                def.MouseLeftButtonDown += (s, e) => SelectScene(string.Empty, def);
                _sceneBorders.Add(def);
                ScenesPanel.Children.Add(def);
            }
            catch { /* banner missing - skip the default tile */ }

            if (!searchDirs.Any()) return;

            var files = searchDirs
                .SelectMany(d => Directory.GetFiles(d))
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => Path.GetFileName(f))
                .Select(g => g.First())
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var border = new Border
                    {
                        Style = (Style)FindResource("SceneItem"),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                        Background = new ImageBrush
                        {
                            ImageSource = new BitmapImage(new Uri(file)),
                            Stretch = Stretch.UniformToFill
                        },
                        ToolTip = Path.GetFileNameWithoutExtension(file)
                    };
                    string rel = "scenes/" + Path.GetFileName(file);
                    border.MouseLeftButtonDown += (s, e) => SelectScene(rel, border);
                    _sceneBorders.Add(border);
                    ScenesPanel.Children.Add(border);
                }
                catch { /* skip unreadable image */ }
            }
        }

        private void LoadColorSchemes()
        {
            foreach (var scheme in ColorSchemeManager.Schemes)
            {
                var border = new Border
                {
                    Style = (Style)FindResource("ColorItem"),
                    Background = new SolidColorBrush(scheme.Swatch),
                    ToolTip = scheme.Display,
                    BorderBrush = new SolidColorBrush(
                        scheme.Name == ColorSchemeManager.Current
                            ? Color.FromRgb(44, 111, 181)
                            : Color.FromRgb(184, 200, 216))
                };
                var captured = scheme;
                border.MouseLeftButtonDown += (s, e) => SelectScheme(captured, border);
                _schemeBorders.Add(border);
                ColorsPanel.Children.Add(border);
            }
        }

        private void SelectScene(string scene, Border chosen)
        {
            SelectedScene = scene;
            var highlight = new SolidColorBrush(Color.FromRgb(44, 111, 181));
            var normal = new SolidColorBrush(Color.FromRgb(184, 200, 216));
            foreach (var b in _sceneBorders)
                b.BorderBrush = b == chosen ? highlight : normal;
        }

        // Selecting a color scheme applies (and saves) it immediately for live preview.
        private void SelectScheme(ColorScheme scheme, Border chosen)
        {
            SelectedScheme = scheme.Name;
            SelectedColor = scheme.Swatch;
            ColorSchemeManager.Apply(scheme.Name);

            var highlight = new SolidColorBrush(Color.FromRgb(44, 111, 181));
            var normal = new SolidColorBrush(Color.FromRgb(184, 200, 216));
            foreach (var b in _schemeBorders)
                b.BorderBrush = b == chosen ? highlight : normal;
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

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            // Color scheme is applied live on selection; nothing else to do here.
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a banner image",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var border = new Border
                {
                    Style = (Style)FindResource("SceneItem"),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(44, 111, 181)),
                    Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(dialog.FileName)),
                        Stretch = Stretch.UniformToFill
                    },
                    ToolTip = Path.GetFileNameWithoutExtension(dialog.FileName)
                };
                AppSettings.CustomBannerPath = dialog.FileName;
                border.MouseLeftButtonDown += (s, args) => SelectScene("__custom__", border);
                _sceneBorders.Add(border);
                ScenesPanel.Children.Add(border);
                SelectScene("__custom__", border);
            }
            catch (Exception ex)
            {
                DoryNative.XpSim.SimMessageBox.Show("Could not load image: " + ex.Message, "Windows Live Messenger");
            }
        }

        // Path to the custom banner image selected via Browse
        private string _customScenePath;

        /// <summary>
        /// Upload the selected banner and color scheme to the server so contacts can see them.
        /// </summary>
        public void SyncToServer()
        {
            try
            {
                // Sync color scheme
                if (!string.IsNullOrEmpty(SelectedScheme))
                {
                    Task.Run(() => SyncColorScheme(SelectedScheme));
                }

                // Sync banner
                string bannerPath = ResolveBannerPath();
                if (!string.IsNullOrEmpty(bannerPath) && File.Exists(bannerPath))
                {
                    Task.Run(() => SyncBanner(bannerPath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SceneDialog.SyncToServer: " + ex.Message);
            }
        }

        private string ResolveBannerPath()
        {
            // Use THIS dialog's current selection, not AppSettings - the caller
            // (Scene_Click) runs SyncToServer() *before* it writes the new scene
            // into AppSettings.ContactListScene, so reading AppSettings here would
            // upload the previous banner (or nothing on first set).
            string sceneRel = SelectedScene;
            if (string.IsNullOrEmpty(sceneRel))
                return null;

            // Custom scene uploaded via Browse
            if (sceneRel == "__custom__")
            {
                string customPath = AppSettings.CustomBannerPath;
                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                    return customPath;
                return null;
            }

            // Built-in scene
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "wlm", sceneRel);
            if (File.Exists(localPath))
                return localPath;

            string sharedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "shared", "assets", "scenes", Path.GetFileName(sceneRel));
            if (File.Exists(sharedPath))
                return sharedPath;

            return null;
        }

        private static void SyncColorScheme(string schemeName)
        {
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                    if (!string.IsNullOrEmpty(ContactStyleManager.CurrentUserEmail))
                        client.Headers["X-Desktop-User"] = ContactStyleManager.CurrentUserEmail;
                    var json = "{\"color_scheme\":\"" + schemeName + "\"}";
                    client.UploadString(ContactStyleManager.ServerBaseUrl + "/api/v1/style/me/scheme", "POST", json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SyncColorScheme: " + ex.Message);
            }
        }

        private static void SyncBanner(string imagePath)
        {
            try
            {
                using (var client = new System.Net.WebClient())
                {
                    var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "multipart/form-data; boundary=" + boundary;
                    if (!string.IsNullOrEmpty(ContactStyleManager.CurrentUserEmail))
                        client.Headers["X-Desktop-User"] = ContactStyleManager.CurrentUserEmail;

                    var fileData = File.ReadAllBytes(imagePath);
                    var fileName = Path.GetFileName(imagePath);

                    var sb = new StringBuilder();
                    sb.AppendLine("--" + boundary);
                    sb.AppendLine("Content-Disposition: form-data; name=\"banner\"; filename=\"" + fileName + "\"");
                    sb.AppendLine("Content-Type: image/" + Path.GetExtension(imagePath).TrimStart('.'));
                    sb.AppendLine();

                    var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
                    var footerBytes = Encoding.UTF8.GetBytes("\r\n--" + boundary + "--\r\n");

                    var payload = new byte[headerBytes.Length + fileData.Length + footerBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, payload, 0, headerBytes.Length);
                    Buffer.BlockCopy(fileData, 0, payload, headerBytes.Length, fileData.Length);
                    Buffer.BlockCopy(footerBytes, 0, payload, headerBytes.Length + fileData.Length, footerBytes.Length);

                    client.UploadData(ContactStyleManager.ServerBaseUrl + "/api/v1/style/me/banner", "POST", payload);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SyncBanner: " + ex.Message);
            }
        }
    }
}
