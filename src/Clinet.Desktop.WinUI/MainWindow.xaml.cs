using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Clinet.Desktop.WinUI.Views;
using Clinet.Desktop.WinUI.ViewModels;

namespace Clinet.Desktop.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        LoadViews();
    }

    private void LoadViews()
    {
        try
        {
            var app = (App)Application.Current;
            
            var dashboardVm = app.GetService<DashboardViewModel>();
            var timelineVm = app.GetService<TimelineViewModel>();
            var violationsVm = app.GetService<ViolationsViewModel>();
            var settingsVm = app.GetService<SettingsViewModel>();

            // Create user controls for each view
            var dashboardView = new DashboardView { DataContext = dashboardVm };
            var timelineView = new TimelineView { DataContext = timelineVm };
            var violationsView = new ViolationsView { DataContext = violationsVm };
            var settingsView = new SettingsView { DataContext = settingsVm };

            DashboardFrame.Content = dashboardView;
            TimelineFrame.Content = timelineView;
            ViolationsFrame.Content = violationsView;
            SettingsFrame.Content = settingsView;

            MainTabView.SelectionChanged += TabView_SelectionChanged;
            TabView_SelectionChanged(null, null);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading views: {ex.Message}";
        }
    }

    private void TabView_SelectionChanged(object? sender, SelectionChangedEventArgs? e)
    {
        if (MainTabView.SelectedIndex == 0)
            StatusText.Text = "Dashboard - Execution Plan Summary";
        else if (MainTabView.SelectedIndex == 1)
            StatusText.Text = "Timeline - Gantt Chart Visualization";
        else if (MainTabView.SelectedIndex == 2)
            StatusText.Text = "Violations - Deadline Miss Analysis";
        else if (MainTabView.SelectedIndex == 3)
            StatusText.Text = "Settings - Configuration";
    }
}
