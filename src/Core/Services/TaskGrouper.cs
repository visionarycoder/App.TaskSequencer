using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Groups tasks by execution pattern for optimized scheduling.
/// Implements Phase 3 of the execution planning workflow.
/// 
/// Execution Patterns:
/// - Independent: No dependencies, no dependents
/// - SequentialChain: Linear A → B → C
/// - FanOut: One task with multiple dependents
/// - FanIn: Multiple prerequisites to one task
/// - ComplexDAG: Mixed patterns
/// </summary>
public class TaskGrouper
{
    /// <summary>
    /// Classifies each task by execution pattern.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Mapping of task IDs to execution patterns</returns>
    public async Task<IReadOnlyDictionary<string, ExecutionPattern>> ClassifyTasksAsync(
        IDependencyGraph graph,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        ct.ThrowIfCancellationRequested();

        var classification = new Dictionary<string, ExecutionPattern>();

        foreach (var taskId in graph.AllTaskIds)
        {
            ct.ThrowIfCancellationRequested();

            var pattern = ClassifyTask(taskId, graph);
            classification[taskId] = pattern;
        }

        return classification.AsReadOnly();
    }

    /// <summary>
    /// Creates execution groups based on task patterns and stratification.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="patterns">Task pattern classification</param>
    /// <param name="stratification">Stratification result</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of execution groups</returns>
    public async Task<IReadOnlyList<TaskExecutionGroup>> CreateExecutionGroupsAsync(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionPattern> patterns,
        StratificationResult stratification,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentNullException.ThrowIfNull(stratification);

        ct.ThrowIfCancellationRequested();

        var groups = new List<TaskExecutionGroup>();
        var processedTasks = new HashSet<string>();
        var groupId = 0;

        // Process by stratification level to maintain ordering
        for (int level = 0; level <= stratification.MaxLevel; level++)
        {
            ct.ThrowIfCancellationRequested();

            if (!stratification.LevelToTasks.TryGetValue(level, out var tasksAtLevel))
                continue;

            // Group tasks at this level by pattern
            var tasksByPattern = tasksAtLevel
                .Where(t => !processedTasks.Contains(t))
                .GroupBy(t => patterns.TryGetValue(t, out var p) ? p : ExecutionPattern.Independent)
                .ToList();

            foreach (var patternGroup in tasksByPattern)
            {
                ct.ThrowIfCancellationRequested();

                var pattern = patternGroup.Key;
                var tasksInGroup = patternGroup.ToList();

                // Create groups based on pattern
                var groupsForPattern = CreateGroupsForPattern(
                    pattern,
                    tasksInGroup,
                    level,
                    graph,
                    groupId,
                    ct);

                groups.AddRange(groupsForPattern);
                groupId += groupsForPattern.Count;

                foreach (var task in tasksInGroup)
                    processedTasks.Add(task);
            }
        }

        return groups.AsReadOnly();
    }

    /// <summary>
    /// Gets the execution order hint for tasks in a group.
    /// </summary>
    /// <param name="group">The task execution group</param>
    /// <param name="graph">The dependency graph</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Suggested execution order for tasks in the group</returns>
    public async Task<IReadOnlyList<string>> GetExecutionOrderAsync(
        TaskExecutionGroup group,
        IDependencyGraph graph,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(graph);

        ct.ThrowIfCancellationRequested();

        return group.Pattern switch
        {
            ExecutionPattern.Independent => GetIndependentOrder(group),
            ExecutionPattern.SequentialChain => GetSequentialOrder(group, graph),
            ExecutionPattern.FanOut => GetFanOutOrder(group, graph),
            ExecutionPattern.FanIn => GetFanInOrder(group, graph),
            ExecutionPattern.ComplexDAG => GetComplexDagOrder(group, graph),
            _ => group.TaskIds
        };
    }

    /// <summary>
    /// Gets grouping statistics.
    /// </summary>
    /// <param name="groups">The execution groups</param>
    /// <returns>Statistics about grouping</returns>
    public (int TotalGroups, int TotalTasks, int ParallelizableGroups) GetGroupingStats(
        IReadOnlyList<TaskExecutionGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var totalTasks = groups.Sum(g => g.TaskIds.Count);
        var parallelizableGroups = groups.Count(g => g.IsParallelizable);

        return (groups.Count, totalTasks, parallelizableGroups);
    }

