namespace Core.Models;

/// <summary>
/// Represents a scheduled execution instance of a task.
/// Result of ExecutionPlanner sequencing.
/// </summary>
public record ExecutionInstance(
    int Id,
    int TaskId,
    DateTime ScheduledStartTime,
    DateTime? FunctionalStartTime,
    DateTime? RequiredEndTime,
    uint DurationMinutes,
    IReadOnlySet<string> PrerequisiteTaskIds,
    bool IsValid,
    string? ValidationMessage = null
)
{
    /// <summary>
    /// Gets duration as TimeSpan.
    /// </summary>
    public TimeSpan GetDuration() => TimeSpan.FromMinutes(DurationMinutes);

    /// <summary>
    /// Calculates planned completion time.
    /// </summary>
    public DateTime GetPlannedCompletionTime() => ScheduledStartTime.Add(GetDuration());

    /// <summary>
    /// Validates execution can complete by required end time.
    /// </summary>
    public bool CanCompleteByDeadline()
    {
        if (!RequiredEndTime.HasValue)
            return true;

        return GetPlannedCompletionTime() <= RequiredEndTime.Value;
    }
}
