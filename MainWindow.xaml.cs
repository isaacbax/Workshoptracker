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
            // If the login window sets these, use them. Otherwise fall back.
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

        #region FILE I/O

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
            {
                // Ensure finished still at bottom even after manual edits
                if (!string.Equals(d.Status, "Picked Up", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(d.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    d.Status = "Picked Up";
                }
                _rows.Add(d);
            }

            ApplyDateGrouping();
            EnsureOrderingRules();
        }

        private void SaveAllToDisk()
        {
            _suppressWatchReload = true;
            try
            {
                // Strip spacer rows
                var realRows = _rows.Where(r => !r.IsSpacer).ToList();

                var active = realRows.Where(d =>
                    !string.Equals(d.Status, "Picked Up", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(d.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

                var finished = realRows.Where(d =>
                    string.Equals(d.Status, "Picked Up", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(d.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

                WriteDesignsToFile(_activeFile, active);
                WriteDesignsToFile(_finishedFile, finished);
            }
            finally
            {
                _suppressWatchReload = false;
            }
        }

        // *** NEW: handles optional ID column and our header layout ***
        private static IEnumerable<DesignItem> ReadDesignsFromFile(string path)
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cols = line.Split(',');

                // Existing Excel sheet: ID + 15 columns = 16.
                // Files written by this app: just 15 columns.
                int offset = cols.Length >= 16 ? 1 : 0;

                var item = new DesignItem
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

                yield return item;
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
            if (_suppressWatchReload)
                return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    LoadAllFromDisk();
                }
                catch
                {
                    // ignore – usually transient file lock
                }
            });
        }

        #endregion

        #region ROW / ORDER LOGIC

        /// <summary>
        /// Keeps Paint Shop at top, Picked Up / Cancelled at bottom.
        /// </summary>
        private void EnsureOrderingRules()
        {
            var realRows = _rows.Where(r => !r.IsSpacer).ToList();

            // Paint Shop to top, then everything else by DateDue, then Picked Up / Cancelled at bottom.
            var top = realRows
                .Where(r => string.Equals(r.Status, "Paint Shop", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var bottom = realRows
                .Where(r =>
                    string.Equals(r.Status, "Picked Up", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var middle = realRows.Except(top).Except(bottom)
                .OrderBy(r => ParseDate(r.DateDueText))
                .ToList();

            _rows.Clear();
            foreach (var r in top) _rows.Add(r);
            foreach (var r in middle) _rows.Add(r);
            foreach (var r in bottom) _rows.Add(r);

            ApplyDateGrouping();
        }

        private static DateTime ParseDate(string text)
        {
            if (DateTime.TryParse(text, out var dt))
                return dt;
            return DateTime.MaxValue;
        }

        /// <summary>
        /// Inserts non-editable spacer rows above and below each date block.
        /// </summary>
        private void ApplyDateGrouping()
        {
            // Remove existing spacers
            var real = _rows.Where(r => !r.IsSpacer).ToList();
            _rows.Clear();

            if (real.Count == 0)
                return;

            var byDate = real
                .OrderBy(r => ParseDate(r.DateDueText))
                .GroupBy(r => r.DateDueText);

            foreach (var group in byDate)
            {
                // Spacer above
                _rows.Add(new DesignItem { IsSpacer = true, DateDueText = group.Key });

                foreach (var row in group)
                    _rows.Add(row);

                // Spacer below
                _rows.Add(new DesignItem { IsSpacer = true, DateDueText = group.Key });
            }
        }

        #endregion

        #region DATAGRID EVENTS

        private void DesignGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is DesignItem item && !item.IsSpacer)
            {
                // track last user
                item.LastUser = AppConfig.CurrentUserName;

                // If status changed to finished, enforce ordering at bottom
                if (string.Equals(item.Status, "Picked Up", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureOrderingRules();
                }
                else if (string.Equals(item.Status, "Paint Shop", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureOrderingRules();
                }

                SaveAllToDisk();
            }
        }

        private void DesignGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // keep custom ordering instead of letting user sort arbitrarily
            e.Handled = true;
            EnsureOrderingRules();
        }

        private void DesignGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DesignItem item)
            {
                e.Row.IsHitTestVisible = !item.IsSpacer;
                e.Row.IsEnabled = !item.IsSpacer;
            }
        }

        #endregion

        #region CONTEXT MENU – ADD / DUPLICATE ROWS

        private DesignItem? GetRowItemFromContext(object sender)
        {
            if (sender is not MenuItem mi)
                return null;

            if (mi.DataContext is DesignItem ctxItem)
                return ctxItem;

            if (mi.CommandParameter is DesignItem paramItem)
                return paramItem;

            return DesignGrid.SelectedItem as DesignItem;
        }

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null || target.IsSpacer)
                return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = 0;

            var newItem = new DesignItem
            {
                DayDue = target.DayDue,
                DateDueText = target.DateDueText,
                Status = target.Status
            };

            _rows.Insert(index, newItem);
            ApplyDateGrouping();
            SaveAllToDisk();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null || target.IsSpacer)
                return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = _rows.Count - 1;

            var newItem = new DesignItem
            {
                DayDue = target.DayDue,
                DateDueText = target.DateDueText,
                Status = target.Status
            };

            _rows.Insert(index + 1, newItem);
            ApplyDateGrouping();
            SaveAllToDisk();
        }

        private void DuplicateRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var target = GetRowItemFromContext(sender);
            if (target == null || target.IsSpacer)
                return;

            int index = _rows.IndexOf(target);
            if (index < 0) index = _rows.Count - 1;

            var clone = new DesignItem
            {
                Retail = target.Retail,
                OE = target.OE,
                Customer = target.Customer,
                SerialNumber = target.SerialNumber,
                DayDue = target.DayDue,
                DateDueText = target.DateDueText,
                Status = target.Status,
                Qty = target.Qty,
                WhatIsIt = target.WhatIsIt,
                PO = target.PO,
                WhatAreWeDoing = target.WhatAreWeDoing,
                Parts = target.Parts,
                ShaftType = target.ShaftType,
                Priority = target.Priority,
                LastUser = AppConfig.CurrentUserName
            };

            _rows.Insert(index + 1, clone);
            ApplyDateGrouping();
            SaveAllToDisk();
        }

        #endregion

        #region WINDOW / VIEW SIZE

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

            // You can also persist this per user via AppConfig if you like
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                SaveAllToDisk();
                AppConfig.SaveSettings();
            }
            catch
            {
                // ignore
            }
        }

        #endregion
    }
}
