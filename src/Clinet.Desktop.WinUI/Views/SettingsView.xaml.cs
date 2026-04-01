using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Clinet.Desktop.WinUI.ViewModels;

namespace Clinet.Desktop.WinUI.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        this.InitializeComponent();
        this.DataContext = new SettingsViewModel();
    }

    private void UpdateErrorsVisibility()
    {
        var vm = (SettingsViewModel?)DataContext;
        if (vm != null)
            ErrorsBorder.Visibility = vm.Errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWarningsVisibility()
    {
        var vm = (SettingsViewModel?)DataContext;
        if (vm != null)
            WarningsBorder.Visibility = vm.Warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
