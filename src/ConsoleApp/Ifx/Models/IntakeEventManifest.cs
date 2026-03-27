using CsvHelper.Configuration.Attributes;

namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Raw manifest record from Intake Event (Availability Window) CSV.
/// Specifies completion deadlines for each task by day of week and time.
/// </summary>
public record IntakeEventManifest
{
    /// <summary>Database ID (internal).</summary>
    [Ignore]
    public int Id { get; set; }

    /// <summary>Task identifier (matches TaskDefinitionManifest.TaskId).</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>Task required on Monday (X to require).</summary>
    public string Monday { get; init; } = string.Empty;

    /// <summary>Task required on Tuesday (X to require).</summary>
    public string Tuesday { get; init; } = string.Empty;

    /// <summary>Task required on Wednesday (X to require).</summary>
    public string Wednesday { get; init; } = string.Empty;

    /// <summary>Task required on Thursday (X to require).</summary>
    public string Thursday { get; init; } = string.Empty;

    /// <summary>Task required on Friday (X to require).</summary>
    public string Friday { get; init; } = string.Empty;

    /// <summary>Task required on Saturday (X to require).</summary>
    public string Saturday { get; init; } = string.Empty;

    /// <summary>Task required on Sunday (X to require).</summary>
    public string Sunday { get; init; } = string.Empty;

    /// <summary>Intake deadline time (e.g., "11:30:00").</summary>
    public string IntakeTime { get; init; } = "23:59:59";
}
