namespace Client.Core.Contract;

/// <summary>
/// Interface for UI/Client operations and user interactions.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Displays execution results to the user.
    /// </summary>
    Task DisplayResultsAsync(object results, CancellationToken ct);

    /// <summary>
    /// Gets user input/parameters for execution.
    /// </summary>
    Task<T> GetUserInputAsync<T>(string prompt, CancellationToken ct) where T : notnull;
}
