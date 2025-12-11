using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DesignSheet.ViewModels
{
    using DesignSheet.Models;

    public class LoginViewModel : INotifyPropertyChanged
    {
        private string _username = "";
        private string _password = "";
        private string _selectedBranch = "";
        private ObservableCollection<string> _branches = new();
        private ObservableCollection<UserRecord> _users = new();

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        public string SelectedBranch
        {
            get => _selectedBranch;
            set { _selectedBranch = value; OnPropertyChanged(nameof(SelectedBranch)); }
        }

        public ObservableCollection<string> Branches
        {
            get => _branches;
            set { _branches = value; OnPropertyChanged(nameof(Branches)); }
        }

        public ObservableCollection<UserRecord> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(nameof(Users)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public LoginViewModel()
        {
            // Placeholder: you can populate Users + Branches from CSV here.
            // For now it's empty but compiles cleanly.
        }

        public bool ValidateLogin(out UserRecord? matchedUser)
        {
            matchedUser = null;
            foreach (var user in Users)
            {
                if (string.Equals(user.Username, Username, System.StringComparison.OrdinalIgnoreCase)
                    && user.Password == Password)
                {
                    matchedUser = user;
                    return true;
                }
            }
            return false;
        }
    }
}
