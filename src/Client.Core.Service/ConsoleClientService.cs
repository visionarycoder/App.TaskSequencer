using Client.Core.Contract;

namespace Client.Core.Service;

/// <summary>
/// Console-based implementation of client service for command-line UI.
/// </summary>
public class ConsoleClientService : IClientService
{
    public async Task DisplayResultsAsync(object results, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(results);

        await Task.Run(() =>
        {
            Console.WriteLine("\n=== Execution Results ===");
            Console.WriteLine(results);
            Console.WriteLine("========================\n");
        }, ct);
    }

    public async Task<T> GetUserInputAsync<T>(string prompt, CancellationToken ct) where T : notnull
    {
        return await Task.Run(() =>
        {
            Console.Write(prompt);
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                throw new InvalidOperationException("User input cannot be empty.");

            return (T)Convert.ChangeType(input, typeof(T))!;
        }, ct);
    }
}
