using App.TaskSequencer.Domain.Models;
using Orleans;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.TaskSequencer.Orchestration.Orleans.Grains;

/// <summary>
/// Grain interface for high-level execution plan coordination and management.
/// Orchestrates multiple execution plan calculations and tracks execution state.
/// </summary>
public interface IExecutionPlanCoordinationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initiates execution plan calculation for a planning period.
    /// </summary>
    Task<ExecutionPlan> StartExecutionPlanningAsync(
        IReadOnlyList<ExecutionEventDefinition> executionEvents,
        DateTime planningPeriodStart,
        DateTime planningPeriodEnd);

    /// <summary>
    /// Gets the current execution plan for a period.
    /// </summary>
    Task<ExecutionPlan?> GetExecutionPlanAsync(DateTime planDate);

    /// <summary>
    /// Gets execution status for a specific task.
    /// </summary>
    Task<ExecutionInstanceEnhanced?> GetTaskExecutionStatusAsync(string taskId);

    /// <summary>
    /// Gets all tasks in the current plan.
    /// </summary>
    Task<IReadOnlyList<ExecutionInstanceEnhanced>> GetAllTasksAsync();

    /// <summary>
    /// Marks a task as completed and updates dependent tasks.
    /// </summary>
    Task MarkTaskCompletedAsync(string taskId, DateTime actualCompletionTime);

    /// <summary>
    /// Detects deadline conflicts and returns list of at-risk tasks.
    /// </summary>
    Task<IReadOnlyList<string>> DetectDeadlineConflictsAsync();

    /// <summary>
    /// Rebalances execution plan when task duration changes.
    /// </summary>
    Task<bool> RebalancePlanAsync(string taskId, ExecutionDuration newDuration);

    /// <summary>
    /// Gets coordination status and metrics.
    /// </summary>
    Task<CoordinationStatus> GetCoordinationStatusAsync();

    /// <summary>
    /// Clears the current plan and resets coordination state.
    /// </summary>
    Task ResetAsync();
}

/// <summary>
/// Represents the coordination status and metrics for execution planning.
/// </summary>
public record CoordinationStatus
{
    /// <summary>Gets or initializes the current planning period start date.</summary>
    public DateTime PlanningPeriodStart { get; init; }

    /// <summary>Gets or initializes the current planning period end date.</summary>
    public DateTime PlanningPeriodEnd { get; init; }

    /// <summary>Gets or initializes the total number of tasks being coordinated.</summary>
    public int TotalTaskCount { get; init; }

    /// <summary>Gets or initializes the number of tasks with valid execution times.</summary>
    public int ValidTaskCount { get; init; }

    /// <summary>Gets or initializes the number of tasks with deadline conflicts.</summary>
    public int ConflictTaskCount { get; init; }

    /// <summary>Gets or initializes the number of completed tasks.</summary>
    public int CompletedTaskCount { get; init; }

    /// <summary>Gets or initializes the number of pending tasks.</summary>
    public int PendingTaskCount { get; init; }

    /// <summary>Gets or initializes the critical path completion time.</summary>
    public DateTime? CriticalPathCompletion { get; init; }

    /// <summary>Gets or initializes when the coordination status was last updated.</summary>
    public DateTime LastUpdateTime { get; init; }

    /// <summary>Gets or initializes whether the plan has converged.</summary>
    public bool HasConverged { get; init; }

    /// <summary>Gets or initializes the number of refinement iterations performed.</summary>
    public int IterationCount { get; init; }
}

/// <summary>
/// Implementation of execution plan coordination grain.
/// Provides high-level coordination for multiple execution plans and task management.
/// </summary>
public class ExecutionPlanCoordinationGrain : Grain, IExecutionPlanCoordinationGrain
{
    private Dictionary<string, ExecutionPlan> executionPlans = new();
    private Dictionary<string, ExecutionInstanceEnhanced> taskExecutionStatus = new();
    private Dictionary<string, IExecutionPlanCoordinatorGrain> coordinatorGrains = new();
    private DateTime planningPeriodStart;
    private DateTime planningPeriodEnd;
    private int iterationCount = 0;
    private DateTime lastUpdateTime = DateTime.UtcNow;
    private bool hasConverged = false;

