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
    public class ChangePasswordViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<bool>? RequestClose;

        private readonly UserRecord _user;
        private readonly string _usersCsvPath;

        private string _currentPassword = "";
        public string CurrentPassword
        {
            get => _currentPassword;
            set { _currentPassword = value; OnPropertyChanged(nameof(CurrentPassword)); }
        }

        private string _newPassword = "";
        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(nameof(NewPassword)); }
        }

        private string _confirmPassword = "";
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { _confirmPassword = value; OnPropertyChanged(nameof(ConfirmPassword)); }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public ChangePasswordViewModel(UserRecord user, string usersCsvPath)
        {
            _user = user;
            _usersCsvPath = usersCsvPath;

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(this, false));
        }

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void Confirm()
        {
            if (!string.Equals(CurrentPassword, _user.Password))
            {
                MessageBox.Show("Current password is incorrect.",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("New password cannot be empty.",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(NewPassword, ConfirmPassword))
            {
                MessageBox.Show("New password and confirm password do not match.",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (!File.Exists(_usersCsvPath))
                {
                    MessageBox.Show("users.csv not found.", "Change Password",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var lines = File.ReadAllLines(_usersCsvPath).ToList();
                if (lines.Count <= 1)
                {
                    MessageBox.Show("users.csv is empty.", "Change Password",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string header = lines[0];
                var body = new List<string>();

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                    {
                        body.Add(line);
                        continue;
                    }

                    var username = parts[0].Trim();
                    if (username.Equals(_user.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        parts[1] = NewPassword;
                        body.Add(string.Join(",", parts));
                    }
                    else
                    {
                        body.Add(line);
                    }
                }

                var newLines = new List<string> { header };
                newLines.AddRange(body);
                File.WriteAllLines(_usersCsvPath, newLines);

                _user.Password = NewPassword;

                MessageBox.Show("Password changed successfully.",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Information);

                RequestClose?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error changing password: {ex.Message}",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
