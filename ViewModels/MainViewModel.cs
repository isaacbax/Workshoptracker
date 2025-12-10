using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DesignSheet.Models;
using DesignSheet.Services;
using DesignSheet.Views;

namespace DesignSheet.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly CsvStore _csvStore;
    private readonly FileWatchService? _watcher;

    private readonly string _dataFolder;
    private readonly string _branch;
    private readonly string _currentUser;

    private string _title = "DesignSheet";
    private string _searchText = "";
    private double _gridFontSize = 13.0;   // Regular default

    private readonly ObservableCollection<WorkRowView> _openRows = new();
    private readonly ObservableCollection<WorkRowView> _closedRows = new();

    public MainViewModel(string dataFolder, UserRecord user)
    {
        _dataFolder = dataFolder;
        _branch = user.Branch;
        _currentUser = user.Username;

        _csvStore = new CsvStore();

        Title = $"DesignSheet - {_branch}";

        OpenFiltered = CollectionViewSource.GetDefaultView(_openRows);
        ClosedFiltered = CollectionViewSource.GetDefaultView(_closedRows);
        OpenFiltered.Filter = FilterRow;
        ClosedFiltered.Filter = FilterRow;

        ReloadCommand = new RelayCommand(_ => Reload(), _ => HasValidFolder());
        BackupCommand = new RelayCommand(_ => Backup(), _ => HasValidFolder());
        ChangePasswordCommand = new RelayCommand(_ => ChangePassword(), _ => HasValidFolder());

        if (HasValidFolder())
        {
            _watcher = new FileWatchService(_dataFolder);
            _watcher.Changed += (_, _) =>
                System.Windows.Application.Current.Dispatcher.Invoke(Reload);
        }

        Reload();
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OpenFiltered.Refresh();
                ClosedFiltered.Refresh();
            }
        }
    }

    // For view zoom: bound from MainWindow buttons, used by WorksGrid FontSize
    public double GridFontSize
    {
        get => _gridFontSize;
        set => SetProperty(ref _gridFontSize, value);
    }

    public ICollectionView OpenFiltered { get; }
    public ICollectionView ClosedFiltered { get; }

    public ICommand ReloadCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand ChangePasswordCommand { get; }

    public string CurrentUser => _currentUser;

    private bool HasValidFolder() =>
        !string.IsNullOrWhiteSpace(_dataFolder) && Directory.Exists(_dataFolder);

    private void Reload()
    {
        if (!HasValidFolder()) return;

        try
        {
            var openPath = Paths.OpenCsv(_dataFolder, _branch);
            var closedPath = Paths.ClosedCsv(_dataFolder, _branch);

            var openRows = _csvStore.LoadWork(openPath);
            var closedRows = _csvStore.LoadWork(closedPath);

            _openRows.Clear();
            _closedRows.Clear();

            BuildWithDateSeparators(openRows, _openRows);
            BuildWithDateSeparators(closedRows, _closedRows);

            // If there are literally no open rows, give one blank row so the user can start typing
            if (_openRows.All(v => v.IsSeparator))
            {
                var blank = new WorkRow
                {
                    STATUS = "quote",
                    LAST_USER = _currentUser
                };
                _openRows.Add(WorkRowView.Item(blank));
            }

            OpenFiltered.Refresh();
            ClosedFiltered.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading CSV files:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BuildWithDateSeparators(WorkRow[] rows, ObservableCollection<WorkRowView> target)
    {
        string? lastDate = null;

        foreach (var r in rows)
        {
            var dateStr = (r.DATE_DUE ?? "").Trim();
            if (string.IsNullOrEmpty(dateStr))
                dateStr = "(No date)";

            if (!string.Equals(lastDate, dateStr, StringComparison.OrdinalIgnoreCase))
            {
                lastDate = dateStr;
                target.Add(WorkRowView.Separator(dateStr));
            }

            target.Add(WorkRowView.Item(r));
        }
    }

    // Public so WorksGrid & Save button can call it
    public void SaveAll()
    {
        if (!HasValidFolder()) return;

        try
        {
            var openPath = Paths.OpenCsv(_dataFolder, _branch);
            var closedPath = Paths.ClosedCsv(_dataFolder, _branch);

            var openRows = _openRows
                .Where(v => !v.IsSeparator && v.Row != null)
                .Select(v => v.Row!)
                .ToArray();

            var closedRows = _closedRows
                .Where(v => !v.IsSeparator && v.Row != null)
                .Select(v => v.Row!)
                .ToArray();

            _csvStore.SaveWork(openPath, openRows);
            _csvStore.SaveWork(closedPath, closedRows);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving CSV files:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Backup()
    {
        if (!HasValidFolder()) return;

        try
        {
            var openPath = Paths.OpenCsv(_dataFolder, _branch);
            var closedPath = Paths.ClosedCsv(_dataFolder, _branch);

            _csvStore.Backup(openPath);
            _csvStore.Backup(closedPath);

            MessageBox.Show("Backup created for open and closed CSV files.",
                "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating backup:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChangePassword()
    {
        if (!HasValidFolder())
        {
            MessageBox.Show("No data folder configured.", "Change Password",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var vm = new ChangePasswordViewModel(_currentUser, _dataFolder, _csvStore);
        var win = new ChangePasswordWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };

        win.ShowDialog();
    }

    private bool FilterRow(object obj)
    {
        if (obj is not WorkRowView view) return true;

        if (view.IsSeparator) return true;

        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var row = view.Row;
        if (row == null) return false;

        var text = SearchText.Trim();
        var cmp = StringComparison.OrdinalIgnoreCase;

        return (row.CUSTOMER?.IndexOf(text, cmp) >= 0) ||
               (row.SERIAL?.IndexOf(text, cmp) >= 0) ||
               (row.STATUS?.IndexOf(text, cmp) >= 0) ||
               (row.WHAT_IS_IT?.IndexOf(text, cmp) >= 0) ||
               (row.WHAT_ARE_WE_DOING?.IndexOf(text, cmp) >= 0) ||
               (row.PO?.IndexOf(text, cmp) >= 0);
    }
}
