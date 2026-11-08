using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WLMRevivalV2Desktop.Views
{
    public partial class AddContactDialog : Window
    {
        public string Email { get; private set; }
        public string DisplayName { get; private set; }

        public AddContactDialog()
        {
            InitializeComponent();
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

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Email = EmailTextBox.Text.Trim();
            DisplayName = DisplayNameTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(Email))
            {
                DoryNative.XpSim.SimMessageBox.Show("Please enter an email address.", "Add Contact", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
