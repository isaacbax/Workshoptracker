using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WorkshopTracker.Models;
using WorkshopTracker.Services;

namespace WorkshopTracker
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly string _branch;
        private readonly string _currentUser;
        private readonly ConfigService _config;
        private readonly WorkFileService _workService;

        public ObservableCollection<WorkRow> OpenRows { get; } = new();
        public ObservableCollection<WorkRow> ClosedRows { get; } = new();

        private FileSystemWatcher? _openWatcher;
        private FileSystemWatcher? _closedWatcher;
        private bool _savingOpen;
        private bool _savingClosed;

        private readonly DispatcherTimer _autoSaveTimer;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilter();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public MainWindow()
            : this("headoffice", "root", new ConfigService())
        {
        }

        public MainWindow(string branch, string currentUser, ConfigService config)
        {
            InitializeComponent();

            _branch = branch;
            _currentUser = currentUser;
            _config = config;
            _workService = new WorkFileService(_config);

            DataContext = this;

            Loaded += MainWindow_Loaded;

            _autoSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            StatusTextBlock.Text = $"User: {_currentUser} | Branch: {_branch}";
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadData();
            SetupWatchers();
        }

        private void ReloadData()
        {
            foreach (var r in OpenRows) r.PropertyChanged -= Row_PropertyChanged;
            foreach (var r in ClosedRows) r.PropertyChanged -= Row_PropertyChanged;

            OpenRows.Clear();
            ClosedRows.Clear();

            foreach (var r in _workService.LoadWorks(_branch, true))
            {
                r.PropertyChanged += Row_PropertyChanged;
                OpenRows.Add(r);
            }

            foreach (var r in _workService.LoadWorks(_branch, false))
            {
                r.PropertyChanged += Row_PropertyChanged;
                ClosedRows.Add(r);
            }

            OpenGrid.ItemsSource = CollectionViewSource.GetDefaultView(OpenRows);
            ClosedGrid.ItemsSource = CollectionViewSource.GetDefaultView(ClosedRows);

            ApplyFilter();
        }

        private void SetupWatchers()
        {
            var openPath = _workService.GetFilePath(_branch, true);
            var closedPath = _workService.GetFilePath(_branch, false);

            if (File.Exists(openPath))
                _openWatcher = CreateWatcher(openPath, true);

            if (File.Exists(closedPath))
                _closedWatcher = CreateWatcher(closedPath, false);
        }

        private FileSystemWatcher CreateWatcher(string filePath, bool isOpen)
        {
            var watcher = new FileSystemWatcher(
                Path.GetDirectoryName(filePath) ?? string.Empty,
                Path.GetFileName(filePath) ?? string.Empty)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            watcher.Changed += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (isOpen && !_savingOpen)
                        ReloadData();
                    else if (!isOpen && !_savingClosed)
                        ReloadData();
                });
            };

            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            SaveInternal();
        }

        private void TriggerAutoSave()
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void SaveInternal()
        {
            try
            {
                _savingOpen = true;
                _savingClosed = true;

                _workService.SaveWorks(_branch, true, OpenRows, _currentUser);
                _workService.SaveWorks(_branch, false, ClosedRows, _currentUser);

                StatusTextBlock.Text = $"Saved at {DateTime.Now:T}";
            }
            finally
            {
                _savingOpen = false;
                _savingClosed = false;
            }
        }

        // Toolbar actions
        private void Reload_Click(object sender, RoutedEventArgs e) => ReloadData();

        private void Save_Click(object sender, RoutedEventArgs e) => SaveInternal();

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            var rows = WorksTabControl.SelectedIndex == 0 ? OpenRows : ClosedRows;

            if (grid.SelectedItem is not WorkRow row)
            {
                MessageBox.Show("Select a row to delete.");
                return;
            }

            if (row.IsGroupRow)
            {
                MessageBox.Show("You cannot delete the date separator rows.");
                return;
            }

            if (MessageBox.Show("Delete selected row?", "Confirm",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes)
                return;

            rows.Remove(row);
            TriggerAutoSave();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchText = (sender as TextBox)?.Text ?? string.Empty;
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OpenGrid == null || ClosedGrid == null)
                return;

            double size = 12;

            if (sender is ComboBox combo &&
                combo.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                double.TryParse(tag, out var parsed))
            {
                size = parsed;
            }

            OpenGrid.FontSize = size;
            ClosedGrid.FontSize = size;
        }

        private void ApplyFilter()
        {
            Predicate<object> filter = o =>
            {
                if (o is not WorkRow row) return false;
                if (row.IsGroupRow) return true;

                if (string.IsNullOrWhiteSpace(SearchText)) return true;

                var s = SearchText.ToLowerInvariant();
                bool Match(string? v) =>
                    !string.IsNullOrEmpty(v) && v.ToLowerInvariant().Contains(s);

                return
                    Match(row.Customer) ||
                    Match(row.Serial) ||
                    Match(row.WhatIsIt) ||
                    Match(row.WhatAreWeDoing) ||
                    Match(row.Parts) ||
                    Match(row.PO) ||
                    Match(row.Status);
            };

            if (OpenGrid.ItemsSource is ICollectionView ov)
            {
                ov.Filter = filter;
                ov.Refresh();
            }

            if (ClosedGrid.ItemsSource is ICollectionView cv)
            {
                cv.Filter = filter;
                cv.Refresh();
            }
        }

        // Context menu helpers
        private DataGrid GetActiveGrid(object sender)
        {
            var ctx = (sender as FrameworkElement)?.Parent as ContextMenu;
            var grid = ctx?.PlacementTarget as DataGrid;
            return grid ?? OpenGrid;
        }

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var grid = GetActiveGrid(sender);
            var rows = grid == OpenGrid ? OpenRows : ClosedRows;

            var row = grid.SelectedItem as WorkRow;
            int index = row != null && !row.IsGroupRow
                ? rows.IndexOf(row)
                : 0;

            var newRow = new WorkRow
            {
                DateDue = row?.DateDue,
                DayDue = row?.DayDue
            };

            rows.Insert(Math.Max(index, 0), newRow);
            TriggerAutoSave();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var grid = GetActiveGrid(sender);
            var rows = grid == OpenGrid ? OpenRows : ClosedRows;

            var row = grid.SelectedItem as WorkRow;
            int index;

            if (row != null && !row.IsGroupRow)
            {
                index = rows.IndexOf(row) + 1;
            }
            else
            {
                index = rows.Count;
            }

            var newRow = new WorkRow
            {
                DateDue = row?.DateDue,
                DayDue = row?.DayDue
            };

            rows.Insert(Math.Max(index, 0), newRow);
            TriggerAutoSave();
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = GetActiveGrid(sender);
            var row = grid.SelectedItem as WorkRow;
            if (row == null || row.IsGroupRow) return;

            var rows = grid == OpenGrid ? OpenRows : ClosedRows;
            var index = rows.IndexOf(row);

            var copy = row.Clone();
            copy.LastUser = _currentUser;

            rows.Insert(index + 1, copy);
            TriggerAutoSave();
        }

        // Drag & drop
        private Point _dragStartPoint;

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var diff = e.GetPosition(null) - _dragStartPoint;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var grid = (DataGrid)sender;
            var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
            if (row == null) return;
            if (row.Item is not WorkRow wr || wr.IsGroupRow) return;

            DragDrop.DoDragDrop(row, wr, DragDropEffects.Move);
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(WorkRow))) return;

            var grid = (DataGrid)sender;
            var target = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
            if (target == null) return;

            var sourceRow = (WorkRow)e.Data.GetData(typeof(WorkRow))!;
            var targetRow = (WorkRow)target.Item;
            if (targetRow.IsGroupRow) return;

            var rows = grid == OpenGrid ? OpenRows : ClosedRows;

            var oldIndex = rows.IndexOf(sourceRow);
            var newIndex = rows.IndexOf(targetRow);

            if (oldIndex == newIndex || oldIndex < 0 || newIndex < 0) return;

            rows.Move(oldIndex, newIndex);
            TriggerAutoSave();
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                    return parent;

                child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        // Auto-save on changes
        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            TriggerAutoSave();
        }
    }
}
