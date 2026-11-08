using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WLMRevivalV2Desktop.Views
{
    public partial class ContactNotification : Window
    {
        private DispatcherTimer _timer;

        public ContactNotification(string contactName, string contactEmail, ImageSource contactImage)
        {
            InitializeComponent();
            
            ContactNameText.Text = contactName;
            StatusText.Text = "has just signed in.";
            
            if (contactImage != null)
                ContactTile.Source = contactImage;

            // Position in bottom-right corner
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 10;
            Top = workingArea.Bottom - Height - 10;

            // Auto-close after 5 seconds
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (s, e) => Close();
            _timer.Start();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Close();
        }
    }
}