    /// <summary>
    /// Initiates execution plan calculation for a planning period.
    /// </summary>
    public async Task<ExecutionPlan> StartExecutionPlanningAsync(
        IReadOnlyList<ExecutionEventDefinition> executionEvents,
        DateTime planningPeriodStart,
        DateTime planningPeriodEnd)
    {
        ArgumentNullException.ThrowIfNull(executionEvents);

        if (planningPeriodStart >= planningPeriodEnd)
            throw new ArgumentException("Planning period start must be before end date.");

        this.planningPeriodStart = planningPeriodStart;
        this.planningPeriodEnd = planningPeriodEnd;
        this.iterationCount = 0;
        this.hasConverged = false;

        // Create initial execution instances
        var initialInstances = executionEvents
            .Select((e, index) => new ExecutionInstanceEnhanced(
                Id: index,
                TaskId: index,
                TaskIdString: e.TaskId,
                TaskName: e.TaskName,
                ScheduledStartTime: e.ScheduledTime.ApplyToDate(planningPeriodStart),
                FunctionalStartTime: null,
                RequiredEndTime: e.IntakeRequirement?.GetIntakeDeadline(planningPeriodStart),
                Duration: ExecutionDuration.Default(),
                PlannedCompletionTime: e.ScheduledTime.ApplyToDate(planningPeriodStart).AddHours(1),
                PrerequisiteTaskIds: e.PrerequisiteTaskIds,
                IsValid: true,
                Status: ExecutionStatus.AwaitingPrerequisites,
                ValidationMessage: null
            ))
            .ToList();

        // Get or create coordinator grain for this period
        var coordinatorKey = planningPeriodStart.ToString("yyyy-MM-dd");
        var coordinatorGrain = GrainFactory.GetGrain<IExecutionPlanCoordinatorGrain>(coordinatorKey);
        this.coordinatorGrains[coordinatorKey] = coordinatorGrain;

        // Calculate execution plan
        var executionPlan = await coordinatorGrain.CalculateExecutionPlanAsync(
            executionEvents,
            initialInstances.AsReadOnly(),
            planningPeriodStart,
            CancellationToken.None
        );

        // Store plan and task status
        this.executionPlans[coordinatorKey] = executionPlan;

        foreach (var task in executionPlan.Tasks)
        {
            this.taskExecutionStatus[task.TaskIdString] = task;
        }

        this.lastUpdateTime = DateTime.UtcNow;
        this.hasConverged = executionPlan.DeadlineMisses.Count == 0;

        return executionPlan;
    }

    /// <summary>
    /// Gets the current execution plan for a period.
    /// </summary>
    public Task<ExecutionPlan?> GetExecutionPlanAsync(DateTime planDate)
    {
        var key = planDate.ToString("yyyy-MM-dd");
        
        if (this.executionPlans.TryGetValue(key, out var plan))
            return Task.FromResult((ExecutionPlan?)plan);

        return Task.FromResult((ExecutionPlan?)null);
    }

    /// <summary>
    /// Gets execution status for a specific task.
    /// </summary>
    public Task<ExecutionInstanceEnhanced?> GetTaskExecutionStatusAsync(string taskId)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        if (this.taskExecutionStatus.TryGetValue(taskId, out var instance))
            return Task.FromResult((ExecutionInstanceEnhanced?)instance);

