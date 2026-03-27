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
    Task InitializeAsync(ExecutionEventDefinition eventDef, ExecutionDuration duration);

    /// <summary>
    /// Get current calculated execution instance.
    /// </summary>
    Task<ExecutionInstanceEnhanced> GetExecutionInstanceAsync();

    /// <summary>
    /// Update start time based on prerequisite completion times.
    /// Called iteratively until all dependencies converge.
    /// </summary>
    Task<DateTime> UpdateStartTimeAsync(Dictionary<string, DateTime> prerequisiteCompletions);

    /// <summary>
    /// Get planned completion time (start + duration).
    /// </summary>
    Task<DateTime> GetPlannedCompletionAsync();

    /// <summary>
    /// Validate deadline compliance.
    /// </summary>
    Task<(bool IsValid, string? Message)> ValidateDeadlineAsync();

    /// <summary>
    /// Mark this task as ready (all constraints satisfied).
    /// </summary>
    Task MarkAsReadyAsync();

    /// <summary>
    /// Get list of prerequisite task IDs that this task depends on.
    /// </summary>
    Task<IReadOnlySet<string>> GetPrerequisitesAsync();

    /// <summary>
    /// Get the unique key for this execution event.
    /// </summary>
    Task<string> GetExecutionEventKeyAsync();
}

/// <summary>
/// Coordinator grain that orchestrates iterative calculation across all task grains.
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
        DateTime periodStartDate);

    /// <summary>
    /// Run one iteration of time slot refinement across all grains.
    /// Updates prerequisite completion times and returns convergence status.
    /// </summary>
    Task<(bool HasConverged, int UpdateCount)> RefineTimeSlotIterationAsync();

    /// <summary>
    /// Get current status of execution plan calculation.
    /// </summary>
    Task<ExecutionPlan> GetCurrentPlanAsync();
}
