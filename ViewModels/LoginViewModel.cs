using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DesignSheet.Models;

namespace DesignSheet.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<bool>? RequestClose;

        private string _username = "";
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        private string _password = "";
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        private string _folderPath = @"S:\IT\20 - Workshop Tracker";
        public string FolderPath
        {
            get => _folderPath;
            set { _folderPath = value; OnPropertyChanged(nameof(FolderPath)); }
        }

        private List<string> _availableBranches = new();
        public List<string> AvailableBranches
        {
            get => _availableBranches;
            set { _availableBranches = value; OnPropertyChanged(nameof(AvailableBranches)); }
        }

        private string? _selectedBranch;
        public string? SelectedBranch
        {
            get => _selectedBranch;
            set { _selectedBranch = value; OnPropertyChanged(nameof(SelectedBranch)); }
        }

        private bool _isRoot;
        public bool IsRoot
        {
            get => _isRoot;
            set { _isRoot = value; OnPropertyChanged(nameof(IsRoot)); }
        }

        private UserRecord? _selectedUser;
        public UserRecord? SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(nameof(SelectedUser)); }
        }

        public ICommand BrowseFolderCommand { get; }
        public ICommand LoginCommand { get; }
        public ICommand CancelCommand { get; }

        private List<UserRecord> _allUsers = new();

        public LoginViewModel()
        {
            BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
            LoginCommand = new RelayCommand(_ => DoLogin());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));

            LoadUsers();
        }

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string UsersCsvPath => Path.Combine(FolderPath, "users.csv");

        private void LoadUsers()
        {
            try
            {
                if (!File.Exists(UsersCsvPath))
                    return;

                var lines = File.ReadAllLines(UsersCsvPath).ToList();
                if (lines.Count <= 1) return;

                // header: username,password,branch
                _allUsers = lines.Skip(1)
                                 .Where(l => !string.IsNullOrWhiteSpace(l))
                                 .Select(l =>
                                 {
                                     var parts = l.Split(',');
                                     return new UserRecord
                                     {
                                         Username = parts.Length > 0 ? parts[0].Trim() : "",
                                         Password = parts.Length > 1 ? parts[1].Trim() : "",
                                         Branch = parts.Length > 2 ? parts[2].Trim() : ""
                                     };
                                 })
                                 .ToList();

                AvailableBranches = _allUsers
                    .Select(u => u.Branch)
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(b => b)
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading users.csv: {ex.Message}",
                    "Login", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseFolder()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = FolderPath;
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                FolderPath = dlg.SelectedPath;
                LoadUsers();
            }
        }

        private void DoLogin()
        {
            if (_allUsers.Count == 0)
            {
                MessageBox.Show("No users found in users.csv.", "Login",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = _allUsers.FirstOrDefault(u =>
                u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == Password);

            if (user == null)
            {
                MessageBox.Show("Invalid username or password.", "Login",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedUser = user;
            IsRoot = user.Username.Equals("root", StringComparison.OrdinalIgnoreCase);

            if (IsRoot)
            {
                if (string.IsNullOrWhiteSpace(SelectedBranch))
                {
                    MessageBox.Show("Select a branch for root user.", "Login",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SelectedUser = new UserRecord
                {
                    Username = user.Username,
                    Password = user.Password,
                    Branch = SelectedBranch!
                };
            }

            RequestClose?.Invoke(this, true);
        }
    }
}
