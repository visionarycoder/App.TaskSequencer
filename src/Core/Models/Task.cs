namespace App.TaskSequencer.Domain.Models;

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
);