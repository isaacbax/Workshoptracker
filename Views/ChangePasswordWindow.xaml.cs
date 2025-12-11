using System.Windows;

namespace DesignSheet.Views
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (DataContext is ViewModels.ChangePasswordViewModel vm)
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
