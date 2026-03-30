using App.TaskSequencer.Domain.Models;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Validates whether execution instances can meet their intake event deadlines.
/// Critical for marking sequences as failed if timing conflicts prevent deadline compliance.
/// </summary>
public class DeadlineValidator
{
    /// <summary>
    /// Validates whether an execution instance can complete by its required intake deadline.
    /// </summary>
    public (bool IsValid, string? ValidationMessage) ValidateDeadline(
        ExecutionEventDefinition executionEvent,
        DateTime actualStartTime,
        ExecutionDuration duration,
        DateTime periodStartDate)
    {
        // If no intake requirement, always valid
        if (executionEvent.IntakeRequirement is null)
            return (true, null);

        // Check if this day has an intake requirement
        if (!executionEvent.IntakeRequirement.MustCompleteByIntake(executionEvent.ScheduledDay))
            return (true, null);

        // Calculate planned completion time
        var plannedCompletion = actualStartTime.Add(duration.ToTimeSpan());

        // Get deadline
        var deadline = executionEvent.IntakeRequirement.GetIntakeDeadline(periodStartDate.AddDays(((int)executionEvent.ScheduledDay - (int)periodStartDate.DayOfWeek + 7) % 7));

        // Check if can meet deadline
        if (plannedCompletion <= deadline)
            return (true, null);

        // Deadline miss
        var message = $"Deadline miss: planned completion {plannedCompletion:yyyy-MM-dd HH:mm:ss}, " +
                     $"intake deadline {deadline:yyyy-MM-dd HH:mm:ss}";

        return (false, message);
    }

    /// <summary>
    /// Validates DST compliance and returns any DST crossing warnings.
    /// </summary>
    public List<string> CheckDSTCrossings(
        DateTime actualStartTime,
        DateTime plannedCompletion)
    {
        var warnings = new List<string>();

        // Check if start and end times are in different DST states
        // This is a simplified check - real implementation may need timezone info
        var startIsDST = actualStartTime.IsDaylightSavingTime();
        var endIsDST = plannedCompletion.IsDaylightSavingTime();

        if (startIsDST != endIsDST)
        {
            warnings.Add($"Execution crosses DST boundary: start {actualStartTime:g} (DST={startIsDST}) " +
                        $"to end {plannedCompletion:g} (DST={endIsDST})");
        }

        return warnings;
    }

    /// <summary>
    /// Detects cascading failures: if a prerequisite is invalid, dependent tasks also become invalid.
    /// </summary>
    public bool ShouldMarkInvalidDueToPrerequisite(
        List<ExecutionEventDefinition> resolvedPrerequisites,
        Dictionary<string, (bool IsValid, string? Message)> validationResults)
    {
        foreach (var prereq in resolvedPrerequisites)
        {
            var prereqKey = prereq.GetExecutionEventKey();

            if (validationResults.TryGetValue(prereqKey, out var result))
            {
                if (!result.IsValid)
                    return true; // Mark as invalid due to prerequisite
            }
        }

        return false;
    }
}
