using System.Windows;
using DesignSheet.ViewModels;

namespace DesignSheet.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ReloadAll();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveAll();
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ChangePasswordWindow
        {
            Owner = this
        };
        dlg.ShowDialog();
    }

    private void ViewRegular_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.GridFontSize = 12.0;
        }
    }

    private void ViewMedium_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.GridFontSize = 14.0;
        }
    }

    private void ViewLarge_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.GridFontSize = 16.0;
        }
    }
}
