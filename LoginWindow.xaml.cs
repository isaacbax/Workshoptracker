using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DesignSheet
{
    public partial class LoginWindow : Window
    {
        private class UserInfo
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Branch { get; set; } // for non-root users
        }

        private readonly List<UserInfo> _users;
        private readonly string[] _branches = new[]
        {
            "headoffice",
            "mackay",
            "brendale",
            "newcastle",
            "bayswater",
            "uds",
            "dubbo",
            "perth",
            "sydney",
            "brokenhill",
            "townsville",
            "launceston"
        };

        public LoginWindow()
        {
            InitializeComponent();

            // Example users – extend / change as needed.
            _users = new List<UserInfo>
            {
                new UserInfo { Username = "root", Password = "root", Branch = null },          // root chooses branch
                new UserInfo { Username = "headoffice", Password = "headoffice", Branch = "headoffice" },
                // add more: new UserInfo { Username="mackayuser", Password="...", Branch="mackay" }
            };

            BranchComboBox.ItemsSource = _branches;
            BranchComboBox.SelectedItem = "headoffice";
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var username = (UserTextBox.Text ?? "").Trim();
            var password = PasswordBox.Password ?? "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter username and password.",
                    "Missing details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)
                && u.Password == password);

            if (user == null)
            {
                MessageBox.Show("Invalid username or password.",
                    "Login failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string branch;

            if (string.Equals(user.Username, "root", StringComparison.OrdinalIgnoreCase))
            {
                // root selects branch from combo
                branch = (BranchComboBox.SelectedItem as string) ?? "headoffice";
            }
            else
            {
                // normal user – branch from table
                branch = user.Branch ?? "headoffice";
            }

            var main = new MainWindow(username, branch);
            main.Show();
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
