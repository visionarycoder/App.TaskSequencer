namespace Access.DataModel.Contract;

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

/// <summary>
/// Represents a unique identifier for entities.
/// </summary>
public record Identifier(Guid Id, string Name);

/// <summary>
/// Data access service interface for loading and persisting task definitions.
/// </summary>
public interface IDataAccessService
{
    /// <summary>
    /// Loads raw manifest data from a CSV file.
    /// </summary>
    Task<IEnumerable<Manifest>> LoadManifestAsync(string filePath, CancellationToken ct);

    /// <summary>
    /// Saves execution results to persistent storage.
    /// </summary>
    Task SaveResultsAsync(string outputPath, object results, CancellationToken ct);
}
