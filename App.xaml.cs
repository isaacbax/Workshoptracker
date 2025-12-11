using System.Windows;
using DesignSheet.Models;
using DesignSheet.ViewModels;
using DesignSheet.Views;

namespace DesignSheet
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginVm = new LoginViewModel();
            var loginWindow = new LoginWindow
            {
                DataContext = loginVm
            };

            bool? result = loginWindow.ShowDialog();

            if (result == true && loginVm.SelectedUser != null)
            {
                string dataFolder = string.IsNullOrWhiteSpace(loginVm.FolderPath)
                    ? @"S:\IT\20 - Workshop Tracker"
                    : loginVm.FolderPath;

                var mainVm = new MainViewModel(dataFolder, loginVm.SelectedUser);

                var mainWindow = new MainWindow
                {
                    DataContext = mainVm
                };

                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
