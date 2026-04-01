using System.Globalization;
using Core.Models;
using CsvHelper;

namespace Core.Services;

/// <summary>
/// Service for parsing CSV files containing task definitions, intake requirements, and duration history.
/// Handles all three input file formats.
/// </summary>
public class ManifestCsvParser
{
    /// <summary>
    /// Parses Task Definition CSV file.
    /// </summary>
    public async Task<List<TaskDefinitionManifest>> ParseTaskDefinitionCsvAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Task definition file not found: {filePath}");

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = csv.GetRecordsAsync<TaskDefinitionManifest>();
        var list = new List<TaskDefinitionManifest>();

        await foreach (var record in records)
        {
            list.Add(record);
        }

        return list;
    }

    /// <summary>
    /// Parses Intake Event (Availability Window) CSV file.
    /// </summary>
    public async Task<List<IntakeEventManifest>> ParseIntakeEventCsvAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Intake event file not found: {filePath}");

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = csv.GetRecordsAsync<IntakeEventManifest>();
        var list = new List<IntakeEventManifest>();

        await foreach (var record in records)
        {
            list.Add(record);
        }

        return list;
    }

    /// <summary>
    /// Parses Execution Duration History CSV file.
    /// </summary>
    public async Task<List<ExecutionDurationManifest>> ParseExecutionDurationCsvAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return []; // Optional file

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = csv.GetRecordsAsync<ExecutionDurationManifest>();
        var list = new List<ExecutionDurationManifest>();

        await foreach (var record in records)
        {
            list.Add(record);
        }

        return list;
    }

    /// <summary>
    /// Parses Master Sequence CSV file.
    /// </summary>
    public async Task<List<MasterSequenceManifest>> ParseMasterSequenceCsvAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Master sequence file not found: {filePath}");

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var records = csv.GetRecordsAsync<MasterSequenceManifest>();
        var list = new List<MasterSequenceManifest>();

        await foreach (var record in records)
        {
            list.Add(record);
        }

        return list;
    }

    /// <summary>
    /// Parses all three CSV files synchronously (convenience method).
    /// </summary>
    public (List<TaskDefinitionManifest> Tasks, List<IntakeEventManifest> IntakeEvents, List<ExecutionDurationManifest> Durations)
        ParseAll(string taskDefPath, string intakeEventPath, string? durationHistoryPath = null)
    {
        var tasks = ParseTaskDefinitionCsvAsync(taskDefPath).GetAwaiter().GetResult();
        var intakeEvents = ParseIntakeEventCsvAsync(intakeEventPath).GetAwaiter().GetResult();
        var durations = !string.IsNullOrEmpty(durationHistoryPath)
            ? ParseExecutionDurationCsvAsync(durationHistoryPath).GetAwaiter().GetResult()
            : [];

        return (tasks, intakeEvents, durations);
    }
}
