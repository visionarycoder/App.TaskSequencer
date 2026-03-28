# migrating to recurrence support

## For Manifest Creators

### Before: Static Day-of-Week Only
```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    Monday = "X",
    Tuesday = "X",
    Wednesday = "X",
    Thursday = "X",
    Friday = "X",
    Saturday = "",
    Sunday = "",
    IntakeTime = "09:00:00"
};
```

### After: Add Recurring Pattern

#### Option A: Single Monthly Day
```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDay = 15  // 15th only
    }
};
```

#### Option B: Multiple Monthly Days (NEW - ONE MANIFEST PER CSV ROW!)
```csharp
// Single manifest supports BOTH 1st and 15th
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15 }  // 1st AND 15th together!
    }
};
```

#### Option C: Multiple Days Including Last Day
```csharp
// 1st, 15th, and last day of month
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15, -1 }  // -1 = last day
    }
};
```

#### Option D: Keep existing day-of-week (backward compatible)
```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    Monday = "X",
    Wednesday = "X",
    Friday = "X",
    IntakeTime = "09:00:00"
    // No RecurrencePattern needed
};
```

## For Manifest Loaders

### If Loading from CSV with Day-of-Week Columns
No changes needed - existing code continues to work:

```csharp
var manifest = csvRow.Deserialize<IntakeEventManifest>();
// IntakeEventManifest.RecurrencePattern is null
// Uses traditional day-of-week matching
```

### If Adding Support for Recurrence Column
```csharp
public IntakeEventManifest LoadFromCsv(Dictionary<string, string> row)
{
    var manifest = new IntakeEventManifest
    {
        TaskId = row["TaskId"],
        IntakeTime = row["IntakeTime"],
        Monday = row.GetValueOrDefault("Monday", ""),
        // ... other days ...
    };

    // NEW: Check for recurrence pattern data
    if (!string.IsNullOrEmpty(row.GetValueOrDefault("RecurrenceFrequency")))
    {
        manifest = manifest with
        {
            RecurrencePattern = ParseRecurrenceFromCsv(row)
        };
    }

    return manifest;
}

private RecurrencePattern ParseRecurrenceFromCsv(Dictionary<string, string> row)
{
    var frequency = Enum.Parse<RecurrenceFrequency>(row["RecurrenceFrequency"]);
    var pattern = new RecurrencePattern
    {
        Frequency = frequency,
        Interval = int.Parse(row["RecurrenceInterval"] ?? "1")
    };

    // Handle optional fields based on frequency
    if (frequency == RecurrenceFrequency.Weekly)
    {
        pattern = pattern with
        {
            WeeklyDays = ParseDayList(row["RecurrenceWeeklyDays"])
        };
    }
    else if (frequency == RecurrenceFrequency.Monthly)
    {
        // NEW: Support multiple days per month in a single CSV row
        // CSV format examples:
        //   "1,15"          -> 1st and 15th
        //   "1,15,-1"       -> 1st, 15th, and last day
        //   "1"             -> 1st only
        //   "-1"            -> last day only
        if (!string.IsNullOrEmpty(row.GetValueOrDefault("RecurrenceMonthlyDays")))
        {
            pattern = pattern with
            {
                MonthlyDays = ParseDayList(row["RecurrenceMonthlyDays"])
            };
        }
        // Backward compatibility: support single day column
        else if (!string.IsNullOrEmpty(row.GetValueOrDefault("RecurrenceMonthlyDay")))
        {
            pattern = pattern with
            {
                MonthlyDay = int.Parse(row["RecurrenceMonthlyDay"])
            };
        }
    }

    if (!string.IsNullOrEmpty(row.GetValueOrDefault("RecurrenceEndDate")))
    {
        pattern = pattern with
        {
            EndDate = DateTime.Parse(row["RecurrenceEndDate"])
        };
    }

    if (!string.IsNullOrEmpty(row.GetValueOrDefault("RecurrenceMaxOccurrences")))
    {
        pattern = pattern with
        {
            MaxOccurrences = int.Parse(row["RecurrenceMaxOccurrences"])
        };
    }

    return pattern;
}

private HashSet<int> ParseDayList(string daysString)
{
    // Expected format: "1,2,3,4,5" for multiple days
    // Or: "1,15,-1" for 1st, 15th, and last day
    return daysString
        .Split(',')
        .Select(s => int.Parse(s.Trim()))
        .ToHashSet();
}
```

## For Execution Schedulers

### Before: Only Static Day-of-Week Matching
```csharp
public bool IsScheduledFor(IntakeEventManifest manifest, DateTime date)
{
    var dayName = date.DayOfWeek.ToString();
    var dayProperty = typeof(IntakeEventManifest)
        .GetProperty(dayName)
        ?.GetValue(manifest)
        ?.ToString() ?? "";

    return dayProperty == "X";
}
```