    /// <summary>
    /// Classifies a single task by its pattern.
    /// </summary>
    private ExecutionPattern ClassifyTask(string taskId, IDependencyGraph graph)
    {
        var hasPrerequisites = graph.TaskToPrerequisites.TryGetValue(taskId, out var prereqs)
            && prereqs.Count > 0;

        var hasDependents = graph.TaskToDependents.TryGetValue(taskId, out var dependents)
            && dependents.Count > 0;

        // Independent: no prerequisites, no dependents
        if (!hasPrerequisites && !hasDependents)
            return ExecutionPattern.Independent;

        // Determine based on prerequisite/dependent counts
        var prereqCount = hasPrerequisites ? prereqs.Count : 0;
        var dependentCount = hasDependents ? dependents.Count : 0;

        if (!hasPrerequisites && dependentCount > 1)
            return ExecutionPattern.FanOut;

        if (prereqCount > 1 && dependentCount <= 1)
            return ExecutionPattern.FanIn;

        if (prereqCount == 1 && dependentCount == 1)
            return ExecutionPattern.SequentialChain;

        if (prereqCount > 1 && dependentCount > 1)
            return ExecutionPattern.ComplexDAG;

        // Default to SequentialChain for single in/out
        return ExecutionPattern.SequentialChain;
    }

    /// <summary>
    /// Creates groups for a specific pattern.
    /// </summary>
    private IReadOnlyList<TaskExecutionGroup> CreateGroupsForPattern(
        ExecutionPattern pattern,
        IReadOnlyList<string> tasks,
        int level,
        IDependencyGraph graph,
        int startGroupId,
        CancellationToken ct)
    {
        var groups = new List<TaskExecutionGroup>();

        switch (pattern)
        {
            case ExecutionPattern.Independent:
                // All independent tasks can be grouped together
                if (tasks.Count > 0)
                {
                    groups.Add(new TaskExecutionGroup
                    {
                        GroupId = $"group_{startGroupId:D4}",
                        Pattern = ExecutionPattern.Independent,
                        TaskIds = tasks.ToList().AsReadOnly(),
                        StratificationLevel = level,
                        IsParallelizable = true,
                        ExecutionOrder = null
                    });
                }
                break;

            case ExecutionPattern.SequentialChain:
                // Chain tasks separately as they must be sequential
                foreach (var task in tasks)
                {
                    groups.Add(new TaskExecutionGroup
                    {
                        GroupId = $"group_{startGroupId + groups.Count:D4}",
                        Pattern = ExecutionPattern.SequentialChain,
                        TaskIds = new[] { task }.ToList().AsReadOnly(),
                        StratificationLevel = level,
                        IsParallelizable = false,
                        ExecutionOrder = null
                    });
                }
                break;

            case ExecutionPattern.FanOut:
                // Group each fan-out pattern
                foreach (var task in tasks)
                {
                    var dependentList = graph.TaskToDependents.TryGetValue(task, out var deps)
                        ? deps.ToList()
                        : new List<string> { task };

                    groups.Add(new TaskExecutionGroup
                    {
                        GroupId = $"group_{startGroupId + groups.Count:D4}",
                        Pattern = ExecutionPattern.FanOut,
                        TaskIds = new[] { task }.Concat(dependentList).ToList().AsReadOnly(),
                        StratificationLevel = level,
                        IsParallelizable = true,
                        ExecutionOrder = new[] { task }.AsReadOnly()
                    });
                }
                break;

            case ExecutionPattern.FanIn:
                // Group all tasks in fan-in together
                if (tasks.Count > 0)
                {
                    groups.Add(new TaskExecutionGroup
                    {
                        GroupId = $"group_{startGroupId:D4}",
                        Pattern = ExecutionPattern.FanIn,
                        TaskIds = tasks.ToList().AsReadOnly(),
                        StratificationLevel = level,
                        IsParallelizable = true,
                        ExecutionOrder = null
                    });
                }
                break;

            case ExecutionPattern.ComplexDAG:
                // Complex DAG tasks processed individually
                foreach (var task in tasks)
                {
                    groups.Add(new TaskExecutionGroup
                    {
                        GroupId = $"group_{startGroupId + groups.Count:D4}",
                        Pattern = ExecutionPattern.ComplexDAG,
                        TaskIds = new[] { task }.ToList().AsReadOnly(),
                        StratificationLevel = level,
                        IsParallelizable = false,
                        ExecutionOrder = null
                    });
                }
                break;
        }

        return groups.AsReadOnly();
    }

