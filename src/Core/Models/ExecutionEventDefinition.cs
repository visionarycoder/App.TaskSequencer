using App.TaskSequencer.Domain.Foundation;
using App.TaskSequencer.Domain.Models;

namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Represents a single scheduled execution event for a task.
/// Generated from TaskDefinitionEnhanced by combining each day × time combination.
/// Example: Task 1 on Monday and Wednesday at 06:00 and 14:00 = 4 ExecutionEventDefinitions.
/// </summary>
public record ExecutionEventDefinition(
    Guid TaskUid,
    string TaskId,
    string TaskName,
    DayOfWeek ScheduledDay,
    TimeOfDay ScheduledTime,
    IReadOnlySet<string> PrerequisiteTaskIds,
    uint DurationMinutes,
    IntakeEventRequirement? IntakeRequirement = null
)
{
    /// <summary>
    /// Gets a unique key for this execution event: TaskId_DayOfWeek_HHmmss
    /// </summary>
    public string GetExecutionEventKey() =>
        $"{TaskId}_{ScheduledDay}_{ScheduledTime.Hour:D2}{ScheduledTime.Minute:D2}{ScheduledTime.Second:D2}";

    /// <summary>
    /// Calculates the deadline for this event (if intake requirement exists).
    /// </summary>
    public DateTime? GetIntakeDeadline(DateTime executionDate)
    {
        if (IntakeRequirement is null)
            return null;

        if (!IntakeRequirement.MustCompleteByIntake(ScheduledDay))
            return null;

        return IntakeRequirement.GetIntakeDeadline(executionDate);
    }
}
