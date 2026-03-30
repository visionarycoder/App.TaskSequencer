using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Assigns stratification levels to tasks based on dependency depth.
/// Implements Phase 3 of the execution planning workflow.
/// 
/// Level 0: Tasks with no prerequisites (can execute in parallel)
/// Level N: Tasks where MAX(level of prerequisites) + 1 = N
/// </summary>
public class TaskStratifier
{
    /// <summary>
    /// Assigns stratification levels to all tasks in the dependency graph.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Stratification result with levels and groupings</returns>
    public async Task<StratificationResult> AssignStratificationLevelsAsync(
        IDependencyGraph graph,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        ct.ThrowIfCancellationRequested();

        var taskToLevel = new Dictionary<string, int>();

        // Assign levels based on longest path from root
        foreach (var taskId in graph.TopologicalOrder)
        {
            ct.ThrowIfCancellationRequested();

            var prerequisites = graph.TaskToPrerequisites.TryGetValue(taskId, out var prereqs)
                ? prereqs
                : new List<string>().AsReadOnly();

            if (prerequisites.Count == 0)
            {
                // Root task: level 0
                taskToLevel[taskId] = 0;
            }
            else
            {
                // Level = MAX(level of all prerequisites) + 1
                var maxPrerequiteLevel = prerequisites
                    .Where(p => taskToLevel.ContainsKey(p))
                    .Select(p => taskToLevel[p])
                    .DefaultIfEmpty(0)
                    .Max();

                taskToLevel[taskId] = maxPrerequiteLevel + 1;
            }
        }

        ct.ThrowIfCancellationRequested();

        // Group tasks by level
        var levelToTasks = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var level in taskToLevel.Values.Distinct().OrderBy(l => l))
        {
            var tasksAtLevel = taskToLevel
                .Where(kvp => kvp.Value == level)
                .Select(kvp => kvp.Key)
                .ToList();

            levelToTasks[level] = tasksAtLevel.AsReadOnly();
        }

        var maxLevel = taskToLevel.Values.Count > 0 ? taskToLevel.Values.Max() : 0;

        return new StratificationResult
        {
            TaskToLevel = taskToLevel,
            LevelToTasks = levelToTasks,
            MaxLevel = maxLevel
        };
    }

    /// <summary>
    /// Gets tasks that can execute in parallel at a given level.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="level">The stratification level</param>
    /// <param name="stratificationResult">The stratification result</param>
    /// <returns>List of task IDs that can execute in parallel at this level</returns>
    public IReadOnlyList<string> GetParallelTasksAtLevel(
        IDependencyGraph graph,
        int level,
        StratificationResult stratificationResult)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(stratificationResult);

        if (!stratificationResult.LevelToTasks.TryGetValue(level, out var tasksAtLevel))
            return new List<string>().AsReadOnly();

        // At a given level, ALL tasks are parallelizable with each other
        // (they only depend on tasks from earlier levels)
        return tasksAtLevel;
    }

    /// <summary>
    /// Gets the critical level - the longest dependency chain.
    /// </summary>
    /// <param name="stratificationResult">The stratification result</param>
    /// <returns>The critical level (maximum level number)</returns>
    public int GetCriticalLevel(StratificationResult stratificationResult)
    {
        ArgumentNullException.ThrowIfNull(stratificationResult);
        return stratificationResult.MaxLevel;
    }

    /// <summary>
    /// Gets all tasks on the critical path (longest dependency chain).
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="stratificationResult">The stratification result</param>
    /// <returns>List of task IDs forming the critical path</returns>
    public IReadOnlyList<string> GetCriticalPath(
        IDependencyGraph graph,
        StratificationResult stratificationResult)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(stratificationResult);

        var criticalLevel = stratificationResult.MaxLevel;
        var criticalPath = new List<string>();
        var visited = new HashSet<string>();

        // Start from leaf tasks at max level and work backward
        if (stratificationResult.LevelToTasks.TryGetValue(criticalLevel, out var leafTasks))
        {
            foreach (var leafTask in leafTasks)
            {
                // For each leaf, trace backward to root, following longest path
                TraceBackwardToCriticalPath(leafTask, graph, stratificationResult, criticalPath, visited);
                if (criticalPath.Count > 0)
                    break; // Found one critical path
            }
        }

        return criticalPath.AsReadOnly();
    }

    private void TraceBackwardToCriticalPath(
        string taskId,
        IDependencyGraph graph,
        StratificationResult stratificationResult,
        List<string> path,
        HashSet<string> visited)
    {
        if (visited.Contains(taskId))
            return;

        visited.Add(taskId);
        path.Add(taskId);

        if (!graph.TaskToPrerequisites.TryGetValue(taskId, out var prerequisites) || prerequisites.Count == 0)
            return;

        // Follow longest prerequisite chain
        if (prerequisites.Count > 0)
        {
            var maxPrereqLevel = prerequisites
                .Where(p => stratificationResult.TaskToLevel.TryGetValue(p, out _))
                .Select(p => (task: p, level: stratificationResult.TaskToLevel[p]))
                .OrderByDescending(x => x.level)
                .First();

            TraceBackwardToCriticalPath(maxPrereqLevel.task, graph, stratificationResult, path, visited);
        }
    }

    /// <summary>
    /// Gets statistics about the stratification.
    /// </summary>
    /// <param name="stratificationResult">The stratification result</param>
    /// <returns>Statistics tuple: (levelCount, totalTasks, avgTasksPerLevel)</returns>
    public (int LevelCount, int TotalTasks, double AvgTasksPerLevel) GetStratificationStats(
        StratificationResult stratificationResult)
    {
        ArgumentNullException.ThrowIfNull(stratificationResult);

        var levelCount = stratificationResult.LevelToTasks.Count;
        var totalTasks = stratificationResult.TotalTasks;
        var avgTasksPerLevel = levelCount > 0 ? (double)totalTasks / levelCount : 0;

        return (levelCount, totalTasks, avgTasksPerLevel);
    }
}
