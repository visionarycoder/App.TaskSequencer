namespace Core.Models;

/// <summary>
/// Represents the complete execution plan for a scheduling increment (day, week, etc.).
/// Contains all valid execution instances linked in dependency chain from start to end.
/// </summary>
public record ExecutionPlan(
    string IncrementId,
    DateTime IncrementStart,
    DateTime IncrementEnd,
    IReadOnlyList<ExecutionInstanceEnhanced> Tasks,
    IReadOnlyList<string> TaskChain,
    int TotalValidTasks,
    int TotalInvalidTasks,
    DateTime? CriticalPathCompletion,
    IReadOnlyList<string> DeadlineMisses,
    IReadOnlyList<string>? DSTWarnings = null
)
{
    /// <summary>
    /// Reconstructs the execution sequence from first task (no prerequisites) through last task.
    /// Uses depth-first traversal of the dependency graph.
    /// </summary>
    public IReadOnlyList<string> BuildExecutionSequence()
    {
        var sequence = new List<string>();
        var visited = new HashSet<string>();

        // Find root tasks (no prerequisites or all prerequisites invalid)
        var rootTasks = Tasks
            .Where(t => t.IsValid && t.PrerequisiteTaskIds.Count == 0)
            .ToList();

        foreach (var root in rootTasks)
        {
            TraverseDepthFirst(root.TaskIdString, visited, sequence);
        }

        return sequence;
    }

    private void TraverseDepthFirst(string taskId, HashSet<string> visited, List<string> sequence)
    {
        if (visited.Contains(taskId))
            return;

        visited.Add(taskId);
        sequence.Add(taskId);

        // Find children: tasks that depend on this task
        var children = Tasks
            .Where(t => t.IsValid && t.PrerequisiteTaskIds.Contains(taskId))
            .Select(t => t.TaskIdString)
            .Distinct();

        foreach (var child in children)
        {
            TraverseDepthFirst(child, visited, sequence);
        }
    }

    /// <summary>
    /// Gets summary statistics about the execution plan.
    /// </summary>
    public (int TotalTasks, int ValidTasks, int InvalidTasks, double ValidPercentage) GetSummary()
    {
        var total = TotalValidTasks + TotalInvalidTasks;
        var validPercentage = total > 0 ? (double)TotalValidTasks / total * 100 : 0;
        return (total, TotalValidTasks, TotalInvalidTasks, validPercentage);
    }

    /// <summary>
    /// Checks if all required tasks are executable (valid).
    /// </summary>
    public bool IsFullyExecutable => TotalInvalidTasks == 0 && DeadlineMisses.Count == 0;
}
