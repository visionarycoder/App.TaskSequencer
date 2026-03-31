using Core.Models;

namespace Core.Services;

/// <summary>
/// Analyzes criticality of tasks using forward/backward pass algorithms.
/// Calculates critical path, slack time, and task urgency.
/// Implements Phase 3 of the execution planning workflow.
/// </summary>
public class CriticalityAnalyzer
{
    /// <summary>
    /// Computes earliest start and end times for all tasks (forward pass).
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="durations">Task durations</param>
    /// <param name="periodStart">Planning period start date</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping task IDs to (EarliestStart, EarliestEnd) tuples</returns>
    public async Task<IReadOnlyDictionary<string, (DateTime Start, DateTime End)>> ComputeEarliestTimesAsync(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime periodStart,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(durations);

        ct.ThrowIfCancellationRequested();

        var earliestTimes = new Dictionary<string, (DateTime Start, DateTime End)>();

        // Process tasks in topological order
        foreach (var taskId in graph.TopologicalOrder)
        {
            ct.ThrowIfCancellationRequested();

            var duration = durations.TryGetValue(taskId, out var dur)
                ? dur.ToTimeSpan()
                : TimeSpan.FromMinutes(15);

            DateTime earliestStart;

            if (!graph.TaskToPrerequisites.TryGetValue(taskId, out var prerequisites) || prerequisites.Count == 0)
            {
                // Root task: starts at period start
                earliestStart = periodStart;
            }
            else
            {
                // Start = MAX(end time of all prerequisites)
                var maxPrerequiteEnd = prerequisites
                    .Where(p => earliestTimes.ContainsKey(p))
                    .Select(p => earliestTimes[p].End)
                    .DefaultIfEmpty(periodStart)
                    .Max();

                earliestStart = maxPrerequiteEnd;
            }

            var earliestEnd = earliestStart + duration;
            earliestTimes[taskId] = (earliestStart, earliestEnd);
        }

        return earliestTimes.AsReadOnly();
    }

    /// <summary>
    /// Computes latest start and end times for all tasks (backward pass).
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="durations">Task durations</param>
    /// <param name="deadline">Planning period deadline</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping task IDs to (LatestStart, LatestEnd) tuples</returns>
    public async Task<IReadOnlyDictionary<string, (DateTime Start, DateTime End)>> ComputeLatestTimesAsync(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime deadline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(durations);

        ct.ThrowIfCancellationRequested();

        var latestTimes = new Dictionary<string, (DateTime Start, DateTime End)>();

        // Process tasks in reverse topological order
        var reverseTopo = graph.TopologicalOrder.Reverse().ToList();

        foreach (var taskId in reverseTopo)
        {
            ct.ThrowIfCancellationRequested();

            var duration = durations.TryGetValue(taskId, out var dur)
                ? dur.ToTimeSpan()
                : TimeSpan.FromMinutes(15);

            DateTime latestEnd;

            if (!graph.TaskToDependents.TryGetValue(taskId, out var dependents) || dependents.Count == 0)
            {
                // Leaf task: ends by deadline
                latestEnd = deadline;
            }
            else
            {
                // End = MIN(start time of all dependents)
                var minDependentStart = dependents
                    .Where(d => latestTimes.ContainsKey(d))
                    .Select(d => latestTimes[d].Start)
                    .DefaultIfEmpty(deadline)
                    .Min();

                latestEnd = minDependentStart;
            }

            var latestStart = latestEnd - duration;
            latestTimes[taskId] = (latestStart, latestEnd);
        }

        return latestTimes.AsReadOnly();
    }

    /// <summary>
    /// Calculates slack (free time) for each task.
    /// </summary>
    /// <param name="earliestTimes">Earliest times from forward pass</param>
    /// <param name="latestTimes">Latest times from backward pass</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping task IDs to slack time (negative = deadline miss)</returns>
    public async Task<IReadOnlyDictionary<string, TimeSpan>> CalculateSlackAsync(
        IReadOnlyDictionary<string, (DateTime Start, DateTime End)> earliestTimes,
        IReadOnlyDictionary<string, (DateTime Start, DateTime End)> latestTimes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(earliestTimes);
        ArgumentNullException.ThrowIfNull(latestTimes);

        ct.ThrowIfCancellationRequested();

        var slack = new Dictionary<string, TimeSpan>();

        var allTaskIds = earliestTimes.Keys.Union(latestTimes.Keys);

        foreach (var taskId in allTaskIds)
        {
            ct.ThrowIfCancellationRequested();

            if (earliestTimes.TryGetValue(taskId, out var earliest) && latestTimes.TryGetValue(taskId, out var latest))
            {
                // Slack = Latest Start - Earliest Start
                // Alternative: Slack = Latest End - Earliest End (equivalent)
                slack[taskId] = latest.Start - earliest.Start;
            }
        }

        return slack.AsReadOnly();
    }

    /// <summary>
    /// Identifies tasks on the critical path (slack = 0).
    /// </summary>
    /// <param name="slack">Slack times for all tasks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of critical task IDs</returns>
    public async Task<IReadOnlyList<string>> IdentifyCriticalPathAsync(
        IReadOnlyDictionary<string, TimeSpan> slack,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slack);

        ct.ThrowIfCancellationRequested();

        var criticalTasks = slack
            .Where(kvp => kvp.Value == TimeSpan.Zero)
            .Select(kvp => kvp.Key)
            .ToList();

