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
using System.Windows.Media;
using System.Windows.Threading;
using WorkshopTracker.Services;

namespace WorkshopTracker
{
    public partial class MainWindow : Window
    {
        private readonly string _branch;
        private readonly string _currentUser;
        private readonly ConfigService _config;

        private const string BaseFolder = @"S:\Public\DesignData\";

        private ObservableCollection<WorkRow> _openRows = new();
        private ObservableCollection<WorkRow> _closedRows = new();

        private ICollectionView? _openView;
        private ICollectionView? _closedView;

        private Point _dragStartPoint;
        private DataGrid? _dragSourceGrid;
        private WorkRow? _draggedRow;

        public MainWindow(string branch, string currentUser, ConfigService config)
        {
            InitializeComponent();

            _branch = branch;
            _currentUser = currentUser;
            _config = config;

            DataContext = this;

            LoadAll();

            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;

            _openView = CollectionViewSource.GetDefaultView(OpenGrid.ItemsSource);
            _closedView = CollectionViewSource.GetDefaultView(ClosedGrid.ItemsSource);

            if (_openView != null)
                _openView.Filter = OpenFilter;

            if (_closedView != null)
                _closedView.Filter = ClosedFilter;

            // Hook events for reordering & status rules
            OpenGrid.CellEditEnding += WorkGrid_CellEditEnding;
            ClosedGrid.CellEditEnding += WorkGrid_CellEditEnding;

            OpenGrid.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
            OpenGrid.PreviewMouseMove += DataGrid_PreviewMouseMove;
            OpenGrid.Drop += DataGrid_Drop;

            ClosedGrid.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
            ClosedGrid.PreviewMouseMove += DataGrid_PreviewMouseMove;
            ClosedGrid.Drop += DataGrid_Drop;

            Loaded += MainWindow_Loaded;
        }

        #region Paths & IO

        private string GetOpenPath() =>
            Path.Combine(BaseFolder, $"{_branch}open.csv");

        private string GetClosedPath() =>
            Path.Combine(BaseFolder, $"{_branch}closed.csv");

        private void LoadAll()
        {
            _openRows = new ObservableCollection<WorkRow>(
                File.Exists(GetOpenPath()) ? LoadFromCsv(GetOpenPath()) : new List<WorkRow>());

            _closedRows = new ObservableCollection<WorkRow>(
                File.Exists(GetClosedPath()) ? LoadFromCsv(GetClosedPath()) : new List<WorkRow>());
        }

        private void SaveAll()
        {
            SaveToCsv(GetOpenPath(), _openRows);
            SaveToCsv(GetClosedPath(), _closedRows);
            StatusTextBlock.Text = $"Saved at {DateTime.Now:T}";
        }

        private static List<WorkRow> LoadFromCsv(string path)
        {
            var result = new List<WorkRow>();

            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
                return result;

            // assume first line is header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = SplitCsv(line);
                if (parts.Length < 15)
                    continue;

                var row = new WorkRow
                {
                    Retail = parts[0],
                    OE = parts[1],
                    Customer = parts[2],
                    Serial = parts[3],
                    DayDue = parts[4],
                    DateDue = ParseNullableDate(parts[5]),
                    Status = parts[6],
                    Qty = ParseInt(parts[7]),
                    WhatIsIt = parts[8],
                    PO = parts[9],
                    WhatAreWeDoing = parts[10],
                    Parts = parts[11],
                    Shaft = parts[12],
                    Priority = parts[13],
                    LastUser = parts[14]
                };

                result.Add(row);
            }

            return result;
        }

        private static void SaveToCsv(string path, IEnumerable<WorkRow> rows)
        {
            var list = rows.ToList();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, false);

            // header
            writer.WriteLine("RETAIL,OE,CUSTOMER,SERIAL,DAY DUE,DATE DUE,STATUS,QTY,WHAT IS IT,PO,WHAT ARE WE DOING,PARTS,SHAFT,PRIORITY,LAST USER");

