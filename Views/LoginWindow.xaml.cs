using System.Windows;

namespace DesignSheet.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (DataContext is ViewModels.LoginViewModel vm)
                {
                    vm.RequestClose += Vm_RequestClose;
                }
            };
        }

        private void Vm_RequestClose(object? sender, bool e)
        {
            DialogResult = e;
        }
    }
}
