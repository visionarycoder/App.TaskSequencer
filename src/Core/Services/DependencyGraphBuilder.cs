using App.TaskSequencer.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Builds and validates the dependency graph from execution events.
/// Implements Phase 2 of the execution planning workflow.
/// </summary>
public class DependencyGraphBuilder
{
    private readonly DependencyResolver dependencyResolver;

    public DependencyGraphBuilder(DependencyResolver dependencyResolver)
    {
        this.dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
    }

    /// <summary>
    /// Builds a complete dependency graph from all execution events.
    /// </summary>
    /// <param name="events">All execution events from the manifest</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete dependency graph</returns>
    /// <exception cref="InvalidOperationException">If circular dependencies are detected</exception>
    public async Task<IDependencyGraph> BuildDependencyGraphAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);

        ct.ThrowIfCancellationRequested();

        // Build task to prerequisites mapping
        var taskToPrerequisites = new Dictionary<string, IReadOnlyList<string>>();
        var taskToDependents = new Dictionary<string, IReadOnlyList<string>>();
        var allTaskIds = new HashSet<string>();

        // Initialize collections
        foreach (var evt in events)
        {
            allTaskIds.Add(evt.TaskId);
            if (!taskToPrerequisites.ContainsKey(evt.TaskId))
                taskToPrerequisites[evt.TaskId] = new List<string>().AsReadOnly();
            if (!taskToDependents.ContainsKey(evt.TaskId))
                taskToDependents[evt.TaskId] = new List<string>().AsReadOnly();
        }

        // Resolve prerequisites using existing resolver
        foreach (var evt in events)
        {
            ct.ThrowIfCancellationRequested();

            var resolvedPrereqs = dependencyResolver.ResolvePrerequisites(evt, events.ToList());
            var prereqIds = resolvedPrereqs
                .Select(p => p.TaskId)
                .Distinct()
                .ToList();

            taskToPrerequisites[evt.TaskId] = prereqIds.AsReadOnly();

            // Build dependents map (inverse of prerequisites)
            foreach (var prereqId in prereqIds)
            {
                if (!taskToDependents.ContainsKey(prereqId))
                    taskToDependents[prereqId] = new List<string>().AsReadOnly();

                var existingDependents = taskToDependents[prereqId].ToList();
                if (!existingDependents.Contains(evt.TaskId))
                {
                    existingDependents.Add(evt.TaskId);
                    taskToDependents[prereqId] = existingDependents.AsReadOnly();
                }
            }
        }

        // Detect circular dependencies
        if (HasCircularDependencies(taskToPrerequisites, taskToDependents, out var cycles))
        {
            var cycleDescription = string.Join(", ", cycles.Select(c => string.Join(" → ", c)));
            throw new InvalidOperationException($"Circular dependencies detected: {cycleDescription}");
        }

        // Compute topological sort
        var topoSort = TopologicalSort(taskToPrerequisites, allTaskIds);

        ct.ThrowIfCancellationRequested();

        return new DependencyGraph(
            taskToPrerequisites,
            taskToDependents,
            topoSort,
            allTaskIds);
    }

    /// <summary>
    /// Detects circular dependencies in the graph using DFS.
    /// </summary>
    /// <param name="taskToPrerequisites">Mapping of tasks to their prerequisites</param>
    /// <param name="taskToDependents">Mapping of tasks to their dependents</param>
    /// <param name="cycles">Output list of circular dependency cycles found</param>
    /// <returns>True if circular dependencies exist</returns>
    public bool HasCircularDependencies(
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToPrerequisites,
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToDependents,
        out IReadOnlyList<IReadOnlyList<string>> cycles)
    {
        cycles = new List<IReadOnlyList<string>>();
        var foundCycles = new List<IReadOnlyList<string>>();

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var allTasks = taskToPrerequisites.Keys.ToList();

        foreach (var task in allTasks)
        {
            if (!visited.Contains(task))
            {
                var path = new List<string>();
                if (DfsCycleDetect(task, taskToPrerequisites, visited, recursionStack, path))
                {
                    foundCycles.Add(path.AsReadOnly());
                }
            }
        }

        cycles = foundCycles.AsReadOnly();
        return foundCycles.Count > 0;
    }

    /// <summary>
    /// DFS helper for circular dependency detection.
    /// </summary>
    private bool DfsCycleDetect(
        string task,
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToPrerequisites,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path)
    {
        visited.Add(task);
        recursionStack.Add(task);
        path.Add(task);

        if (taskToPrerequisites.TryGetValue(task, out var prerequisites))
        {
            foreach (var prereq in prerequisites)
            {
                if (!visited.Contains(prereq))
                {
                    if (DfsCycleDetect(prereq, taskToPrerequisites, visited, recursionStack, path))
                        return true;
                }
                else if (recursionStack.Contains(prereq))
                {
                    // Found a cycle - extract the cycle path
                    var cycleStart = path.IndexOf(prereq);
                    path.Add(prereq); // Complete the cycle
                    return true;
                }
            }
        }

        path.Remove(task);
        recursionStack.Remove(task);
        return false;
    }

    /// <summary>
    /// Computes topological sort using Kahn's algorithm.
    /// </summary>
    /// <param name="taskToPrerequisites">Mapping of tasks to their prerequisites</param>
    /// <param name="allTaskIds">All task IDs in the graph</param>
    /// <returns>Topologically sorted list of task IDs</returns>
    public IReadOnlyList<string> TopologicalSort(
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToPrerequisites,
        IReadOnlySet<string> allTaskIds)
    {
        // Calculate in-degree for each task
        var inDegree = new Dictionary<string, int>();
        foreach (var taskId in allTaskIds)
        {
            inDegree[taskId] = taskToPrerequisites.TryGetValue(taskId, out var prereqs)
                ? prereqs.Count
                : 0;
        }

        // Find all tasks with no prerequisites
        var queue = new Queue<string>();
        foreach (var taskId in allTaskIds.Where(t => inDegree[t] == 0))
        {
            queue.Enqueue(taskId);
        }

        var result = new List<string>();
        var tempInDegree = new Dictionary<string, int>(inDegree);

        // Build inverse graph (dependents)
        var dependents = new Dictionary<string, List<string>>();
        foreach (var taskId in allTaskIds)
        {
            dependents[taskId] = new List<string>();
        }

        foreach (var (task, prereqs) in taskToPrerequisites)
        {
            foreach (var prereq in prereqs)
            {
                if (dependents.ContainsKey(prereq))
                    dependents[prereq].Add(task);
            }
        }

        // Process tasks in topological order
        while (queue.Count > 0)
        {
            var task = queue.Dequeue();
            result.Add(task);

            // Process all dependents of this task
            if (dependents.TryGetValue(task, out var dependentList))
            {
                foreach (var dependent in dependentList)
                {
                    tempInDegree[dependent]--;
                    if (tempInDegree[dependent] == 0)
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }
        }

        // If result doesn't include all tasks, there's a cycle (shouldn't happen if we validated)
        if (result.Count != allTaskIds.Count)
            throw new InvalidOperationException("Topological sort failed: unprocessed tasks due to cycles");

        return result.AsReadOnly();
    }
}

