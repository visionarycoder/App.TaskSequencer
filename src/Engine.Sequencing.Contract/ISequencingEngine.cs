namespace Engine.Sequencing.Contract;

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

/// <summary>
/// Represents a single execution instance with calculated timing.
/// </summary>
public record ExecutionInstance(
    string TaskId,
    DateTime FunctionalStartTime,
    DateTime FunctionalEndTime,
    TimeSpan Duration
);

/// <summary>
/// Engine service interface for business rules and sequencing logic.
/// </summary>
public interface ISequencingEngine
{
    /// <summary>
    /// Calculates execution sequence based on dependencies and timing constraints.
    /// Input: TaskDefinitions for a specific execution window.
    /// Output: Ordered ExecutionInstances ready for execution.
    /// </summary>
    IReadOnlyList<ExecutionInstance> CalculateSequenceAsync(IEnumerable<TaskDefinition> taskDefinitions, CancellationToken ct);

    /// <summary>
    /// Validates task definitions for correctness and consistency.
    /// </summary>
    void ValidateTaskDefinitions(IEnumerable<TaskDefinition> tasks);
}
