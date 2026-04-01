using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.TaskSequencer.Client.Desktop.Maui.Services;
using System.Collections.ObjectModel;

namespace Clinet.Desktop.WinUI.ViewModels;

/// <summary>
/// ViewModel for the Violations page - displays deadline misses and conflicts.
/// </summary>
public partial class ViolationsViewModel : ObservableObject
{
    private readonly ExecutionPlanService ExecutionPlanService;

    [ObservableProperty]
    private ObservableCollection<DeadlineViolation> violations = [];

    [ObservableProperty]
    private int totalViolations;

    [ObservableProperty]
    private int criticalViolations;

    [ObservableProperty]
    private int moderateViolations;

    [ObservableProperty]
    private int minorViolations;

    [ObservableProperty]
    private DeadlineViolation? selectedViolation;

    [ObservableProperty]
    private string filterSeverity = "All";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Ready";

    public ViolationsViewModel()
    {
        ExecutionPlanService = null!;
    }

    public ViolationsViewModel(ExecutionPlanService executionPlanService)
    {
        ExecutionPlanService = executionPlanService ?? throw new ArgumentNullException(nameof(executionPlanService));
    }

    [RelayCommand]
    public void SelectViolation(DeadlineViolation? violation)
    {
        SelectedViolation = violation;
    }

    [RelayCommand]
    public void FilterBySeverity(string severity)
    {
        FilterSeverity = severity;
        UpdateFilteredViolations();
    }

    [RelayCommand]
    public async Task ExportToCSVAsync(CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var fileName = $"violations_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

            using (var writer = new StreamWriter(filePath))
            {
                await writer.WriteLineAsync("Task ID,Task Name,Required End,Projected End,Overdue Minutes", ct);

                foreach (var violation in Violations)
                {
                    await writer.WriteLineAsync(
                        $"\"{violation.TaskId}\",\"{violation.TaskName}\"," +
                        $"\"{violation.RequiredEnd:g}\",\"{violation.ProjectedEnd:g}\"," +
                        $"\"{violation.OverdueMinutes:F2}\"", ct);
                }
            }

            StatusMessage = $"Exported to {fileName}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Populate violations from plan analysis.
    /// </summary>
    public void UpdateViolations(List<DeadlineViolation> violations)
    {
        Violations.Clear();
        foreach (var violation in violations)
            Violations.Add(violation);

        TotalViolations = violations.Count;
        CriticalViolations = violations.Count(v => v.OverdueMinutes > 240);
        ModerateViolations = violations.Count(v => v.OverdueMinutes > 60 && v.OverdueMinutes <= 240);
        MinorViolations = violations.Count(v => v.OverdueMinutes <= 60);

        StatusMessage = $"Showing {violations.Count} violations";
    }

    private void UpdateFilteredViolations()
    {
        // Filter implementation
    }
}
