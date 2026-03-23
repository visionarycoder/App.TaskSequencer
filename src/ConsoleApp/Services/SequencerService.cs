using ConsoleApp.Ifx.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConsoleApp.Services;

/// <summary>
/// Calculates task execution sequence based on dependencies and timing constraints.
/// Input: TaskDefinitions for a specific execution window (provided by crontab).
/// Output: Ordered ExecutionInstances ready for execution.
/// </summary>
public class ExecutionPlanner
{
    /// <summary>
    /// Calculates execution sequence for given tasks in a specific window.
    /// </summary>
    /// <param name="taskDefinitions">Tasks to sequence (for one execution window)</param>
    /// <returns>Ordered list of execution instances</returns>
    public List<ExecutionInstance> CalculateSequence(List<TaskDefinition> taskDefinitions)
    {
        ArgumentNullException.ThrowIfNull(taskDefinitions);
        if (taskDefinitions.Count == 0)
            throw new ArgumentException("Task definitions cannot be empty.", nameof(taskDefinitions));

        ValidateTaskDefinitions(taskDefinitions);
        DetectCircularDependencies(taskDefinitions);

        var sorted = TopologicalSort(taskDefinitions);
        var instances = CalculateFunctionalStartTimes(sorted);
        ValidateTimeConstraints(instances);

        return instances;
    }

    /// <summary>
    /// Validates all task definitions have required fields and timing.
    /// </summary>
    private static void ValidateTaskDefinitions(List<TaskDefinition> tasks)
    {
        var invalid = tasks.Where(t => string.IsNullOrWhiteSpace(t.Id) || !t.ValidateTimingRequirements()).ToList();
        if (invalid.Any())
            throw new InvalidOperationException($"Invalid tasks: {invalid.Count} missing ID or timing requirements.");

        var missingPrereqs = tasks
            .Where(t => t.PrerequisiteIds.Any(p => !tasks.Any(d => d.Id == p)))
            .ToList();

        if (missingPrereqs.Any())
            throw new InvalidOperationException($"Tasks reference non-existent prerequisites: {string.Join(", ", missingPrereqs.Select(t => t.Id))}");
    }

    /// <summary>
    /// Detects circular dependencies using DFS.
    /// </summary>
    private static void DetectCircularDependencies(List<TaskDefinition> tasks)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var task in tasks)
        {
            if (!visited.Contains(task.Id))
                DfsDetectCycle(task, tasks, visited, recursionStack);
        }
    }

    /// <summary>
    /// DFS helper for cycle detection.
    /// </summary>
    private static void DfsDetectCycle(TaskDefinition task, List<TaskDefinition> allTasks, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(task.Id);
        recursionStack.Add(task.Id);

        foreach (var prereqId in task.PrerequisiteIds)
        {
            var prerequisite = allTasks.First(t => t.Id == prereqId);

            if (!visited.Contains(prereqId))
                DfsDetectCycle(prerequisite, allTasks, visited, recursionStack);
            else if (recursionStack.Contains(prereqId))
                throw new InvalidOperationException($"Circular dependency: {task.Id} → {prereqId}");
        }

        recursionStack.Remove(task.Id);
    }

    /// <summary>
    /// Topological sort using Kahn's algorithm.
    /// </summary>
    private static List<TaskDefinition> TopologicalSort(List<TaskDefinition> tasks)
    {
        var inDegree = tasks.ToDictionary(t => t.Id, t => t.PrerequisiteIds.Count);
        var queue = new Queue<TaskDefinition>(tasks.Where(t => inDegree[t.Id] == 0));
        var sorted = new List<TaskDefinition>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            var dependents = tasks.Where(t => t.PrerequisiteIds.Contains(current.Id));
            foreach (var dependent in dependents)
            {
                inDegree[dependent.Id]--;
                if (inDegree[dependent.Id] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (sorted.Count != tasks.Count)
            throw new InvalidOperationException("Topological sort incomplete; circular dependencies exist.");

        return sorted;
    }

    /// <summary>
    /// Calculates functional start times ensuring prerequisites complete first.
    /// </summary>
    private static List<ExecutionInstance> CalculateFunctionalStartTimes(List<TaskDefinition> sortedTasks)
    {
        var completionTimes = new Dictionary<string, DateTime>();
        var instances = new List<ExecutionInstance>();

        foreach (var task in sortedTasks)
        {
            var functionalStartTime = CalculateEarliestStartTime(task, sortedTasks, completionTimes);
            var completionTime = functionalStartTime.Add(task.GetDuration());
            completionTimes[task.Id] = completionTime;

            var instance = new ExecutionInstance(
                task.Id,
                task.BaseScheduledStartTime ?? functionalStartTime,
                functionalStartTime,
                task.BaseRequiredEndTime,
                task.DurationMinutes,
                task.PrerequisiteIds,
                true
            );

            instances.Add(instance);
        }

        return instances;
    }

    /// <summary>
    /// Calculates earliest start time for a task.
    /// </summary>
    private static DateTime CalculateEarliestStartTime(TaskDefinition task, List<TaskDefinition> allTasks, Dictionary<string, DateTime> completionTimes)
    {
        if (!task.PrerequisiteIds.Any())
            return task.BaseScheduledStartTime ?? DateTime.Now;

        var latestPrerequisiteCompletion = task.PrerequisiteIds
            .Max(prereqId => completionTimes.TryGetValue(prereqId, out var time) ? time : DateTime.MinValue);

        var baseStart = task.BaseScheduledStartTime ?? DateTime.Now;
        return latestPrerequisiteCompletion > baseStart ? latestPrerequisiteCompletion : baseStart;
    }

    /// <summary>
    /// Validates no task violates its required end time.
    /// </summary>
    private static void ValidateTimeConstraints(List<ExecutionInstance> instances)
    {
        var violations = instances
            .Where(i => i.RequiredEndTime.HasValue && i.GetPlannedCompletionTime() > i.RequiredEndTime.Value)
            .ToList();

        if (violations.Any())
        {
            var details = string.Join(", ", violations.Select(i => $"{i.TaskId}"));
            throw new InvalidOperationException($"Time constraint violations: {details}");
        }
    }
}
