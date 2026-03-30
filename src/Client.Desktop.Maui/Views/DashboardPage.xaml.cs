using App.TaskSequencer.Client.Desktop.Maui.ViewModels;

namespace App.TaskSequencer.Client.Desktop.Maui.Views;

public partial class DashboardPage : ContentPage
{
	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
