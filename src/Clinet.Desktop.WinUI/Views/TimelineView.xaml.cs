using Microsoft.UI.Xaml.Controls;
using Clinet.Desktop.WinUI.ViewModels;

namespace Clinet.Desktop.WinUI.Views;

public sealed partial class TimelineView : UserControl
{
    public TimelineView()
    {
        this.InitializeComponent();
        this.DataContext = new TimelineViewModel();
    }
}
