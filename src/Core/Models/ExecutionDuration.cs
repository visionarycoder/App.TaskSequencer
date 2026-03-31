namespace Core.Models;

/// <summary>
/// Represents duration information with estimation metadata.
/// Tracks whether duration is estimated (default 15 min) or actual (from execution history).
/// </summary>
public record ExecutionDuration(
    uint DurationMinutes,
    bool IsEstimated,
    bool IsPendingReplacement = false
)
{
    /// <summary>
    /// Creates a default 15-minute estimated duration.
    /// </summary>
    public static ExecutionDuration Default() =>
        new(15, IsEstimated: true, IsPendingReplacement: false);

    /// <summary>
    /// Creates an actual duration from execution history.
    /// </summary>
    public static ExecutionDuration Actual(uint minutes) =>
        new(minutes, IsEstimated: false, IsPendingReplacement: false);

    /// <summary>
    /// Creates a duration pending replacement (from failed execution).
    /// </summary>
    public static ExecutionDuration PendingReplacement(uint estimatedMinutes) =>
        new(estimatedMinutes, IsEstimated: true, IsPendingReplacement: true);

    /// <summary>
    /// Returns duration as TimeSpan.
    /// </summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromMinutes(DurationMinutes);
}
