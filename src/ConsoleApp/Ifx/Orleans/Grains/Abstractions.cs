using ConsoleApp.Ifx.Models;
using Orleans;

namespace ConsoleApp.Ifx.Orleans.Grains;

/// <summary>
/// Grain interface for calculating and refining execution instance time slots.
/// Uses iterative updates to resolve dependencies and adjust start times.
/// </summary>
public interface IExecutionTaskGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the grain with task definition and event details.
    /// </summary>
    Task InitializeAsync(ExecutionEventDefinition eventDef, ExecutionDuration duration, CancellationToken ct);

    /// <summary>
    /// Get current calculated execution instance.
    /// </summary>
    Task<ExecutionInstanceEnhanced> GetExecutionInstanceAsync(CancellationToken ct);

    /// <summary>
    /// Update start time based on prerequisite completion times.
    /// Called iteratively until all dependencies converge.
    /// Returns the new start time (may be same as before if no adjustment needed).
    /// </summary>
    Task<DateTime> UpdateStartTimeAsync(IReadOnlyDictionary<string, DateTime> prerequisiteCompletions, CancellationToken ct);

    /// <summary>
    /// Get planned completion time (start + duration).
    /// </summary>
    Task<DateTime> GetPlannedCompletionAsync(CancellationToken ct);

    /// <summary>
    /// Validate deadline compliance.
    /// Returns tuple of (IsValid, Status, Message).
    /// </summary>
    Task<(bool IsValid, ExecutionStatus Status, string? Message)> ValidateDeadlineAsync(CancellationToken ct);

    /// <summary>
    /// Mark this task as ready (all constraints satisfied).
    /// </summary>
    Task MarkAsReadyAsync(CancellationToken ct);

    /// <summary>
    /// Get list of prerequisite task IDs that this task depends on.
    /// </summary>
    Task<IReadOnlySet<string>> GetPrerequisitesAsync(CancellationToken ct);

    /// <summary>
    /// Get the unique key for this execution event.
    /// </summary>
    Task<string> GetExecutionEventKeyAsync(CancellationToken ct);

    /// <summary>
    /// Get deadline slack time (time between planned completion and deadline).
    /// Positive value = slack (task completes early).
    /// Negative value = deficit (task misses deadline).
    /// Null = no deadline specified.
    /// </summary>
    Task<TimeSpan?> GetDeadlineSlackAsync(CancellationToken ct);

    /// <summary>
    /// Set prerequisite task IDs explicitly. Used during stratification phase.
    /// </summary>
    Task SetPrerequisitesAsync(IReadOnlySet<string> prerequisiteTaskIds, CancellationToken ct);

    /// <summary>
    /// Get the duration of this task.
    /// </summary>
    Task<ExecutionDuration> GetDurationAsync(CancellationToken ct);
}

/// <summary>
/// Coordinator grain that orchestrates iterative calculation across all task grains.
/// Manages the iterative refinement loop (Phase 5 of execution planning).
/// </summary>
public interface IExecutionPlanCoordinatorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Execute iterative calculation loop until all task time slots converge.
    /// Returns the final resolved execution plan.
    /// </summary>
    Task<ExecutionPlan> CalculateExecutionPlanAsync(
        IReadOnlyList<ExecutionEventDefinition> executionEvents,
        IReadOnlyList<ExecutionInstanceEnhanced> initialInstances,
        DateTime periodStartDate,
        CancellationToken ct);

    /// <summary>
    /// Run one iteration of time slot refinement across all grains.
    /// Updates prerequisite completion times and returns convergence status.
    /// </summary>
    Task<(bool HasConverged, int UpdateCount)> RefineTimeSlotIterationAsync(CancellationToken ct);

    /// <summary>
    /// Get current status of execution plan calculation.
    /// </summary>
    Task<ExecutionPlan> GetCurrentPlanAsync(CancellationToken ct);

    /// <summary>
    /// Get convergence information including iteration count and status.
    /// </summary>
    Task<ConvergenceInfo> GetConvergenceInfoAsync(CancellationToken ct);

    /// <summary>
    /// Get list of tasks that have deadline conflicts (cannot meet deadlines).
    /// </summary>
    Task<IReadOnlyList<string>> GetConflictingTasksAsync(CancellationToken ct);
}

/// <summary>
/// Represents convergence information for an execution plan refinement.
/// </summary>
public record ConvergenceInfo
{
    /// <summary>Gets or initializes whether convergence was reached.</summary>
    public bool HasConverged { get; init; }

    /// <summary>Gets or initializes the number of iterations performed.</summary>
    public int IterationCount { get; init; }

    /// <summary>Gets or initializes the reason for convergence termination.</summary>
    public ConvergenceReason Reason { get; init; }

    /// <summary>Gets or initializes the number of valid (deadline-compliant) tasks.</summary>
    public int ValidTaskCount { get; init; }

    /// <summary>Gets or initializes the number of invalid (deadline-missing) tasks.</summary>
    public int InvalidTaskCount { get; init; }

    /// <summary>Gets or initializes the completion time of the critical path.</summary>
    public DateTime? CriticalPathCompletion { get; init; }

    /// <summary>Gets or initializes the total time spent on convergence.</summary>
    public TimeSpan ElapsedTime { get; init; }
}

/// <summary>
/// Enum indicating why convergence was reached.
/// </summary>
public enum ConvergenceReason
{
    /// <summary>All tasks became valid or no changes in last iteration.</summary>
    Organic = 0,

    /// <summary>Reached maximum iteration limit.</summary>
    MaxIterations = 1,

    /// <summary>Reached time budget.</summary>
    TimeLimit = 2,

    /// <summary>Forced by caller.</summary>
    ForceConverged = 3
}
