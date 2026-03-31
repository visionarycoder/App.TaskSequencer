namespace Core.Models;

/// <summary>
/// Represents the valid execution window for a task.
/// Contains the earliest and latest times the task can start,
/// and flags indicating feasibility and constraints.
/// </summary>
public record ExecutionWindow(
    string TaskId,
    DateTime EarliestStartTime,
    DateTime LatestStartTime,
    bool IsFeasible,
    string? ConstraintViolation = null
)
{
    /// <summary>
    /// Gets the duration of the execution window.
    /// </summary>
    public TimeSpan GetWindowDuration() => LatestStartTime - EarliestStartTime;

    /// <summary>
    /// Checks if a given time falls within the execution window.
    /// </summary>
    public bool IsTimeInWindow(DateTime time) => time >= EarliestStartTime && time <= LatestStartTime;

    /// <summary>
    /// Gets a description of the window for logging/debugging.
    /// </summary>
    public string GetDescription()
    {
        if (!IsFeasible && !string.IsNullOrEmpty(ConstraintViolation))
            return $"Task {TaskId}: INFEASIBLE - {ConstraintViolation}";

        return $"Task {TaskId}: {EarliestStartTime:yyyy-MM-dd HH:mm:ss} to {LatestStartTime:yyyy-MM-dd HH:mm:ss}";
    }
}
