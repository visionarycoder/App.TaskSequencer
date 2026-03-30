using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.TaskSequencer.Client.Desktop.Maui.Services;
using System.Collections.ObjectModel;

namespace App.TaskSequencer.Client.Desktop.Maui.ViewModels;

/// <summary>
/// ViewModel for the Dashboard page - displays execution plan summary and statistics.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ExecutionPlanService ExecutionPlanService;

    [ObservableProperty]
    private int totalTasks;

    [ObservableProperty]
    private int validTasks;

    [ObservableProperty]
    private int invalidTasks;

    [ObservableProperty]
    private double validPercentage;

    [ObservableProperty]
    private string criticalPathEnd = "--";

    [ObservableProperty]
    private int criticalTaskCount;

    [ObservableProperty]
    private int maxParallelLevel;

    [ObservableProperty]
    private int executionGroups;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<string> errors = new();

    [ObservableProperty]
    private ObservableCollection<string> warnings = new();

    public DashboardViewModel()
    {
        ExecutionPlanService = null!;
    }

    public DashboardViewModel(ExecutionPlanService executionPlanService)
    {
        ExecutionPlanService = executionPlanService ?? throw new ArgumentNullException(nameof(executionPlanService));
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Refreshing execution plan...";

        try
        {
            // This would be called after an execution plan is loaded
            // For now, just update the UI state
            StatusMessage = "Execution plan loaded successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Errors.Add(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Update dashboard with plan statistics.
    /// </summary>
    public void UpdateFromPlanStatistics(PlanStatistics statistics)
    {
        TotalTasks = statistics.TotalTasks;
        ValidTasks = statistics.ValidTasks;
        InvalidTasks = statistics.InvalidTasks;
        ValidPercentage = statistics.TotalTasks > 0 
            ? (statistics.ValidTasks * 100.0) / statistics.TotalTasks 
            : 0;
        CriticalPathEnd = statistics.CriticalPathEnd != DateTime.MinValue
            ? statistics.CriticalPathEnd.ToString("g")
            : "--";
        CriticalTaskCount = statistics.CriticalTaskCount;
        MaxParallelLevel = statistics.MaxParallelLevel;
        ExecutionGroups = statistics.ExecutionGroups;
    }

    /// <summary>
    /// Update error and warning collections.
    /// </summary>
    public void UpdateMessages(List<string> errors, List<string> warnings)
    {
        Errors.Clear();
        foreach (var error in errors)
            Errors.Add(error);

        Warnings.Clear();
        foreach (var warning in warnings)
            Warnings.Add(warning);
    }
}
