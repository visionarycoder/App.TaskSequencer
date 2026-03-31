namespace Core.Models;

/// <summary>
/// Represents a time of day without date (HH:mm:ss).
/// </summary>
public record TimeOfDay(int Hour, int Minute, int Second = 0)
{
    /// <summary>
    /// Converts to TimeSpan.
    /// </summary>
    public TimeSpan ToTimeSpan() => new(Hour, Minute, Second);

    /// <summary>
    /// Formats as HH:mm:ss string.
    /// </summary>
    public override string ToString() => $"{Hour:D2}:{Minute:D2}:{Second:D2}";

    /// <summary>
    /// Parses time string in format HH:mm:ss or HH:mm.
    /// </summary>
    public static TimeOfDay Parse(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            throw new ArgumentException("Time string cannot be empty", nameof(timeString));

        var parts = timeString.Trim().Split(':');
        if (parts.Length < 2 || parts.Length > 3)
            throw new ArgumentException($"Invalid time format: {timeString}", nameof(timeString));

        if (!int.TryParse(parts[0], out var hour))
            throw new ArgumentException($"Invalid hour: {parts[0]}", nameof(timeString));

        if (!int.TryParse(parts[1], out var minute))
            throw new ArgumentException($"Invalid minute: {parts[1]}", nameof(timeString));

        var second = 0;
        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[2], out second))
                throw new ArgumentException($"Invalid second: {parts[2]}", nameof(timeString));
        }

        if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
            throw new ArgumentException($"Time component out of range: {timeString}", nameof(timeString));

        return new TimeOfDay(hour, minute, second);
    }

    /// <summary>
    /// Applies this time to a given date to create a DateTime.
    /// </summary>
    public DateTime ApplyToDate(DateTime date) =>
        new(date.Year, date.Month, date.Day, Hour, Minute, Second);
}
