using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.TaskSequencer.Client.Desktop.Maui.Services;
using System.Collections.ObjectModel;

namespace App.TaskSequencer.Client.Desktop.Maui.ViewModels;

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
    public async Task ExportToCSVAsync()
    {
        try
        {
            var fileName = $"violations_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            using (var writer = new StreamWriter(filePath))
            {
                await writer.WriteLineAsync("Task ID,Task Name,Required End,Projected End,Overdue Minutes");

                foreach (var violation in Violations)
                {
                    //TODO: DeadlineViolation model needs Severity property for full export
                    await writer.WriteLineAsync(
                        $"\"{violation.TaskId}\",\"{violation.TaskName}\"," +
                        $"\"{violation.RequiredEnd:g}\",\"{violation.ProjectedEnd:g}\"," +
                        $"\"{violation.OverdueMinutes:F2}\"");
                }
            }

            StatusMessage = $"Exported to {fileName}";
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
        //TODO: Add Severity property to DeadlineViolation to enable severity-based filtering
        //CriticalViolations = violations.Count(v => v.Severity == "Critical");
        //ModerateViolations = violations.Count(v => v.Severity == "Moderate");
        //MinorViolations = violations.Count(v => v.Severity == "Minor");
        CriticalViolations = 0;
        ModerateViolations = 0;
        MinorViolations = 0;

        StatusMessage = $"Found {violations.Count} deadline violations";
    }

    private void UpdateFilteredViolations()
    {
        if (FilterSeverity == "All")
            return; // Show all

        //TODO: Implement severity filtering when Severity property is available
        //var filtered = Violations.Where(v => v.Severity == FilterSeverity).ToList();
        //Violations.Clear();
        //foreach (var violation in filtered)
        //    Violations.Add(violation);
    }

    /// <summary>
    /// Get color for violation severity.
    /// </summary>
    public static Color GetSeverityColor(string severity)
    {
        return severity switch
        {
            "Critical" => Colors.Red,
            "Moderate" => Colors.Orange,
            "Minor" => Colors.Yellow,
            _ => Colors.Green
        };
    }
}
