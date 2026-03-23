using Access.DataModel.Contract;
using System.Text;

namespace Access.DataModel.Service;

/// <summary>
/// Data access implementation for CSV-based manifest loading and result persistence.
/// </summary>
public class CsvDataAccessService : IDataAccessService
{
    private const string CsvDelimiter = ",";

    public async Task<IEnumerable<Manifest>> LoadManifestAsync(string filePath, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Manifest file not found: {filePath}");

        var manifests = new List<Manifest>();

        return await Task.Run(async () =>
        {
            using var reader = new StreamReader(filePath);
            var header = await reader.ReadLineAsync(ct);

            if (string.IsNullOrWhiteSpace(header))
                throw new InvalidOperationException("CSV file is empty or has no header.");

            var headerColumns = header.Split(CsvDelimiter);
            int rowNumber = 1;

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                rowNumber++;
                var values = line.Split(CsvDelimiter);

                if (values.Length < headerColumns.Length)
                    throw new InvalidOperationException($"Row {rowNumber} has fewer columns than header.");

                try
                {
                    manifests.Add(new Manifest
                    {
                        Id = rowNumber - 1,
                        TaskId = values[0].Trim(),
                        Duration = values[1].Trim(),
                        ScheduledStartTime = values.Length > 2 ? values[2].Trim() : string.Empty,
                        RequiredEndTime = values.Length > 3 ? values[3].Trim() : string.Empty,
                        Prerequisites = values.Length > 4 ? values[4].Trim() : string.Empty
                    });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error parsing row {rowNumber}: {ex.Message}", ex);
                }
            }

            return manifests.AsEnumerable();
        }, ct);
    }

    public async Task SaveResultsAsync(string outputPath, object results, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(results);

        await Task.Run(async () =>
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(outputPath, results.ToString() ?? string.Empty, Encoding.UTF8, ct);
        }, ct);
    }
}
