using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace DesignSheet
{
    public partial class MainWindow : Window
    {
        // CHANGE THIS to your shared drive / OneDrive-synced folder.
        private const string CsvRootFolder = @"S:\Public\DesignData";

        private const int FileRetryCount = 5;
        private const int FileRetryDelayMs = 200;

        private readonly string _currentUser;
        private readonly string _currentBranch;
        private readonly string _userViewSettingsPath;

        private string _activeCsvPath;
        private string _finishedCsvPath;

        private bool _pendingChanges;
        private bool _isSaving;

        private FileSystemWatcher _activeWatcher;
        private FileSystemWatcher _finishedWatcher;

        private DispatcherTimer _saveTimer;

        private DesignItem _draggedItem;
        private Point _dragStartPoint;

        public ObservableCollection<DesignItem> ActiveDesigns { get; } =
            new ObservableCollection<DesignItem>();

        public ObservableCollection<DesignItem> FinishedDesigns { get; } =
            new ObservableCollection<DesignItem>();

        private ICollectionView _activeView;
        private ICollectionView _finishedView;

        public MainWindow(string username, string branch)
        {
            InitializeComponent();

            _currentUser = username;
            _currentBranch = branch;

            // Per-user view settings file under %AppData%\DesignSheet\<user>_view.txt
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsFolder = Path.Combine(appData, "DesignSheet");
            Directory.CreateDirectory(settingsFolder);
            _userViewSettingsPath = Path.Combine(settingsFolder, $"{_currentUser}_view.txt");

            DataContext = this;

            _activeView = CollectionViewSource.GetDefaultView(ActiveDesigns);
            _activeView.Filter = FilterDesigns;

            _finishedView = CollectionViewSource.GetDefaultView(FinishedDesigns);
            _finishedView.Filter = FilterDesigns;

            if (!Directory.Exists(CsvRootFolder))
            {
                Directory.CreateDirectory(CsvRootFolder);
            }

            _activeCsvPath = Path.Combine(CsvRootFolder, $"{_currentBranch}.csv");
            _finishedCsvPath = Path.Combine(CsvRootFolder, $"{_currentBranch}finished.csv");

            LoadAllFromDisk();
            StartWatchers();

            // Load saved view (column widths/order/sort) for this user (Active grid only)
            LoadUserViewSettings();

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _saveTimer.Tick += SaveTimer_Tick;
            _saveTimer.Start();
        }

        #region Safe file IO

        private static List<string> SafeReadAllLines(string path)
        {
            for (int attempt = 0; attempt < FileRetryCount; attempt++)
            {
                try
                {
                    var lines = new List<string>();
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            lines.Add(line);
                        }
                    }

                    return lines;
                }
                catch (IOException)
                {
                    if (attempt == FileRetryCount - 1)
                        throw;
                    System.Threading.Thread.Sleep(FileRetryDelayMs);
                }
            }

            return new List<string>();
        }

        private static void SafeWriteAllLines(string path, IEnumerable<string> lines)
        {
            string tempPath = path + ".tmp";

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }
            }

            for (int attempt = 0; attempt < FileRetryCount; attempt++)
            {
                try
                {
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                    break;
                }
                catch (IOException)
                {
                    if (attempt == FileRetryCount - 1)
                        throw;
                    System.Threading.Thread.Sleep(FileRetryDelayMs);
                }
            }
        }

        #endregion

        #region Load / Save both CSVs

        private void EnsureFilesExist()
        {
            if (!File.Exists(_activeCsvPath))
            {
                SafeWriteAllLines(_activeCsvPath, new[] { DesignItem.CsvHeader });
            }

            if (!File.Exists(_finishedCsvPath))
            {
                SafeWriteAllLines(_finishedCsvPath, new[] { DesignItem.CsvHeader });
            }
        }

        private void LoadAllFromDisk()
        {
            try
            {
                EnsureFilesExist();
            }
            catch (IOException)
            {
                // If we can't create them right now, just bail.
                return;
            }

            var allItems = new List<DesignItem>();

            try
            {
                // Read ACTIVE
                var activeLines = SafeReadAllLines(_activeCsvPath);
                if (activeLines.Count > 1)
                {
                    foreach (var line in activeLines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        allItems.Add(DesignItem.FromCsvLine(line));
                    }
                }

                // Read FINISHED
                var finishedLines = SafeReadAllLines(_finishedCsvPath);
                if (finishedLines.Count > 1)
                {
                    foreach (var line in finishedLines.Skip(1))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        allItems.Add(DesignItem.FromCsvLine(line));
                    }
                }
            }
            catch (IOException)
            {
                MessageBox.Show("The CSV files are currently in use and could not be read.",
                    "File in use",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ActiveDesigns.Clear();
            FinishedDesigns.Clear();

            foreach (var item in allItems)
            {
                if (IsTerminalStatus(item.Status))
                    FinishedDesigns.Add(item);
                else
                    ActiveDesigns.Add(item);
            }

            _activeView?.Refresh();
            _finishedView?.Refresh();

            // Normalize actual files to this partition
            try
            {
                SaveAllToDisk();
            }
            catch (IOException)
            {
                // ignore here
            }
        }

        private void SaveAllToDisk()
        {
            _isSaving = true;
            try
            {
                var activeLines = new List<string> { DesignItem.CsvHeader };
                foreach (var d in ActiveDesigns)
                {
                    activeLines.Add(d.ToCsvRow());
                }

                var finishedLines = new List<string> { DesignItem.CsvHeader };
                foreach (var d in FinishedDesigns)
                {
                    finishedLines.Add(d.ToCsvRow());
                }

                SafeWriteAllLines(_activeCsvPath, activeLines);
                SafeWriteAllLines(_finishedCsvPath, finishedLines);
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void SaveTimer_Tick(object sender, EventArgs e)
        {
            if (_pendingChanges)
            {
                try
                {
                    SaveAllToDisk();
                    _pendingChanges = false;
                }
                catch (IOException)
                {
                    // try again next tick
                }
            }
        }

        private void MarkDirty()
        {
            _pendingChanges = true;
        }

        #endregion

        #region File watchers

        private void StartWatchers()
        {
            StopWatchers();

            var activeDir = Path.GetDirectoryName(_activeCsvPath);
            var activeFile = Path.GetFileName(_activeCsvPath);

            var finishedDir = Path.GetDirectoryName(_finishedCsvPath);
            var finishedFile = Path.GetFileName(_finishedCsvPath);

            if (!string.IsNullOrEmpty(activeDir) && !string.IsNullOrEmpty(activeFile))
            {
                _activeWatcher = new FileSystemWatcher(activeDir, activeFile)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _activeWatcher.Changed += CsvWatcherOnChanged;
                _activeWatcher.Renamed += CsvWatcherOnChanged;
            }

            if (!string.IsNullOrEmpty(finishedDir) && !string.IsNullOrEmpty(finishedFile))
            {
                _finishedWatcher = new FileSystemWatcher(finishedDir, finishedFile)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _finishedWatcher.Changed += CsvWatcherOnChanged;
                _finishedWatcher.Renamed += CsvWatcherOnChanged;
            }
        }

        private void StopWatchers()
        {
            if (_activeWatcher != null)
            {
                _activeWatcher.EnableRaisingEvents = false;
                _activeWatcher.Changed -= CsvWatcherOnChanged;
                _activeWatcher.Renamed -= CsvWatcherOnChanged;
                _activeWatcher.Dispose();
                _activeWatcher = null;
            }

            if (_finishedWatcher != null)
            {
                _finishedWatcher.EnableRaisingEvents = false;
                _finishedWatcher.Changed -= CsvWatcherOnChanged;
                _finishedWatcher.Renamed -= CsvWatcherOnChanged;
                _finishedWatcher.Dispose();
                _finishedWatcher = null;
            }
        }

        private void CsvWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            if (_isSaving) return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    LoadAllFromDisk();
                }
                catch (IOException)
                {
                    // ignore one-off
                }
            });
        }

        #endregion

        #region Search / Filter

        private bool FilterDesigns(object obj)
        {
            var d = obj as DesignItem;
            if (d == null) return false;

            var text = SearchTextBox != null ? SearchTextBox.Text : null;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.ToLowerInvariant();

            bool Contains(string s) =>
                !string.IsNullOrEmpty(s) && s.ToLowerInvariant().Contains(text);

            if (Contains(d.Retail)) return true;
            if (Contains(d.OE)) return true;
            if (Contains(d.Customer)) return true;
            if (Contains(d.SerialNumber)) return true;
            if (Contains(d.DayDue)) return true;
            if (Contains(d.Status)) return true;
            if (Contains(d.WhatIsIt)) return true;
            if (Contains(d.PO)) return true;
            if (Contains(d.WhatAreWeDoing)) return true;
            if (Contains(d.Parts)) return true;
            if (Contains(d.ShaftType)) return true;
            if (Contains(d.Priority)) return true;
            if (Contains(d.LastUser)) return true;

            if (d.Qty.ToString(CultureInfo.InvariantCulture).Contains(text))
                return true;

            if (d.DateDue != default(DateTime) &&
                d.DateDue.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToLowerInvariant()
                    .Contains(text))
                return true;

            return false;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _activeView?.Refresh();
            _finishedView?.Refresh();
        }

        #endregion

        #region Status helpers

        private static bool IsTerminalStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.Trim().ToLowerInvariant();
            return s == "cancelled" || s == "picked up";
        }

        #endregion

        #region Window / buttons

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox != null)
            {
                SearchTextBox.Text = string.Empty;
            }

            LoadAllFromDisk();

            _activeView?.Refresh();
            _finishedView?.Refresh();
        }

        private void AddRow_Click(object sender, RoutedEventArgs e)
        {
            var item = new DesignItem
            {
                Retail = "N",
                OE = "N",
                Customer = "",
                SerialNumber = "",
                DayDue = DateTime.Today.DayOfWeek.ToString(),
                DateDue = DateTime.Today,
                Status = "Quote",
                Qty = 1,
                WhatIsIt = "",
                PO = "",
                WhatAreWeDoing = "",
                Parts = "",
                ShaftType = "Domestic",
                Priority = "N",
                LastUser = _currentUser
            };

            ActiveDesigns.Add(item);
            MarkDirty();
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            // Delete from whichever grid has selection
            var activeSelected = DesignGrid.SelectedItems.Cast<DesignItem>().ToList();
            var finishedSelected = FinishedGrid.SelectedItems.Cast<DesignItem>().ToList();

            if (activeSelected.Count == 0 && finishedSelected.Count == 0) return;

            foreach (var d in activeSelected)
            {
                ActiveDesigns.Remove(d);
            }

            foreach (var d in finishedSelected)
            {
                FinishedDesigns.Remove(d);
            }

            MarkDirty();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAllToDisk();
            _pendingChanges = false;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region Drag & drop row reordering (Active grid only)

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;

                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void DesignGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);

            var row = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            _draggedItem = row != null ? row.Item as DesignItem : null;
        }

        private void DesignGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_draggedItem == null) return;

            var currentPos = e.GetPosition(null);
            if (Math.Abs(currentPos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            DragDrop.DoDragDrop(DesignGrid, _draggedItem, DragDropEffects.Move);
        }

        private void DesignGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(DesignItem)))
                return;

            var dropped = e.Data.GetData(typeof(DesignItem)) as DesignItem;
            var targetRow = FindAncestor<DataGridRow>((DependencyObject)e.OriginalSource);
            var target = targetRow != null ? targetRow.Item as DesignItem : null;

            if (dropped == null || target == null || dropped == target)
                return;

            int oldIndex = ActiveDesigns.IndexOf(dropped);
            int newIndex = ActiveDesigns.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0)
                return;

            ActiveDesigns.Move(oldIndex, newIndex);
            MarkDirty();
        }

        #endregion

        #region Editing events

        private void DesignGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            var grid = sender as DataGrid;
            if (grid == null) return;

            if (e.Row.Item is DesignItem item)
            {
                item.LastUser = _currentUser;

                // Date due parsing
                if ((e.Column.Header as string) == "Date due")
                {
                    var tb = e.EditingElement as TextBox;
                    if (tb != null)
                    {
                        if (DateTime.TryParseExact(
                                tb.Text,
                                "dd/MM/yyyy",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var dt))
                        {
                            item.DateDue = dt;
                        }
                    }
                }

                // Move between collections if status changed
                bool isTerminal = IsTerminalStatus(item.Status);

                bool inActive = ActiveDesigns.Contains(item);
                bool inFinished = FinishedDesigns.Contains(item);

                if (inActive && isTerminal)
                {
                    ActiveDesigns.Remove(item);
                    FinishedDesigns.Add(item);
                }
                else if (inFinished && !isTerminal)
                {
                    FinishedDesigns.Remove(item);
                    ActiveDesigns.Add(item);
                }
            }

            MarkDirty();
        }

        private void DesignGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // Let default sorting happen; we just mark dirty so user view can be saved
            MarkDirty();
        }

        #endregion

        #region Save / load user view (columns) 

        private void LoadUserViewSettings()
        {
            if (string.IsNullOrEmpty(_userViewSettingsPath) ||
                !File.Exists(_userViewSettingsPath))
                return;

            try
            {
                var lines = File.ReadAllLines(_userViewSettingsPath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    if (!int.TryParse(parts[0], out var index)) continue;
                    if (index < 0 || index >= DesignGrid.Columns.Count) continue;

                    var col = DesignGrid.Columns[index];

                    // DisplayIndex
                    if (int.TryParse(parts[1], out var displayIndex))
                    {
                        if (displayIndex >= 0 && displayIndex < DesignGrid.Columns.Count)
                            col.DisplayIndex = displayIndex;
                    }

                    // Width
                    if (double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var widthVal))
                    {
                        if (Enum.TryParse(parts[3], out DataGridLengthUnitType unitType))
                        {
                            col.Width = new DataGridLength(widthVal, unitType);
                        }
                    }

                    // SortDirection
                    if (parts.Length >= 5 && parts[4] != "None")
                    {
                        if (Enum.TryParse(parts[4], out ListSortDirection sortDir))
                        {
                            col.SortDirection = sortDir;
                            var view = CollectionViewSource.GetDefaultView(DesignGrid.ItemsSource);
                            if (view != null)
                            {
                                view.SortDescriptions.Clear();
                                var sortMember = !string.IsNullOrEmpty(col.SortMemberPath)
                                    ? col.SortMemberPath
                                    : col.Header?.ToString();

                                if (!string.IsNullOrEmpty(sortMember))
                                {
                                    view.SortDescriptions.Add(new SortDescription(sortMember, sortDir));
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignore bad settings
            }
        }

        private void SaveUserViewSettings()
        {
            if (string.IsNullOrEmpty(_userViewSettingsPath))
                return;

            try
            {
                var lines = new List<string>();
                for (int i = 0; i < DesignGrid.Columns.Count; i++)
                {
                    var col = DesignGrid.Columns[i];
                    var width = col.Width;
                    var sortDir = col.SortDirection.HasValue ? col.SortDirection.Value.ToString() : "None";

                    lines.Add(string.Join("|",
                        i.ToString(CultureInfo.InvariantCulture),
                        col.DisplayIndex.ToString(CultureInfo.InvariantCulture),
                        width.Value.ToString(CultureInfo.InvariantCulture),
                        width.UnitType.ToString(),
                        sortDir));
                }

                File.WriteAllLines(_userViewSettingsPath, lines);
            }
            catch
            {
                // ignore
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveUserViewSettings();
        }

        #endregion
    }
}
