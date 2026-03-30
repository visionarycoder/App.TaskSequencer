# recurrence pattern architecture

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                      CSV FILE                               │
├─────────────────────────────────────────────────────────────┤
│ TaskId  | IntakeTime | RecurrenceFrequency | MonthlyDays   │
│ --------|------------|---------------------|----------------│
│ task-01 | 09:00:00   | Monthly             | "1,15"        │
│ task-02 | 10:00:00   | Weekly              | "1,2,3,4,5"   │
│ task-03 | 08:00:00   | Hourly              | (empty)       │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────┐
        │   CSV Deserializer              │
        │   (CsvHelper)                   │
        └─────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────┐
        │ IntakeEventManifest             │
        │ + RecurrencePattern             │
        │ {                               │
        │   TaskId: "task-01"             │
        │   IntakeTime: "09:00:00"        │
        │   RecurrencePattern {           │
        │     Frequency: Monthly          │
        │     Interval: 1                 │
        │     MonthlyDays: {1, 15}        │
        │   }                             │
        │ }                               │
        └─────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────┐
        │ Validation                      │
        │ manifest.IsValid()              │
        │ pattern.IsValid()               │
        └─────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────────────────────┐
        │ RecurrenceCalculationService                    │
        │ .CalculateOccurrences(pattern, time, range)    │
        └─────────────────────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────────────────────┐
        │ List<DateTime> - Calculated Occurrences        │
        │                                                 │
        │ 2025-01-01 09:00:00 (1st)                      │
        │ 2025-01-15 09:00:00 (15th)                     │
        │ 2025-02-01 09:00:00 (1st)                      │
        │ 2025-02-15 09:00:00 (15th)                     │
        │ 2025-03-01 09:00:00 (1st)                      │
        │ 2025-03-15 09:00:00 (15th)                     │
        │ ... and so on ...                              │
        └─────────────────────────────────────────────────┘
                           │
                           ▼
        ┌─────────────────────────────────────────────────┐
        │ Execution Scheduler                             │
        │ (Use occurrences for task scheduling)          │
        └─────────────────────────────────────────────────┘
```

## Class Structure

```
┌──────────────────────────────────────────┐
│      RecurrenceFrequency (Enum)          │
├──────────────────────────────────────────┤
│ None                                     │
│ Minutely                                 │
│ Hourly                                   │
│ Daily                                    │
│ Weekly                                   │
│ Monthly                                  │
│ Yearly                                   │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│      RecurrencePattern (Record)          │
├──────────────────────────────────────────┤
│ Frequency: RecurrenceFrequency           │
│ Interval: int                            │
│ MaxOccurrences: int?                     │
│ EndDate: DateTime?                       │
│ WeeklyDays: IReadOnlySet<int>            │
│ MonthlyDay: int? [LEGACY]                │
│ MonthlyDays: IReadOnlySet<int> [NEW]     │
├──────────────────────────────────────────┤
│ + IsValid(): bool                        │
│ + GetAllMonthlyDays(): IReadOnlySet<int> │
└──────────────────────────────────────────┘
         ▲                    ▲
         │                    │
         └────────┬───────────┘
                  │
┌──────────────────────────────────────────────────┐
│   IntakeEventManifest (Record)                   │
├──────────────────────────────────────────────────┤
│ TaskId: string                                   │
│ IntakeTime: string                               │
│ [Legacy Day-of-Week Props]                       │
│ Monday, Tuesday, Wednesday, ... Sunday: string   │
│ RecurrencePattern: RecurrencePattern?            │
├──────────────────────────────────────────────────┤
│ + IsRecurring: bool (property)                   │
│ + IsValid(): bool                                │
└──────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────┐
│   RecurrenceCalculationService (Class)           │
├──────────────────────────────────────────────────┤
│ - Private calculation methods per frequency      │
├──────────────────────────────────────────────────┤
│ + CalculateOccurrences(                          │
│     pattern: RecurrencePattern,                  │
│     intakeTime: string,                          │
│     startDateTime: DateTime,                     │
│     endDateTime: DateTime                        │
│   ): IEnumerable<DateTime>                       │
└──────────────────────────────────────────────────┘
```

## State Transitions

```
CSV Row Input
     │
     ▼
