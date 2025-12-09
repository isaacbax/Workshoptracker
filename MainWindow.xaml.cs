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
using System.Windows.Media;
using System.Windows.Threading;

namespace DesignSheet
{
    public partial class MainWindow : Window
    {
        // Two collections (two tabs)
        private readonly ObservableCollection<DesignItem> _activeRows = new();
        private readonly ObservableCollection<DesignItem> _finishedRows = new();

        private readonly string _branch;
        private readonly string _activeFile;
        private readonly string _finishedFile;

        // Watchers (refresh when other users update files)
        private FileSystemWatcher? _activeWatcher;
        private FileSystemWatcher? _finishedWatcher;
        private bool _suppressWatchReload;

        // Debounce refresh (avoids multiple reloads per write)
        private readonly DispatcherTimer _reloadDebounceTimer;

        // Auto-save debounce (prevents double-save loops)
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _pendingAutoSave;

        // Drag + drop
        private const string DragFormat = "DesignSheet.DesignItem";
        private DesignItem? _dragItem;

        public MainWindow()
        {
            _branch = string.IsNullOrWhiteSpace(AppConfig.CurrentBranch) ? "headoffice" : AppConfig.CurrentBranch;

            string baseFolder = string.IsNullOrWhiteSpace(AppConfig.BaseFolder)
                ? @"S:\Public\DesignData"
                : AppConfig.BaseFolder;

            AppConfig.BaseFolder = baseFolder;

            InitializeComponent();

            _activeFile = Path.Combine(baseFolder, $"{_branch}.csv");
            _finishedFile = Path.Combine(baseFolder, $"{_branch}finished.csv");

            // Bind both grids
            DesignGrid.ItemsSource = _activeRows;
            FinishedGrid.ItemsSource = _finishedRows;

            _reloadDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _reloadDebounceTimer.Tick += (_, __) =>
            {
                _reloadDebounceTimer.Stop();
                if (_suppressWatchReload) return;
                SafeReloadFromDisk();
            };

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _autoSaveTimer.Tick += (_, __) =>
            {
                _autoSaveTimer.Stop();
                if (!_pendingAutoSave) return;
                _pendingAutoSave = false;

                SaveAllToDisk();
                SafeReloadFromDisk(); // refresh view after autosave
            };

            LoadAllFromDisk();
            SetupWatchers();
        }

        // ----------------------------
        // Helpers
        // ----------------------------

        private static bool IsFinishedStatus(string? status) =>
            string.Equals(status?.Trim(), "Picked Up", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase);

        private static bool IsPaintShop(string? status) =>
            string.Equals(status?.Trim(), "Paint Shop", StringComparison.OrdinalIgnoreCase);

