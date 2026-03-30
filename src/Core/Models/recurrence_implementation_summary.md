# intake event recurrence feature - implementation summary

## Overview

The `IntakeEventManifest` model has been extended to support recurring events in addition to the existing static day-of-week scheduling. This enables support for:

- **Hourly occurrences** (every N hours)
- **Minute-based intervals** (every 15 minutes, etc.)
- **Daily patterns** (every N days)
- **Weekly patterns** (every weekday, every N weeks)
- **Monthly patterns** (specific day or last day of month)
- **Yearly patterns** (every N years)

## New Components

### 1. **RecurrencePattern.cs**
Defines the recurrence frequency and occurrence calculation parameters.

**Key Types:**
- `RecurrenceFrequency` enum: None, Minutely, Hourly, Daily, Weekly, Monthly, Yearly
- `RecurrencePattern` record: Specifies frequency, interval, max occurrences, end date, and day constraints

**Key Properties:**
- `MonthlyDays`: Collection of days for monthly recurrence (e.g., 1st and 15th in one manifest)
- `MonthlyDay`: Single day (DEPRECATED - use MonthlyDays for better support)
- `GetAllMonthlyDays()`: Helper method that combines both properties for backward compatibility

**Example - Multiple Days Per Month (NEW):**
```csharp
var pattern = new RecurrencePattern
{
    Frequency = RecurrenceFrequency.Monthly,
    MonthlyDays = new HashSet<int> { 1, 15 }  // 1st AND 15th - ONE manifest!
};
```

**Example - Single Day (Backward Compatible):**
```csharp
var pattern = new RecurrencePattern
{
    Frequency = RecurrenceFrequency.Monthly,
    MonthlyDay = 15  // Just 15th
};
```

### 2. **RecurrenceCalculationService.cs**
Calculates all occurrence times within a time window based on the recurrence pattern.

**Key Methods:**
- `CalculateOccurrences()`: Returns all calculated occurrences for a given time range

**Example:**
```csharp
var service = new RecurrenceCalculationService();
var occurrences = service.CalculateOccurrences(
    pattern,
    intakeTime: "09:00:00",
    startDateTime: new DateTime(2025, 1, 1),
    endDateTime: new DateTime(2025, 1, 31)
);
```

### 3. **Enhanced IntakeEventManifest**
Updated to support recurrence patterns while maintaining backward compatibility.

**New Properties:**
- `RecurrencePattern`: Optional recurrence pattern for repeated events
- `IsRecurring`: Read-only property indicating if event is recurring
- `IsValid()`: Validates both static and recurring schedules

**Backward Compatibility:**
Existing manifests without `RecurrencePattern` continue to use day-of-week scheduling (Monday, Tuesday, etc.)

## Design Principles

### 1. **Backward Compatible**
- Existing day-of-week schedules work unchanged
- `RecurrencePattern` is optional (nullable)
- If no recurrence pattern, manifest uses legacy behavior

### 2. **Clear Distinction**
- Static schedules use day-of-week properties
- Recurring schedules use `RecurrencePattern`
- `IsRecurring` property clarifies which mode is active

### 3. **Type-Safe Validation**
- `RecurrencePattern.IsValid()` ensures consistency
- `IntakeEventManifest.IsValid()` validates the complete schedule
- Invalid patterns are detected before calculation

### 4. **Flexible Occurrences**
- Limit by count: `MaxOccurrences = 100`
- Limit by date: `EndDate = new DateTime(2025, 12, 31)`
- Weekly patterns support multiple days
- Monthly patterns support **multiple days in a single manifest** (e.g., 1st, 15th, and last day)

## CSV Integration (One Manifest Per Row!)

**CSV Column Examples:**

For monthly events on 1st and 15th:

```
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays
task-001,09:00:00,Monthly,1,"1,15"
```

For events on 1st, 15th, and last day:

```
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays
task-002,10:00:00,Monthly,1,"1,15,-1"
```

**Result**: One CSV row generates one manifest that produces multiple monthly occurrences automatically!

## Usage Scenarios

### Scenario 1: Every 15 Minutes
```csharp
new IntakeEventManifest
{
    TaskId = "health-check",
    IntakeTime = "00:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Minutely,
        Interval = 15
    }
}
```

### Scenario 2: Business Hours (Every Weekday at 10 AM)
```csharp
new IntakeEventManifest
{
    TaskId = "daily-report",
    IntakeTime = "10:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Weekly,
        Interval = 1,
        WeeklyDays = new HashSet<int> { 1, 2, 3, 4, 5 }  // Mon-Fri
    }
}
```

### Scenario 3: End of Month
```csharp
new IntakeEventManifest
{
    TaskId = "monthly-audit",
    IntakeTime = "17:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDay = -1  // -1 means last day of month
    }
}
```

### Scenario 4: 1st and 15th of Every Month (NEW - ONE MANIFEST!)
```csharp
new IntakeEventManifest
{
    TaskId = "bi-monthly-review",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15 }
        // Single manifest generates 2 occurrences per month!
    }
}
```

### Scenario 5: Multiple Days Including Last Day
```csharp
new IntakeEventManifest
{
    TaskId = "statement-processing",
    IntakeTime = "10:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15, -1 }
        // Single manifest generates 3 occurrences per month!
    }
}
```

### Scenario 6: Legacy Static Schedule (Backward Compatible)
```csharp
new IntakeEventManifest
{
    TaskId = "legacy-task",
    Monday = "X",
    Wednesday = "X",
    Friday = "X",
    IntakeTime = "15:00:00"
    // No RecurrencePattern - uses traditional day-of-week schedule
}
```

## File Structure

```
src/ConsoleApp/
├── Ifx/Models/
│   ├── IntakeEventManifest.cs          // Enhanced with recurrence support
│   ├── RecurrencePattern.cs            // NEW: Recurrence pattern definition
│   └── recurrence_usage.md             // Documentation with examples
└── Services/
    └── RecurrenceCalculationService.cs // NEW: Occurrence calculation service
```

## Integration Points

### For Manifest Loading
When loading manifests from CSV, set `RecurrencePattern` if the source provides:
- Frequency type
- Interval value
- Max occurrences or end date
- Day specifications (for weekly/monthly)

### For Execution Scheduling
Use `RecurrenceCalculationService` to expand:
- Single recurring manifest into multiple execution instances
- Or pass directly to scheduler that supports patterns

### For Validation
Call `manifest.IsValid()` before:
- Persisting to database
- Scheduling execution
- Converting to execution instances

## Testing Considerations

### Unit Tests Should Cover
- ✓ Each recurrence frequency type
- ✓ Interval calculations (every 15 mins, every 2 hours, etc.)
- ✓ Max occurrences limit
- ✓ End date boundary handling
- ✓ Weekly day selections (all combinations)
- ✓ Monthly day variations (1-31, -1 for last day)
- ✓ Backward compatibility with static schedules
- ✓ Validation of invalid patterns

### Integration Tests Should Cover
- ✓ Manifest loading with recurrence patterns
- ✓ Occurrence generation within time windows
- ✓ Boundary cases (leap years, month-end, DST transitions)

## Performance Notes

- `RecurrenceCalculationService` generates occurrences on-demand
- No pre-generation of all occurrences (memory efficient)
- Suitable for patterns extending years into future
- Consider pagination for very large result sets

## Future Enhancements

Potential future additions:
- RRULE (RFC 5545) format support for exchange compatibility
- Timezone-aware calculations
- Business day/holiday exclusions
- Composite patterns (e.g., "every weekday except holidays")
- Cron expression support
