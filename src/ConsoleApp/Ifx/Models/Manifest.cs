namespace ConsoleApp.Ifx.Models;

/// <summary>
/// Represents a single row from the CSV file.
/// Raw data before conversion to TaskDefinition.
/// </summary>
public record Manifest
{
    public int Id { get; set; }
    public string TaskId { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string ScheduledStartTime { get; init; } = string.Empty;
    public string RequiredEndTime { get; init; } = string.Empty;
    public string Prerequisites { get; init; } = string.Empty;
}
