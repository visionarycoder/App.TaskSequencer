namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Represents a task definition with dependencies and timing requirements.
/// Blueprint for execution sequencing.
/// </summary>
public record TaskDefinition(
    Guid Uid,
    string TaskId,
    uint DurationMinutes,
    IReadOnlySet<string> PrerequisiteIds,
    DateTime? StartTime,
    DateTime? EndTime
)
{
    /// <summary>
    /// Validates task has at least one timing anchor.
    /// </summary>
    public bool ValidateTimingRequirements() => StartTime.HasValue || EndTime.HasValue;

    /// <summary>
    /// Gets duration as TimeSpan.
    /// </summary>
    public TimeSpan GetDuration() => TimeSpan.FromMinutes(DurationMinutes);
}