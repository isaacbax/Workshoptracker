using System.Windows;
using DesignSheet.Models;
using DesignSheet.ViewModels;
using DesignSheet.Views;

namespace DesignSheet;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Show login window first
        var loginVm = new LoginViewModel();
        var loginWindow = new LoginWindow
        {
            DataContext = loginVm
        };

        bool? result = loginWindow.ShowDialog();

        // Login successful
        if (result == true && loginVm.SelectedUser != null && !string.IsNullOrWhiteSpace(loginVm.FolderPath))
        {
            var mainVm = new MainViewModel(loginVm.FolderPath, loginVm.SelectedUser);
            var mainWindow = new MainWindow
            {
                DataContext = mainVm
            };
            mainWindow.Show();
        }
        else
        {
            // Cancelled or failed login
            Shutdown();
        }
    }
}
