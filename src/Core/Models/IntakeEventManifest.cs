using CsvHelper.Configuration.Attributes;

namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Raw manifest record from Intake Event (Availability Window) CSV.
/// Specifies completion deadlines for each task by day of week and time.
/// Supports both static schedules (day-of-week based) and dynamic recurrence patterns (e.g., every 15 minutes, hourly).
/// </summary>
public record IntakeEventManifest
{
    /// <summary>Database ID (internal).</summary>
    [Ignore]
    public int Id { get; set; }

    /// <summary>Task identifier (matches TaskDefinitionManifest.TaskId).</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Task required on Monday (X to require). Used for static weekly schedules.</summary>
    public string Monday { get; init; } = string.Empty;

    /// <summary>Task required on Tuesday (X to require). Used for static weekly schedules.</summary>
    public string Tuesday { get; init; } = string.Empty;

    /// <summary>Task required on Wednesday (X to require). Used for static weekly schedules.</summary>
    public string Wednesday { get; init; } = string.Empty;

    /// <summary>Task required on Thursday (X to require). Used for static weekly schedules.</summary>
    public string Thursday { get; init; } = string.Empty;

    /// <summary>Task required on Friday (X to require). Used for static weekly schedules.</summary>
    public string Friday { get; init; } = string.Empty;

    /// <summary>Task required on Saturday (X to require). Used for static weekly schedules.</summary>
    public string Saturday { get; init; } = string.Empty;

    /// <summary>Task required on Sunday (X to require). Used for static weekly schedules.</summary>
    public string Sunday { get; init; } = string.Empty;

    /// <summary>Intake deadline time (e.g., "11:30:00"). Used for both static and recurring schedules.</summary>
    public string IntakeTime { get; init; } = "23:59:59";

    /// <summary>
    /// Gets or initializes the recurrence pattern for repeated events.
    /// If not set or Frequency=None, the event uses the static day-of-week schedule.
    /// Examples: Every 15 minutes, hourly, every 2 hours, daily, etc.
    /// </summary>
    [Ignore]
    public RecurrencePattern? RecurrencePattern { get; init; }

    /// <summary>
    /// Determines if this manifest uses dynamic recurrence (recurring) or static day-of-week scheduling.
    /// </summary>
    public bool IsRecurring => RecurrencePattern?.Frequency != RecurrenceFrequency.None;

    /// <summary>
    /// Validates the intake event manifest for consistency.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(TaskId))
            return false;

        if (string.IsNullOrWhiteSpace(IntakeTime))
            return false;

        // If recurring, validate recurrence pattern
        if (IsRecurring && (RecurrencePattern == null || !RecurrencePattern.IsValid()))
            return false;

        // If not recurring, ensure at least one day is specified
        if (!IsRecurring)
        {
            var anyDaySpecified = !string.IsNullOrWhiteSpace(Monday)
                || !string.IsNullOrWhiteSpace(Tuesday)
                || !string.IsNullOrWhiteSpace(Wednesday)
                || !string.IsNullOrWhiteSpace(Thursday)
                || !string.IsNullOrWhiteSpace(Friday)
                || !string.IsNullOrWhiteSpace(Saturday)
                || !string.IsNullOrWhiteSpace(Sunday);

            if (!anyDaySpecified)
                return false;
        }

        return true;
    }
}
