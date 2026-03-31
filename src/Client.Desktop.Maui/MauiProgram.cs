using App.TaskSequencer.Client.Desktop.Maui.Services;
using App.TaskSequencer.Client.Desktop.Maui.ViewModels;
using Core.Services;
using Microsoft.Extensions.Logging;
using Orleans;

namespace App.TaskSequencer.Client.Desktop.Maui;

public static class MauiProgram
{
	private static IClusterClient? GrainClient;

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

		// Register Orleans client
		builder.Services.AddSingleton<IClusterClient>(sp =>
		{
			//TODO: Fix ClientBuilder initialization - requires IServiceCollection and IConfiguration
			// var client = new ClientBuilder(sp as IServiceCollection, sp.GetRequiredService<IConfiguration>())
			// 	.UseLocalhostClustering()
			// 	.Build();
			// return client;
			throw new NotImplementedException("Orleans client initialization not yet implemented");
		});

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

		var app = builder.Build();
		
		// Initialize Orleans client connection
		//TODO: Initialize Orleans client when proper configuration is available
		//_ = InitializeOrleansClient(app.Services);
		
		return app;
	}

//	/// <summary>
//	/// Initializes the Orleans grain client for connection to the Orleans silo.
//	/// Expects silo to be running on localhost:11111 (started by Aspire.AppHost).
//	/// </summary>
//	private static async Task InitializeOrleansClient(IServiceProvider serviceProvider)
//	{
//		try
//		{
//			GrainClient = serviceProvider.GetRequiredService<IClusterClient>();
//			await GrainClient.Connect();
//			Log.Information("✓ Successfully connected to Orleans silo");
//		}
//		catch (Exception ex)
//		{
//			Log.Error($"✗ Failed to connect to Orleans silo: {ex.Message}");
//		}
//	}

	/// <summary>
	/// Gets the Orleans grain client for making grain calls throughout the app.
	/// </summary>
	public static IClusterClient GetGrainClient()
	{
		return GrainClient ?? throw new InvalidOperationException("Orleans client not initialized. Ensure Aspire.AppHost is running.");
	}
}