/// <summary>
/// Implementation of the dependency graph interface.
/// </summary>
internal class DependencyGraph : IDependencyGraph
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _taskToPrerequisites;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _taskToDependents;
    private readonly IReadOnlyList<string> _topologicalOrder;
    private readonly IReadOnlySet<string> _allTaskIds;
    private Dictionary<string, int>? _depthFromRoot;
    private Dictionary<string, int>? _depthToLeaf;

    public DependencyGraph(
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToPrerequisites,
        IReadOnlyDictionary<string, IReadOnlyList<string>> taskToDependents,
        IReadOnlyList<string> topologicalOrder,
        IReadOnlySet<string> allTaskIds)
    {
        _taskToPrerequisites = taskToPrerequisites ?? throw new ArgumentNullException(nameof(taskToPrerequisites));
        _taskToDependents = taskToDependents ?? throw new ArgumentNullException(nameof(taskToDependents));
        _topologicalOrder = topologicalOrder ?? throw new ArgumentNullException(nameof(topologicalOrder));
        _allTaskIds = allTaskIds ?? throw new ArgumentNullException(nameof(allTaskIds));
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToPrerequisites => _taskToPrerequisites;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToDependents => _taskToDependents;
    public IReadOnlyList<string> TopologicalOrder => _topologicalOrder;
    public IReadOnlySet<string> AllTaskIds => _allTaskIds;

    public int ComputeDepthFromRoot(string taskId)
    {
        if (!_allTaskIds.Contains(taskId))
            throw new ArgumentException($"Task {taskId} not found in graph");

        // Lazy compute all depths on first access
        if (_depthFromRoot == null)
        {
            _depthFromRoot = new Dictionary<string, int>();
            foreach (var task in _topologicalOrder)
            {
                ComputeDepthFromRootHelper(task, _depthFromRoot);
            }
        }

        return _depthFromRoot.TryGetValue(taskId, out var depth) ? depth : 0;
    }

    public int ComputeDepthToLeaf(string taskId)
    {
        if (!_allTaskIds.Contains(taskId))
            throw new ArgumentException($"Task {taskId} not found in graph");

        // Lazy compute all depths on first access
        if (_depthToLeaf == null)
        {
            _depthToLeaf = new Dictionary<string, int>();
            var reverseTopo = _topologicalOrder.Reverse().ToList();
            foreach (var task in reverseTopo)
            {
                ComputeDepthToLeafHelper(task, _depthToLeaf);
            }
        }

        return _depthToLeaf.TryGetValue(taskId, out var depth) ? depth : 0;
    }

    private int ComputeDepthFromRootHelper(string taskId, Dictionary<string, int> depths)
    {
        if (depths.TryGetValue(taskId, out var depth))
            return depth;

        if (!_taskToPrerequisites.TryGetValue(taskId, out var prerequisites) || prerequisites.Count == 0)
        {
            depths[taskId] = 0;
            return 0;
        }

        var maxPrereqDepth = 0;
        foreach (var prereq in prerequisites)
        {
            var prereqDepth = ComputeDepthFromRootHelper(prereq, depths);
            maxPrereqDepth = Math.Max(maxPrereqDepth, prereqDepth);
        }

        depth = maxPrereqDepth + 1;
        depths[taskId] = depth;
        return depth;
    }

    private int ComputeDepthToLeafHelper(string taskId, Dictionary<string, int> depths)
    {
        if (depths.TryGetValue(taskId, out var depth))
            return depth;

        if (!_taskToDependents.TryGetValue(taskId, out var dependents) || dependents.Count == 0)
        {
            depths[taskId] = 0;
            return 0;
        }

        var maxDependentDepth = 0;
        foreach (var dependent in dependents)
        {
            var dependentDepth = ComputeDepthToLeafHelper(dependent, depths);
            maxDependentDepth = Math.Max(maxDependentDepth, dependentDepth);
        }

        depth = maxDependentDepth + 1;
        depths[taskId] = depth;
        return depth;
    }
}
