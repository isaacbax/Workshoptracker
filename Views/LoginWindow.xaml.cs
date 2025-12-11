using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using WorkshopTracker.Services;

namespace WorkshopTracker
{
    public partial class LoginWindow : Window
    {
        // Adjust if your share path is slightly different
        private const string BaseFolder = @"S:\IT\20 - Workshop Tracker\";
        private const string UsersFileName = "users.csv";

        private class CsvUser
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public string Branch { get; set; } = "";
        }

        private List<CsvUser> _users = new();

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadUsers();
                PopulateBranches();
                MessageTextBlock.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users.csv: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private string GetUsersPath()
        {
            return Path.Combine(BaseFolder, UsersFileName);
        }

        private void LoadUsers()
        {
            var path = GetUsersPath();
            if (!File.Exists(path))
                throw new FileNotFoundException("users.csv not found", path);

            var allLines = File.ReadAllLines(path);

            _users = new List<CsvUser>();

            for (int i = 0; i < allLines.Length; i++)
            {
                var line = allLines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // Skip header row (username,password,branch)
                if (i == 0 && line.StartsWith("username", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                _users.Add(new CsvUser
                {
                    Username = parts[0].Trim(),
                    Password = parts[1].Trim(),
                    Branch = parts[2].Trim()
                });
            }

            if (_users.Count == 0)
                throw new InvalidOperationException("No users were loaded from users.csv.");
        }

        private void PopulateBranches()
        {
            var branches = _users
                .Select(u => u.Branch)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(b => b)
                .ToList();

            BranchComboBox.ItemsSource = branches;

            if (branches.Count > 0)
                BranchComboBox.SelectedIndex = 0;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            MessageTextBlock.Text = "";

            var username = (UsernameTextBox.Text ?? "").Trim();
            var password = PasswordBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                MessageTextBlock.Text = "Please enter username and password.";
                return;
            }

            var user = _users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            if (user == null || !string.Equals(user.Password, password))
            {
                MessageTextBlock.Text = "Invalid username or password.";
                return;
            }

            // Decide branch
            string branch;

            if (string.Equals(user.Username, "root", StringComparison.OrdinalIgnoreCase))
            {
                // root can choose any branch from dropdown
                if (BranchComboBox.SelectedItem is string selected &&
                    !string.IsNullOrWhiteSpace(selected))
                {
                    branch = selected;
                }
                else
                {
                    branch = user.Branch;
                }
            }
            else
            {
                // normal user must use their own branch
                branch = user.Branch;
            }

            try
            {
                var config = new ConfigService(); // path is fixed inside ConfigService
                var main = new MainWindow(branch, user.Username, config);
                main.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open main window: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}
