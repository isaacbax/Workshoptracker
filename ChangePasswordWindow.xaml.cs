using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DesignSheet
{
    public partial class ChangePasswordWindow : Window
    {
        private readonly string _username;
        private readonly List<UserRecord> _users;

        public ChangePasswordWindow(string username, List<UserRecord> users)
        {
            InitializeComponent();

            _username = username;
            _users = users;

            UsernameLabel.Text = $"Change password for {_username}";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string oldPass = OldPasswordBox.Password;
            string newPass = NewPasswordBox.Password;

            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(_username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                MessageBox.Show("User not found.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (user.PasswordHash != oldPass)
            {
                MessageBox.Show("Old password is incorrect.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(newPass))
            {
                MessageBox.Show("New password cannot be empty.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            user.PasswordHash = newPass;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) =>
            Close();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
