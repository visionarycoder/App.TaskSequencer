namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Defines the recurrence frequency for repeated events.
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>No recurrence - single occurrence only.</summary>
    None = 0,

    /// <summary>Every N minutes.</summary>
    Minutely = 1,

    /// <summary>Every N hours.</summary>
    Hourly = 2,

    /// <summary>Every N days.</summary>
    Daily = 3,

    /// <summary>Every N weeks on specified days.</summary>
    Weekly = 4,

    /// <summary>Every N months on specified day.</summary>
    Monthly = 5,

    /// <summary>Every N years.</summary>
    Yearly = 6
}

/// <summary>
/// Specifies a recurrence pattern for repeated intake events.
/// Supports patterns like "every 15 minutes", "hourly", "daily", etc.
/// </summary>
public record RecurrencePattern
{
    /// <summary>
    /// Gets or initializes the frequency type (Minutely, Hourly, Daily, etc.).
    /// </summary>
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.None;

    /// <summary>
    /// Gets or initializes the interval between occurrences.
    /// Example: Frequency=Hourly, Interval=2 means every 2 hours.
    /// </summary>
    public int Interval { get; init; } = 1;

    /// <summary>
    /// Gets or initializes the maximum number of occurrences (null = unlimited).
    /// </summary>
    public int? MaxOccurrences { get; init; }

    /// <summary>
    /// Gets or initializes the end date for the recurrence (null = no end date).
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Gets or initializes days of week for weekly recurrence (Monday=1, Tuesday=2, etc.).
    /// </summary>
    public IReadOnlySet<int> WeeklyDays { get; init; } = new HashSet<int>();

    /// <summary>
    /// Gets or initializes day of month for monthly recurrence (1-31, or -1 for last day).
    /// DEPRECATED: Use MonthlyDays instead for multiple days per month.
    /// Kept for backward compatibility.
    /// </summary>
    public int? MonthlyDay { get; init; }

    /// <summary>
    /// Gets or initializes days of month for monthly recurrence.
    /// Supports multiple days (e.g., 1st and 15th) in a single manifest.
    /// Values: 1-31 for specific days, -1 for last day of month.
    /// Example: new HashSet<int> { 1, 15 } for 1st and 15th.
    /// </summary>
    public IReadOnlySet<int> MonthlyDays { get; init; } = new HashSet<int>();

    /// <summary>
    /// Validates the recurrence pattern for consistency.
    /// </summary>
    public bool IsValid()
    {
        if (Interval < 1)
            return false;

        if (Frequency == RecurrenceFrequency.Weekly && WeeklyDays.Count == 0)
            return false;

        if (Frequency == RecurrenceFrequency.Monthly)
        {
            var daysToValidate = new HashSet<int>();
            
            // Collect days from both MonthlyDay (legacy) and MonthlyDays (new)
            if (MonthlyDay.HasValue)
                daysToValidate.Add(MonthlyDay.Value);
            
            foreach (var day in MonthlyDays)
                daysToValidate.Add(day);

            // If monthly frequency, must have at least one day specified
            if (daysToValidate.Count == 0)
                return false;

            // Validate all days are in valid range
            if (daysToValidate.Any(d => d < -1 || d == 0 || d > 31))
                return false;
        }

        if (MaxOccurrences.HasValue && MaxOccurrences < 1)
            return false;

        return true;
    }

    /// <summary>
    /// Gets all monthly days to use (combining legacy and new properties).
    /// </summary>
    public IReadOnlySet<int> GetAllMonthlyDays()
    {
        var days = new HashSet<int>(MonthlyDays);
        
        if (MonthlyDay.HasValue)
            days.Add(MonthlyDay.Value);
        
        return days;
    }
}
