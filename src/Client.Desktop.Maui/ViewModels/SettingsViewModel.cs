using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.TaskSequencer.Client.Desktop.Maui.Services;
using System.Collections.ObjectModel;

namespace App.TaskSequencer.Client.Desktop.Maui.ViewModels;

/// <summary>
/// ViewModel for Settings page - CSV file selection and execution planning.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ExecutionPlanService ExecutionPlanService;
    private readonly DashboardViewModel DashboardViewModel;
    private readonly TimelineViewModel TimelineViewModel;
    private readonly ViolationsViewModel ViolationsViewModel;

    [ObservableProperty]
    private string? taskDefinitionsPath;

    [ObservableProperty]
    private string? intakeEventsPath;

    [ObservableProperty]
    private string? durationManifestPath;

    [ObservableProperty]
    private DateTime incrementStart = DateTime.Now.Date;

    [ObservableProperty]
    private DateTime incrementEnd = DateTime.Now.Date.AddDays(7);

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<string> errors = [];

    [ObservableProperty]
    private ObservableCollection<string> warnings = [];

    public event EventHandler? ExecutionPlanLoaded;

    public SettingsViewModel()
    {
        ExecutionPlanService = null!;
        DashboardViewModel = null!;
        TimelineViewModel = null!;
        ViolationsViewModel = null!;
    }

    public SettingsViewModel(
        ExecutionPlanService executionPlanService,
        DashboardViewModel dashboardViewModel,
        TimelineViewModel timelineViewModel,
        ViolationsViewModel violationsViewModel)
    {
        ExecutionPlanService = executionPlanService ?? throw new ArgumentNullException(nameof(executionPlanService));
        DashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
        TimelineViewModel = timelineViewModel ?? throw new ArgumentNullException(nameof(timelineViewModel));
        ViolationsViewModel = violationsViewModel ?? throw new ArgumentNullException(nameof(violationsViewModel));
    }

    [RelayCommand]
    public async Task SelectTaskDefinitionsAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Task Definitions CSV",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } }
                })
            });

            if (result != null)
                TaskDefinitionsPath = result.FullPath;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SelectIntakeEventsAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Intake Events CSV",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } }
                })
            });

            if (result != null)
                IntakeEventsPath = result.FullPath;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SelectDurationManifestAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Duration Manifest CSV (Optional)",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".csv" } }
                })
            });

            if (result != null)
                DurationManifestPath = result.FullPath;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadAndPlanAsync()
    {
        if (string.IsNullOrEmpty(TaskDefinitionsPath) || 
            string.IsNullOrEmpty(IntakeEventsPath))
        {
            StatusMessage = "Please select Task Definitions and Intake Events CSV files";
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading and planning execution...";
        Errors.Clear();
        Warnings.Clear();

        try
        {
            var result = await ExecutionPlanService.LoadAndPlanAsync(
                TaskDefinitionsPath,
                IntakeEventsPath,
                DurationManifestPath,
                IncrementStart,
                IncrementEnd);

            if (!result.Success)
            {
                StatusMessage = result.ErrorMessage ?? "Planning failed";
                foreach (var error in result.Errors)
                    Errors.Add(error);
                return;
            }

            // Update all view models with results
            var stats = ExecutionPlanService.GetPlanStatistics(result.Analysis!);
            DashboardViewModel.UpdateFromPlanStatistics(stats);
            DashboardViewModel.UpdateMessages(result.Errors, result.Warnings);

            var tasks = ExecutionPlanService.GetExecutionTasks(result.Analysis);
            TimelineViewModel.UpdateTimeline(tasks);

            var violations = ExecutionPlanService.GetDeadlineViolations(result.Analysis);
            ViolationsViewModel.UpdateViolations(violations);

            StatusMessage = $"Plan created: {result.ValidTasks} valid, {result.InvalidTasks} invalid";
            
            // Notify listeners
            ExecutionPlanLoaded?.Invoke(this, EventArgs.Empty);
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

    [RelayCommand]
    public void ClearFiles()
    {
        TaskDefinitionsPath = null;
        IntakeEventsPath = null;
        DurationManifestPath = null;
        StatusMessage = "Files cleared";
    }

    [RelayCommand]
    public void SetIncrementToday()
    {
        IncrementStart = DateTime.Now.Date;
        IncrementEnd = DateTime.Now.Date.AddDays(1);
    }

    [RelayCommand]
    public void SetIncrementThisWeek()
    {
        IncrementStart = DateTime.Now.Date;
        IncrementEnd = DateTime.Now.Date.AddDays(7);
    }

    [RelayCommand]
    public void SetIncrementThisMonth()
    {
        IncrementStart = DateTime.Now.Date;
        IncrementEnd = DateTime.Now.Date.AddDays(30);
    }
}
