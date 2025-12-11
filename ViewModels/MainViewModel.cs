using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DesignSheet.Models
{
    // Simple user model for login
    public class UserRecord
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Branch { get; set; } = "";
    }

    // Main row model for the grids
    public class WorkRow : INotifyPropertyChanged
    {
        private bool _isGroupRow;
        private string _retail = "";
        private string _oe = "";
        private string _customer = "";
        private string _serial = "";
        private string _dayDue = "";
        private DateTime? _dateDue;
        private string _status = "";
        private int _qty;
        private string _whatIsIt = "";
        private string _po = "";
        private string _whatAreWeDoing = "";
        private string _parts = "";
        private string _shaft = "";
        private string _priority = "";
        private string _lastUser = "";

        public bool IsGroupRow
        {
            get => _isGroupRow;
            set { _isGroupRow = value; OnPropertyChanged(nameof(IsGroupRow)); }
        }

        public string Retail
        {
            get => _retail;
            set { _retail = value; OnPropertyChanged(nameof(Retail)); }
        }

        public string OE
        {
            get => _oe;
            set { _oe = value; OnPropertyChanged(nameof(OE)); }
        }

        public string Customer
        {
            get => _customer;
            set { _customer = value; OnPropertyChanged(nameof(Customer)); }
        }

        public string Serial
        {
            get => _serial;
            set { _serial = value; OnPropertyChanged(nameof(Serial)); }
        }

        public string DayDue
        {
            get => _dayDue;
            set { _dayDue = value; OnPropertyChanged(nameof(DayDue)); }
        }

        public DateTime? DateDue
        {
            get => _dateDue;
            set { _dateDue = value; OnPropertyChanged(nameof(DateDue)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public int Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(nameof(Qty)); }
        }

        public string WhatIsIt
        {
            get => _whatIsIt;
            set { _whatIsIt = value; OnPropertyChanged(nameof(WhatIsIt)); }
        }

        public string PO
        {
            get => _po;
            set { _po = value; OnPropertyChanged(nameof(PO)); }
        }

        public string WhatAreWeDoing
        {
            get => _whatAreWeDoing;
            set { _whatAreWeDoing = value; OnPropertyChanged(nameof(WhatAreWeDoing)); }
        }

        public string Parts
        {
            get => _parts;
            set { _parts = value; OnPropertyChanged(nameof(Parts)); }
        }

        public string Shaft
        {
            get => _shaft;
            set { _shaft = value; OnPropertyChanged(nameof(Shaft)); }
        }

        public string Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public string LastUser
        {
            get => _lastUser;
            set { _lastUser = value; OnPropertyChanged(nameof(LastUser)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public WorkRow Clone()
        {
            return (WorkRow)MemberwiseClone();
        }
    }
}

namespace DesignSheet.ViewModels
{
    using DesignSheet.Models;

    public class MainViewModel : INotifyPropertyChanged
    {
        private string _currentUser = "";
        private string _currentBranch = "";

        public ObservableCollection<WorkRow> OpenRows { get; } = new();
        public ObservableCollection<WorkRow> ClosedRows { get; } = new();

        public string CurrentUser
        {
            get => _currentUser;
            set { _currentUser = value; OnPropertyChanged(nameof(CurrentUser)); }
        }

        public string CurrentBranch
        {
            get => _currentBranch;
            set { _currentBranch = value; OnPropertyChanged(nameof(CurrentBranch)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainViewModel()
        {
            // ViewModel is currently "thin"; most heavy logic is in MainWindow.xaml.cs.
            // You can gradually move logic here if you want stricter MVVM later.
        }
    }
}
