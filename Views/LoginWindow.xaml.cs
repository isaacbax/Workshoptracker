using System.Windows;
using DesignSheet.ViewModels;

namespace DesignSheet.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        // Hook CloseRequested when DataContext is a LoginViewModel
        DataContextChanged += LoginWindow_DataContextChanged;
    }

    private void LoginWindow_DataContextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LoginViewModel oldVm)
        {
            oldVm.CloseRequested -= Vm_CloseRequested;
        }

        if (e.NewValue is LoginViewModel newVm)
        {
            newVm.CloseRequested += Vm_CloseRequested;
        }
    }

    private void Vm_CloseRequested(bool result)
    {
        DialogResult = result;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && vm.BrowseFolderCommand != null)
        {
            if (vm.BrowseFolderCommand.CanExecute(null))
                vm.BrowseFolderCommand.Execute(null);
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && vm.LoginCommand != null)
        {
            if (vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && vm.CancelCommand != null)
        {
            if (vm.CancelCommand.CanExecute(null))
                vm.CancelCommand.Execute(null);
        }
        else
        {
            DialogResult = false;
        }
    }
}
