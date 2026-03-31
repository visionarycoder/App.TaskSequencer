using System.Text.RegularExpressions;

namespace Core;

public class GlobalConstants
{
    // Default Duration Settings
    public const uint DefaultTaskDurationMinutes = 15;
    public const int BusinessHoursStart = 6;      // 06:00:00
    public const int BusinessHoursEnd = 18;       // 18:00:00
    public const int DefaultIntakeTimeHour = 11;  // 11:30:00 based on intake_events.csv

    // Execution Types
    public const string ExecutionType_Scheduled = "Scheduled";
    public const string ExecutionType_OnDemand = "OnDemand";

    // Schedule Types
    public const string ScheduleType_Recurring = "Recurring";
    public const string ScheduleType_OneOff = "OneOff";

    // Execution Status
    public const string Status_Pending = "Pending";
    public const string Status_Running = "Running";
    public const string Status_Completed = "Completed";
    public const string Status_Failed = "Failed";
    public const string Status_Paused = "Paused";

    // Constraints & Limits
    public const int MaxTaskDependencies = 50;
    public const int MaxConcurrentExecutions = 10;
    public const int MaxTaskDepth = 100;  // For circular dependency detection
    public const int DefaultTimeoutSeconds = 300;

    // Retry Configuration
    public const int MaxRetryAttempts = 3;
    public const int RetryDelayMilliseconds = 1000;

    // Infrastructure
    public const string SystemTaskIdPrefix = "SYS_";
    public const string OrleansGrainTypePrefix = "app.tasksequencer.grains";
}

public static partial class GlobalVariables
{
    /// <summary>ISO 8601 Duration format (e.g., "PT1H30M45S", "P1D", "PT30S")</summary>
    [GeneratedRegex(@"^PT(?:(\d+)D)?(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?$", RegexOptions.IgnoreCase)]
    public static partial Regex Iso8601Pattern();

    /// <summary>Time format (e.g., "14:30:00", "14:30", "9:15:30")</summary>
    [GeneratedRegex(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?$")]
    public static partial Regex TimeFormatPattern();

    /// <summary>Integer pattern (e.g., "123", "0", "999")</summary>
    [GeneratedRegex(@"^\d+$")]
    public static partial Regex IntegerPattern();

    /// <summary>Weekday names pipe-separated (e.g., "Monday|Wednesday|Friday")</summary>
    [GeneratedRegex(@"^(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)(\|(Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday))*$", RegexOptions.IgnoreCase)]
    public static partial Regex WeekdayPattern();

    /// <summary>Abbreviated weekday names comma-separated (e.g., "M,W,F", "Mon,Wed,Fri", "Tu,Th")</summary>
    [GeneratedRegex(@"^(M|T|W|Th|F|Sa|Su|Mon|Tue|Wed|Thu|Fri|Sat|Sun)(,(M|T|W|Th|F|Sa|Su|Mon|Tue|Wed|Thu|Fri|Sat|Sun))*$", RegexOptions.IgnoreCase)]
    public static partial Regex AbbreviatedWeekdayPattern();

    /// <summary>Weekday range using dash notation (e.g., "M-F", "Mon-Fri", "Monday-Friday")</summary>
    [GeneratedRegex(@"^(M|T|W|Th|F|Sa|Su|Mon|Tue|Wed|Thu|Fri|Sat|Sun|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)-(M|T|W|Th|F|Sa|Su|Mon|Tue|Wed|Thu|Fri|Sat|Sun|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)$", RegexOptions.IgnoreCase)]
    public static partial Regex WeekdayRangePattern();

    /// <summary>ISO 8601 date format (e.g., "2026-03-28", "2024-12-31")</summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    public static partial Regex Iso8601DatePattern();

    /// <summary>ISO 8601 datetime format with optional timezone (e.g., "2026-03-28T14:30:00Z", "2026-03-28T14:30:00-05:00")</summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(Z|[+-]\d{2}:\d{2})?$")]
    public static partial Regex Iso8601DateTimePattern();

    /// <summary>Unix epoch timestamp in seconds with optional fractional seconds (e.g., "1711600800", "1711600800.123")</summary>
    [GeneratedRegex(@"^\d{10}(\.\d+)?$")]
    public static partial Regex UnixTimestampPattern();

    /// <summary>UUID/GUID pattern with or without braces (e.g., "550e8400-e29b-41d4-a716-446655440000", "{550e8400-e29b-41d4-a716-446655440000}")</summary>
    [GeneratedRegex(@"^(\{)?[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(\})?$", RegexOptions.IgnoreCase)]
    public static partial Regex UuidPattern();

    /// <summary>Email address pattern (e.g., "user@domain.com", "alerts@company.org")</summary>
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    public static partial Regex EmailPattern();

    /// <summary>Execution status values (e.g., "Pending", "Running", "Completed", "Failed", "Paused")</summary>
    [GeneratedRegex(@"^(Pending|Running|Completed|Failed|Paused|OnDemand|Scheduled)$", RegexOptions.IgnoreCase)]
    public static partial Regex ExecutionStatusPattern();

    /// <summary>Comma or pipe-separated numeric IDs (e.g., "1,3,5" or "1|3|5")</summary>
    [GeneratedRegex(@"^\d+([,|]\d+)*$")]
    public static partial Regex NumericIdListPattern();
}