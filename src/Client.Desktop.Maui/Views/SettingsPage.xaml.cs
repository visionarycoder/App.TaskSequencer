using App.TaskSequencer.Client.Desktop.Maui.ViewModels;

namespace App.TaskSequencer.Client.Desktop.Maui.Views;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;

		// Navigate to Dashboard when plan is loaded
		viewModel.ExecutionPlanLoaded += async (s, e) =>
		{
			await Shell.Current.GoToAsync("DashboardPage");
		};
	}
}
