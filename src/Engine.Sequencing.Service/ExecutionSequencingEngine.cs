using Engine.Sequencing.Contract;
using System.Collections.Generic;
using System.Linq;

namespace Engine.Sequencing.Service;

/// <summary>
/// Business rules engine for calculating task execution sequences.
/// Implements algorithms for topological sorting and timing validation.
/// </summary>
public class ExecutionSequencingEngine : ISequencingEngine
{
    public IReadOnlyList<ExecutionInstance> CalculateSequenceAsync(IEnumerable<TaskDefinition> taskDefinitions, CancellationToken ct)
    {
        var tasks = taskDefinitions.ToList();

        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
            throw new ArgumentException("Task definitions cannot be empty.", nameof(taskDefinitions));

        ValidateTaskDefinitions(tasks);
        DetectCircularDependencies(tasks);

        var sorted = TopologicalSort(tasks);
        var instances = CalculateFunctionalStartTimes(sorted);
        ValidateTimeConstraints(instances);

        return instances.AsReadOnly();
    }

    public void ValidateTaskDefinitions(IEnumerable<TaskDefinition> tasks)
    {
        var taskList = tasks.ToList();

        var invalid = taskList
            .Where(t => string.IsNullOrWhiteSpace(t.TaskId) || !t.ValidateTimingRequirements())
            .ToList();

        if (invalid.Any())
            throw new InvalidOperationException($"Invalid tasks: {invalid.Count} missing ID or timing requirements.");

        var missingPrereqs = taskList
            .Where(t => t.PrerequisiteIds.Any(p => !taskList.Any(d => d.TaskId == p)))
            .ToList();

        if (missingPrereqs.Any())
            throw new InvalidOperationException(
                $"Tasks reference non-existent prerequisites: {string.Join(", ", missingPrereqs.Select(t => t.TaskId))}");
    }

    private static void DetectCircularDependencies(List<TaskDefinition> tasks)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var task in tasks)
        {
            if (!visited.Contains(task.TaskId))
                VisitTask(task.TaskId, tasks, visited, recursionStack);
        }
    }

    private static void VisitTask(string taskId, List<TaskDefinition> tasks, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(taskId);
        recursionStack.Add(taskId);

        var task = tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null) return;

        foreach (var prereqId in task.PrerequisiteIds)
        {
            if (!visited.Contains(prereqId))
                VisitTask(prereqId, tasks, visited, recursionStack);
            else if (recursionStack.Contains(prereqId))
                throw new InvalidOperationException($"Circular dependency detected involving task: {taskId}");
        }

        recursionStack.Remove(taskId);
    }

    private static List<TaskDefinition> TopologicalSort(List<TaskDefinition> tasks)
    {
        var result = new List<TaskDefinition>();
        var visited = new HashSet<string>();
        var temp = new HashSet<string>();

        foreach (var task in tasks)
        {
            if (!visited.Contains(task.TaskId))
                TopologicalSortUtil(task.TaskId, tasks, visited, temp, result);
        }

        return result;
    }

    private static void TopologicalSortUtil(string taskId, List<TaskDefinition> tasks, HashSet<string> visited,
        HashSet<string> temp, List<TaskDefinition> result)
    {
        visited.Add(taskId);
        temp.Add(taskId);

        var task = tasks.FirstOrDefault(t => t.TaskId == taskId);
        if (task == null) return;

        foreach (var prereqId in task.PrerequisiteIds)
        {
            if (!visited.Contains(prereqId))
                TopologicalSortUtil(prereqId, tasks, visited, temp, result);
        }

        temp.Remove(taskId);
        result.Add(task);
    }

    private static List<ExecutionInstance> CalculateFunctionalStartTimes(List<TaskDefinition> sorted)
    {
        var instances = new List<ExecutionInstance>();
        var completionTimes = new Dictionary<string, DateTime>();

        foreach (var task in sorted)
        {
            var startTime = task.StartTime ?? DateTime.MinValue;

            if (task.PrerequisiteIds.Any())
            {
                var maxPrereqEnd = task.PrerequisiteIds
                    .Where(p => completionTimes.ContainsKey(p))
                    .Max(p => completionTimes[p]);

                startTime = new[] { startTime, maxPrereqEnd }.Max();
            }

            var endTime = task.EndTime ?? startTime.Add(task.GetDuration());

            instances.Add(new ExecutionInstance(
                task.TaskId,
                startTime,
                endTime,
                task.GetDuration()
            ));

            completionTimes[task.TaskId] = endTime;
        }

        return instances;
    }

    private static void ValidateTimeConstraints(List<ExecutionInstance> instances)
    {
        var invalid = instances.Where(i => i.FunctionalEndTime < i.FunctionalStartTime).ToList();

        if (invalid.Any())
            throw new InvalidOperationException(
                $"Invalid time constraints: {invalid.Count} instances have end time before start time.");
    }
}
