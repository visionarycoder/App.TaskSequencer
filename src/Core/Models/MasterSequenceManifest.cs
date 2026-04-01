using CsvHelper.Configuration.Attributes;

namespace Core.Models;

/// <summary>
/// Raw manifest record from Master Sequence CSV.
/// Defines the execution sequence and cycle details for each interface in the system.
/// </summary>
public record MasterSequenceManifest
{
    /// <summary>Database ID (internal).</summary>
    [Ignore]
    public int Id { get; set; }

    /// <summary>Sequence order for execution (e.g., 41, 42, 35).</summary>
    public string Sequence { get; init; } = string.Empty;

    /// <summary>Interface identifier (e.g., "2010", "2010 IDL DT").</summary>
    public string InterfaceId { get; init; } = string.Empty;

    /// <summary>Internal cycle type (e.g., "Monthly", "Daily", "Adhoc").</summary>
    public string InternalCycleType { get; init; } = string.Empty;

    /// <summary>Internal cycle detail description (e.g., "Twice monthly, Payroll Calendar").</summary>
    public string InternalCycleDetail { get; init; } = string.Empty;

    /// <summary>Internal delivery time (e.g., "TBD", "3:30pm").</summary>
    public string InternalDeliveryTime { get; init; } = string.Empty;

    /// <summary>Internal delivery time detail (e.g., "Day 4", "between 7am and 8am").</summary>
    public string InternalDeliveryTimeDetail { get; init; } = string.Empty;

    /// <summary>External cycle type (e.g., "Nightly", "OnDemand", "N/A").</summary>
    public string ExternalCycleType { get; init; } = string.Empty;

    /// <summary>External cycle day (e.g., "OnDemand", "Mon-Sun", "N/A").</summary>
    public string ExternalCycleDay { get; init; } = string.Empty;

    /// <summary>External batch start time (e.g., "10:00pm", "AdHoc", "N/A").</summary>
    public string ExternalBatchStartTime { get; init; } = string.Empty;

    /// <summary>External batch end time (e.g., "6:00am", "AdHoc", "N/A").</summary>
    public string ExternalBatchEndTime { get; init; } = string.Empty;

    /// <summary>External delivery time detail description.</summary>
    public string ExternalDeliveryTimeDetail { get; init; } = string.Empty;

    /// <summary>Hard dependency description - references another interface ID that must complete first.</summary>
    public string HardDependencyDescription { get; init; } = string.Empty;
}
