using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DesignSheet
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<DesignItem> _rows = new();
        private readonly string _branch;
        private readonly string _activeFile;
        private readonly string _finishedFile;

        private FileSystemWatcher? _activeWatcher;
        private FileSystemWatcher? _finishedWatcher;
        private bool _suppressWatchReload;

        public MainWindow()
        {
            _branch = string.IsNullOrWhiteSpace(AppConfig.CurrentBranch)
                ? "headoffice"
                : AppConfig.CurrentBranch;

            string baseFolder = string.IsNullOrWhiteSpace(AppConfig.BaseFolder)
                ? @"S:\Public\DesignData"
                : AppConfig.BaseFolder;

            AppConfig.BaseFolder = baseFolder;

            InitializeComponent();

            _activeFile = Path.Combine(baseFolder, $"{_branch}.csv");
            _finishedFile = Path.Combine(baseFolder, $"{_branch}finished.csv");

            DesignGrid.ItemsSource = _rows;

            LoadAllFromDisk();
            SetupWatchers();
        }

        // ----------------------------
        // Helpers
        // ----------------------------

        private static bool IsFinishedStatus(string? status) =>
            string.Equals(status, "Picked Up", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsPaintShop(string? status) =>
            string.Equals(status, "Paint Shop", StringComparison.OrdinalIgnoreCase);

        private List<DesignItem> GetRealRowsSnapshot()
        {
            // strip spacer rows and clone to avoid binding side-effects
            return _rows
                .Where(r => !r.IsSpacer)
                .Select(r => r.Clone())
                .ToList();
        }

        // ----------------------------
        // File I/O
        // ----------------------------

        private void LoadAllFromDisk()
        {
            _rows.Clear();

            var active = File.Exists(_activeFile)
                ? ReadDesignsFromFile(_activeFile)
                : Enumerable.Empty<DesignItem>();

            var finished = File.Exists(_finishedFile)
                ? ReadDesignsFromFile(_finishedFile)
                : Enumerable.Empty<DesignItem>();

            foreach (var d in active)
                _rows.Add(d);

            foreach (var d in finished)
                _rows.Add(d);

            EnsureOrderingRules(); // also calls ApplyDateGrouping()
        }

        private void SaveAllToDiskAndRefreshUI()
        {
            _suppressWatchReload = true;
            try
            {
                var realRows = GetRealRowsSnapshot();

                var active = realRows.Where(d => !IsFinishedStatus(d.Status)).ToList();
                var finished = realRows.Where(d => IsFinishedStatus(d.Status)).ToList();

                WriteDesignsToFile(_activeFile, active);
                WriteDesignsToFile(_finishedFile, finished);
            }
            finally
            {
                _suppressWatchReload = false;
            }

            // Reload from disk so the UI always matches what was saved
            LoadAllFromDisk();
        }

        private static IEnumerable<DesignItem> ReadDesignsFromFile(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = line.Split(',');

                // Excel export may include an ID column
                int offset = cols.Length >= 16 ? 1 : 0;

                yield return new DesignItem
                {
                    Retail = cols.Length > offset + 0 ? cols[offset + 0] : string.Empty,
                    OE = cols.Length > offset + 1 ? cols[offset + 1] : string.Empty,
                    Customer = cols.Length > offset + 2 ? cols[offset + 2] : string.Empty,
                    SerialNumber = cols.Length > offset + 3 ? cols[offset + 3] : string.Empty,
                    DayDue = cols.Length > offset + 4 ? cols[offset + 4] : string.Empty,
                    DateDueText = cols.Length > offset + 5 ? cols[offset + 5] : string.Empty,
                    Status = cols.Length > offset + 6 ? cols[offset + 6] : string.Empty,
                    Qty = cols.Length > offset + 7 ? cols[offset + 7] : string.Empty,
                    WhatIsIt = cols.Length > offset + 8 ? cols[offset + 8] : string.Empty,
                    PO = cols.Length > offset + 9 ? cols[offset + 9] : string.Empty,
                    WhatAreWeDoing = cols.Length > offset + 10 ? cols[offset + 10] : string.Empty,
                    Parts = cols.Length > offset + 11 ? cols[offset + 11] : string.Empty,
                    ShaftType = cols.Length > offset + 12 ? cols[offset + 12] : string.Empty,
                    Priority = cols.Length > offset + 13 ? cols[offset + 13] : string.Empty,
                    LastUser = cols.Length > offset + 14 ? cols[offset + 14] : string.Empty
                };
            }
        }

        private static void WriteDesignsToFile(string path, IEnumerable<DesignItem> designs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var lines = designs.Select(d =>
                string.Join(",",
                    d.Retail ?? string.Empty,
                    d.OE ?? string.Empty,
                    d.Customer ?? string.Empty,
                    d.SerialNumber ?? string.Empty,
                    d.DayDue ?? string.Empty,
                    d.DateDueText ?? string.Empty,
                    d.Status ?? string.Empty,
                    d.Qty ?? string.Empty,
                    d.WhatIsIt ?? string.Empty,
                    d.PO ?? string.Empty,
                    d.WhatAreWeDoing ?? string.Empty,
                    d.Parts ?? string.Empty,
                    d.ShaftType ?? string.Empty,
                    d.Priority ?? string.Empty,
                    d.LastUser ?? string.Empty));

            File.WriteAllLines(path, lines);
        }

        private void SetupWatchers()
        {
            _activeWatcher?.Dispose();
            _finishedWatcher?.Dispose();

            string folder = Path.GetDirectoryName(_activeFile) ?? ".";

            _activeWatcher = new FileSystemWatcher(folder)
            {
                Filter = Path.GetFileName(_activeFile),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _activeWatcher.Changed += FileWatcher_Changed;
            _activeWatcher.EnableRaisingEvents = true;

            _finishedWatcher = new FileSystemWatcher(folder)
            {
                Filter = Path.GetFileName(_finishedFile),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _finishedWatcher.Changed += FileWatcher_Changed;
            _finishedWatcher.EnableRaisingEvents = true;
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (_suppressWatchReload) return;

            Dispatcher.Invoke(() =>
            {
                try { LoadAllFromDisk(); }
                catch { /* ignore transient locks */ }
            });
        }

        // ----------------------------
        // Ordering + Grouping
        // ----------------------------

        private static DateTime ParseDate(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, out var dt))
                return dt;

            return DateTime.MaxValue;
        }

        private void EnsureOrderingRules()
        {
            // normalize (no spacers) then rebuild list in correct order
            var realRows = _rows.Where(r => !r.IsSpacer).ToList();

            var top = realRows.Where(r => IsPaintShop(r.Status)).ToList();
            var bottom = realRows.Where(r => IsFinishedStatus(r.Status)).ToList();

            var middle = realRows.Except(top).Except(bottom)
                .OrderBy(r => ParseDate(r.DateDueText))
                .ToList();

            _rows.Clear();
            foreach (var r in top) _rows.Add(r);
            foreach (var r in middle) _rows.Add(r);
            foreach (var r in bottom) _rows.Add(r);

            ApplyDateGrouping(); // add the single spacer after each group
        }

        // ✅ One spacer row AFTER each group (your request)
        private void ApplyDateGrouping()
        {
            var real = _rows.Where(r => !r.IsSpacer).ToList();
            _rows.Clear();

            if (real.Count == 0) return;

            var byDate = real
                .OrderBy(r => ParseDate(r.DateDueText))
                .GroupBy(r => r.DateDueText ?? string.Empty);

            foreach (var group in byDate)
            {
                foreach (var row in group)
                    _rows.Add(row);

                _rows.Add(new DesignItem { IsSpacer = true, DateDueText = group.Key });
            }
        }

        // ----------------------------
        // DataGrid handlers
        // ----------------------------

        private void DesignGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is not DesignItem item)
                return;

            // Spacer rows can be edited visually, but we never save them.
            if (item.IsSpacer)
                return;

            item.LastUser = AppConfig.CurrentUserName;

            // If user changed status to Picked Up/Cancelled, it must go to finished file
            // If user changed to Paint Shop, it must bubble to top
            if (IsFinishedStatus(item.Status) || IsPaintShop(item.Status))
            {
                // Save -> split -> write -> reload
                SaveAllToDiskAndRefreshUI();
            }
            else
            {
                // For normal edits, still save and refresh to keep grouping/order correct
                SaveAllToDiskAndRefreshUI();
            }
        }

        private void DesignGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Keep our custom ordering rules
            e.Handled = true;
            EnsureOrderingRules();
        }

        private void DesignGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // You asked to remove the no-edit feature: allow interaction on all rows
            e.Row.IsHitTestVisible = true;
            e.Row.IsEnabled = true;
        }

        // ----------------------------
        // Context menu row ops
        // ----------------------------

        private DesignItem? GetRowItemFromContext(object sender)
        {
            if (sender is not MenuItem mi) return null;

            if (mi.DataContext is DesignItem ctxItem) return ctxItem;
            if (mi.CommandParameter is DesignItem paramItem) return paramItem;

            return DesignGrid.SelectedItem as DesignItem;
        }

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null) return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = 0;

            _rows.Insert(index, new DesignItem
            {
                DayDue = target.DayDue,
                DateDueText = target.DateDueText,
                Status = target.Status,
                LastUser = AppConfig.CurrentUserName
            });

            SaveAllToDiskAndRefreshUI();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null) return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = _rows.Count - 1;

            _rows.Insert(index + 1, new DesignItem
            {
                DayDue = target.DayDue,
                DateDueText = target.DateDueText,
                Status = target.Status,
                LastUser = AppConfig.CurrentUserName
            });

            SaveAllToDiskAndRefreshUI();
        }

        private void DuplicateRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null) return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = _rows.Count - 1;

            var clone = target.Clone();
            clone.LastUser = AppConfig.CurrentUserName;

            _rows.Insert(index + 1, clone);

            SaveAllToDiskAndRefreshUI();
        }

        // ----------------------------
        // Toolbar / window handlers
        // ----------------------------

        private void ViewSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            string size = (e.AddedItems.Count > 0 ? e.AddedItems[0]?.ToString() : null) ?? "Standard";

            double fontSize = size switch
            {
                "Medium" => 14,
                "Large" => 16,
                _ => 12
            };

            DesignGrid.FontSize = fontSize;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                SaveAllToDiskAndRefreshUI();
                AppConfig.SaveSettings();
            }
            catch { }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e) => Window_Closing(sender, e);

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Reload_Click(object sender, RoutedEventArgs e) => LoadAllFromDisk();

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var selected = DesignGrid.SelectedItem as DesignItem;

            if (selected != null)
            {
                int index = _rows.IndexOf(selected);
                if (index < 0) index = _rows.Count - 1;

                _rows.Insert(index + 1, new DesignItem
                {
                    DayDue = selected.DayDue,
                    DateDueText = selected.DateDueText,
                    Status = selected.Status,
                    LastUser = AppConfig.CurrentUserName
                });
            }
            else
            {
                _rows.Add(new DesignItem { LastUser = AppConfig.CurrentUserName });
            }

            SaveAllToDiskAndRefreshUI();
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var selected = DesignGrid.SelectedItem as DesignItem;
            if (selected == null) return;

            if (!selected.IsSpacer)
                _rows.Remove(selected);

            SaveAllToDiskAndRefreshUI();
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveAllToDiskAndRefreshUI();

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var users = AppConfig.LoadUsers();
                var dlg = new ChangePasswordWindow(AppConfig.CurrentUserName, users) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    AppConfig.SaveUsers(users);
                }
            }
            catch
            {
                MessageBox.Show("Unable to open Change Password window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var view = CollectionViewSource.GetDefaultView(DesignGrid.ItemsSource);
            if (sender is not TextBox tb || view == null) return;

            string term = tb.Text.Trim();
            if (string.IsNullOrEmpty(term))
            {
                view.Filter = null;
                return;
            }

            term = term.ToLowerInvariant();

            view.Filter = o =>
            {
                if (o is not DesignItem d) return false;

                bool Match(string? s) => (s ?? "").ToLowerInvariant().Contains(term);

                return Match(d.Retail) ||
                       Match(d.OE) ||
                       Match(d.Customer) ||
                       Match(d.SerialNumber) ||
                       Match(d.DayDue) ||
                       Match(d.DateDueText) ||
                       Match(d.Status) ||
                       Match(d.Qty) ||
                       Match(d.WhatIsIt) ||
                       Match(d.PO) ||
                       Match(d.WhatAreWeDoing) ||
                       Match(d.Parts) ||
                       Match(d.ShaftType) ||
                       Match(d.Priority) ||
                       Match(d.LastUser);
            };
        }

        private void DesignGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
        private void DesignGrid_MouseMove(object sender, MouseEventArgs e) { }
        private void DesignGrid_Drop(object sender, DragEventArgs e) { }

        private void DesignGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row) row.IsSelected = true;
        }
    }
}
