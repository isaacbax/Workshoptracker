using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DesignSheet
{
    public partial class LoginWindow : Window
    {
        private readonly List<UserRecord> _users = new();

        public LoginWindow()
        {
            InitializeComponent();

            LoadUsers();
            BaseFolderTextBlock.Text = AppConfig.BaseFolder;

            BranchComboBox.ItemsSource = new[]
            {
                "headoffice","mackay","brendale","newcastle","bayswater",
                "uds","dubbo","perth","sydney","brokenhill","townsville","launceston"
            };
            BranchComboBox.SelectedIndex = 0;
        }

        private void LoadUsers()
        {
            _users.Clear();

            if (!File.Exists(AppConfig.UsersFile))
            {
                // seed a root/root user if file missing
                _users.Add(new UserRecord
                {
                    Username = "root",
                    PasswordHash = "root",
                    Branch = "headoffice"
                });
                AppConfig.SaveUsers(_users);
                return;
            }

            foreach (var line in File.ReadAllLines(AppConfig.UsersFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');

                if (parts.Length >= 3)
                {
                    _users.Add(new UserRecord
                    {
                        Username = parts[0].Trim(),
                        PasswordHash = parts[1].Trim(),
                        Branch = parts[2].Trim()
                    });
                }
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter both username and password.",
                    "Missing details",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var user = _users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
                u.PasswordHash == password);

            if (user == null)
            {
                MessageBox.Show("Invalid username or password.",
                    "Login failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // root is allowed to pick branch; others use their own branch
            string branch = user.Branch;
            if (user.Username.Equals("root", StringComparison.OrdinalIgnoreCase) &&
                BranchComboBox.SelectedItem is string selBranch &&
                !string.IsNullOrWhiteSpace(selBranch))
            {
                branch = selBranch;
            }

            AppConfig.CurrentUserName = user.Username;
            AppConfig.CurrentBranch = branch;

            var main = new MainWindow(AppConfig.BaseFolder, user.Username, branch);
            main.Show();
            Close();
        }

        private void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.ChangeRootFolder(this);
            BaseFolderTextBlock.Text = AppConfig.BaseFolder;
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
