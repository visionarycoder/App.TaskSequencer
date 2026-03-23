using System.Text.RegularExpressions;

namespace ConsoleApp.Ifx;

public static class Utils
{
    private static readonly Regex Iso8601Pattern = new(@"^PT(?:(\d+)D)?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TimeFormatPattern = new(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$", RegexOptions.Compiled);

    private static readonly Regex IntegerPattern = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Parses duration string to uint seconds (for TaskDefinition.DurationSeconds).
    /// Supports formats: "HH:mm:ss", "HH:mm", "mm:ss", ISO 8601 ("PT1H30M45S"), or integer seconds.
    /// </summary>
    public static uint ParseDuration(string durationString)
    {
        var timeSpan = ParseDurationAsTimeSpan(durationString);
        return (uint)timeSpan.TotalSeconds;
    }

    /// <summary>
    /// Parses a duration string to TimeSpan.
    /// Supports formats: "HH:mm:ss", "HH:mm", "mm:ss", ISO 8601 ("PT1H30M45S"), or integer seconds.
    /// </summary>
    public static TimeSpan ParseDurationAsTimeSpan(string durationString)
    {
        if (string.IsNullOrWhiteSpace(durationString))
            return TimeSpan.Zero;

        durationString = durationString.Trim();

        if (Iso8601Pattern.IsMatch(durationString) && TimeSpan.TryParse(durationString.Replace("PT", "", StringComparison.OrdinalIgnoreCase), out var isoTimeSpan))
            return isoTimeSpan;

        if (TimeFormatPattern.IsMatch(durationString) && TimeSpan.TryParse(durationString, out var timeSpan))
            return timeSpan;

        if (IntegerPattern.IsMatch(durationString) && int.TryParse(durationString, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        return TimeSpan.Zero;
    }

    /// <summary>
    /// Parses a datetime string to DateTime?.
    /// Returns null if the string is empty or whitespace.
    /// </summary>
    public static DateTime? ParseDateTime(string dateTimeString) =>
        string.IsNullOrWhiteSpace(dateTimeString)
            ? null
            : DateTime.TryParse(dateTimeString.Trim(), out var dateTime)
                ? dateTime
                : null;

    /// <summary>
    /// Parses a comma-separated list of task IDs to IReadOnlySet.
    /// </summary>
    public static IReadOnlySet<string> ParsePrerequisites(string prerequisitesString)
    {
        if (string.IsNullOrWhiteSpace(prerequisitesString))
            return new HashSet<string>().AsReadOnly();

        return new HashSet<string>(
            prerequisitesString
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ).AsReadOnly();
    }

    /// <summary>
    /// Parses duration string to uint minutes (for TaskDefinition.DurationMinutes).
    /// Handles: minutes as integer, HH:mm:ss format.
    /// </summary>
    public static uint ParseDurationMinutes(string durationString)
    {
        if (string.IsNullOrWhiteSpace(durationString))
            return 15; // Default 15 minutes

        durationString = durationString.Trim();

        // Try integer minutes
        if (uint.TryParse(durationString, out var minutes))
            return minutes;

        // Try HH:mm:ss format
        if (TimeSpan.TryParse(durationString, out var timeSpan))
            return (uint)timeSpan.TotalMinutes;

        return 15; // Default fallback
    }
}
