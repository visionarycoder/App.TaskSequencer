using App.TaskSequencer.Domain.Foundation;
using App.TaskSequencer.Domain.Models;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Resolves dependencies for execution events and adjusts start times based on prerequisite completion.
/// Implements the core dependency resolution algorithm from the requirements.
/// </summary>
public class DependencyResolver
{
    /// <summary>
    /// Resolves prerequisites for an execution event.
    /// Returns the latest feasible execution of each prerequisite that must complete before this event.
    /// </summary>
    public List<ExecutionEventDefinition> ResolvePrerequisites(
        ExecutionEventDefinition executionEvent,
        List<ExecutionEventDefinition> allExecutionEvents)
    {
        var resolvedPrerequisites = new List<ExecutionEventDefinition>();

        // If no prerequisites, return empty
        if (executionEvent.PrerequisiteTaskIds.Count == 0)
            return resolvedPrerequisites;

        // For each prerequisite task
        foreach (var prereqTaskId in executionEvent.PrerequisiteTaskIds)
        {
            // Find all execution events for this prerequisite task
            var prereqEvents = allExecutionEvents
                .Where(e => e.TaskId == prereqTaskId)
                .ToList();

            if (prereqEvents.Count == 0)
            {
                // Prerequisite task has no execution events (e.g., OnDemand with no schedule)
                // Mark as validation error - prerequisite never executes
                continue;
            }

            // Filter to feasible events: those on same day as execution event that occur before it,
            // or latest event from any previous day
            var feasiblePrereqEvents = FindFeasiblePrerequisiteEvents(executionEvent, prereqEvents);

            if (feasiblePrereqEvents.Count == 0)
            {
                // No feasible prerequisite execution found
                // This is a validation error
                continue;
            }

            // Select the LATEST (most recent) feasible prerequisite
            var latestPrereq = SelectLatestPrerequisiteEvent(executionEvent, feasiblePrereqEvents);
            resolvedPrerequisites.Add(latestPrereq);
        }

        return resolvedPrerequisites;
    }

    /// <summary>
    /// Finds prerequisite execution events that are feasible for this execution event.
    /// Feasible = scheduled same day before execution event, or from earlier in the week/period.
    /// </summary>
    private List<ExecutionEventDefinition> FindFeasiblePrerequisiteEvents(
        ExecutionEventDefinition executionEvent,
        List<ExecutionEventDefinition> prereqEvents)
    {
        var feasible = new List<ExecutionEventDefinition>();

        // Group by day (for week: Mon=0, Tue=1, ..., Sun=6)
        var executionDayValue = (int)executionEvent.ScheduledDay;

        foreach (var prereqEvent in prereqEvents)
        {
            var prereqDayValue = (int)prereqEvent.ScheduledDay;

            // Case 1: Same day - prerequisite must be earlier in the day
            if (prereqDayValue == executionDayValue)
            {
                if (prereqEvent.ScheduledTime.ToTimeSpan() < executionEvent.ScheduledTime.ToTimeSpan())
                {
                    feasible.Add(prereqEvent);
                }
            }
            // Case 2: Earlier in week - always feasible
            else if (prereqDayValue < executionDayValue)
            {
                feasible.Add(prereqEvent);
            }
            // Case 3: Later in week - NOT feasible (would need to go to previous week)
        }

        return feasible;
    }

    /// <summary>
    /// Selects the latest (most recent) feasible prerequisite event.
    /// Prioritizes: latest day, then latest time on that day.
    /// </summary>
    private ExecutionEventDefinition SelectLatestPrerequisiteEvent(
        ExecutionEventDefinition executionEvent,
        List<ExecutionEventDefinition> feasiblePrereqEvents)
    {
        // Sort by day descending (latest first), then by time descending
        var sorted = feasiblePrereqEvents
            .OrderByDescending(e => (int)e.ScheduledDay)
            .ThenByDescending(e => e.ScheduledTime.ToTimeSpan())
            .FirstOrDefault();

        if (sorted == null)
        {
            // Fallback (should not happen given input was non-empty)
            throw new InvalidOperationException("No feasible prerequisite found");
        }

        return sorted;
    }

    /// <summary>
    /// Calculates the adjusted (functional) start time for an execution event based on prerequisite completion times.
    /// </summary>
    public DateTime CalculateAdjustedStartTime(
        ExecutionEventDefinition executionEvent,
        List<ExecutionEventDefinition> resolvedPrerequisites,
        Dictionary<string, (DateTime ScheduledStart, DateTime PlannedCompletion, ExecutionDuration Duration)> eventTimingLookup,
        DateTime periodStartDate)
    {
        var scheduledStartTime = ApplyTimeToDate(executionEvent.ScheduledDay, executionEvent.ScheduledTime, periodStartDate);

        // If no prerequisites, no adjustment needed
        if (resolvedPrerequisites.Count == 0)
            return scheduledStartTime;

        // Find latest completion time among all prerequisites
        var latestPrereqCompletion = DateTime.MinValue;

        foreach (var prereq in resolvedPrerequisites)
        {
            var prereqKey = prereq.GetExecutionEventKey();

            if (eventTimingLookup.TryGetValue(prereqKey, out var timing))
            {
                if (timing.PlannedCompletion > latestPrereqCompletion)
                    latestPrereqCompletion = timing.PlannedCompletion;
            }
        }

        // Adjusted start = MAX(scheduled start, latest prerequisite completion)
        return latestPrereqCompletion > scheduledStartTime
            ? latestPrereqCompletion
            : scheduledStartTime;
    }

    /// <summary>
    /// Applies a day-of-week and time-of-day to a period start date.
    /// Handles week-based scheduling (Monday = 0, Sunday = 6).
    /// </summary>
    private DateTime ApplyTimeToDate(
        DayOfWeek scheduleDay,
        TimeOfDay scheduleTime,
        DateTime periodStart)
    {
        // Calculate days to add from period start (assumed to be a Monday)
        var periodDayOfWeek = periodStart.DayOfWeek;
        var daysToAdd = ((int)scheduleDay - (int)periodDayOfWeek + 7) % 7;

        var targetDate = periodStart.AddDays(daysToAdd);
        return scheduleTime.ApplyToDate(targetDate);
    }
}