┌─────────────────────────┐
│ Parse RecurrenceFrequency│─────► None ──┐
└─────────────────────────┘              │
     │                                  │
     ▼                                  ▼
 Minutely, Hourly, Daily, Weekly, Monthly, Yearly
     │      │      │      │      │        │
     │      │      │      │      │        │
     ├──────┴──────┴──────┴──────┴────────┘
     │
     ▼
┌──────────────────────────────────────────┐
│ Check Frequency-Specific Properties      │
│                                          │
│ Weekly? → Need WeeklyDays                │
│ Monthly? → Need MonthlyDays or MonthlyDay│
│ Other? → Only need Interval              │
└──────────────────────────────────────────┘
     │
     ▼
┌──────────────────────────────────────────┐
│ Validate Pattern                         │
│ pattern.IsValid()                        │
└──────────────────────────────────────────┘
     │
     ├─► Invalid ──► Exception
     │
     ▼
   Valid
     │
     ▼
┌──────────────────────────────────────────┐
│ Calculate Occurrences                    │
│ Select calculation method by frequency   │
└──────────────────────────────────────────┘
     │
     ▼
List<DateTime> Results
```

## Example: Monthly "1st and 15th"

```
Input Manifest:
┌────────────────────────────────────┐
│ TaskId: "report"                   │
│ IntakeTime: "09:00:00"             │
│ RecurrencePattern:                 │
│   Frequency: Monthly               │
│   Interval: 1                      │
│   MonthlyDays: {1, 15}             │
└────────────────────────────────────┘

Calculation Process (for Jan 2025):
┌─────────────────────────────────────────┐
│ For Month = January 2025                │
│   For Day in {1, 15}:                   │
│     Create DateTime:                    │
│       2025-01-01 09:00:00 ✓             │
│       2025-01-15 09:00:00 ✓             │
│                                         │
│ For Month = February 2025               │
│   For Day in {1, 15}:                   │
│     Create DateTime:                    │
│       2025-02-01 09:00:00 ✓             │
│       2025-02-15 09:00:00 ✓             │
│                                         │
│ ... Continue for all months ...         │
└─────────────────────────────────────────┘

Output:
┌─────────────────────────────────────────┐
│ 2025-01-01 09:00:00                     │
│ 2025-01-15 09:00:00                     │
│ 2025-02-01 09:00:00                     │
│ 2025-02-15 09:00:00                     │
│ 2025-03-01 09:00:00                     │
│ 2025-03-15 09:00:00                     │
│ 2025-04-01 09:00:00                     │
│ 2025-04-15 09:00:00                     │
│ ...                                     │
└─────────────────────────────────────────┘
```

## Multiple Days Comparison

### Before Enhancement
```
3 CSV Rows Required:
TaskId      | IntakeTime | MonthlyDay
task-001    | 09:00:00   | 1
task-001    | 09:00:00   | 15
task-001    | 09:00:00   | -1 (last day)

Result: 3 separate manifests, 3 calculations
```

### After Enhancement
```
1 CSV Row Required:
TaskId      | IntakeTime | MonthlyDays
task-001    | 09:00:00   | "1,15,-1"

Result: 1 manifest, generates 3 occurrences per month
```

## Validation Flow

```
manifest.IsValid()
    │
    ├─► Check TaskId (required)
    ├─► Check IntakeTime (required)
    │
    ├─► If IsRecurring:
    │   │
    │   ├─► pattern.IsValid()
    │   │   │
    │   │   ├─► Interval >= 1? ✓/✗
    │   │   │
    │   │   ├─► If Monthly:
    │   │   │   │
    │   │   │   ├─► GetAllMonthlyDays().Count > 0? ✓/✗
    │   │   │   │
    │   │   │   ├─► All days in range [-1, 1-31]? ✓/✗
    │   │   │   │
    │   │   │   └─► No invalid values (0, 32+)?  ✓/✗
    │   │   │
    │   │   ├─► If Weekly:
    │   │   │   │
    │   │   │   └─► WeeklyDays.Count > 0? ✓/✗
    │   │   │       Days in range [1-7]? ✓/✗
    │   │   │
    │   │   ├─► MaxOccurrences >= 1? ✓/✗ (if set)
    │   │   │
    │   │   └─► EndDate is valid? ✓/✗ (if set)
    │   │
    │   └─► Return true/false
    │
    ├─► Else (static schedule):
    │   │
    │   └─► At least one day specified? ✓/✗
    │
    └─► Return true/false
```
