using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DesignSheet.Models;
using DesignSheet.Views;

namespace DesignSheet.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly string _dataFolder;
        private readonly string _branch;
        private readonly string _usersCsvPath;
        private readonly UserRecord _userRecord;

        public string CurrentUser => _userRecord.Username;

        public ObservableCollection<WorkRowView> OpenRows { get; } = new();
        public ObservableCollection<WorkRowView> ClosedRows { get; } = new();

        public ICollectionView OpenRowsView { get; }
        public ICollectionView ClosedRowsView { get; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    OpenRowsView.Refresh();
                    ClosedRowsView.Refresh();
                }
            }
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set { _selectedTabIndex = value; OnPropertyChanged(nameof(SelectedTabIndex)); }
        }

        public ICommand ReloadAllCommand { get; }
        public ICommand SaveBackupCommand { get; }
        public ICommand ChangePasswordCommand { get; }

        private bool _debugShown;

        public MainViewModel(string dataFolder, UserRecord user)
        {
            _dataFolder = dataFolder;
            _userRecord = user;
            _branch = string.IsNullOrWhiteSpace(user.BranchClean) ? "headoffice" : user.BranchClean;
            _usersCsvPath = Path.Combine(_dataFolder, "users.csv");

            OpenRowsView = CollectionViewSource.GetDefaultView(OpenRows);
            ClosedRowsView = CollectionViewSource.GetDefaultView(ClosedRows);

            OpenRowsView.Filter = FilterRow;
            ClosedRowsView.Filter = FilterRow;

            ReloadAllCommand = new RelayCommand(_ => LoadAll());
            SaveBackupCommand = new RelayCommand(_ => SaveBackup());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());

            LoadAll();
            SetupWatchers();
        }

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string OpenCsvPath => Path.Combine(_dataFolder, $"{_branch}open.csv");
        private string ClosedCsvPath => Path.Combine(_dataFolder, $"{_branch}closed.csv");

        // ---------- Load / Save ----------

        public void LoadAll()
        {
            try
            {
                if (!_debugShown)
                {
                    _debugShown = true;
                    MessageBox.Show(
                        $"Branch from users.csv: '{_branch}'\n" +
                        $"Data folder: {_dataFolder}\n\n" +
                        $"Trying to load:\n{OpenCsvPath}\n{ClosedCsvPath}",
                        "Debug – CSV paths");
                }

                LoadFile(OpenCsvPath, OpenRows, "open");
                LoadFile(ClosedCsvPath, ClosedRows, "closed");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading CSV files: {ex.Message}",
                    "Load", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFile(string path,
                              ObservableCollection<WorkRowView> target,
                              string labelForDebug)
        {
            target.Clear();

            if (!File.Exists(path))
            {
                MessageBox.Show(
                    $"{Path.GetFileName(path)} was NOT found in:\n{_dataFolder}\n\n" +
                    $"(branch = '{_branch}', file type = {labelForDebug})",
                    "CSV not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var lines = File.ReadAllLines(path).ToList();
            if (lines.Count <= 1)
            {
                MessageBox.Show(
                    $"{Path.GetFileName(path)} exists but has no data rows.\n" +
                    "You will only see headers.",
                    "CSV empty",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var rows = lines
                .Skip(1)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(ParseWorkRow)
                .Where(r => r != null)
                .Cast<WorkRow>()
                .ToList();

            if (rows.Count == 0)
            {
                MessageBox.Show(
                    $"{Path.GetFileName(path)} loaded, but no valid rows were parsed.",
                    "CSV parse result empty",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                $"{Path.GetFileName(path)} loaded.\nParsed rows: {rows.Count}",
                "CSV loaded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            var ordered = rows
                .OrderBy(r => ParseDate(r.DATE_DUE))
                .ThenBy(r => r.CUSTOMER)
                .ToList();

            string? currentDate = null;
            foreach (var row in ordered)
            {
                var key = row.DATE_DUE ?? "";
                if (!string.Equals(currentDate, key, StringComparison.Ordinal))
                {
                    currentDate = key;
                    target.Add(WorkRowView.Separator(currentDate));
                }

                target.Add(WorkRowView.Item(row));
            }
        }

        private WorkRow? ParseWorkRow(string line)
        {
            var parts = line.Split(',');
            if (parts.Length == 0) return null;

            string Get(int i) => i < parts.Length ? parts[i].Trim() : "";

            return new WorkRow
            {
                RETAIL = Get(0),
                OE = Get(1),
                CUSTOMER = Get(2),
                SERIAL = Get(3),
                DAY_DUE = Get(4),
                DATE_DUE = Get(5),
                STATUS = Get(6),
                QTY = Get(7),
                WHAT_IS_IT = Get(8),
                PO = Get(9),
                WHAT_ARE_WE_DOING = Get(10),
                PARTS = Get(11),
                SHAFT = Get(12),
                PRIORITY = Get(13),
                LAST_USER = Get(14)
            };
        }

        private DateTime ParseDate(string? s)
            => DateTime.TryParse(s, out var d) ? d : DateTime.MaxValue;

        public void SaveAll()
        {
            SaveFile(OpenCsvPath, OpenRows);
            SaveFile(ClosedCsvPath, ClosedRows);
        }

        private void SaveFile(string path, ObservableCollection<WorkRowView> source)
        {
            var header = "RETAIL,OE,CUSTOMER,SERIAL,DAY_DUE,DATE_DUE,STATUS,QTY,WHAT_IS_IT,PO,WHAT_ARE_WE_DOING,PARTS,SHAFT,PRIORITY,LAST_USER";

            var lines = source
                .Where(v => !v.IsSeparator && v.Row != null)
                .Select(v => v.Row)
                .Select(r => string.Join(",",
                    r.RETAIL,
                    r.OE,
                    r.CUSTOMER,
                    r.SERIAL,
                    r.DAY_DUE,
                    r.DATE_DUE,
                    r.STATUS,
                    r.QTY,
                    r.WHAT_IS_IT,
                    r.PO,
                    r.WHAT_ARE_WE_DOING,
                    r.PARTS,
                    r.SHAFT,
                    r.PRIORITY,
                    r.LAST_USER))
                .ToList();

            lines.Insert(0, header);
            File.WriteAllLines(path, lines);
        }

        private void SaveBackup()
        {
            try
            {
                SaveAll();
                MessageBox.Show("Backup saved.", "Save backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving backup: {ex.Message}",
                    "Save backup", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Filtering ----------

        private bool FilterRow(object obj)
        {
            if (obj is not WorkRowView view) return false;
            if (view.IsSeparator) return true;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var row = view.Row;
            if (row == null) return true;

            var search = SearchText.Trim();

            string[] fields =
            {
                row.CUSTOMER,
                row.SERIAL,
                row.STATUS,
                row.WHAT_IS_IT,
                row.PO,
                row.WHAT_ARE_WE_DOING,
                row.PARTS,
                row.SHAFT
            };

            return fields.Any(f => !string.IsNullOrEmpty(f) &&
                                   f.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ---------- Change password ----------

        private void ChangePassword()
        {
            try
            {
                var vm = new ChangePasswordViewModel(_userRecord, _usersCsvPath);
                var wnd = new ChangePasswordWindow
                {
                    DataContext = vm,
                    Owner = Application.Current.MainWindow
                };
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Change Password: {ex.Message}",
                    "Change Password", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- File watchers for 'live-like' updates ----------

        private FileSystemWatcher? _openWatcher;
        private FileSystemWatcher? _closedWatcher;
        private bool _ignoreNextChange;

        private void SetupWatchers()
        {
            try
            {
                if (!Directory.Exists(_dataFolder))
                    return;

                _openWatcher = CreateWatcher(OpenCsvPath);
                _closedWatcher = CreateWatcher(ClosedCsvPath);
            }
            catch
            {
                // if watcher fails, just skip auto-reload
            }
        }

        private FileSystemWatcher CreateWatcher(string path)
        {
            var dir = Path.GetDirectoryName(path)!;
            var file = Path.GetFileName(path);

            var watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };

            watcher.Changed += (s, e) =>
            {
                if (_ignoreNextChange)
                {
                    _ignoreNextChange = false;
                    return;
                }

                Application.Current.Dispatcher.Invoke(LoadAll);
            };

            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        public void NotifySavedByThisInstance()
        {
            _ignoreNextChange = true;
        }
    }
}
