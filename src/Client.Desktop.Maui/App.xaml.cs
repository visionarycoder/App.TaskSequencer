using Microsoft.Extensions.DependencyInjection;

namespace App.TaskSequencer.Client.Desktop.Maui;

public partial class App : Application
{
	public App()
	{
		try
		{
			InitializeComponent();
		}
		catch (Exception ex)
		{
			// Handle resource loading errors gracefully
			System.Diagnostics.Debug.WriteLine($"Error initializing App XAML: {ex.Message}");
			// Continue initialization even if XAML resource loading fails
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}