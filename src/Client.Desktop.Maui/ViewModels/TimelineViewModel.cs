using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using App.TaskSequencer.Client.Desktop.Maui.Services;
using System.Collections.ObjectModel;

namespace App.TaskSequencer.Client.Desktop.Maui.ViewModels;

/// <summary>
/// ViewModel for the Timeline (Gantt Chart) page.
/// </summary>
public partial class TimelineViewModel : ObservableObject
{
    private readonly ExecutionPlanService ExecutionPlanService;

    [ObservableProperty]
    private ObservableCollection<ExecutionTaskDisplay> executionTasks = new();

    [ObservableProperty]
    private DateTime timelineStart;

    [ObservableProperty]
    private DateTime timelineEnd;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private ExecutionTaskDisplay? selectedTask;

    public TimelineViewModel()
    {
        ExecutionPlanService = null!;
    }

    public TimelineViewModel(ExecutionPlanService executionPlanService)
    {
        ExecutionPlanService = executionPlanService ?? throw new ArgumentNullException(nameof(executionPlanService));
    }

    [RelayCommand]
    public void SelectTask(ExecutionTaskDisplay? task)
    {
        SelectedTask = task;
    }

    [RelayCommand]
    public async Task ZoomInAsync()
    {
        // Reduce time range for zoom in
        var range = TimelineEnd - TimelineStart;
        var center = TimelineStart.AddTicks(range.Ticks / 2);
        var newRange = TimeSpan.FromTicks(range.Ticks / 2);
        
        TimelineStart = center.Subtract(TimeSpan.FromTicks(newRange.Ticks / 2));
        TimelineEnd = center.Add(TimeSpan.FromTicks(newRange.Ticks / 2));

        await Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ZoomOutAsync()
    {
        // Increase time range for zoom out
        var range = TimelineEnd - TimelineStart;
        var center = TimelineStart.AddTicks(range.Ticks / 2);
        var newRange = TimeSpan.FromTicks(range.Ticks * 2);
        
        TimelineStart = center.Subtract(TimeSpan.FromTicks(newRange.Ticks / 2));
        TimelineEnd = center.Add(TimeSpan.FromTicks(newRange.Ticks / 2));

        await Task.CompletedTask;
    }

    /// <summary>
    /// Populate timeline with execution tasks.
    /// </summary>
    public void UpdateTimeline(List<ExecutionTaskDisplay> tasks)
    {
        ExecutionTasks.Clear();
        foreach (var task in tasks)
            ExecutionTasks.Add(task);

        if (tasks.Any())
        {
            TimelineStart = tasks.Min(t => t.ScheduledStartTime);
            TimelineEnd = tasks.Max(t => t.PlannedCompletionTime);
        }

        StatusMessage = $"Timeline shows {tasks.Count} tasks";
    }

    /// <summary>
    /// Calculate pixel position for task on timeline.
    /// </summary>
    public double GetTaskXPosition(ExecutionTaskDisplay task)
    {
        var duration = (TimelineEnd - TimelineStart).TotalMinutes;
        if (duration <= 0) return 0;

        var taskOffset = (task.ScheduledStartTime - TimelineStart).TotalMinutes;
        return (taskOffset / duration) * 100;
    }

    /// <summary>
    /// Calculate pixel width for task on timeline.
    /// </summary>
    public double GetTaskWidth(ExecutionTaskDisplay task)
    {
        var duration = (TimelineEnd - TimelineStart).TotalMinutes;
        if (duration <= 0) return 0;

        var taskDuration = (task.PlannedCompletionTime - task.ScheduledStartTime).TotalMinutes;
        return (taskDuration / duration) * 100;
    }

    /// <summary>
    /// Get color for task based on status.
    /// </summary>
    public Color GetTaskColor(ExecutionTaskDisplay task)
    {
        return task switch
        {
            { IsValid: false } => Colors.Red,      // Invalid/deadline miss
            { IsCritical: true } => Colors.Orange, // Critical path
            _ => Colors.Green                       // Valid, non-critical
        };
    }
}
