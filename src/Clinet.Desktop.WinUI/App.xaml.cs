using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Clinet.Desktop.WinUI.ViewModels;
using Clinet.Desktop.WinUI.Services;

namespace Clinet.Desktop.WinUI;

public partial class App : Application
{
    private Window? _window;
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        SetupDependencyInjection();
    }

    private void SetupDependencyInjection()
    {
        var services = new ServiceCollection();
        
        // Register Services
        services.AddSingleton<ExecutionPlanService>();

        // Register ViewModels (Singletons to persist state)
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<TimelineViewModel>();
        services.AddSingleton<ViolationsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService(typeof(T)) as T;
    }
}
