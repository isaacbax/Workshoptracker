using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DesignSheet.Models;
using DesignSheet.Services;

namespace DesignSheet.ViewModels;

public sealed class ChangePasswordViewModel : ViewModelBase
{
    private readonly string _username;
    private readonly string _dataFolder;
    private readonly CsvStore _csvStore;

    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";

    public ChangePasswordViewModel(string username, string dataFolder, CsvStore csvStore)
    {
        _username = username;
        _dataFolder = dataFolder;
        _csvStore = csvStore;

        ConfirmChangePasswordCommand = new RelayCommand(_ => ChangePassword());
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public ICommand ConfirmChangePasswordCommand { get; }

    private void ChangePassword()
    {
        if (string.IsNullOrWhiteSpace(_dataFolder))
        {
            MessageBox.Show("No data folder configured.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var usersPath = Paths.UsersCsv(_dataFolder);
        if (!System.IO.File.Exists(usersPath))
        {
            MessageBox.Show("users.csv not found.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        UserRecord[] users;
        try
        {
            users = _csvStore.LoadUsers(_dataFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading users.csv:\n{ex.Message}", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var user = users.FirstOrDefault(u =>
            string.Equals(u.Username, _username, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            MessageBox.Show("Current user not found in users.csv.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (user.Password != CurrentPassword)
        {
            MessageBox.Show("Current password is incorrect.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            MessageBox.Show("New password cannot be empty.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            MessageBox.Show("New password and confirmation do not match.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        user.Password = NewPassword;

        try
        {
            _csvStore.SaveUsers(_dataFolder, users);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving users.csv:\n{ex.Message}", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show("Password changed successfully.", "Change Password",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
