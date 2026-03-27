using CsvHelper.Configuration.Attributes;

namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Raw manifest record from Execution Duration History CSV.
/// Captures actual execution times for existing task runs to refine scheduling estimates.
/// </summary>
public record ExecutionDurationManifest
{
    /// <summary>Database ID (internal).</summary>
    [Ignore]
    public int Id { get; set; }

    /// <summary>Task identifier (matches TaskDefinitionManifest.TaskId).</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Date task executed (e.g., "2024-03-25").</summary>
    public string ExecutionDate { get; init; } = string.Empty;

    /// <summary>Scheduled start time (e.g., "06:00:00").</summary>
    public string ExecutionTime { get; init; } = string.Empty;

    /// <summary>Actual elapsed time in minutes.</summary>
    public string ActualDurationMinutes { get; init; } = string.Empty;

    /// <summary>Execution status: "Completed", "Failed", "Timeout", etc.</summary>
    public string Status { get; init; } = "Completed";
}
