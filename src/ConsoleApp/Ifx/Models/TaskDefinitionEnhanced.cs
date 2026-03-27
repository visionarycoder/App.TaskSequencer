namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Enhanced task definition with full execution scheduling details.
/// Represents a template for creating ExecutionEventDefinition instances.
/// </summary>
public record TaskDefinitionEnhanced(
    Guid Uid,
    string TaskId,
    string TaskName,
    uint DurationMinutes,
    IReadOnlySet<string> PrerequisiteIds,
    ExecutionType ExecutionType,
    ScheduleType ScheduleType,
    IReadOnlySet<DayOfWeek> ScheduledDays,
    IReadOnlyList<TimeOfDay> ScheduledTimes,
    IntakeEventRequirement? IntakeRequirement = null
);

/// <summary>
/// Execution type enumeration.
/// </summary>
public enum ExecutionType
{
    /// <summary>Task scheduled for automatic execution.</summary>
    Scheduled = 0,

    /// <summary>Task executed on demand (not scheduled).</summary>
    OnDemand = 1
}

/// <summary>
/// Schedule type enumeration.
/// </summary>
public enum ScheduleType
{
    /// <summary>Task executes multiple times (recurring).</summary>
    Recurring = 0,

    /// <summary>Task executes once.</summary>
    OneOff = 1
}
