namespace App.TaskSequencer.Contracts.Orleans.Grains;

using App.TaskSequencer.Contracts.Models;
using Orleans;

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
    /// Update start time based on prerequisite completions.
    /// </summary>
    Task<DateTime> UpdateStartTimeAsync(IReadOnlyDictionary<string, DateTime> prerequisiteCompletions, CancellationToken ct);

    /// <summary>
    /// Get the planned completion time (start + duration).
    /// </summary>
    Task<DateTime> GetPlannedCompletionAsync(CancellationToken ct);

    /// <summary>
    /// Validate deadline compliance.
    /// </summary>
    Task<bool> ValidateDeadlineAsync(CancellationToken ct);

    /// <summary>
    /// Mark grain as ready for scheduling.
    /// </summary>
    Task MarkAsReadyAsync(CancellationToken ct);
}

/// <summary>
/// Coordinator grain for orchestrating execution plan refinement iterations.
/// </summary>
public interface IExecutionPlanCoordinatorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Calculate complete execution plan with iterative refinement.
    /// </summary>
    Task<ExecutionPlan> CalculateExecutionPlanAsync(
        IReadOnlyList<ExecutionInstanceEnhanced> initialPlan,
        int maxIterations,
        CancellationToken ct);

    /// <summary>
    /// Execute single refinement iteration.
    /// </summary>
    Task<(bool converged, int updateCount, bool allValid)> RefineTimeSlotIterationAsync(CancellationToken ct);

    /// <summary>
    /// Get current convergence status.
    /// </summary>
    Task<(int iterationCount, bool converged, int lastUpdateCount)> GetConvergenceStatusAsync(CancellationToken ct);

    /// <summary>
    /// Get current execution plan snapshot.
    /// </summary>
    Task<ExecutionPlan> GetCurrentPlanAsync(CancellationToken ct);
}
