using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Forms;
using DesignSheet.Models;

namespace DesignSheet.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Raised when the login window should close.
    // true = login OK, false = cancel
    public event Action<bool>? CloseRequested;

    private string _username = "";
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    private string _folderPath = @"S:\IT\20 - Workshop Tracker";
    public string FolderPath
    {
        get => _folderPath;
        set { _folderPath = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Branches { get; } = new();

    private string? _selectedBranch;
    public string? SelectedBranch
    {
        get => _selectedBranch;
        set { _selectedBranch = value; OnPropertyChanged(); }
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    // User that successfully logged in
    public UserRecord? SelectedUser { get; private set; }

    // Backing list loaded from users.csv
    private readonly ObservableCollection<UserRecord> _users = new();

    public ICommand BrowseFolderCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand CancelCommand { get; }

    public LoginViewModel()
    {
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        LoginCommand = new RelayCommand(_ => DoLogin());
        CancelCommand = new RelayCommand(_ => Cancel());

        LoadUsers();
    }

    private void LoadUsers()
    {
        try
        {
            var usersPath = Path.Combine(FolderPath, "users.csv");
            if (!File.Exists(usersPath))
            {
                ErrorMessage = $"users.csv not found in {FolderPath}";
                return;
            }

            _users.Clear();
            Branches.Clear();

            var lines = File.ReadAllLines(usersPath);
            if (lines.Length == 0)
            {
                ErrorMessage = "users.csv is empty.";
                return;
            }

            // Expect header: username,password,branch
            foreach (var line in lines.Skip(1)) // skip header
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                var user = new UserRecord
                {
                    Username = parts[0].Trim(),
                    Password = parts[1].Trim(),
                    Branch = parts[2].Trim()
                };
                _users.Add(user);
            }

            var distinctBranches = _users
                .Select(u => u.Branch)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct()
                .OrderBy(b => b);

            foreach (var b in distinctBranches)
                Branches.Add(b);

            if (Branches.Count > 0 && SelectedBranch == null)
                SelectedBranch = Branches[0];

            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading users: {ex.Message}";
        }
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog();
        dlg.Description = "Select workshop tracker folder";
        dlg.SelectedPath = FolderPath;
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            FolderPath = dlg.SelectedPath;
            LoadUsers();
        }
    }

    private void DoLogin()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password.";
            return;
        }

        var user = _users.FirstOrDefault(u =>
            string.Equals(u.Username, Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == Password);

        if (user == null)
        {
            ErrorMessage = "Invalid username or password.";
            return;
        }

        // Non-root users must use their branch; root can select any branch
        if (!string.Equals(user.Username, "root", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(user.Branch, SelectedBranch, StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = $"User is assigned to branch '{user.Branch}' not '{SelectedBranch}'.";
                return;
            }
        }

        SelectedUser = user;
        CloseRequested?.Invoke(true);
    }

    private void Cancel()
    {
        SelectedUser = null;
        CloseRequested?.Invoke(false);
    }
}
