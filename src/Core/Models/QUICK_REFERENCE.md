# quick reference - multiple monthly days support

## TL;DR

**Problem**: Events running on 1st AND 15th required 2 CSV rows
**Solution**: Now supported in ONE row with `MonthlyDays: "1,15"`

## Quick Examples

### CSV: 1st and 15th
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceMonthlyDays
task-001,09:00:00,Monthly,"1,15"
```

### Code: 1st and 15th
```csharp
new RecurrencePattern
{
    Frequency = RecurrenceFrequency.Monthly,
    MonthlyDays = new HashSet<int> { 1, 15 }
}
```

### Code: 1st, 15th, and Last Day
```csharp
new RecurrencePattern
{
    Frequency = RecurrenceFrequency.Monthly,
    MonthlyDays = new HashSet<int> { 1, 15, -1 }
}
```

## Changes Made

| File | Change |
|------|--------|
| `RecurrencePattern.cs` | Added `MonthlyDays` property |
| `RecurrenceCalculationService.cs` | Updated to handle multiple days |
| Documentation | Added 5 new guides |

## Properties

### RecurrencePattern
```csharp
// NEW: Multiple days per month
public IReadOnlySet<int> MonthlyDays { get; init; }

// LEGACY: Still works, backward compatible
public int? MonthlyDay { get; init; }

// Helper: Merges both
public IReadOnlySet<int> GetAllMonthlyDays()
```

## Day Numbers

```
Monthly:  1-31 (day of month), -1 (last day)
Weekly:   1 (Mon) - 7 (Sun)
```

## CSV Column

**Before**: One value only
```csv
RecurrenceMonthlyDay: 1
RecurrenceMonthlyDay: 15
```

**After**: Multiple values, one row
```csv
RecurrenceMonthlyDays: "1,15"
RecurrenceMonthlyDays: "1,15,-1"
RecurrenceMonthlyDays: "-1"
```

## Validation

```csharp
if (manifest.IsValid())
{
    var service = new RecurrenceCalculationService();
    var occurrences = service.CalculateOccurrences(
        manifest.RecurrencePattern!,
        manifest.IntakeTime,
        startDate,
        endDate
    );
}
```

## Result Example

For `MonthlyDays: {1, 15}`, generates:
```
2025-01-01 09:00:00
2025-01-15 09:00:00
2025-02-01 09:00:00
2025-02-15 09:00:00
2025-03-01 09:00:00
2025-03-15 09:00:00
...
```

## Backward Compatibility

✅ All existing code works unchanged
✅ Single-day monthly patterns still work
✅ Static day-of-week schedules unaffected

## Files to Reference

- **Model**: `src/ConsoleApp/Ifx/Models/RecurrencePattern.cs`
- **Service**: `src/ConsoleApp/Services/RecurrenceCalculationService.cs`
- **CSV Guide**: `src/ConsoleApp/Ifx/Models/csv_format_guide.md`
- **Architecture**: `src/ConsoleApp/Ifx/Models/recurrence_architecture_diagrams.md`
- **Migration**: `src/ConsoleApp/Ifx/Models/recurrence_migration_guide.md`