        return Task.FromResult((ExecutionInstanceEnhanced?)null);
    }

    /// <summary>
    /// Gets all tasks in the current plan.
    /// </summary>
    public Task<IReadOnlyList<ExecutionInstanceEnhanced>> GetAllTasksAsync()
    {
        var allTasks = (IReadOnlyList<ExecutionInstanceEnhanced>)this.taskExecutionStatus.Values.ToList();
        return Task.FromResult(allTasks);
    }

    /// <summary>
    /// Marks a task as completed and updates dependent tasks.
    /// </summary>
    public async Task MarkTaskCompletedAsync(string taskId, DateTime actualCompletionTime)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        if (!this.taskExecutionStatus.TryGetValue(taskId, out var task))
            throw new InvalidOperationException($"Task not found: {taskId}");

        // Update task status
        var updatedTask = task with
        {
            Status = ExecutionStatus.Completed,
            PlannedCompletionTime = actualCompletionTime
        };

        this.taskExecutionStatus[taskId] = updatedTask;

        // Update dependent tasks
        var dependentTasks = this.taskExecutionStatus.Values
            .Where(t => t.PrerequisiteTaskIds.Contains(taskId))
            .ToList();

        foreach (var dependent in dependentTasks)
        {
            if (dependent.ScheduledStartTime < actualCompletionTime)
            {
                var adjustedDependent = dependent with
                {
                    FunctionalStartTime = actualCompletionTime,
                    PlannedCompletionTime = actualCompletionTime.Add(dependent.Duration.ToTimeSpan()),
                    Status = ExecutionStatus.AwaitingPrerequisites
                };

                this.taskExecutionStatus[dependent.TaskIdString] = adjustedDependent;
            }
        }

        this.lastUpdateTime = DateTime.UtcNow;

        // Re-coordinate with updated task information
        await RecoordinateAsync();
    }

    /// <summary>
    /// Detects deadline conflicts and returns list of at-risk tasks.
    /// </summary>
    public Task<IReadOnlyList<string>> DetectDeadlineConflictsAsync()
    {
        var conflicts = (IReadOnlyList<string>)this.taskExecutionStatus.Values
            .Where(t => t.RequiredEndTime.HasValue && 
                       t.PlannedCompletionTime > t.RequiredEndTime.Value)
            .Select(t => t.TaskIdString)
            .ToList();

        return Task.FromResult(conflicts);
    }

    /// <summary>
    /// Rebalances execution plan when task duration changes.
    /// </summary>
    public async Task<bool> RebalancePlanAsync(string taskId, ExecutionDuration newDuration)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(newDuration);

        if (!this.taskExecutionStatus.TryGetValue(taskId, out var task))
            throw new InvalidOperationException($"Task not found: {taskId}");

        // Update task duration
        var startTime = task.FunctionalStartTime ?? task.ScheduledStartTime;
        var updatedTask = task with
        {
            Duration = newDuration,
            PlannedCompletionTime = startTime.Add(newDuration.ToTimeSpan()),
            Status = ExecutionStatus.DurationPending
        };

        this.taskExecutionStatus[taskId] = updatedTask;

        // Cascade changes to dependent tasks
        await CascadeDurationChangesAsync(taskId);

        this.lastUpdateTime = DateTime.UtcNow;
        this.hasConverged = false;

        // Re-coordinate
        await RecoordinateAsync();

        return true;
    }

    /// <summary>
    /// Gets coordination status and metrics.
    /// </summary>
    public Task<CoordinationStatus> GetCoordinationStatusAsync()
    {
        var conflicts = this.taskExecutionStatus.Values
            .Count(t => t.RequiredEndTime.HasValue && 
                       t.PlannedCompletionTime > t.RequiredEndTime.Value);

        var completed = this.taskExecutionStatus.Values
            .Count(t => t.Status == ExecutionStatus.Completed);

        var pending = this.taskExecutionStatus.Values
            .Count(t => t.Status != ExecutionStatus.Completed && t.Status != ExecutionStatus.Invalid);

        var criticalPath = this.taskExecutionStatus.Values.Any()
            ? this.taskExecutionStatus.Values.Max(t => t.PlannedCompletionTime)
            : (DateTime?)null;

        var status = new CoordinationStatus
        {
            PlanningPeriodStart = this.planningPeriodStart,
            PlanningPeriodEnd = this.planningPeriodEnd,
            TotalTaskCount = this.taskExecutionStatus.Count,
            ValidTaskCount = this.taskExecutionStatus.Values.Count(t => t.IsValid),
            ConflictTaskCount = conflicts,
            CompletedTaskCount = completed,
            PendingTaskCount = pending,
            CriticalPathCompletion = criticalPath,
            LastUpdateTime = this.lastUpdateTime,
            HasConverged = this.hasConverged,
            IterationCount = this.iterationCount
        };

        return Task.FromResult(status);
    }

    /// <summary>
    /// Clears the current plan and resets coordination state.
    /// </summary>
    public Task ResetAsync()
    {
        this.executionPlans.Clear();
        this.taskExecutionStatus.Clear();
        this.coordinatorGrains.Clear();
        this.iterationCount = 0;
        this.hasConverged = false;
        this.lastUpdateTime = DateTime.UtcNow;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Private helper: Cascade duration changes to dependent tasks.
    /// </summary>
    private async Task CascadeDurationChangesAsync(string taskId)
    {
        var queue = new Queue<string>();
        queue.Enqueue(taskId);
        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var currentTaskId = queue.Dequeue();

            if (processed.Contains(currentTaskId))
                continue;

            processed.Add(currentTaskId);

            if (!this.taskExecutionStatus.TryGetValue(currentTaskId, out var currentTask))
                continue;

            // Find all tasks that depend on this one
            var dependents = this.taskExecutionStatus.Values
                .Where(t => t.PrerequisiteTaskIds.Contains(currentTaskId))
                .ToList();

            var currentCompletion = currentTask.PlannedCompletionTime;

            foreach (var dependent in dependents)
            {
                var newStartTime = dependent.ScheduledStartTime < currentCompletion
                    ? currentCompletion
                    : dependent.ScheduledStartTime;

                var adjustedDependent = dependent with
                {
                    FunctionalStartTime = newStartTime,
                    PlannedCompletionTime = newStartTime.Add(dependent.Duration.ToTimeSpan()),
                    Status = ExecutionStatus.DurationPending
                };

                this.taskExecutionStatus[dependent.TaskIdString] = adjustedDependent;
                queue.Enqueue(dependent.TaskIdString);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Private helper: Re-coordinates execution plan after changes.
    /// </summary>
    private async Task RecoordinateAsync()
    {
        if (!this.coordinatorGrains.Any())
            return;

        this.iterationCount++;

        // Trigger re-coordination on all coordinator grains
        var coordinationTasks = this.coordinatorGrains.Values
            .Select(g => g.RefineTimeSlotIterationAsync(CancellationToken.None))
            .ToList();

        var results = await Task.WhenAll(coordinationTasks);

        // Update convergence status
        this.hasConverged = results.All(r => r.HasConverged);

        this.lastUpdateTime = DateTime.UtcNow;
    }
}
