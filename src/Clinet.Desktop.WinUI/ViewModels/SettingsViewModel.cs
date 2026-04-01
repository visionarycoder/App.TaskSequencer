using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clinet.Desktop.WinUI.Services;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;

namespace Clinet.Desktop.WinUI.ViewModels;

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
    public async Task SelectTaskDefinitionsAsync(CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            TaskDefinitionsPath = await SelectCsvFileAsync("Select Task Definitions CSV", ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Selection cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SelectIntakeEventsAsync(CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            IntakeEventsPath = await SelectCsvFileAsync("Select Intake Events CSV", ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Selection cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SelectDurationManifestAsync(CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            DurationManifestPath = await SelectCsvFileAsync("Select Duration Manifest CSV", ct);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Selection cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error selecting file: {ex.Message}";
        }
    }

    private async Task<string?> SelectCsvFileAsync(string title, CancellationToken ct)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            FileTypeFilter = { ".csv" }
        };
        picker.CommitButtonText = "Open";

        // Required to use FOP on WinUI
        var hwnd = Microsoft.UI.Win32Interop.GetActiveWindow();
        Windows.Win32.PInvoke.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    [RelayCommand]
    public async Task ExecutePlanAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(TaskDefinitionsPath) || string.IsNullOrEmpty(IntakeEventsPath))
        {
            StatusMessage = "Please select both Task Definitions and Intake Events files";
            return;
        }

        IsLoading = true;
        StatusMessage = "Executing plan...";
        Errors.Clear();
        Warnings.Clear();

        try
        {
            var result = await ExecutionPlanService.LoadAndPlanAsync(
                TaskDefinitionsPath,
                IntakeEventsPath,
                DurationManifestPath,
                IncrementStart,
                IncrementEnd,
                ct);

            if (result.Success)
            {
                StatusMessage = "Plan executed successfully";
                UpdateViewModels(result);
                ExecutionPlanLoaded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusMessage = result.ErrorMessage ?? "Plan execution failed";
                Errors.Add(result.ErrorMessage ?? "Unknown error");
            }

            foreach (var error in result.Errors)
                Errors.Add(error);

            foreach (var warning in result.Warnings)
                Warnings.Add(warning);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Plan execution cancelled";
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

    private void UpdateViewModels(ExecutionPlanResult result)
    {
        if (result.Analysis == null) return;

        DashboardViewModel.UpdateFromPlanStatistics(ExecutionPlanService.GetPlanStatistics(result.Analysis));
        DashboardViewModel.UpdateMessages(result.Errors, result.Warnings);

        TimelineViewModel.UpdateTimeline(ExecutionPlanService.GetExecutionTasks(result.Analysis));
        ViolationsViewModel.UpdateViolations(ExecutionPlanService.GetDeadlineViolations(result.Analysis));
    }
}
