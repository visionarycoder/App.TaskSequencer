namespace ConsoleApp.Ifx.Models;
/// <summary>
/// Extension methods for converting Manifest to TaskDefinition.
/// </summary>
public static class ManifestExtensions
{
    /// <summary>
    /// Converts a Manifest CSV row to a TaskDefinition.
    /// </summary>
    public static TaskDefinition ToTaskDefinition(this Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var prerequisites = Utils.ParsePrerequisites(manifest.Prerequisites ?? string.Empty);
        var durationMinutes = Utils.ParseDurationMinutes(manifest.Duration ?? string.Empty);

        return new TaskDefinition(
            manifest.TaskId,
            durationMinutes,
            prerequisites,
            Utils.ParseDateTime(manifest.ScheduledStartTime),
            Utils.ParseDateTime(manifest.RequiredEndTime)
        );
    }
}
