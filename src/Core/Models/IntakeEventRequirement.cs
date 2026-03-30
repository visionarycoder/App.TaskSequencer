using App.TaskSequencer.Domain.Foundation;

namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Represents a task's intake event requirement (deadline constraint).
/// Specifies which days a task must be complete and by what time.
/// </summary>
public record IntakeEventRequirement(
    string TaskId,
    IReadOnlySet<DayOfWeek> RequiredDays,
    TimeOfDay IntakeTime
)
{
    /// <summary>
    /// Checks if task must be complete by intake time on given day.
    /// </summary>
    public bool MustCompleteByIntake(DayOfWeek day) => RequiredDays.Contains(day);

    /// <summary>
    /// Calculates the deadline (intake time on the given day).
    /// </summary>
    public DateTime GetIntakeDeadline(DateTime executionDate) =>
        IntakeTime.ApplyToDate(executionDate.Date);

    /// <summary>
    /// Checks if execution can complete by deadline.
    /// </summary>
    public bool CanMeetDeadline(DateTime plannedCompletion)
    {
        var deadline = GetIntakeDeadline(plannedCompletion.Date);
        return plannedCompletion <= deadline;
    }
}