        private static DateTime ParseDate(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text) && DateTime.TryParse(text, out var dt))
                return dt;
            return DateTime.MaxValue;
        }

        private void SafeReloadFromDisk()
        {
            try
            {
                LoadAllFromDisk();
            }
            catch
            {
                // ignore transient file locks
            }
        }

        // ----------------------------
        // Load/Save
        // ----------------------------

        private void LoadAllFromDisk()
        {
            _activeRows.Clear();
            _finishedRows.Clear();

            var active = File.Exists(_activeFile) ? ReadDesignsFromFile(_activeFile) : Enumerable.Empty<DesignItem>();
            var finished = File.Exists(_finishedFile) ? ReadDesignsFromFile(_finishedFile) : Enumerable.Empty<DesignItem>();

            foreach (var d in active) _activeRows.Add(d);
            foreach (var d in finished) _finishedRows.Add(d);

            EnsureOrderingRules(_activeRows);
            EnsureFinishedOrder(_finishedRows);

            ApplySearchFilter(SearchTextBox?.Text);
        }

        private void SaveAllToDisk()
        {
            _suppressWatchReload = true;
            try
            {
                var activeReal = _activeRows.Where(r => !r.IsSpacer).Select(r => r.Clone()).ToList();
                var finishedReal = _finishedRows.Where(r => !r.IsSpacer).Select(r => r.Clone()).ToList();

                // Move finished from active -> finished
                var moveToFinished = activeReal.Where(r => IsFinishedStatus(r.Status)).ToList();
                if (moveToFinished.Count > 0)
                {
                    activeReal = activeReal.Where(r => !IsFinishedStatus(r.Status)).ToList();
                    finishedReal.AddRange(moveToFinished);
                }

                // Move non-finished from finished -> active
                var moveBackToActive = finishedReal.Where(r => !IsFinishedStatus(r.Status)).ToList();
                if (moveBackToActive.Count > 0)
                {
                    finishedReal = finishedReal.Where(r => IsFinishedStatus(r.Status)).ToList();
                    activeReal.AddRange(moveBackToActive);
                }

                WriteDesignsToFile(_activeFile, activeReal);
                WriteDesignsToFile(_finishedFile, finishedReal);
            }
            finally
            {
                // Let watchers reload again shortly (covers network shares)
                _suppressWatchReload = false;
            }
        }

        private static IEnumerable<DesignItem> ReadDesignsFromFile(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = line.Split(',');

                // tolerate an extra leading column in older CSVs
                int offset = cols.Length >= 16 ? 1 : 0;

                yield return new DesignItem
                {
                    Retail = cols.Length > offset + 0 ? cols[offset + 0] : "",
                    OE = cols.Length > offset + 1 ? cols[offset + 1] : "",
                    Customer = cols.Length > offset + 2 ? cols[offset + 2] : "",
                    SerialNumber = cols.Length > offset + 3 ? cols[offset + 3] : "",
                    DayDue = cols.Length > offset + 4 ? cols[offset + 4] : "",
                    DateDueText = cols.Length > offset + 5 ? cols[offset + 5] : "",
                    Status = cols.Length > offset + 6 ? cols[offset + 6] : "",
                    Qty = cols.Length > offset + 7 ? cols[offset + 7] : "",
                    WhatIsIt = cols.Length > offset + 8 ? cols[offset + 8] : "",
                    PO = cols.Length > offset + 9 ? cols[offset + 9] : "",
                    WhatAreWeDoing = cols.Length > offset + 10 ? cols[offset + 10] : "",
                    Parts = cols.Length > offset + 11 ? cols[offset + 11] : "",
                    ShaftType = cols.Length > offset + 12 ? cols[offset + 12] : "",
                    Priority = cols.Length > offset + 13 ? cols[offset + 13] : "",
                    LastUser = cols.Length > offset + 14 ? cols[offset + 14] : ""
                };
            }
        }

        private static void WriteDesignsToFile(string path, IEnumerable<DesignItem> designs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var lines = designs.Select(d =>
                string.Join(",",
                    d.Retail ?? "",
                    d.OE ?? "",
                    d.Customer ?? "",
                    d.SerialNumber ?? "",
                    d.DayDue ?? "",
                    d.DateDueText ?? "",
                    d.Status ?? "",
                    d.Qty ?? "",
                    d.WhatIsIt ?? "",
                    d.PO ?? "",
                    d.WhatAreWeDoing ?? "",
                    d.Parts ?? "",
                    d.ShaftType ?? "",
                    d.Priority ?? "",
                    d.LastUser ?? ""
                ));

            File.WriteAllLines(path, lines);
        }

        // ----------------------------
        // Watchers (refresh for other users)
        // ----------------------------

        private void SetupWatchers()
        {
            _activeWatcher?.Dispose();
            _finishedWatcher?.Dispose();

            var folder = Path.GetDirectoryName(_activeFile) ?? ".";

            _activeWatcher = new FileSystemWatcher(folder)
            {
                Filter = Path.GetFileName(_activeFile),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _activeWatcher.Changed += FileWatcher_Changed;
            _activeWatcher.Created += FileWatcher_Changed;
            _activeWatcher.Renamed += FileWatcher_Changed;
            _activeWatcher.EnableRaisingEvents = true;

            _finishedWatcher = new FileSystemWatcher(folder)
            {
                Filter = Path.GetFileName(_finishedFile),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _finishedWatcher.Changed += FileWatcher_Changed;
            _finishedWatcher.Created += FileWatcher_Changed;
            _finishedWatcher.Renamed += FileWatcher_Changed;
            _finishedWatcher.EnableRaisingEvents = true;
        }

        private void FileWatcher_Changed(object? sender, FileSystemEventArgs e)
        {
            if (_suppressWatchReload) return;

            Dispatcher.Invoke(() =>
            {
                // debounce reload
                _reloadDebounceTimer.Stop();
                _reloadDebounceTimer.Start();
            });
        }

        // ----------------------------
        // Ordering rules
        // ----------------------------

        private static void EnsureOrderingRules(ObservableCollection<DesignItem> list)
        {
            var real = list.Where(r => !r.IsSpacer).ToList();

            var top = real.Where(r => IsPaintShop(r.Status)).ToList();
            var middle = real.Where(r => !IsPaintShop(r.Status) && !IsFinishedStatus(r.Status))
                             .OrderBy(r => ParseDate(r.DateDueText))
                             .ToList();

            var ordered = new List<DesignItem>();
            ordered.AddRange(top);
            ordered.AddRange(middle);

            list.Clear();

            // One spacer after each DateDue group
            var groups = ordered.GroupBy(r => r.DateDueText ?? string.Empty)
                                .OrderBy(g => ParseDate(g.Key));

            foreach (var g in groups)
            {
                foreach (var row in g) list.Add(row);
                list.Add(new DesignItem { IsSpacer = true, DateDueText = g.Key });
            }
        }

        private static void EnsureFinishedOrder(ObservableCollection<DesignItem> list)
        {
            var real = list.Where(r => !r.IsSpacer)
                           .OrderByDescending(r => ParseDate(r.DateDueText))
                           .ToList();

            list.Clear();
            foreach (var r in real) list.Add(r);
        }

        // ----------------------------
        // Window chrome
        // ----------------------------

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                SaveAllToDisk();
                AppConfig.SaveSettings();
            }
            catch { }
        }

        // ----------------------------
        // Toolbar
        // ----------------------------

        private void Reload_Click(object sender, RoutedEventArgs e) => SafeReloadFromDisk();

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAllToDisk();
            SafeReloadFromDisk();
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var selected = DesignGrid.SelectedItem as DesignItem;
            int index = selected == null ? _activeRows.Count : _activeRows.IndexOf(selected);
            if (index < 0) index = _activeRows.Count;

            var newItem = new DesignItem
            {
                DateDueText = selected?.IsSpacer == false ? selected.DateDueText : "",
                DayDue = selected?.IsSpacer == false ? selected.DayDue : "",
                LastUser = AppConfig.CurrentUserName
            };

            if (selected?.IsSpacer == true) index = Math.Max(0, index);

            _activeRows.Insert(index, newItem);

            SaveAllToDisk();
            SafeReloadFromDisk();
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (DesignGrid.IsKeyboardFocusWithin)
            {
                var selected = DesignGrid.SelectedItem as DesignItem;
                if (selected == null || selected.IsSpacer) return;
                _activeRows.Remove(selected);
            }
            else if (FinishedGrid.IsKeyboardFocusWithin)
            {
                var selected = FinishedGrid.SelectedItem as DesignItem;
                if (selected == null || selected.IsSpacer) return;
                _finishedRows.Remove(selected);
            }
            else
            {
                var selected = DesignGrid.SelectedItem as DesignItem;
                if (selected == null || selected.IsSpacer) return;
                _activeRows.Remove(selected);
            }

            SaveAllToDisk();
            SafeReloadFromDisk();
        }

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

        // Font size changer
        private void ViewSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            string label = "";
            if (e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0] is ComboBoxItem cbi) label = cbi.Content?.ToString() ?? "";
                else label = e.AddedItems[0]?.ToString() ?? "";
            }

            double size = label switch
            {
                "Medium" => 14,
                "Large" => 16,
                _ => 12
            };

            DesignGrid.FontSize = size;
            FinishedGrid.FontSize = size;
        }

        // ----------------------------
        // Search (both tabs)
        // ----------------------------

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter((sender as TextBox)?.Text);
        }

        private void ApplySearchFilter(string? raw)
        {
            string term = (raw ?? "").Trim().ToLowerInvariant();

            Predicate<object> filter = o =>
            {
                if (o is not DesignItem d) return false;
                if (d.IsSpacer) return true;
                if (string.IsNullOrWhiteSpace(term)) return true;

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

            var view1 = CollectionViewSource.GetDefaultView(DesignGrid.ItemsSource);
            if (view1 != null) view1.Filter = o => filter(o);

            var view2 = CollectionViewSource.GetDefaultView(FinishedGrid.ItemsSource);
            if (view2 != null) view2.Filter = o => filter(o);
        }

        // ----------------------------
        // Auto-save hooks
        // ----------------------------

        // Fires when edit is ending (e.g., leaving a cell)
        private void DesignGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is not DesignItem item) return;
            if (item.IsSpacer) return;

            item.LastUser = AppConfig.CurrentUserName;

            QueueAutoSave();
        }

        // Fires when the current cell changes (covers dropdown selection changes reliably)
        private void DesignGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            // This catches combo box edits + some text edits when focus moves.
            QueueAutoSave();
        }

        private void QueueAutoSave()
        {
            if (_suppressWatchReload) return;

            _pendingAutoSave = true;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void DesignGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.IsEnabled = true;
            e.Row.IsHitTestVisible = true;
        }

        private void DesignGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            EnsureOrderingRules(_activeRows);
            EnsureFinishedOrder(_finishedRows);
        }

        // ----------------------------
        // Right click support
        // ----------------------------

        private void DesignGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            var row = FindVisualParent<DataGridRow>(dep);
            if (row != null)
            {
                row.IsSelected = true;
                DesignGrid.SelectedItem = row.Item;
            }
        }

        private void DesignGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
                row.IsSelected = true;
        }

        private DesignItem? GetContextRow(object sender)
        {
            if (sender is MenuItem mi)
            {
                if (mi.DataContext is DesignItem di) return di;

                if (mi.Parent is ContextMenu cm &&
                    cm.PlacementTarget is FrameworkElement fe &&
                    fe.DataContext is DesignItem di2)
                    return di2;
            }

            return DesignGrid.SelectedItem as DesignItem;
        }

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var target = GetContextRow(sender);
            if (target == null || target.IsSpacer) return;

            int idx = _activeRows.IndexOf(target);
            if (idx < 0) idx = 0;

            _activeRows.Insert(idx, new DesignItem
            {
                DateDueText = target.DateDueText,
                DayDue = target.DayDue,
                Status = target.Status,
                LastUser = AppConfig.CurrentUserName
            });

            QueueAutoSave();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetContextRow(sender);
            if (target == null || target.IsSpacer) return;

            int idx = _activeRows.IndexOf(target);
            if (idx < 0) idx = _activeRows.Count - 1;

            _activeRows.Insert(idx + 1, new DesignItem
            {
                DateDueText = target.DateDueText,
                DayDue = target.DayDue,
                Status = target.Status,
                LastUser = AppConfig.CurrentUserName
            });

            QueueAutoSave();
        }

        private void DuplicateRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetContextRow(sender);
            if (target == null || target.IsSpacer) return;

            int idx = _activeRows.IndexOf(target);
            if (idx < 0) idx = _activeRows.Count - 1;

            var clone = target.Clone();
            clone.LastUser = AppConfig.CurrentUserName;

            _activeRows.Insert(idx + 1, clone);

            QueueAutoSave();
        }

        // ----------------------------
        // Drag & drop reorder (Active)
        // ----------------------------

        private void DragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not DesignItem item) return;
            if (item.IsSpacer) return;

            _dragItem = item;
            DesignGrid.SelectedItem = item;

            DragDrop.DoDragDrop(DesignGrid, new DataObject(DragFormat, item), DragDropEffects.Move);
            e.Handled = true;
        }

        private void DesignGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DragFormat)) return;
            if (e.Data.GetData(DragFormat) is not DesignItem dragged) return;
            if (_dragItem == null) return;

            var dep = e.OriginalSource as DependencyObject;
            var row = FindVisualParent<DataGridRow>(dep);
            if (row?.Item is not DesignItem target) return;
            if (target.IsSpacer) return;
            if (ReferenceEquals(dragged, target)) return;

            var real = _activeRows.Where(r => !r.IsSpacer).ToList();
            int oldIndex = real.IndexOf(dragged);
            int newIndex = real.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0) return;

            real.RemoveAt(oldIndex);
            if (newIndex > oldIndex) newIndex--;
            real.Insert(newIndex, dragged);

            _activeRows.Clear();
            foreach (var r in real) _activeRows.Add(r);

            EnsureOrderingRules(_activeRows);

            QueueAutoSave();
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
