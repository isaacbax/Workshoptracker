using DesignSheet.Models;
using DesignSheet.ViewModels;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DesignSheet.Views;

public partial class WorksGrid : UserControl
{
    public WorksGrid()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(WorksGrid),
            new PropertyMetadata(null));

    // ---------- Helpers ----------

    private MainViewModel? GetMainViewModel()
        => DataContext as MainViewModel
           ?? Application.Current.MainWindow?.DataContext as MainViewModel;

    private string GetCurrentUserName()
        => GetMainViewModel()?.CurrentUser ?? "";

    private ObservableCollection<WorkRowView>? GetUnderlyingCollection()
    {
        if (ItemsSource is ICollectionView view &&
            view.SourceCollection is ObservableCollection<WorkRowView> list)
        {
            return list;
        }

        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    // ---------- LAST USER + save on edit ----------

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row?.Item is WorkRowView view && view.Row != null && !view.IsSeparator)
        {
            var vm = GetMainViewModel();
            var user = vm?.CurrentUser;
            if (!string.IsNullOrWhiteSpace(user))
            {
                view.Row.LAST_USER = user;
            }

            // Always save so other users see it
            vm?.SaveAll();
        }
    }

    // ---------- Right-click context menu actions ----------

    private void AddRowAbove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not WorkRowView target)
            return;
        if (target.IsSeparator)
            return;

        var list = GetUnderlyingCollection();
        if (list == null)
            return;

        int index = list.IndexOf(target);
        if (index < 0) return;

        var user = GetCurrentUserName();

        var newRow = new WorkRow
        {
            STATUS = "quote",
            LAST_USER = user
        };

        list.Insert(index, WorkRowView.Item(newRow));

        GetMainViewModel()?.SaveAll();
    }

    private void AddRowBelow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not WorkRowView target)
            return;
        if (target.IsSeparator)
            return;

        var list = GetUnderlyingCollection();
        if (list == null)
            return;

        int index = list.IndexOf(target);
        if (index < 0) return;

        var user = GetCurrentUserName();

        var newRow = new WorkRow
        {
            STATUS = "quote",
            LAST_USER = user
        };

        list.Insert(index + 1, WorkRowView.Item(newRow));

        GetMainViewModel()?.SaveAll();
    }

    private void CopyRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.CommandParameter is not WorkRowView target)
            return;
        if (target.IsSeparator || target.Row == null)
            return;

        var list = GetUnderlyingCollection();
        if (list == null)
            return;

        int index = list.IndexOf(target);
        if (index < 0) return;

        var user = GetCurrentUserName();
        var src = target.Row;

        var copy = new WorkRow
        {
            RETAIL = src.RETAIL,
            OE = src.OE,
            CUSTOMER = src.CUSTOMER,
            SERIAL = src.SERIAL,
            DAY_DUE = src.DAY_DUE,
            DATE_DUE = src.DATE_DUE,
            STATUS = src.STATUS,
            QTY = src.QTY,
            WHAT_IS_IT = src.WHAT_IS_IT,
            PO = src.PO,
            WHAT_ARE_WE_DOING = src.WHAT_ARE_WE_DOING,
            PARTS = src.PARTS,
            SHAFT = src.SHAFT,
            PRIORITY = src.PRIORITY,
            LAST_USER = user
        };

        list.Insert(index + 1, WorkRowView.Item(copy));

        GetMainViewModel()?.SaveAll();
    }

    // ---------- Drag & Drop row reordering ----------

    private Point _dragStartPoint;
    private bool _isDragging;
    private WorkRowView? _draggedItem;

    private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
        _draggedItem = null;

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is WorkRowView item && !item.IsSeparator)
        {
            _draggedItem = item;
        }
    }

    private void Grid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        if (_draggedItem == null)
            return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (!_isDragging &&
            (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _isDragging = true;
            DragDrop.DoDragDrop(Grid, _draggedItem, DragDropEffects.Move);
            _isDragging = false;
        }
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(WorkRowView)))
        {
            e.Effects = DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(WorkRowView)))
            return;

        var dragged = e.Data.GetData(typeof(WorkRowView)) as WorkRowView;
        if (dragged == null || dragged.IsSeparator)
            return;

        var list = GetUnderlyingCollection();
        if (list == null)
            return;

        var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (targetRow?.Item is not WorkRowView target || target.IsSeparator)
            return;

        int oldIndex = list.IndexOf(dragged);
        int newIndex = list.IndexOf(target);

        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            return;

        list.Move(oldIndex, newIndex);

        GetMainViewModel()?.SaveAll();
    }
}
