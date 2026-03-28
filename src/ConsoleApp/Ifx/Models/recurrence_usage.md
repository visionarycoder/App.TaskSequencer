# intake event recurrence support

## Overview

The `IntakeEventManifest` model now supports both:

1. **Static Schedules**: Traditional day-of-week based schedules (Monday, Tuesday, etc.)
2. **Dynamic Recurrence**: Repeated events at specific intervals (hourly, every 15 minutes, etc.)

## Recurrence Frequency Types

- **Minutely**: Every N minutes (e.g., every 15 minutes)
- **Hourly**: Every N hours (e.g., every 2 hours)
- **Daily**: Every N days
- **Weekly**: Every N weeks on specified days
- **Monthly**: Every N months on a specific day
- **Yearly**: Every N years

## Usage Examples

### Example 1: Every 15 Minutes

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-001",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Minutely,
        Interval = 15,
        MaxOccurrences = 100  // Stop after 100 occurrences
    }
};
```

### Example 2: Every 2 Hours

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-002",
    IntakeTime = "08:30:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Hourly,
        Interval = 2,
        EndDate = new DateTime(2025, 12, 31)  // Stop on this date
    }
};
```

### Example 3: Every Weekday (Monday-Friday)

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-003",
    IntakeTime = "11:30:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Weekly,
        Interval = 1,
        WeeklyDays = new HashSet<int> { 1, 2, 3, 4, 5 }  // Mon-Fri
    }
};
```

### Example 4: Every Last Day of Month

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-004",
    IntakeTime = "17:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDay = -1  // -1 means last day of month
    }
};
```

### Example 5: 1st AND 15th of Every Month (Multiple Days in One Manifest!)

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-005",
    IntakeTime = "09:00:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15 }
        // Single manifest, one CSV row - generates occurrences for BOTH 1st and 15th
    }
};
```

### Example 6: 1st, 15th, and Last Day of Month

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-006",
    IntakeTime = "10:30:00",
    RecurrencePattern = new RecurrencePattern
    {
        Frequency = RecurrenceFrequency.Monthly,
        MonthlyDays = new HashSet<int> { 1, 15, -1 }
        // Single manifest generates 3 occurrences per month
    }
};
```

### Example 7: Static Weekly Schedule (Backward Compatible)

```csharp
var manifest = new IntakeEventManifest
{
    TaskId = "task-007",
    Monday = "X",
    Wednesday = "X",
    Friday = "X",
    IntakeTime = "15:00:00"
    // RecurrencePattern is null, so uses day-of-week schedule
};
```

## Calculating Occurrence Times

Use `RecurrenceCalculationService` to generate all occurrences within a time window:

```csharp
var service = new RecurrenceCalculationService();

var pattern = new RecurrencePattern
{
    Frequency = RecurrenceFrequency.Hourly,
    Interval = 3
};

var occurrences = service.CalculateOccurrences(
    pattern,
    intakeTime: "09:00:00",
    startDateTime: new DateTime(2025, 1, 1),
    endDateTime: new DateTime(2025, 1, 2)
);

foreach (var occurrence in occurrences)
{
    Console.WriteLine($"Scheduled: {occurrence:yyyy-MM-dd HH:mm:ss}");
}
```

## Backward Compatibility

Existing manifests without a `RecurrencePattern` continue to work as before:

```csharp
var isRecurring = manifest.IsRecurring;  // false if no pattern

if (manifest.IsRecurring)
{
    // Use recurrence pattern
}
else
{
    // Use day-of-week schedule (Monday, Tuesday, etc.)
}
```

## Validation

Both static and recurring schedules are validated:

```csharp
if (manifest.IsValid())
{
    // Safe to use
}
else
{
    // Fix validation errors
}
```

## Pattern Validation Rules

- `Interval` must be >= 1
- `MaxOccurrences` must be >= 1 (if set)
- Weekly patterns must specify at least one day
- Monthly patterns with day specification: 1-31 or -1 (last day)
- Static schedules must specify at least one day if not recurring
