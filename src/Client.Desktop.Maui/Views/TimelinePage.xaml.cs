using App.TaskSequencer.Client.Desktop.Maui.ViewModels;

namespace App.TaskSequencer.Client.Desktop.Maui.Views;

public partial class TimelinePage : ContentPage
{
	public TimelinePage(TimelineViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
