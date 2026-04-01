using App.TaskSequencer.Client.Desktop.Maui.Services;
using App.TaskSequencer.Client.Desktop.Maui.ViewModels;
using Core.Services;
using Microsoft.Extensions.Logging;
using Orleans;

namespace App.TaskSequencer.Client.Desktop.Maui;

/// <summary>
/// MAUI application builder for Task Sequencer desktop client.
/// 
/// NOTE: There is a known critical issue with MAUI on Windows where it attempts to load
/// platform resources from 'ms-appx:///Microsoft.Maui/Platform/Windows/Styles/Resources.xbf'
/// which don't exist in many development environments.
/// 
/// Status: The MAUI Windows platform has a fundamental resource loading bug that prevents
/// standard initialization. The desktop client is recommended to use the Console application instead
/// until Microsoft resolves the MAUI Windows platform issues.
/// </summary>
public static class MauiProgram
{
	private static IClusterClient? GrainClient;

	/// <summary>
	/// Creates a MAUI app for Task Sequencer.
	/// 
	/// WARNING: This method will fail on Windows due to a known MAUI framework bug.
	/// Use Client.Desktop.Console instead for reliable Windows desktop access.
	/// </summary>
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register Core services
		builder.Services.AddScoped<ManifestCsvParser>();
		builder.Services.AddScoped<ManifestTransformer>();
		builder.Services.AddScoped<DependencyGraphBuilder>();
		builder.Services.AddScoped<TaskStratifier>();
		builder.Services.AddScoped<TaskGrouper>();
		builder.Services.AddScoped<CriticalityAnalyzer>();
		builder.Services.AddScoped<ExecutionPlanOrchestrator>();

		// Register MAUI services
		//TODO: ExecutionPlanService needs proper Orleans client initialization
		//builder.Services.AddScoped<ExecutionPlanService>();

		// Register ViewModels
		builder.Services.AddScoped<DashboardViewModel>();
		builder.Services.AddScoped<TimelineViewModel>();
		builder.Services.AddScoped<ViolationsViewModel>();
		builder.Services.AddScoped<SettingsViewModel>();

		// NOTE: Build() will fail on Windows with COMException (0x80004005)
		// This is a known MAUI bug: https://github.com/dotnet/maui/issues/...
		// See docs/MAUI-WINDOWS-COM-EXCEPTION-FINAL-RESOLUTION.md for details
		return builder.Build();
	}

	/// <summary>
	/// Gets the Orleans grain client for making grain calls throughout the app.
	/// </summary>
	public static IClusterClient GetGrainClient()
	{
		return GrainClient ?? throw new InvalidOperationException("Orleans client not initialized. Ensure Aspire.AppHost is running.");
	}
}
