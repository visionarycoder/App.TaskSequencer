# csv format guide for recurrence manifests

## One Manifest Per CSV Row

The enhanced model now supports **one manifest per CSV row** that can generate multiple monthly occurrences. This simplifies data entry and eliminates the need for duplicate rows.

## CSV Structure

### Basic Columns (Always Required)
```
TaskId              | string | Unique task identifier (e.g., "task-001")
IntakeTime          | string | Time of day in HH:mm:ss format (e.g., "09:00:00")
```

### Recurrence Columns (Optional)
```
RecurrenceFrequency | string | None|Minutely|Hourly|Daily|Weekly|Monthly|Yearly
RecurrenceInterval  | int    | How many periods between occurrences (default: 1)
```

### Frequency-Specific Columns

#### For Weekly Patterns
```
RecurrenceWeeklyDays | string | Comma-separated day numbers (1=Mon, 2=Tue, ..., 7=Sun)
                               | Example: "1,2,3,4,5" for weekdays
```

#### For Monthly Patterns (NEW - Multiple Days!)
```
RecurrenceMonthlyDays | string | Comma-separated day numbers (1-31, or -1 for last day)
                                | Example: "1,15" for 1st and 15th
                                | Example: "1,15,-1" for 1st, 15th, and last day
```

#### Optional Columns (All Frequencies)
```
RecurrenceMaxOccurrences | int       | Stop after this many occurrences (null = unlimited)
RecurrenceEndDate        | datetime  | Stop on this date (format: YYYY-MM-DD)
```

### Legacy Columns (Backward Compatible - Not Recommended)
```
Monday   | string | Leave blank or use with other day-of-week columns
Tuesday  | string |
Wednesday| string |
Thursday | string |
Friday   | string |
Saturday | string |
Sunday   | string |
        Value "X" means the task runs on that day
```

## Example CSV Formats

### Example 1: Events Every 15 Minutes (All Day)
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval
health-check,00:00:00,Minutely,15
```

**Result**: Generates occurrences at 00:00, 00:15, 00:30, ... 23:45 every single day until end date or max occurrences reached.

---

### Example 2: Events Every 2 Hours (Business Hours)
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMaxOccurrences
data-sync,08:00:00,Hourly,2,12
```

**Result**: Generates 12 occurrences starting at 08:00 (08:00, 10:00, 12:00, 14:00, 16:00, 18:00, 20:00, 22:00, 00:00, 02:00, 04:00, 06:00)

---

### Example 3: Weekday Events (Monday-Friday)
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceWeeklyDays
daily-report,10:00:00,Weekly,1,1;2;3;4;5
```

Or with comma separator:
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceWeeklyDays
daily-report,10:00:00,Weekly,1,"1,2,3,4,5"
```

**Result**: Generates daily occurrences at 10:00 on Monday through Friday.

---

### Example 4: 1st and 15th of Every Month (NEW!)
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays
bi-monthly-review,09:00:00,Monthly,1,"1,15"
```

**Result**: Single row generates TWO occurrences per month (1st and 15th at 09:00).

---

### Example 5: 1st, 15th, and Last Day of Month
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays,RecurrenceEndDate
statement-processing,10:00:00,Monthly,1,"1,15,-1",2025-12-31
```

**Result**: Single row generates THREE occurrences per month through end of 2025.

---

### Example 6: Last Day of Month Only
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays
month-end-audit,17:00:00,Monthly,1,-1
```

**Result**: Generates one occurrence on the last day of each month.

---

### Example 7: Every Other Month on the 10th
```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays
quarterly-report,14:00:00,Monthly,2,10
```

**Result**: Generates occurrences on the 10th of every other month (Jan 10, Mar 10, May 10, etc.)

---

### Example 8: Legacy Day-of-Week Format (Backward Compatible)
```csv
TaskId,IntakeTime,Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday
legacy-task,15:00:00,X,X,X,X,X,,
```

**Result**: Works as before - runs on Mon, Tue, Wed, Thu, Fri at 15:00 (no RecurrencePattern needed).

---

## Complete Example with Multiple Rows

```csv
TaskId,IntakeTime,RecurrenceFrequency,RecurrenceInterval,RecurrenceMonthlyDays,RecurrenceWeeklyDays,RecurrenceMaxOccurrences,RecurrenceEndDate
health-check,00:00:00,Minutely,15,,,,
data-sync,08:00:00,Hourly,2,,,12,
daily-report,10:00:00,Weekly,1,,"1,2,3,4,5",,2025-12-31
bi-monthly,09:00:00,Monthly,1,"1,15",,,
month-end,17:00:00,Monthly,1,-1,,,
```

**Results**:
- Row 1: Every 15 minutes, every day
- Row 2: Every 2 hours, max 12 times total
- Row 3: Every weekday (Mon-Fri) at 10:00, until Dec 31, 2025
- Row 4: On 1st and 15th of every month
- Row 5: On last day of every month

---

## Column Separator Options

The parsing code supports both comma and semicolon separators for day lists:

```csv
RecurrenceWeeklyDays: "1,2,3,4,5"    (comma separator)
RecurrenceWeeklyDays: "1;2;3;4;5"    (semicolon separator)
RecurrenceMonthlyDays: "1,15,-1"     (comma separator)
RecurrenceMonthlyDays: "1;15;-1"     (semicolon separator)
```

Both formats work fine - choose based on your CSV tool's abilities.

---

## Day Number Reference

### Weekly Days
```
1 = Monday
2 = Tuesday
3 = Wednesday
4 = Thursday
5 = Friday
6 = Saturday
7 = Sunday
```

### Monthly Days
```
1-31  = Specific day of month
-1    = Last day of month (handles Feb 28/29 automatically)
```

---

## Important Notes

1. **One Manifest Per Row**: No need to duplicate rows for multiple monthly days anymore
2. **No RecurrenceFrequency = Static Schedule**: If `RecurrenceFrequency` is blank or "None", the manifest uses traditional day-of-week columns
3. **Validation**: Invalid patterns are detected before calculation (e.g., day 32, invalid frequency, etc.)
4. **Timezone**: All times are in local timezone - no UTC conversion applied
5. **Leap Years**: Month-end (-1) handles February 28/29 automatically
6. **Empty Cells**: Leave blank for unused columns - parsing handles null/empty gracefully

---

## Performance Considerations

- **In-Memory Calculation**: All occurrences are calculated on-demand, not pre-generated
- **Large Date Ranges**: Huge time windows or very small intervals (e.g., every 1 minute for 10 years) may generate large result sets
- **Pagination**: If results are large, consider filtering by date window before full calculation

---

## Validation Rules

```
RecurrenceFrequency = "Minutely"
  ✓ RecurrenceInterval >= 1
  ✓ No other frequency-specific fields required

RecurrenceFrequency = "Weekly"
  ✓ RecurrenceWeeklyDays must be provided
  ✓ Days must be 1-7
  ✓ At least one day required

RecurrenceFrequency = "Monthly"
  ✓ RecurrenceMonthlyDays must be provided (or RecurrenceMonthlyDay for legacy)
  ✓ Days must be 1-31 or -1
  ✓ At least one day required

All Frequencies (Optional)
  ✓ RecurrenceMaxOccurrences >= 1 (if provided)
  ✓ RecurrenceEndDate >= today (if provided)
  ✓ RecurrenceInterval >= 1
```
