using ConsoleApp.Ifx.Models;

namespace ConsoleApp.Ifx.Services;

/// <summary>
/// Represents a directed acyclic graph (DAG) of task dependencies.
/// </summary>
public interface IDependencyGraph
{
    /// <summary>
    /// Gets the mapping of tasks to their direct prerequisites.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToPrerequisites { get; }

    /// <summary>
    /// Gets the mapping of tasks to their direct dependents.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToDependents { get; }

    /// <summary>
    /// Gets the topological sort order of all tasks.
    /// Tasks can be executed in this order respecting all dependencies.
    /// </summary>
    IReadOnlyList<string> TopologicalOrder { get; }

    /// <summary>
    /// Gets all tasks in the graph.
    /// </summary>
    IReadOnlySet<string> AllTaskIds { get; }

    /// <summary>
    /// Computes the depth of a task from root tasks (longest path backward).
    /// Root tasks have depth 0.
    /// </summary>
    int ComputeDepthFromRoot(string taskId);

    /// <summary>
    /// Computes the depth of a task to leaf tasks (longest path forward).
    /// Leaf tasks have depth 0.
    /// </summary>
    int ComputeDepthToLeaf(string taskId);
}

/// <summary>
/// Result of task stratification (level assignment).
/// </summary>
public record StratificationResult
{
    /// <summary>Gets the mapping of task IDs to their stratification levels.</summary>
    public required Dictionary<string, int> TaskToLevel { get; init; }

    /// <summary>Gets the mapping of levels to task IDs at that level.</summary>
    public required Dictionary<int, IReadOnlyList<string>> LevelToTasks { get; init; }

    /// <summary>Gets the maximum stratification level.</summary>
    public required int MaxLevel { get; init; }

    /// <summary>Gets the total number of tasks in the stratification.</summary>
    public int TotalTasks => TaskToLevel.Count;
}

/// <summary>
/// Represents execution patterns for task grouping.
/// </summary>
public enum ExecutionPattern
{
    /// <summary>Task with no prerequisites and no dependents.</summary>
    Independent = 0,

    /// <summary>Linear chain of tasks: A → B → C.</summary>
    SequentialChain = 1,

    /// <summary>One task with multiple dependents: A → B, C, D.</summary>
    FanOut = 2,

    /// <summary>Multiple prerequisite tasks feeding one: A, B, C → D.</summary>
    FanIn = 3,

    /// <summary>Complex DAG with mixed patterns.</summary>
    ComplexDAG = 4
}

/// <summary>
/// Represents a group of tasks with similar execution characteristics.
/// </summary>
public record TaskExecutionGroup
{
    /// <summary>Gets the unique identifier for this group.</summary>
    public required string GroupId { get; init; }

    /// <summary>Gets the execution pattern for this group.</summary>
    public required ExecutionPattern Pattern { get; init; }

    /// <summary>Gets the task IDs in this group.</summary>
    public required IReadOnlyList<string> TaskIds { get; init; }

    /// <summary>Gets the stratification level of this group.</summary>
    public required int StratificationLevel { get; init; }

    /// <summary>Gets whether tasks in this group can execute in parallel.</summary>
    public required bool IsParallelizable { get; init; }

    /// <summary>Gets the suggested execution order for tasks in this group (if sequential).</summary>
    public IReadOnlyList<string>? ExecutionOrder { get; init; }
}

/// <summary>
/// Criticality metrics for tasks in execution plan.
/// </summary>
public record CriticalityMetrics
{
    /// <summary>Gets the list of task IDs on the critical path (slack = 0).</summary>
    public required IReadOnlyList<string> CriticalTasks { get; init; }

    /// <summary>Gets the planned completion time of the critical path.</summary>
    public required DateTime CriticalPathCompletion { get; init; }

    /// <summary>Gets the slack time for each task (negative = deadline miss).</summary>
    public required IReadOnlyDictionary<string, TimeSpan> TaskSlack { get; init; }

    /// <summary>Gets the list of critical path task IDs in execution order.</summary>
    public IReadOnlyList<string>? CriticalPathSequence { get; init; }
}

/// <summary>
/// Complete analysis of execution plan from dependency analysis through criticality.
/// </summary>
public record ExecutionPlanAnalysis
{
    /// <summary>Gets the dependency graph (Phase 2).</summary>
    public required IDependencyGraph DependencyGraph { get; init; }

    /// <summary>Gets the task stratification result (Phase 3).</summary>
    public required StratificationResult Stratification { get; init; }

    /// <summary>Gets the execution groups (Phase 3).</summary>
    public required IReadOnlyList<TaskExecutionGroup> ExecutionGroups { get; init; }

    /// <summary>Gets the criticality metrics (Phase 3).</summary>
    public required CriticalityMetrics CriticalityInfo { get; init; }

    /// <summary>Gets the validation result from dependency analysis.</summary>
    public required ValidationResult ValidationResult { get; init; }

    /// <summary>Gets when the analysis was completed.</summary>
    public DateTime AnalysisCompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Validation result from dependency analysis.
/// </summary>
public record ValidationResult
{
    /// <summary>Gets whether validation passed (no circular dependencies, all prerequisites satisfied).</summary>
    public required bool IsValid { get; init; }

    /// <summary>Gets the list of validation errors found.</summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>Gets the list of validation warnings found.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Gets task IDs that have circular dependencies (if any).</summary>
    public IReadOnlyList<IReadOnlyList<string>>? CircularDependencies { get; init; }

    /// <summary>Gets task IDs with missing prerequisites.</summary>
    public IReadOnlySet<string>? MissingPrerequisiteTasks { get; init; }

    /// <summary>Gets task IDs that are unreachable (orphaned).</summary>
    public IReadOnlySet<string>? UnreachableTasks { get; init; }
}
