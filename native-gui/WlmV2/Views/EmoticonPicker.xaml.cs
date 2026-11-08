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
    public partial class EmoticonPicker : Window
    {
        public string SelectedEmoticon { get; private set; }
        private Window _ownerWindow;

        public EmoticonPicker(Window owner)
        {
            _ownerWindow = owner;
            InitializeComponent();
            LoadEmoticons();
            
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
            // When window is activated, set up a check for when it loses focus
            Deactivated += OnWindowDeactivated;
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            // Only close if no emoticon was selected (DialogResult not set)
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

        private void LoadEmoticons()
        {
            // Load from local emoticon assets (copied from APK)
            string emoticonsPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "wlm", "emoticons");
            
            // Also check the CustomWLM extracted assets
            string customWlmPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..",
                "client", "CustomWLM", "Assets", "Extracted", "msgsres", "CUSTOM_4000");

            var emoticonFiles = new[] { emoticonsPath, customWlmPath }
                .SelectMany(path => 
                {
                    try { return Directory.GetFiles(path, "*.gif"); }
                    catch { return Array.Empty<string>(); }
                })
                .Where(f => !Path.GetFileName(f).StartsWith("usertile"))
                .ToArray();

            if (emoticonFiles.Length == 0)
            {
                // Fallback to text emoticons if no images found
                LoadTextEmoticons();
                return;
            }

            foreach (var file in emoticonFiles)
            {
                try
                {
                    var img = new Image
                    {
                        Width = 24,
                        Height = 24,
                        Stretch = Stretch.Uniform,
                        Source = new BitmapImage(new Uri(file))
                    };
                    
                    var border = new Border
                    {
                        Style = (Style)FindResource("EmoticonItem"),
                        Child = img,
                        ToolTip = Path.GetFileNameWithoutExtension(file)
                    };
                    
                    string emoticonText = $"[{Path.GetFileNameWithoutExtension(file)}]";
                    border.MouseLeftButtonDown += (s, e) => SelectEmoticon(emoticonText);
                    FeaturedEmoticonsPanel.Children.Add(border);
                }
                catch { /* Skip invalid images */ }
            }
        }

        private void LoadTextEmoticons()
        {
            string[] emoticons = new string[] { ":)", ":(", ";)", ":D", ":P", "XD", ":O", "<3", "B)", ":/" };
            
            foreach (var emoticon in emoticons)
            {
                var border = new Border
                {
                    Style = (Style)FindResource("EmoticonItem"),
                    Child = new TextBlock
                    {
                        Text = emoticon,
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                border.MouseLeftButtonDown += (s, e) => SelectEmoticon(emoticon);
                FeaturedEmoticonsPanel.Children.Add(border);
            }
        }

        private void SelectEmoticon(string emoticon)
        {
            SelectedEmoticon = emoticon;
            if (IsModal())
            {
                DialogResult = true;
            }
            Close();
        }

        private bool IsModal()
        {
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
    }
}
