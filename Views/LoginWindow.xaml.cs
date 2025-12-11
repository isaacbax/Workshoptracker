// Views/LoginWindow.xaml.cs
using System;
using System.Windows;
using WorkshopTracker.Services;
using WorkshopTracker.Models;
using WorkshopTracker; // for MainWindow

namespace WorkshopTracker.Views
{
    public partial class LoginWindow : Window
    {
        private readonly ConfigService _config;
        private readonly UserService _userService;

        public LoginWindow()
        {
            InitializeComponent();

            _config = new ConfigService();
            _userService = new UserService(_config);
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = (UsernameTextBox.Text ?? string.Empty).Trim();
            var password = (PasswordBox.Password ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Enter username and password.");
                return;
            }

            var user = _userService.ValidateLogin(username, password);
            if (user == null)
            {
                MessageBox.Show("Invalid username or password.");
                return;
            }

            // For now, always use the headoffice branch
            var branchToUse = string.IsNullOrWhiteSpace(user.Branch)
                ? "headoffice"
                : user.Branch;

            var main = new MainWindow(branchToUse, user.Username, _config);
            main.Show();
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