### After: Support Both Static and Recurring
```csharp
public IEnumerable<DateTime> GetScheduledTimes(
    IntakeEventManifest manifest,
    DateTime windowStart,
    DateTime windowEnd)
{
    if (manifest.IsRecurring)
    {
        // Use recurrence calculation service
        var service = new RecurrenceCalculationService();
        return service.CalculateOccurrences(
            manifest.RecurrencePattern!,
            manifest.IntakeTime,
            windowStart,
            windowEnd
        );
    }
    else
    {
        // Use traditional day-of-week matching
        return GetStaticScheduledTimes(manifest, windowStart, windowEnd);
    }
}

private IEnumerable<DateTime> GetStaticScheduledTimes(
    IntakeEventManifest manifest,
    DateTime windowStart,
    DateTime windowEnd)
{
    if (!TimeSpan.TryParse(manifest.IntakeTime, out var timeOfDay))
        throw new FormatException($"Invalid time: {manifest.IntakeTime}");

    var dayMap = new Dictionary<DayOfWeek, string>
    {
        { DayOfWeek.Monday, manifest.Monday },
        { DayOfWeek.Tuesday, manifest.Tuesday },
        { DayOfWeek.Wednesday, manifest.Wednesday },
        { DayOfWeek.Thursday, manifest.Thursday },
        { DayOfWeek.Friday, manifest.Friday },
        { DayOfWeek.Saturday, manifest.Saturday },
        { DayOfWeek.Sunday, manifest.Sunday }
    };

    var times = new List<DateTime>();
    var current = windowStart.Date;

    while (current <= windowEnd.Date)
    {
        if (dayMap.TryGetValue(current.DayOfWeek, out var dayValue) && dayValue == "X")
        {
            var time = current.Add(timeOfDay);
            if (time >= windowStart && time <= windowEnd)
                times.Add(time);
        }

        current = current.AddDays(1);
    }

    return times;
}
```

## For Database/Storage

### Schema Migration (if using traditional day-of-week table)

No schema changes required for backward compatibility. To support recurrence:

```sql
-- Option 1: JSON column (recommended for flexibility)
ALTER TABLE IntakeEventManifests 
ADD RecurrencePatternJson NVARCHAR(MAX) NULL;

-- Option 2: Normalized table (for strong typing)
CREATE TABLE RecurrencePatterns
(
    Id BIGINT PRIMARY KEY IDENTITY(1,1),
    ManifestId BIGINT NOT NULL,
    Frequency INT NOT NULL,
    Interval INT NOT NULL,
    MaxOccurrences INT NULL,
    EndDate DATETIME2 NULL,
    WeeklyDays NVARCHAR(50) NULL,
    MonthlyDay INT NULL,
    FOREIGN KEY (ManifestId) REFERENCES IntakeEventManifests(Id)
);
```

### Persistence Example (with JSON)
```csharp
public async Task SaveManifestAsync(IntakeEventManifest manifest)
{
    var json = manifest.RecurrencePattern != null
        ? JsonSerializer.Serialize(manifest.RecurrencePattern)
        : null;

    // Save to database
    using var connection = new SqlConnection(_connectionString);
    const string query = @"
        INSERT INTO IntakeEventManifests 
            (TaskId, Monday, Tuesday, ..., IntakeTime, RecurrencePatternJson)
        VALUES 
            (@taskId, @monday, ..., @intakeTime, @recurrencePatternJson)";

    var command = new SqlCommand(query, connection);
    command.Parameters.AddWithValue("@taskId", manifest.TaskId);
    // ... add other parameters ...
    command.Parameters.AddWithValue("@recurrencePatternJson", json ?? DBNull.Value);

    await connection.OpenAsync();
    await command.ExecuteNonQueryAsync();
}

public async Task<IntakeEventManifest> LoadManifestAsync(string taskId)
{
    // ... load base fields ...

    var manifest = new IntakeEventManifest { /* ... */ };

    if (!string.IsNullOrEmpty(recurrenceJson))
    {
        manifest = manifest with
        {
            RecurrencePattern = JsonSerializer.Deserialize<RecurrencePattern>(recurrenceJson)
        };
    }

    return manifest;
}
```

## For Validation

### Add to Your Validation Pipeline
```csharp
public ValidationResult ValidateManifest(IntakeEventManifest manifest)
{
    var errors = new List<string>();

    if (!manifest.IsValid())
    {
        errors.Add("Manifest validation failed");

        if (string.IsNullOrWhiteSpace(manifest.TaskId))
            errors.Add("TaskId is required");

        if (string.IsNullOrWhiteSpace(manifest.IntakeTime))
            errors.Add("IntakeTime is required");

        if (manifest.IsRecurring && manifest.RecurrencePattern != null)
        {
            if (!manifest.RecurrencePattern.IsValid())
                errors.Add("RecurrencePattern is invalid");

            if (manifest.RecurrencePattern.Frequency == RecurrenceFrequency.Minutely &&
                manifest.RecurrencePattern.Interval < 1)
                errors.Add("Minutely interval must be at least 1");
        }
        else if (!manifest.IsRecurring)
        {
            var anyDaySet = !string.IsNullOrEmpty(manifest.Monday) ||
                           !string.IsNullOrEmpty(manifest.Tuesday) ||
                           // ... check all days ...
                           !string.IsNullOrEmpty(manifest.Sunday);

            if (!anyDaySet)
                errors.Add("At least one day must be specified for non-recurring schedules");
        }
    }

    return new ValidationResult(errors.Count == 0, errors);
}
```

## Testing Migration Path

### Add Tests for New Functionality
```csharp
[Fact]
public void CalculateOccurrences_HourlyPattern_ReturnsCorrectCount()
{
    var pattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Hourly,
        Interval = 2
    };

    var service = new RecurrenceCalculationService();
    var occurrences = service.CalculateOccurrences(
        pattern,
        "09:00:00",
        new DateTime(2025, 1, 1),
        new DateTime(2025, 1, 1, 23, 59, 59)
    ).ToList();

    Assert.Equal(12, occurrences.Count);  // 9AM to 9PM in 2-hour intervals
}

[Fact]
public void IsValid_StaticSchedule_RequiresAtLeastOneDay()
{
    var manifest = new IntakeEventManifest
    {
        TaskId = "task",
        IntakeTime = "09:00:00"
        // All day properties empty
    };

    Assert.False(manifest.IsValid());
}

[Fact]
public void IsValid_RecurringPattern_ValidatesPattern()
{
    var manifest = new IntakeEventManifest
    {
        TaskId = "task",
        IntakeTime = "09:00:00",
        RecurrencePattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 0  // Invalid
        }
    };

    Assert.False(manifest.IsValid());
}
