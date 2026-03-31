using Core.Models;

namespace Core.Services;

/// <summary>
/// Calculates execution windows for tasks based on dependencies and deadlines.
/// Implements the core business rule: calculate when tasks can execute based on
/// prerequisite completion times and intake deadlines.
/// </summary>
public class ExecutionWindowCalculator
{
    private const int DEFAULT_DURATION_MINUTES = 15;

    /// <summary>
    /// Calculates the execution window for a task given current time, all tasks, and intake deadlines.
    /// </summary>
    /// <param name="instance">The ExecutionEventDefinition to calculate window for</param>
    /// <param name="currentTime">Current time as reference point</param>
    /// <param name="allTasks">List of all ExecutionEventDefinition objects</param>
    /// <param name="intakeTimes">Dictionary mapping TaskId to intake deadline times</param>
    /// <returns>ExecutionWindow describing when task can execute</returns>
    public object CalculateWindow(
        object instance,
        DateTime currentTime,
        List<object> allTasks,
        Dictionary<string, DateTime> intakeTimes)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(allTasks);
        ArgumentNullException.ThrowIfNull(intakeTimes);

        if (instance is not ExecutionEventDefinition executionEvent)
            throw new ArgumentException($"Expected ExecutionEventDefinition, got {instance.GetType().Name}");

        // Step 1: Calculate earliest start time based on dependencies
        var earliestStartTime = CalculateEarliestStartTime(
            executionEvent, currentTime, allTasks);

        // Step 2: Calculate duration for this task
        var durationMinutes = (int)executionEvent.DurationMinutes > 0
            ? (int)executionEvent.DurationMinutes
            : DEFAULT_DURATION_MINUTES;

        // Step 3: Calculate latest start time based on deadlines
        var latestStartTime = CalculateLatestStartTime(
            executionEvent, earliestStartTime, durationMinutes, intakeTimes);

        // Step 4: Determine feasibility
        var isFeasible = earliestStartTime <= latestStartTime;
        var constraintViolation = isFeasible ? null :
            "Task cannot complete before deadline: dependencies extend beyond intake time";

        return new ExecutionWindow(
            TaskId: executionEvent.TaskId,
            EarliestStartTime: earliestStartTime,
            LatestStartTime: latestStartTime,
            IsFeasible: isFeasible,
            ConstraintViolation: constraintViolation);
    }

    /// <summary>
    /// Calculates the earliest time this task can start based on prerequisites.
    /// </summary>
    private DateTime CalculateEarliestStartTime(
        ExecutionEventDefinition executionEvent,
        DateTime currentTime,
        List<object> allTasks)
    {
        // If no prerequisites, can start at scheduled time (or now if later)
        if (executionEvent.PrerequisiteTaskIds.Count == 0)
        {
            return currentTime;
        }

        // Find all prerequisite tasks
        var prerequisiteTasks = allTasks
            .OfType<ExecutionEventDefinition>()
            .Where(t => executionEvent.PrerequisiteTaskIds.Contains(t.TaskId))
            .ToList();

        if (prerequisiteTasks.Count == 0)
        {
            // Missing dependencies - start at current time
            return currentTime;
        }

        // Compute completion time for each prerequisite
        var latestPrereqCompletion = prerequisiteTasks
            .Select(prereq =>
            {
                var duration = prereq.DurationMinutes > 0 ? prereq.DurationMinutes : DEFAULT_DURATION_MINUTES;
                // Task scheduled on ScheduledDay at ScheduledTime
                // For simplicity, use current date
                var executionDate = GetExecutionDateForDay(currentTime.Date, prereq.ScheduledDay);
                var scheduledTime = prereq.ScheduledTime.ApplyToDate(executionDate);
                var completionTime = scheduledTime.AddMinutes(duration);
                return completionTime;
            })
            .Max();

        // Take the later of: latest prerequisite completion or current time
        return Max(latestPrereqCompletion, currentTime);
    }

    /// <summary>
    /// Calculates the latest time this task can start to meet deadlines.
    /// </summary>
    private DateTime CalculateLatestStartTime(
        ExecutionEventDefinition executionEvent,
        DateTime earliestStartTime,
        int durationMinutes,
        Dictionary<string, DateTime> intakeTimes)
    {
        // If no intake deadline, can start anytime up to very late
        if (!intakeTimes.TryGetValue(executionEvent.TaskId, out var intakeDeadline))
        {
            // No deadline - allow unlimited time
            return earliestStartTime.AddDays(365); // Arbitrary far future
        }

        // Must complete by intake deadline
        // Latest start = intake deadline - duration
        var latestStart = intakeDeadline.AddMinutes(-durationMinutes);

        // But not before earliest start
        return Max(latestStart, earliestStartTime);
    }

    /// <summary>
    /// Gets the execution date for a given day of week, based on a reference date.
    /// </summary>
    private DateTime GetExecutionDateForDay(DateTime referenceDate, DayOfWeek targetDay)
    {
        var currentDay = referenceDate.DayOfWeek;
        var daysUntilTarget = (int)targetDay - (int)currentDay;

        if (daysUntilTarget < 0)
            daysUntilTarget += 7; // Next week

        return referenceDate.AddDays(daysUntilTarget);
    }

    /// <summary>
    /// Returns the later of two DateTime values.
    /// </summary>
    private DateTime Max(DateTime dt1, DateTime dt2) => dt1 > dt2 ? dt1 : dt2;
}
