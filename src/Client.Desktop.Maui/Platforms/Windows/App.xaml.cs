using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace App.TaskSequencer.Client.Desktop.Maui.Platforms.Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		// Initialize Windows-specific error handling
		WindowsInitializationHelper.Initialize();

		try
		{
			this.InitializeComponent();
		}
		catch (COMException comEx) when (comEx.HResult == unchecked((int)0x80004005))
		{
			// Handle resource loading error gracefully
			System.Diagnostics.Debug.WriteLine($"App.xaml initialization - resource error (0x80004005): {comEx.Message}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"App.xaml initialization error: {ex}");
		}
	}

	protected override MauiApp CreateMauiApp()
	{
		return WindowsInitializationHelper.TryCreateMauiApp(() => MauiProgram.CreateMauiApp());
	}
}

