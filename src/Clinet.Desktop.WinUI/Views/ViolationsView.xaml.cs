using Microsoft.UI.Xaml.Controls;
using Clinet.Desktop.WinUI.ViewModels;

namespace Clinet.Desktop.WinUI.Views;

public sealed partial class ViolationsView : UserControl
{
    public ViolationsView()
    {
        this.InitializeComponent();
        this.DataContext = new ViolationsViewModel();
    }
}