            foreach (var r in list)
            {
                if (r.IsGroupRow)
                    continue; // we don't persist group/header rows

                var fields = new[]
                {
                    EscapeCsv(r.Retail),
                    EscapeCsv(r.OE),
                    EscapeCsv(r.Customer),
                    EscapeCsv(r.Serial),
                    EscapeCsv(r.DayDue),
                    r.DateDue?.ToString("d/M/yyyy", CultureInfo.InvariantCulture) ?? "",
                    EscapeCsv(r.Status),
                    r.Qty.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(r.WhatIsIt),
                    EscapeCsv(r.PO),
                    EscapeCsv(r.WhatAreWeDoing),
                    EscapeCsv(r.Parts),
                    EscapeCsv(r.Shaft),
                    EscapeCsv(r.Priority),
                    EscapeCsv(r.LastUser)
                };

                writer.WriteLine(string.Join(",", fields));
            }
        }

        private static int ParseInt(string s)
        {
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                return value;
            return 0;
        }

        private static DateTime? ParseNullableDate(string s)
        {
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.Date;
            return null;
        }

        private static string[] SplitCsv(string line)
        {
            // very simple CSV splitter (no embedded commas)
            return line.Split(',');
        }

        private static string EscapeCsv(string s)
        {
            s ??= "";
            if (s.Contains(",") || s.Contains("\""))
            {
                s = s.Replace("\"", "\"\"");
                return $"\"{s}\"";
            }
            return s;
        }

        #endregion

        #region Window lifecycle & status rules

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Move existing picked up / cancelled rows out of open
            MoveExistingClosedStatusesToClosed();

            // Build date dividers & paint shop ordering
            RebuildOpenWithDividers();

            RefreshViews();
        }

        private static bool IsClosedStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim().ToLowerInvariant();
            return status == "picked up" || status == "cancelled";
        }

        private static bool IsPaintShopStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            status = status.Trim().ToLowerInvariant();
            return status == "paint shop";
        }

        private void WorkGrid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
                return;

            if (e.Column?.Header == null)
                return;

            if (!string.Equals(e.Column.Header.ToString(), "STATUS", StringComparison.OrdinalIgnoreCase))
                return;

            if (sender is not DataGrid grid)
                return;

            var rowItem = e.Row?.Item as WorkRow;
            if (rowItem == null)
                return;

            if (rowItem.IsGroupRow)
                return;

            string? newStatus = null;

            if (e.EditingElement is ComboBox cb)
                newStatus = cb.Text;
            else if (e.EditingElement is TextBox tb)
                newStatus = tb.Text;

            if (string.IsNullOrWhiteSpace(newStatus))
                return;

            newStatus = newStatus.Trim();

            // defer until binding has updated the row
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyStatusRules(grid, rowItem, newStatus);
            }), DispatcherPriority.Background);
        }

        private void ApplyStatusRules(DataGrid editingGrid, WorkRow rowItem, string newStatus)
        {
            rowItem.Status = newStatus;

            if (IsClosedStatus(newStatus))
            {
                rowItem.LastUser = _currentUser;
                MoveRowBetweenCollections(_openRows, _closedRows, rowItem);
            }

            // rebuild open list (paint shop on top + date dividers)
            RebuildOpenWithDividers();

            RefreshViews();
        }

        private void MoveExistingClosedStatusesToClosed()
        {
            var toMove = _openRows
                .Where(r => !r.IsGroupRow && IsClosedStatus(r.Status))
                .ToList();

            foreach (var r in toMove)
            {
                _openRows.Remove(r);
                _closedRows.Add(r);
            }
        }

        private void MoveRowBetweenCollections(ObservableCollection<WorkRow> from,
                                               ObservableCollection<WorkRow> to,
                                               WorkRow row)
        {
            if (from.Contains(row))
            {
                from.Remove(row);
                to.Add(row);
            }
        }

        /// <summary>
        /// Rebuilds _openRows to:
        ///  - put all paint shop rows first (keeping their relative order)
        ///  - insert uneditable IsGroupRow header rows when DateDue changes.
        /// </summary>
        private void RebuildOpenWithDividers()
        {
            // Get all real rows (no group rows)
            var baseRows = _openRows.Where(r => !r.IsGroupRow).ToList();

            // Paint shop rows first, but keep their current relative order
            var paintRows = baseRows.Where(r => IsPaintShopStatus(r.Status)).ToList();
            var otherRows = baseRows.Where(r => !IsPaintShopStatus(r.Status)).ToList();

            var ordered = new List<WorkRow>();
            ordered.AddRange(paintRows);
            ordered.AddRange(otherRows);

            _openRows.Clear();

            DateTime? currentDate = null;
            bool first = true;

            foreach (var row in ordered)
            {
                var date = row.DateDue?.Date;

                if (date.HasValue && (first || currentDate == null || date.Value != currentDate.Value))
                {
                    currentDate = date.Value;
                    first = false;

                    _openRows.Add(new WorkRow
                    {
                        IsGroupRow = true,
                        Customer = currentDate.Value.ToString("dd/MM/yyyy"),
                        DateDue = currentDate.Value
                    });
                }

                _openRows.Add(row);
            }
        }

        #endregion

        #region Toolbar handlers

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            LoadAll();
            OpenGrid.ItemsSource = _openRows;
            ClosedGrid.ItemsSource = _closedRows;

            _openView = CollectionViewSource.GetDefaultView(OpenGrid.ItemsSource);
            _closedView = CollectionViewSource.GetDefaultView(ClosedGrid.ItemsSource);

            if (_openView != null)
                _openView.Filter = OpenFilter;
            if (_closedView != null)
                _closedView.Filter = ClosedFilter;

            MoveExistingClosedStatusesToClosed();
            RebuildOpenWithDividers();
            RefreshViews();

            StatusTextBlock.Text = "Reloaded";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveAll();
        }

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            var grid = WorksTabControl.SelectedIndex == 0 ? OpenGrid : ClosedGrid;
            if (grid.SelectedItem is WorkRow row && !row.IsGroupRow)
            {
                var collection = grid == OpenGrid ? _openRows : _closedRows;
                collection.Remove(row);

                if (grid == OpenGrid)
                    RebuildOpenWithDividers();

                RefreshViews();
            }
        }

        // Font size selector – with IsLoaded/ null checks to avoid NRE
        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Ignore the first call fired during InitializeComponent
            if (!IsLoaded)
                return;

            if (FontSizeComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag &&
                double.TryParse(tag, out double size))
            {
                if (OpenGrid != null)
                    OpenGrid.FontSize = size;

                if (ClosedGrid != null)
                    ClosedGrid.FontSize = size;
            }
        }

        #endregion

        #region Search filtering

        private bool OpenFilter(object obj)
        {
            if (obj is not WorkRow row) return false;
            return FilterRow(row);
        }

        private bool ClosedFilter(object obj)
        {
            if (obj is not WorkRow row) return false;
            return FilterRow(row);
        }

        private bool FilterRow(WorkRow row)
        {
            if (row.IsGroupRow)
                return true; // always show group rows

            var text = SearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
                return true;

            text = text.Trim().ToLowerInvariant();

            bool Match(string? s) =>
                !string.IsNullOrEmpty(s) &&
                s.ToLowerInvariant().Contains(text);

            return Match(row.Retail) ||
                   Match(row.OE) ||
                   Match(row.Customer) ||
                   Match(row.Serial) ||
                   Match(row.DayDue) ||
                   (row.DateDue?.ToString("d/M/yyyy")?.Contains(text) ?? false) ||
                   Match(row.Status) ||
                   row.Qty.ToString(CultureInfo.InvariantCulture).Contains(text) ||
                   Match(row.WhatIsIt) ||
                   Match(row.PO) ||
                   Match(row.WhatAreWeDoing) ||
                   Match(row.Parts) ||
                   Match(row.Shaft) ||
                   Match(row.Priority) ||
                   Match(row.LastUser);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshViews();
        }

        private void RefreshViews()
        {
            _openView?.Refresh();
            _closedView?.Refresh();
        }

        #endregion

        #region Drag & drop reordering

        private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _dragSourceGrid = sender as DataGrid;
            _draggedRow = null;

            if (_dragSourceGrid == null)
                return;

            var row = GetRowUnderMouse(_dragSourceGrid, e.GetPosition(_dragSourceGrid));
            _draggedRow = row;
        }

        private void DataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (_dragSourceGrid == null || _draggedRow == null)
                return;

            var position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_draggedRow.IsGroupRow)
                return;

            DragDrop.DoDragDrop(_dragSourceGrid, _draggedRow, DragDropEffects.Move);
        }

        private void DataGrid_Drop(object sender, DragEventArgs e)
        {
            if (_draggedRow == null)
                return;

            var targetGrid = sender as DataGrid;
            if (targetGrid == null || targetGrid != _dragSourceGrid)
                return;

            var point = e.GetPosition(targetGrid);
            var targetRow = GetRowUnderMouse(targetGrid, point);

            var collection = targetGrid == OpenGrid ? _openRows : _closedRows;
            var oldIndex = collection.IndexOf(_draggedRow);

            if (oldIndex < 0)
                return;

            int newIndex;

            if (targetRow == null)
            {
                newIndex = collection.Count - 1;
            }
            else
            {
                newIndex = collection.IndexOf(targetRow);
                if (newIndex < 0)
                    newIndex = collection.Count - 1;
            }

            if (newIndex == oldIndex)
                return;

            collection.RemoveAt(oldIndex);
            collection.Insert(newIndex, _draggedRow);
        }

        private static WorkRow? GetRowUnderMouse(DataGrid grid, Point position)
        {
            var element = grid.InputHitTest(position) as DependencyObject;
            while (element != null && element is not DataGridRow)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            return (element as DataGridRow)?.Item as WorkRow;
        }

        #endregion

        #region Context menu: add / copy rows

        private void AddRowAbove_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = 0;

            collection.Insert(index, new WorkRow());

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            RefreshViews();
        }

        private void AddRowBelow_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = collection.Count - 1;

            collection.Insert(index + 1, new WorkRow());

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            RefreshViews();
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            var (grid, row) = GetContextMenuTarget(sender);
            if (grid == null || row == null || row.IsGroupRow)
                return;

            var collection = grid == OpenGrid ? _openRows : _closedRows;
            var index = collection.IndexOf(row);
            if (index < 0) index = collection.Count - 1;

            var copy = row.Clone();
            collection.Insert(index + 1, copy);

            if (grid == OpenGrid)
                RebuildOpenWithDividers();

            RefreshViews();
        }

        private (DataGrid? grid, WorkRow? row) GetContextMenuTarget(object sender)
        {
            if (sender is not MenuItem menuItem)
                return (null, null);

            if (menuItem.Parent is not ContextMenu ctx)
                return (null, null);

            var grid = ctx.PlacementTarget as DataGrid;
            var row = grid?.SelectedItem as WorkRow;
            return (grid, row);
        }

        #endregion
    }

    /// <summary>
    /// Row model for the workshop tracker.
    /// </summary>
    public class WorkRow : INotifyPropertyChanged
    {
        private bool _isGroupRow;
        private string? _retail;
        private string? _oe;
        private string? _customer;
        private string? _serial;
        private string? _dayDue;
        private DateTime? _dateDue;
        private string? _status;
        private int _qty;
        private string? _whatIsIt;
        private string? _po;
        private string? _whatAreWeDoing;
        private string? _parts;
        private string? _shaft;
        private string? _priority;
        private string? _lastUser;

        public bool IsGroupRow
        {
            get => _isGroupRow;
            set { _isGroupRow = value; OnPropertyChanged(nameof(IsGroupRow)); }
        }

        public string? Retail
        {
            get => _retail;
            set { _retail = value; OnPropertyChanged(nameof(Retail)); }
        }

        public string? OE
        {
            get => _oe;
            set { _oe = value; OnPropertyChanged(nameof(OE)); }
        }

        public string? Customer
        {
            get => _customer;
            set { _customer = value; OnPropertyChanged(nameof(Customer)); }
        }

        public string? Serial
        {
            get => _serial;
            set { _serial = value; OnPropertyChanged(nameof(Serial)); }
        }

        public string? DayDue
        {
            get => _dayDue;
            set { _dayDue = value; OnPropertyChanged(nameof(DayDue)); }
        }

        public DateTime? DateDue
        {
            get => _dateDue;
            set { _dateDue = value; OnPropertyChanged(nameof(DateDue)); }
        }

        public string? Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public int Qty
        {
            get => _qty;
            set { _qty = value; OnPropertyChanged(nameof(Qty)); }
        }

        public string? WhatIsIt
        {
            get => _whatIsIt;
            set { _whatIsIt = value; OnPropertyChanged(nameof(WhatIsIt)); }
        }

        public string? PO
        {
            get => _po;
            set { _po = value; OnPropertyChanged(nameof(PO)); }
        }

        public string? WhatAreWeDoing
        {
            get => _whatAreWeDoing;
            set { _whatAreWeDoing = value; OnPropertyChanged(nameof(WhatAreWeDoing)); }
        }

        public string? Parts
        {
            get => _parts;
            set { _parts = value; OnPropertyChanged(nameof(Parts)); }
        }

        public string? Shaft
        {
            get => _shaft;
            set { _shaft = value; OnPropertyChanged(nameof(Shaft)); }
        }

        public string? Priority
        {
            get => _priority;
            set { _priority = value; OnPropertyChanged(nameof(Priority)); }
        }

        public string? LastUser
        {
            get => _lastUser;
            set { _lastUser = value; OnPropertyChanged(nameof(LastUser)); }
        }

        public WorkRow Clone()
        {
            return new WorkRow
            {
                IsGroupRow = this.IsGroupRow,
                Retail = this.Retail,
                OE = this.OE,
                Customer = this.Customer,
                Serial = this.Serial,
                DayDue = this.DayDue,
                DateDue = this.DateDue,
                Status = this.Status,
                Qty = this.Qty,
                WhatIsIt = this.WhatIsIt,
                PO = this.PO,
                WhatAreWeDoing = this.WhatAreWeDoing,
                Parts = this.Parts,
                Shaft = this.Shaft,
                Priority = this.Priority,
                LastUser = this.LastUser
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
