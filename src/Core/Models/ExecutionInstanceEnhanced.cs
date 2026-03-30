namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Enhanced execution instance with comprehensive scheduling and validation details.
/// Result of the complete dependency resolution and deadline feasibility analysis.
/// </summary>
public record ExecutionInstanceEnhanced(
    int Id,
    int TaskId,
    string TaskIdString,
    string TaskName,
    DateTime ScheduledStartTime,
    DateTime? FunctionalStartTime,
    DateTime? RequiredEndTime,
    ExecutionDuration Duration,
    DateTime PlannedCompletionTime,
    IReadOnlySet<string> PrerequisiteTaskIds,
    bool IsValid,
    ExecutionStatus Status = ExecutionStatus.Initializing,
    string? ValidationMessage = null
)
{
    /// <summary>
    /// Checks if execution can complete by required end time (if deadline exists).
    /// </summary>
    public bool CanCompleteByDeadline()
    {
        if (!RequiredEndTime.HasValue)
            return true;

        return PlannedCompletionTime <= RequiredEndTime.Value;
    }

    /// <summary>
    /// Gets the actual start time (considering prerequisite adjustments).
    /// </summary>
    public DateTime GetActualStartTime() =>
        FunctionalStartTime ?? ScheduledStartTime;

    /// <summary>
    /// Gets duration as TimeSpan.
    /// </summary>
    public TimeSpan GetDurationSpan() => Duration.ToTimeSpan();

    /// <summary>
    /// Gets the time between planned completion and deadline (slack time).
    /// Negative value means deadline miss.
    /// </summary>
    public TimeSpan? GetDeadlineSlack()
    {
        if (!RequiredEndTime.HasValue)
            return null;

        return RequiredEndTime.Value - PlannedCompletionTime;
    }

    /// <summary>
    /// Checks if duration is still estimated (vs. actual from history).
    /// </summary>
    public bool IsDurationEstimated => Duration.IsEstimated;
}
