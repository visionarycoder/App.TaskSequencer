using CsvHelper.Configuration.Attributes;

namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Raw manifest record from Task Definition CSV.
/// Represents a single row before conversion to TaskDefinitionEnhanced.
/// </summary>
public record TaskDefinitionManifest
{
    /// <summary>Database ID (internal).</summary>
    [Ignore]
    public int Id { get; set; }

    /// <summary>Unique task identifier (e.g., "1", "EXTRACT_DATA").</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Human-readable task name.</summary>
    public string TaskName { get; init; } = string.Empty;

    /// <summary>Execution type: "Scheduled" or "OnDemand".</summary>
    public string ExecutionType { get; init; } = "Scheduled";

    /// <summary>Schedule type: "Recurring" or "OneOff".</summary>
    public string ScheduleType { get; init; } = "Recurring";

    /// <summary>Duration in minutes as string (e.g., "120" or "02:00").</summary>
    public string DurationMinutes { get; init; } = "15";

    /// <summary>Comma-separated list of prerequisite task IDs.</summary>
    public string Prerequisites { get; init; } = string.Empty;

    /// <summary>Pipe-separated execution days (e.g., "Monday|Wednesday|Friday").</summary>
    public string ExecutionDays { get; init; } = string.Empty;

    /// <summary>Pipe-separated execution times (e.g., "06:00:00|14:00:00").</summary>
    public string ExecutionTimes { get; init; } = string.Empty;
}