    private IReadOnlyList<string> GetIndependentOrder(TaskExecutionGroup group)
    {
        // Can execute in any order - return as-is
        return group.TaskIds;
    }

    private IReadOnlyList<string> GetSequentialOrder(TaskExecutionGroup group, IDependencyGraph graph)
    {
        // Follow dependency chain
        if (group.TaskIds.Count == 0)
            return group.TaskIds;

        var order = new List<string>();
        var processed = new HashSet<string>();

        // Find root (no prerequisites in group)
        var root = group.TaskIds.FirstOrDefault(t =>
            !graph.TaskToPrerequisites.TryGetValue(t, out var prereqs)
            || prereqs.Count == 0
            || prereqs.All(p => !group.TaskIds.Contains(p)));

        if (root != null)
        {
            TraceSequentialPath(root, group, graph, order, processed);
        }

        // Add any remaining tasks
        foreach (var task in group.TaskIds.Where(t => !processed.Contains(t)))
            order.Add(task);

        return order.AsReadOnly();
    }

    private IReadOnlyList<string> GetFanOutOrder(TaskExecutionGroup group, IDependencyGraph graph)
    {
        // Root first, then dependents in any order
        var order = new List<string>();

        // Find root (smallest in-degree within group)
        var root = group.TaskIds.OrderBy(t =>
            graph.TaskToPrerequisites.TryGetValue(t, out var prereqs)
                ? prereqs.Count(p => group.TaskIds.Contains(p))
                : 0
        ).FirstOrDefault();

        if (root != null)
        {
            order.Add(root);
            foreach (var task in group.TaskIds.Where(t => t != root))
                order.Add(task);
        }

        return order.AsReadOnly();
    }

    private IReadOnlyList<string> GetFanInOrder(TaskExecutionGroup group, IDependencyGraph graph)
    {
        // Prerequisites first (parallelizable), then root
        var order = new List<string>();
        var hasMultipleDependents = group.TaskIds
            .Any(t => graph.TaskToDependents.TryGetValue(t, out var deps)
                && deps.Count(d => group.TaskIds.Contains(d)) > 1);

        if (hasMultipleDependents)
        {
            // Find task with multiple dependents - it's the root
            var root = group.TaskIds.FirstOrDefault(t =>
                graph.TaskToDependents.TryGetValue(t, out var deps)
                && deps.Count(d => group.TaskIds.Contains(d)) > 1);

            if (root != null)
            {
                foreach (var task in group.TaskIds.Where(t => t != root))
                    order.Add(task);
                order.Add(root);
            }
        }
        else
        {
            order.AddRange(group.TaskIds);
        }

        return order.AsReadOnly();
    }

    private IReadOnlyList<string> GetComplexDagOrder(TaskExecutionGroup group, IDependencyGraph graph)
    {
        // Use topological sort within group
        return graph.TopologicalOrder
            .Where(t => group.TaskIds.Contains(t))
            .ToList()
            .AsReadOnly();
    }

    private void TraceSequentialPath(
        string taskId,
        TaskExecutionGroup group,
        IDependencyGraph graph,
        List<string> order,
        HashSet<string> processed)
    {
        if (processed.Contains(taskId) || !group.TaskIds.Contains(taskId))
            return;

        processed.Add(taskId);
        order.Add(taskId);

        if (graph.TaskToDependents.TryGetValue(taskId, out var dependents))
        {
            var dependent = dependents.FirstOrDefault(d => group.TaskIds.Contains(d));
            if (dependent != null)
                TraceSequentialPath(dependent, group, graph, order, processed);
        }
    }
}