        return criticalTasks.AsReadOnly();
    }

    /// <summary>
    /// Computes complete criticality metrics.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="durations">Task durations</param>
    /// <param name="periodStart">Planning period start</param>
    /// <param name="deadline">Planning period deadline</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete criticality metrics</returns>
    public async Task<CriticalityMetrics> ComputeCriticalityMetricsAsync(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime periodStart,
        DateTime deadline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(durations);

        ct.ThrowIfCancellationRequested();

        // Forward pass
        var earliestTimes = await ComputeEarliestTimesAsync(graph, durations, periodStart, ct);
        ct.ThrowIfCancellationRequested();

        // Backward pass
        var latestTimes = await ComputeLatestTimesAsync(graph, durations, deadline, ct);
        ct.ThrowIfCancellationRequested();

        // Calculate slack
        var slack = await CalculateSlackAsync(earliestTimes, latestTimes, ct);
        ct.ThrowIfCancellationRequested();

        // Identify critical path
        var criticalTasks = await IdentifyCriticalPathAsync(slack, ct);
        ct.ThrowIfCancellationRequested();

        // Calculate critical path completion
        var criticalPathCompletion = criticalTasks.Count > 0
            ? criticalTasks
                .Select(t => earliestTimes.TryGetValue(t, out var times) ? times.End : periodStart)
                .Max()
            : periodStart;

        // Get critical path sequence
        var criticalPathSequence = GetCriticalPathSequence(graph, criticalTasks);

        return new CriticalityMetrics
        {
            CriticalTasks = criticalTasks,
            CriticalPathCompletion = criticalPathCompletion,
            TaskSlack = slack,
            CriticalPathSequence = criticalPathSequence
        };
    }

    /// <summary>
    /// Gets the sequence of tasks forming the critical path.
    /// </summary>
    /// <param name="graph">The dependency graph</param>
    /// <param name="criticalTasks">List of critical task IDs</param>
    /// <returns>Critical path as ordered sequence</returns>
    private IReadOnlyList<string> GetCriticalPathSequence(
        IDependencyGraph graph,
        IReadOnlyList<string> criticalTasks)
    {
        if (criticalTasks.Count == 0)
            return new List<string>().AsReadOnly();

        // Find roots among critical tasks
        var sequence = new List<string>();
        var visited = new HashSet<string>();
        var criticalSet = new HashSet<string>(criticalTasks);

        foreach (var task in graph.TopologicalOrder)
        {
            if (criticalSet.Contains(task) && !visited.Contains(task))
            {
                if (!graph.TaskToPrerequisites.TryGetValue(task, out var prereqs)
                    || prereqs.Count == 0
                    || prereqs.All(p => !criticalSet.Contains(p)))
                {
                    // Found a root critical task
                    TraceCriticalPath(task, graph, criticalSet, sequence, visited);
                }
            }
        }

        // Add any remaining critical tasks not reached
        foreach (var task in criticalTasks.Where(t => !visited.Contains(t)))
        {
            sequence.Add(task);
        }

        return sequence.AsReadOnly();
    }

    private void TraceCriticalPath(
        string taskId,
        IDependencyGraph graph,
        HashSet<string> criticalSet,
        List<string> sequence,
        HashSet<string> visited)
    {
        if (visited.Contains(taskId))
            return;

        visited.Add(taskId);
        sequence.Add(taskId);

        if (graph.TaskToDependents.TryGetValue(taskId, out var dependents))
        {
            foreach (var dependent in dependents.Where(d => criticalSet.Contains(d)))
            {
                TraceCriticalPath(dependent, graph, criticalSet, sequence, visited);
            }
        }
    }

    /// <summary>
    /// Gets tasks that are NOT on critical path (have slack > 0).
    /// </summary>
    /// <param name="slack">Slack times for all tasks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of non-critical task IDs</returns>
    public async Task<IReadOnlyList<string>> GetNonCriticalTasksAsync(
        IReadOnlyDictionary<string, TimeSpan> slack,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slack);

        ct.ThrowIfCancellationRequested();

        var nonCritical = slack
            .Where(kvp => kvp.Value > TimeSpan.Zero)
            .Select(kvp => kvp.Key)
            .ToList();

        return nonCritical.AsReadOnly();
    }

    /// <summary>
    /// Gets tasks that would miss deadlines (negative slack).
    /// </summary>
    /// <param name="slack">Slack times for all tasks</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of at-risk task IDs with their deficit</returns>
    public async Task<IReadOnlyDictionary<string, TimeSpan>> GetDeadlineMissesAsync(
        IReadOnlyDictionary<string, TimeSpan> slack,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slack);

        ct.ThrowIfCancellationRequested();

        var misses = slack
            .Where(kvp => kvp.Value < TimeSpan.Zero)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Negate());

        return misses.AsReadOnly();
    }

    /// <summary>
    /// Gets criticality statistics.
    /// </summary>
    /// <param name="slack">Slack times for all tasks</param>
    /// <returns>Statistics including critical percentage, average slack, min/max slack</returns>
    public (double CriticalPercentage, TimeSpan AverageSlack, TimeSpan MinSlack, TimeSpan MaxSlack) GetCriticalityStats(
        IReadOnlyDictionary<string, TimeSpan> slack)
    {
        ArgumentNullException.ThrowIfNull(slack);

        if (slack.Count == 0)
            return (0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

        var criticalCount = slack.Count(kvp => kvp.Value == TimeSpan.Zero);
        var criticalPercentage = (double)criticalCount / slack.Count * 100;

        var totalSlack = slack.Values.Aggregate(TimeSpan.Zero, (acc, s) => acc + s);
        var averageSlack = slack.Count > 0
            ? TimeSpan.FromMilliseconds(totalSlack.TotalMilliseconds / slack.Count)
            : TimeSpan.Zero;

        var minSlack = slack.Values.Min();
        var maxSlack = slack.Values.Max();

        return (criticalPercentage, averageSlack, minSlack, maxSlack);
    }
}
