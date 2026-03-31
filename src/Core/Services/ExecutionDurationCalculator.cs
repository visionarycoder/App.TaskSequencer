using Core.Models;

namespace Core.Services;

/// <summary>
/// Calculates execution duration for tasks based on history or defaults.
/// Implements the core business rule: estimate durations from historical data,
/// or apply 15-minute default. For grouped tasks, sum subtasks + 10% buffer.
/// </summary>
public class ExecutionDurationCalculator
{
    private const int DEFAULT_DURATION_MINUTES = 15;
    private const double GROUPED_TASK_BUFFER_PERCENT = 0.10;

    /// <summary>
    /// Calculates duration for a single task.
    /// If task has explicit duration in event, uses that.
    /// If historical data exists, uses average of all matching executions.
    /// Otherwise, defaults to 15 minutes.
    /// </summary>
    /// <param name="instance">The ExecutionEventDefinition to calculate duration for</param>
    /// <param name="historicalData">List of ExecutionInstance objects with historical execution data</param>
    /// <returns>Tuple of (DurationMinutes, IsEstimated flag)</returns>
    public (int DurationMinutes, bool IsEstimated) GetDuration(
        object instance,
        List<object> historicalData)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(historicalData);

        if (instance is not ExecutionEventDefinition executionEvent)
            throw new ArgumentException($"Expected ExecutionEventDefinition, got {instance.GetType().Name}");

        // If task has explicit duration > 0, use it
        if (executionEvent.DurationMinutes > 0)
        {
            return ((int)executionEvent.DurationMinutes, false);
        }

        // Filter historical data for ExecutionInstance objects matching this task
        var matchingHistoricalInstances = historicalData
            .OfType<ExecutionInstance>()
            .Where(ei => TaskIdsMatch(ei.TaskId, executionEvent.TaskId))
            .ToList();

        // If we have historical data, calculate average
        if (matchingHistoricalInstances.Count > 0)
        {
            var averageDuration = (int)Math.Round(
                matchingHistoricalInstances.Average(ei => ei.DurationMinutes));
            return (averageDuration, false);
        }

        // Default to 15 minutes if no history
        return (DEFAULT_DURATION_MINUTES, true);
    }

    /// <summary>
    /// Calculates duration for a grouped task (compound task with subtasks).
    /// Sums all subtask durations and adds 10% buffer for inter-task overhead.
    /// </summary>
    /// <param name="instance">The ExecutionEventDefinition for the group</param>
    /// <param name="subtaskDurations">List of (subtask name, duration in minutes) tuples</param>
    /// <param name="historicalData">List of ExecutionInstance objects (for future use with group history)</param>
    /// <returns>Tuple of (TotalDurationMinutes including buffer, IsEstimated flag)</returns>
    public (int DurationMinutes, bool IsEstimated) GetDurationForGroupedTask(
        object instance,
        List<(string, int)> subtaskDurations,
        List<object> historicalData)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(subtaskDurations);
        ArgumentNullException.ThrowIfNull(historicalData);

        if (instance is not ExecutionEventDefinition executionEvent)
            throw new ArgumentException($"Expected ExecutionEventDefinition, got {instance.GetType().Name}");

        // If no subtasks, return default
        if (subtaskDurations.Count == 0)
        {
            return (DEFAULT_DURATION_MINUTES, true);
        }

        // Sum all subtask durations
        var totalDuration = subtaskDurations.Sum(st => st.Item2);

        // Add 10% buffer for inter-task overhead
        var durationWithBuffer = (int)Math.Round(totalDuration * (1 + GROUPED_TASK_BUFFER_PERCENT));

        return (durationWithBuffer, true);
    }

    /// <summary>
    /// Matches task IDs between ExecutionInstance (int) and ExecutionEventDefinition (string).
    /// Handles cases like "T001" matching with int 1, or "1" matching with int 1.
    /// </summary>
    private bool TaskIdsMatch(int instanceTaskId, string eventTaskId)
    {
        // Try to extract numeric part from eventTaskId
        if (int.TryParse(eventTaskId.Replace("T", ""), out var eventTaskIdInt))
        {
            return instanceTaskId == eventTaskIdInt;
        }

        // Fallback: try direct integer parse
        if (int.TryParse(eventTaskId, out var directParse))
        {
            return instanceTaskId == directParse;
        }

        return false;
    }
}
