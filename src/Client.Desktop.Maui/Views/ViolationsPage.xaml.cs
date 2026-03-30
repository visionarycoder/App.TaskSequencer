using App.TaskSequencer.Client.Desktop.Maui.ViewModels;

namespace App.TaskSequencer.Client.Desktop.Maui.Views;

public partial class ViolationsPage : ContentPage
{
	public ViolationsPage(ViolationsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
