# Execution Sequencing Pipeline (Planning Algorithm)

## Overview

This document describes the transformation pipeline from multiple CSV sources to a consolidated `ExecutionPlan` containing feasible (and infeasible) execution sequences. The planning algorithm analyzes all dependencies and timing constraints simultaneously to resolve scheduling conflicts and flag deadline violations.

**NOTE: This is a PLANNING TOOL ONLY.** It does not use Orleans agents for real-time execution. Instead, it performs batch analysis of all sequences to produce:
- A matrix of execution instances (tasks × scheduled contexts)
- Resolved start/end times for each valid sequence
- Failure markers for sequences that cannot meet deadlines
- A unified calendar showing all scheduled tasks

### Business Context

App.TaskSequencer orchestrates task execution within an interconnected financial ecosystem. Tasks function as **data movers** across system boundaries:

- **Triggering Events**: Activities like payroll cascade through dependent tasks
- **Distributed Dependencies**: Tasks depend on work completed on remote systems
- **Critical Path**: Some tasks are dependencies for downstream systems
- **Cross-Boundary Data Flow**: Data transits repeatedly across system boundaries until all transactions are 100% complete
- **Deadline Constraints**: Each task must complete **before** its intake event occurs

The system calculates feasible execution schedules that respect both predecessor dependencies and intake event deadlines, producing a single consolidated output plan.

---

## Input Files

### File 1: Task Definition CSV Extended Format

The existing Task Definition CSV is extended with two new columns to define the execution schedule.

### Example CSV Structure

```csv
Interface Number,Interface Name,Execution Type,Schedule Type,Duration Minutes,Suggested Start Time,Precursor Interface Numbers,Execution Days,Execution Times
1,Extract Data,Scheduled,Recurring,120,06:00:00,"",Monday|Wednesday|Friday,06:00:00,14:00:00
2,Validate Data,Scheduled,Recurring,60,07:15:00,"1",Monday|Wednesday|Friday,08:00:00
3,Generate Report,Scheduled,Recurring,90,08:30:00,"2",Monday|Wednesday|Friday,09:00:00
4,Archive Results,Scheduled,Recurring,45,10:00:00,"1,3",Tuesday|Thursday,17:00:00
5,Cleanup,OnDemand,OneOff,30,"",,,"
```

### Column Definitions

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Unique task identifier |
| `Interface Name` | string | Human-readable task name |
| `Execution Type` | enum | `OnDemand` or `Scheduled` |
| `Schedule Type` | enum | `OneOff` or `Recurring` |
| `Duration Minutes` | uint | Expected execution duration |
| `Suggested Start Time` | time | Initial timing suggestion |
| `Precursor Interface Numbers` | csv-list | Comma-separated task IDs that must complete first (empty = no dependencies) |
| **`Execution Days`** | pipe-list | **NEW:** Pipe-separated day names (e.g., `Monday\|Wednesday\|Friday`). Empty for OnDemand tasks. |
| **`Execution Times`** | pipe-list | **NEW:** Pipe-separated execution times (e.g., `06:00:00\|14:00:00`). Can be single or recurring times. Empty for OnDemand. |

### Scheduling Semantics

- **Scheduled + Recurring + Days + Times**: Creates matrix of execution instances
  - Example: Task 1 runs on Mon, Wed, Fri at 06:00 and 14:00 = 6 execution instances per week
  
- **Scheduled + OneOff + Times**: Single instance at specified time

- **OnDemand**: No schedule; times are empty/ignored

---

### File 2: Availability Window / Intake Event Requirements CSV

Defines completion deadlines for each task. Rows indicate **which days** the task's output is needed by a downstream system or intake process.

#### Example CSV Structure

```csv
Interface Number,Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday,Intake Time
1,X,X,X,X,X,,,11:30:00
2,X,X,X,X,X,,,12:00:00
3,X,X,X,X,X,,,12:15:00
4,,,X,,X,,,18:00:00
5,,,,,,X,X,23:59:59
```

#### Column Definitions

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Task identifier (links to Task Definition CSV) |
| `Monday` through `Sunday` | enum | `X` marks days when this task must be completed by the intake time; empty otherwise |
| **`Intake Time`** | time | The deadline time on those days by which task execution must be complete (e.g., 11:30:00) |

#### Intake Event Semantics

- **`X` in a column**: Task must complete before intake event occurs on that day
  - Example: Task 1 marked `X` Mon-Fri means instances on those days must finish ≤ 11:30:00
  
- **Empty column**: No completion requirement for that day
  - Example: Task 4 has no `X` on Mon/Tue/Wed/Thu/Sat/Sun, only requires completion by 18:00 on Wed and Fri

- **Multiple instances same day**: If a task runs twice on the same day (e.g., 06:00 and 14:00), both must complete before the intake time
  - Task 1 at 14:00 on Monday is feasible only if it starts early enough to finish by 11:30:00 (likely infeasible; would be marked invalid)

---

### File 3: Execution Duration Manifest (Optional, Imported Periodically)

As tasks execute and complete in the real world, captured actual execution times are imported to refine scheduling. This file allows overriding default or estimated durations with observed data.

#### Example CSV Structure

```csv
Interface Number,Execution Date,Execution Time,Actual Duration Minutes,Status
1,2024-03-25,06:00:00,125,Completed
1,2024-03-25,14:00:00,118,Completed
2,2024-03-25,08:15:00,58,Completed
3,2024-03-25,09:10:00,92,Completed
2,2024-03-26,08:00:00,61,Completed
```

#### Column Definitions

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Task identifier |
| `Execution Date` | date | Date when task executed |
| `Execution Time` | time | Scheduled start time for this execution |
| `Actual Duration Minutes` | uint | Real elapsed time (supersedes default/estimated) |
| `Status` | enum | `Completed`, `Failed`, `Timeout`, etc. |

#### Duration Update Semantics

- **On First Import (No Duration Data)**:
  - All durations default to **15 minutes**
  - ExecutionInstance.DurationMinutes = 15
  - ExecutionInstance.IsEstimated = true

- **On Subsequent Imports**:
  - Match by (InterfaceNumber, ExecutionDate, ExecutionTime)
  - If match found and Status = "Completed":
    - Override DurationMinutes with Actual Duration Minutes
    - ExecutionInstance.IsEstimated = false
    - Recalculate downstream task deadlines if affected
  - If match found and Status != "Completed":
    - Leave DurationMinutes unchanged (will retry next execution)
    - Flag down-stream tasks as "dependent on failed execution"

- **Incremental Updates**:
  - Subsequent imports can refine individual task durations
  - System recalculates affected execution plans
  - Historical data retained for trending analysis

---

## Transformation Pipeline

### Phase 0: Load Input Files
Load both CSV files into strongly-typed records with validation.

**Task Definition Manifest:**
```csharp
public record TaskDefinitionManifest
{
    public int Id { get; set; }
    public string InterfaceNumber { get; set; }
    public string Duration { get; set; }
    public string ScheduledStartTime { get; set; }
    public string RequiredEndTime { get; set; }
    public string Precursors { get; set; }
    public string ExecutionDays { get; init; }      // "Monday|Wednesday|Friday"
    public string ExecutionTimes { get; init; }     // "06:00:00|14:00:00"
    public string ExecutionType { get; init; }      // "Scheduled" or "OnDemand"
    public string ScheduleType { get; init; }       // "OneOff" or "Recurring"
}
```

**Intake Event Manifest (NEW):**
```csharp
public record IntakeEventManifest
{
    public int Id { get; set; }
    public string InterfaceNumber { get; set; }
    public bool RequiredMonday { get; init; }
    public bool RequiredTuesday { get; init; }
    public bool RequiredWednesday { get; init; }
    public bool RequiredThursday { get; init; }
    public bool RequiredFriday { get; init; }
    public bool RequiredSaturday { get; init; }
    public bool RequiredSunday { get; init; }
    public string IntakeTime { get; init; }         // "11:30:00"
}
```

**Execution Duration Manifest (NEW):**
```csharp
public record ExecutionDurationManifest
{
    public int Id { get; set; }
    public string InterfaceNumber { get; set; }
    public string ExecutionDate { get; init; }      // "2024-03-25"
    public string ExecutionTime { get; init; }      // "06:00:00"
    public uint ActualDurationMinutes { get; init; } // Real elapsed time
    public string Status { get; init; }              // "Completed", "Failed", "Timeout"
}
```

---

### Phase 0.5: Resolve Execution Durations
Apply actual duration data (if available) or default to 15 minutes.

**Logic:**
1. Load all `ExecutionDurationManifest` records into a lookup index by (InterfaceNumber, ExecutionDate, ExecutionTime)
2. For each task defined in the Task Definition CSV:
   - Try lookup: Does (InterfaceNumber, date, time) exist in duration history?
   - If found AND Status = "Completed": Use ActualDurationMinutes
   - If found AND Status != "Completed": Use default 15 minutes (mark as pending replacement)
   - If NOT found: Use 15 minutes (mark as estimated)

**ExecutionDuration Value Object:**
```csharp
public record ExecutionDuration(
    uint DurationMinutes,
    bool IsEstimated,                               // true = default/15min, false = actual data
    bool IsPendingReplacement                       // true = failed prior run, awaiting retry
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
}
```

---

### Phase 1: Build Availability Window Requirements
Parse intake event manifest into strongly-typed requirements.

```csharp
public record IntakeEventRequirement(
    string InterfaceNumber,
    IReadOnlySet<DayOfWeek> RequiredDays,          // Days when task must be complete
    TimeOfDay IntakeTime                            // Deadline time on all required days
)
{
    /// <summary>
    /// Check if a given ExecutionEventDefinition must be complete by intake time.
    /// </summary>
    public bool MustCompleteByIntake(DayOfWeek day) => RequiredDays.Contains(day);
    
    /// <summary>
    /// Calculate the deadline (intake time on the given day).
    /// </summary>
    public DateTime GetIntakeDeadline(DateTime executionDate) => 
        executionDate.Date.Add(IntakeTime.ToTimeSpan());
}
```

### Phase 2: TaskDefinition Creation
Transform `TaskDefinitionManifest` → `TaskDefinition` (stronger types, validation).

```csharp
public record TaskDefinition(
    Guid Uid,
    string InterfaceNumber,
    string InterfaceName,
    uint DurationMinutes,
    IReadOnlySet<string> PrerequisiteIds,
    ExecutionType ExecutionType,                    // Scheduled | OnDemand
    ScheduleType ScheduleType,                      // OneOff | Recurring
    IReadOnlySet<DayOfWeek> ScheduledDays,         // Monday, Wednesday, Friday, etc.
    IReadOnlyList<TimeOfDay> ScheduledTimes,       // Multiple times per day
    IntakeEventRequirement? IntakeRequirement       // Linked from Availability Window file
);

public enum ExecutionType { Scheduled, OnDemand }
public enum ScheduleType { OneOff, Recurring }
public record TimeOfDay(int Hour, int Minute, int Second);
```

### Phase 3: Execution Matrix Generation
For each `TaskDefinition`, generate all `ExecutionEventDefinition` instances.

```csharp
public record ExecutionEventDefinition(
    Guid TaskUid,
    string InterfaceNumber,
    DayOfWeek ScheduledDay,
    TimeOfDay ScheduledTime,
    IReadOnlySet<string> PrerequisiteInterfaceNumbers,
    IntakeEventRequirement? IntakeRequirement      // Deadline constraint for this event
)
{
    /// <summary>
    /// Unique key: InterfaceNumber_DayOfWeek_HHmmss
    /// Used as Orleans grain key.
    /// </summary>
    public string GetExecutionEventKey() => 
        $"{InterfaceNumber}_{ScheduledDay}_{ScheduledTime:HHmmss}";
}
```

**Example:** Task 1 (Monday, Wednesday, Friday at 06:00, 14:00) generates 6 events:
- `1_Monday_060000`
- `1_Monday_140000`
- `1_Wednesday_060000`
- `1_Wednesday_140000`
- `1_Friday_060000`
- `1_Friday_140000`

### Phase 4: Dependency Resolution with Intake Deadline Constraints
For each `ExecutionEventDefinition`, resolve the full set of prerequisite `ExecutionEventDefinition` instances while respecting intake event deadlines.

**Dependency Resolution Rules:**
1. **Prerequisite Matching**: When Task A has Prerequisite Task B, every execution of A must wait for B's corresponding execution
   - **"Corresponding"** means: same day, same time (or latest time before A's scheduled time on the same day)
   - If prerequisite not scheduled for A's day, use latest execution from previous available day

2. **Deadline Feasibility**: For each `ExecutionEventDefinition`, check if it can complete before its intake deadline
   - Planned completion time = ScheduledStartTime + ResolutionAdjustment + DurationMinutes
   - Must occur ≤ IntakeDeadline
   - If infeasible, mark as invalid with "Cannot complete before intake deadline" message

3. **Transitive Deadline Constraints**: If Task A depends on Task B, and B has an intake deadline, adjust A's required start time accordingly
   - A's start time must be: MAX(A's scheduled time, B's estimated completion time)
   - A's end time must be: A's start time + A's duration ≤ A's intake deadline

**Example Walkthrough:** 
- Task 3 on Monday at 09:00 depends on Task 2 (which runs at 08:00 on the same day).
- Task 4 on Tuesday at 17:00 depends on Tasks 1 and 3 (which don't run on Tuesday).
  - Use Monday's latest executions: `1_Monday_140000` and `3_Monday_090000`.

### Phase 5: ExecutionInstance Creation
Create `ExecutionInstance` records for each resolved execution event.

```csharp
public record ExecutionInstance(
    int Id,
    int TaskId,
    string InterfaceNumber,
    DateTime ScheduledStartTime,
    DateTime? FunctionalStartTime,           // Adjusted start after dependency resolution
    DateTime? RequiredEndTime,                // Intake deadline
    ExecutionDuration Duration,               // NEW: duration info with estimation flag
    DateTime PlannedCompletionTime,           // NEW: calculated as ScheduledStart + Duration
    IReadOnlySet<string> PrerequisiteTaskIds, // Resolved ExecutionEventKeys
    bool IsValid,
    string? ValidationMessage = null
);
```

---

## Execution Plan: Task Chain Linking

### Overview

An **Execution Plan** represents the end-to-end chain of valid execution instances from the first task in a scheduling increment (e.g., a business day) to the last task. It provides a logical trace of dependencies and data flow.

```csharp
public record ExecutionPlan(
    string IncrementId,                      // e.g., "2024-03-25" or "week-52"
    DateTime IncrementStart,
    DateTime IncrementEnd,
    IReadOnlyList<ExecutionInstance> Tasks,  // All valid instances for this increment
    IReadOnlyList<string> TaskChain,         // Linked chain of InterfaceNumbers from start to end
    int TotalValidTasks,
    int TotalInvalidTasks,
    DateTime? CriticalPathCompletion,        // Earliest all prerequisites can complete
    IReadOnlyList<string> DeadlineMisses     // Tasks that cannot meet intake deadlines
)
{
    /// <summary>
    /// Reconstructs the execution sequence from first task (no prerequisites) through last task.
    /// </summary>
    public IReadOnlyList<string> BuildExecutionSequence()
    {
        var sequence = new List<string>();
        var visited = new HashSet<string>();
        
        // Find root tasks (no prerequisites or all prerequisites invalid)
        var rootTasks = Tasks
            .Where(t => t.IsValid && t.PrerequisiteTaskIds.Count == 0)
            .ToList();
        
        foreach (var root in rootTasks)
        {
            TraverseDepthFirst(root.InterfaceNumber, visited, sequence);
        }
        
        return sequence;
    }
    
    private void TraverseDepthFirst(string taskId, HashSet<string> visited, List<string> sequence)
    {
        if (visited.Contains(taskId)) return;
        visited.Add(taskId);
        sequence.Add(taskId);
        
        // Find children: tasks that depend on this task
        var children = Tasks
            .Where(t => t.IsValid && t.PrerequisiteTaskIds.Contains(taskId))
            .Select(t => t.InterfaceNumber)
            .Distinct();
        
        foreach (var child in children)
        {
            TraverseDepthFirst(child, visited, sequence);
        }
    }
}
```

### Execution Plan Construction

```
Step 1: Filter all ExecutionInstance records for the increment
   Input:  Full set of ExecutionInstance from all days/times
   Output: ExecutionInstance[] for this specific increment (e.g., Monday)

Step 2: Separate Valid and Invalid
   Valid:   ExecutionInstance.IsValid == true
   Invalid: ExecutionInstance.IsValid == false
   Count:   TotalValidTasks, TotalInvalidTasks

Step 3: Identify Root Tasks
   Root = ExecutionInstance where PrerequisiteTaskIds.Count == 0
          (or all prerequisites are invalid)

Step 4: Build Task Chain via Depth-First Traversal
   Starting from each root task:
   - Mark task as visited
   - Add to sequence
   - Find all children (tasks that depend on this task)
   - Recursively traverse children

Step 5: Calculate Critical Path
   CriticalPathCompletion = MAX(last task's PlannedCompletionTime) for all chains

Step 6: Collect Deadline Misses
   DeadlineMisses = All tasks where IsValid == false AND ValidationMessage contains "Deadline"
```

### Example Execution Plan

```
Increment: 2024-03-25 (Monday)
├─ Total Valid Tasks: 5
├─ Total Invalid Tasks: 2
├─ Critical Path Completion: 2024-03-25 12:30:00
│
├─ Root Tasks (no prerequisites):
│  └─ Task 1 (Extract Data): 06:00 → 07:00
│
├─ Execution Sequence:
│  1. Task 1 (06:00-07:00)    [Extract Data]
│  2. Task 2 (08:00-08:58)    [Validate Data, depends on 1]
│  3. Task 3 (09:00-10:30)    [Generate Report, depends on 2]
│  4. Task 4 (17:00-17:45)    [Archive Results, depends on 1,3]
│  5. Task 5 (18:00-18:45)    [Cleanup, depends on 4]
│
├─ Deadline Misses:
│  ✗ Task 1 (14:00 execution): cannot complete by 11:30 intake deadline
│  ✗ Task 3 (not scheduled Mon): forced to previous day Mon 09:00, misses Wed deadline?
│
└─ Data Flow:
   [Source] → Task 1 → Task 2 → Task 3 → Task 4 → Task 5 → [Sink]
             (120m)   (58m)   (90m)   (45m)   (45m)
   Total pipeline duration: ~358 minutes (5+ hours)
   All complete by 17:45 (Mon deadline was 18:00 for Task 4)
```

---

## Orleans Agent Architecture

### Grain Interface: `IExecutionInstanceGrain`

Each `ExecutionInstance` is managed by a single Orleans grain.

```csharp
namespace ConsoleApp.Ifx.Orleans.Grains.Abstractions;

public interface IExecutionInstanceGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the grain with execution event data and deadline constraints.
    /// </summary>
    Task InitializeAsync(ExecutionEventDefinition eventDef, 
                        IReadOnlySet<string> resolvedPrerequisiteKeys,
                        DateTime? intakeDeadline);

    /// <summary>
    /// Get current execution state.
    /// </summary>
    Task<ExecutionInstance> GetExecutionInstanceAsync();

    /// <summary>
    /// Calculate the resolved start time, accounting for:
    /// - Prerequisite completion times
    /// - Required end times (intake deadlines)
    /// - Available time windows
    /// </summary>
    Task<DateTime> CalculateResolvedStartTimeAsync();

    /// <summary>
    /// Validate that execution can complete within all constraints:
    /// - All prerequisites are available
    /// - Completion time respects intake deadline (if present)
    /// Returns tuple: (isValid, validationMessage)
    /// </summary>
    Task<(bool IsValid, string? ValidationMessage)> ValidateExecutabilityAsync();
    
    /// <summary>
    /// Get the current planned completion time (start + duration).
    /// Accounts for actual start time after dependency resolution.
    /// </summary>
    Task<DateTime> GetPlannedCompletionTimeAsync();
    
    /// <summary>
    /// Check if this instance meets its intake deadline.
    /// Returns (meetsDeadline, plannedCompletion, deadline)
    /// </summary>
    Task<(bool MeetsDeadline, DateTime PlannedCompletion, DateTime? Deadline)> 
        CheckDeadlineAsync();

    /// <summary>
    /// Get the estimated duration (15 min default if no actual data available).
    /// </summary>
    Task<ExecutionDuration> GetDurationAsync();
    
    /// <summary>
    /// Update duration with actual execution data (when it becomes available).
    /// Triggers re-validation of this instance and dependent tasks.
    /// </summary>
    Task<bool> UpdateActualDurationAsync(uint actualMinutes);
    
    /// <summary>
    /// Get child tasks that depend on this task.
    /// </summary>
    Task<IReadOnlyList<string>> GetDependentTasksAsync();

    /// <summary>
    /// Mark this instance as ready to execute.
    /// </summary>
    Task MarkAsReadyAsync();

    /// <summary>
    /// Get the resolved prerequisites (ExecutionInstance objects).
    /// </summary>
    Task<IReadOnlyList<ExecutionInstance>> GetResolvedPrerequisitesAsync();
}

/// <summary>
/// Orchestrator grain that manages execution plan generation and distribution.
/// </summary>
public interface IExecutionPlanOrchestratorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Build execution plan for a given increment from all valid execution instances.
    /// Traces chain from root tasks (no prerequisites) through leaf tasks.
    /// </summary>
    Task<ExecutionPlan> BuildExecutionPlanAsync(string incrementId, 
                                               IReadOnlyList<ExecutionInstance> allInstances);
    
    /// <summary>
    /// Get existing execution plan.
    /// </summary>
    Task<ExecutionPlan?> GetExecutionPlanAsync(string incrementId);
    
    /// <summary>
    /// Recalculate plan after duration updates.
    /// Called when actual execution times become available.
    /// </summary>
    Task<ExecutionPlan> RecalcuteExecutionPlanAsync(string incrementId, 
                                                   string updatedTaskInterfaceNumber);
}
```
```

### Grain Lifecycle

1. **Construction** → Grain instantiated with key `InterfaceNumber_DayOfWeek_HHmmss`

2. **Initialization** → `InitializeAsync()` receives execution event details, prerequisite keys, and intake deadline

3. **Duration Resolution** → `GetDurationAsync()` retrieves duration (15 min default or actual from history)

4. **Deadline Validation** → `CheckDeadlineAsync()` evaluates if execution can complete before intake event (if required)

5. **Start Time Resolution** → `CalculateResolvedStartTimeAsync()` fetches prerequisite grains, calculates completion times, adjusts actual start time

6. **Validation** → `ValidateExecutabilityAsync()` checks both prerequisite and deadline feasibility

7. **Readiness** → `MarkAsReadyAsync()` transitions to executable state (only if all constraints pass)

8. **(Later) Duration Update** → `UpdateActualDurationAsync()` when real execution time becomes available
   - Recalculates own planned completion
   - Triggers re-validation of dependent tasks
   - Updates orchestrator's execution plan

### Grain Communication Pattern

```
PHASE 1: Initialization
┌────────────────────────────────────────────────────────┐
│ OrchService: ManifestToExecutionSequencer              │
│ - Loads all 3 CSV files                                │
│ - Builds ExecutionEventDefinition matrix               │
│ - Resolves dependencies                                │
│ - Applies durations (actual or default 15min)          │
└────────────────────────────────────────────────────────┘
                        │
                        ├─→ For each ExecutionEventDefinition:
                        │   • Create IExecutionInstanceGrain key
                        │   • Call Initialize(eventDef, prereqs, deadline)
                        │   • Call GetDuration() [returns 15min or actual]
                        │   • Call CalculateResolvedStartTime()
                        │   • Call ValidateExecutability()
                        │   • If Valid: Call MarkAsReady()
                        │
                        ↓
        ┌────────────────────────────────────────┐
        │ Parallel Grain Execution               │
        │ - One grain per ExecutionInstance      │
        │ - Independent deadline validation      │
        │ - All ValidExecutionInstances returned │
        └────────────────────────────────────────┘


PHASE 2: Execution Plan Generation
┌────────────────────────────────────────────────────────┐
│ ExecutionPlanOrchestratorGrain                         │
│ - Receives all valid ExecutionInstances                │
│ - Traces dependency chain (root → leaf)                │
│ - Builds linked execution sequence                     │
│ - Calculates critical path & completion time          │
└────────────────────────────────────────────────────────┘
                        │
                        ↓
        ┌────────────────────────────────────────┐
        │ ExecutionPlan                          │
        │ ├─ TaskChain [1→2→3→4→5]               │
        │ ├─ CriticalPathCompletion: HH:MM       │
        │ ├─ DeadlineMisses: [list]              │
        │ └─ Valid: 5, Invalid: 2                │
        └────────────────────────────────────────┘


PHASE 3: Duration Updates (Later)
┌────────────────────────────────────────────────────────┐
│ DurationUpdateService                                  │
│ - Watches for real execution completion               │
│ - Imports ExecutionDurationManifest                    │
│ - Matches (InterfaceNumber, Date, Time)                │
└────────────────────────────────────────────────────────┘
                        │
                        ├─→ For each matched execution:
                        │   • Call IExecutionInstanceGrain.UpdateActualDurationAsync()
                        │   • Grain recalculates PlannedCompletionTime
                        │   • Triggers dependent grain re-validation
                        │   • Notifies ExecutionPlanOrchestrator
                        │
                        ↓
        ┌────────────────────────────────────────┐
        │ ExecutionPlanOrchestrator               │
        │ - Calls RecalculateExecutionPlanAsync() │
        │ - Updates plan with refined durations  │
        │ - May expose new deadline misses       │
        └────────────────────────────────────────┘
```

---

## Dependency Resolution Algorithm

### Input
- `ExecutionEventDefinition` for Task A scheduled on Monday 09:00
- Task A's prerequisites: [Task B, Task C]
- Task A's intake requirement (if any): must complete by 12:00 Monday
- Full matrix of all `ExecutionEventDefinition` instances with their intake requirements

### Algorithm

```
PHASE 1: Select Prerequisite Instances
─────────────────────────────────────
1. For each PrerequisiteTaskId in Task A's prerequisites:

   a. Find all ExecutionEventDefinition instances of Prerequisite task
   
   b. Filter to those feasible on or before Task A's day/time:
      - If prerequisite is scheduled on Monday before 09:00 → INCLUDE
      - If prerequisite is scheduled on Monday after 09:00 → EXCLUDE
      - If prerequisite is scheduled on Friday (before Monday) → INCLUDE
      - Otherwise → EXCLUDE
   
   c. Select the LATEST feasible execution:
      - Most recent day
      - Most recent time on that day
   
   d. Add to ExecutionInstance.PrerequisiteTaskIds

2. If any prerequisite has no feasible execution:
   ExecutionInstance.IsValid = false
   ExecutionInstance.ValidationMessage = "Missing prerequisite execution"


PHASE 2: Validate Deadline Feasibility (NEW)
──────────────────────────────────────────────
3. If Task A has an IntakeEventRequirement and must complete on Monday:

   a. Retrieve intake deadline: 12:00 Monday for Task A
   
   b. Fetch actual_completion_time for each resolved prerequisite:
      - Task B's scheduled time: 08:00 (estimated completion: 09:00)
      - Task C's scheduled time: 07:00 (estimated completion: 08:00)
      - Take MAX(09:00, 08:00) = 09:00
   
   c. Calculate earliest possible start for Task A:
      - Must start no earlier than 09:00 (when last prerequisite finishes)
      - Must start no later than 09:00 (ScheduledStartTime)
      - Actual Start = MAX(09:00, 09:00) = 09:00
   
   d. Calculate Task A's planned completion:
      - Planned Completion = 09:00 + 120 minutes = 11:00
   
   e. Validate against deadline:
      - 11:00 ≤ 12:00 → VALID ✓
      - If 11:00 > 12:00 → Mark Invalid with "Deadline miss: estimated completion 11:00, intake deadline 12:00"

4. Repeat for any other days Task A runs (Tue, Wed, etc.)
```

### Example Walkthrough

**Setup:**
- Task 1: Mon-Fri 06:00 (120 min), Mon-Fri 14:00 (120 min) — Intake: Mon-Fri by 11:30
- Task 2: Mon-Fri 08:00 (60 min) — depends on Task 1 — Intake: Mon-Fri by 12:00
- Task 3: Mon-Wed 09:00 (90 min) — depends on Task 2 — Intake: Mon-Wed by 12:15
- Task 4: Tue-Thu 17:00 (45 min) — depends on Task 1, Task 3 — Intake: Tue-Thu by 18:00

**Resolving Task 2 on Monday 08:00 with deadline 12:00:**
```
Prerequisites: Task 1
  Latest feasible: 1_Mon_060000 (completes ~07:00)

Prerequisite resolution complete at 07:00
Task 2 scheduled start: 08:00
Actual start: MAX(08:00, 07:00) = 08:00
Task 2 duration: 60 min
Planned completion: 09:00
Intake deadline: 12:00
Feasibility: 09:00 ≤ 12:00 → VALID ✓
```

**Resolving Task 3 on Monday 09:00 with deadline 12:15:**
```
Prerequisites: Task 2 (Mon 08:00)
  Task 2's estimated completion: 09:00

Prerequisite resolution complete at 09:00
Task 3 scheduled start: 09:00
Actual start: MAX(09:00, 09:00) = 09:00
Task 3 duration: 90 min
Planned completion: 10:30
Intake deadline: 12:15
Feasibility: 10:30 ≤ 12:15 → VALID ✓
```

**Resolving Task 4 on Tuesday 17:00 with deadline 18:00 (INFEASIBLE):**
```
Prerequisites: Task 1, Task 3
  Task 1 latest on Tue: 1_Tue_140000 (completes ~16:00)
  Task 3 not on Tuesday, latest from Mon: 3_Mon_090000 (completes ~10:30)

Prerequisite resolution: MAX(16:00, 10:30) = 16:00
Task 4 scheduled start: 17:00
Actual start: MAX(17:00, 16:00) = 17:00
Task 4 duration: 45 min
Planned completion: 17:45
Intake deadline: 18:00
Feasibility: 17:45 ≤ 18:00 → VALID ✓
```

**Resolving Task 1 on Monday 14:00 with deadline 11:30 (INFEASIBLE):**
```
Prerequisites: None

Task 1 has no prerequisites
Task 1 scheduled start: 14:00
No adjustment needed
Planned completion: 16:00 (14:00 + 120 min)
Intake deadline: 11:30
Feasibility: 16:00 ≤ 11:30 → INVALID ✗
ValidationMessage: "Scheduled time 14:00 cannot complete before intake deadline 11:30"
```

**Result for the week:**
```
VALID instances:
- 1_Mon_060000 (completes 08:00, deadline 11:30) ✓
- 1_Tue_060000 (completes 08:00, deadline 11:30) ✓
- 1_Wed_060000 (completes 08:00, deadline 11:30) ✓
- 1_Thu_060000 (completes 08:00, deadline 11:30) ✓
- 1_Fri_060000 (completes 08:00, deadline 11:30) ✓
- 1_Mon_140000 (completes 16:00, deadline 11:30) ✗
- 2_Mon_080000 (completes 09:00, deadline 12:00) ✓
- 3_Mon_090000 (completes 10:30, deadline 12:15) ✓
- 4_Tue_170000 (completes 17:45, deadline 18:00) ✓
- ... [others]

INVALID instances:
- 1_Mon_140000, 1_Tue_140000, etc. (all 14:00 instances where deadline is 11:30)
```

---

## Data Flow Summary

```
Task Definition CSV    Intake Event CSV    Duration History CSV (Optional)
    ↓                      ↓                         ↓
[TaskDefManifest]   [IntakeEventManifest]  [ExecutionDurationManifest]
    ↓                      ↓                         ↓
    └──────────┬───────────┼──────────────────┬────┘
               ↓           ↓                  ↓
        [TaskDefinition with IntakeEventRequirement + ExecutionDuration linked]
               ↓
        [ExecutionEventDefinition matrix]
           (days × times combinations)
               ↓
  [Dependency Resolution + Duration Assignment + Deadline Validation]
           (Phase 1: Prerequisites)
           (Phase 2: Duration Lookup/Default 15min)
           (Phase 3: Intake Deadline Check)
               ↓
        [ExecutionInstance Records]
     (marked Valid/Invalid + DurationInfo)
               ↓
        [Orleans Agents]
     (IExecutionInstanceGrain)
           ↓
[ExecutionPlan: Task Chain from First to Last]
    ├─ Valid execution sequence
    ├─ Critical path completion time
    ├─ Deadline misses
    └─ Data flow visualization
```

---

## State Management in Orleans Grains

### Grain State

```csharp
public class ExecutionInstanceState
{
    public string ExecutionEventKey { get; set; }      // "1_Monday_060000"
    public string InterfaceNumber { get; set; }
    public DayOfWeek ScheduledDay { get; set; }
    public TimeOfDay ScheduledTime { get; set; }
    
    // NEW: Duration tracking with estimation metadata
    public ExecutionDuration Duration { get; set; }    // Duration info (estimated or actual)
    public uint DurationMinutes => Duration.DurationMinutes;
    public bool IsEstimatedDuration => Duration.IsEstimated;
    public bool IsPendingDurationReplacement => Duration.IsPendingReplacement;
    
    public ISet<string> ResolvedPrerequisiteKeys { get; set; } = new();
    
    // Deadline tracking
    public DateTime? IntakeDeadline { get; set; }       // e.g., 2024-03-25 11:30:00
    public bool HasIntakeRequirement { get; set; }
    
    public DateTime? CalculatedStartTime { get; set; }
    public DateTime? PlannedCompletionTime { get; set; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Initializing;
    public string? ValidationMessage { get; set; }
}

public enum ExecutionStatus
{
    Initializing,
    AwaitingPrerequisites,
    ReadyToExecute,
    Invalid,
    Completed,
    DeadlineMiss,                                      // Failed deadline check
    DurationPending                                    // Awaiting actual duration data
}
```

### Validation in `ValidateExecutabilityAsync()`

```csharp
public async Task<bool> ValidateExecutabilityAsync()
{
    // Prerequisite validation (existing)
    var prerequisites = await GetResolvedPrerequisitesAsync();
    foreach (var prereq in prerequisites)
    {
        if (!prereq.IsValid)
            return false;
    }
    
    // NEW: Deadline validation
    if (State.HasIntakeRequirement && State.IntakeDeadline.HasValue)
    {
        var lastPrerequiteFinish = prerequisites
            .Max(p => p.GetPlannedCompletionTime());
        
        var actualStart = State.ScheduledStartTime > lastPrerequiteFinish
            ? State.ScheduledStartTime
            : lastPrerequiteFinish;
        
        State.PlannedCompletionTime = actualStart.AddMinutes(State.DurationMinutes);
        
        if (State.PlannedCompletionTime > State.IntakeDeadline)
        {
            State.ValidationMessage = 
                $"Deadline miss: estimated completion {State.PlannedCompletionTime:HH:mm}, " +
                $"intake deadline {State.IntakeDeadline:HH:mm}";
            State.Status = ExecutionStatus.DeadlineMiss;
            return false;
        }
    }
    
    State.Status = ExecutionStatus.ReadyToExecute;
    return true;
}
```

---

## Duration Estimation & Refinement Strategy

### Day One: Default Duration (15 Minutes)

On initial import with no historical execution data:
- All tasks assigned **15-minute** default duration
- ExecutionDuration.IsEstimated = true
- Marks tasks as "pending actual data"
- Enables creation of ExecutionPlan immediately (no waiting for real data)

**Rationale:**
- 15 minutes provides reasonable middle ground (not too pessimistic, not too optimistic)
- Allows scheduler to calculate critical path and deadline conflicts
- Flexible for later refinement as tasks actually run

### Incremental Duration Refinement

As tasks execute and complete:

```
Timeline:
  Day 1:  Tasks run with 15-min estimates → ExecutionPlan v1
  Day 2:  Import Task 1's actual time (120 min) → ExecutionPlan v2
  Day 3:  Import Task 2's actual time (58 min) → ExecutionPlan v3
  Day 4:  Fine-tune remaining tasks → ExecutionPlan v4
```

**Per-Task Update Flow:**
1. Task completes execution in real system
2. Actual duration (and status) recorded
3. ExecutionDurationManifest row imported via CSV
4. `DurationUpdateService` matches (InterfaceNumber, Date, ExecutionTime)
5. Calls `IExecutionInstanceGrain.UpdateActualDurationAsync(actualMinutes)`
6. Grain recalculates:
   - Its own PlannedCompletionTime
   - Detects any newly-infeasible dependent tasks
   - Notifies dependent grains
7. Orchestrator recalculates ExecutionPlan
8. New plan reflects refined durations + any newly-discovered deadline issues

**Example Refinement:**

```
Day 1 Plan (estimated durations):
├─ Task 1: 06:00 + 15min = 06:15 (estimated)
├─ Task 2: 08:00 + 15min = 08:15 (but depends on 1)
│  Adjusted: 06:15 + 15min = 06:30 (estimated)
└─ Critical path: 06:30
  Deadline 11:30 → VALID ✓

Day 2 (Task 1 completes at 07:00, actual = 60 min):
├─ Task 1: 06:00 + 60min = 07:00 (actual)
├─ Task 2: 08:00 + 15min = 08:15 (estimated)
│  Adjusted: 07:00 + 15min = 07:15 (refined)
└─ Critical path: 07:15
  Deadline 11:30 → VALID ✓

Day 7 (All tasks have actual data):
├─ Task 1: 06:00 + 60min = 07:00 (actual)
├─ Task 2: 07:00 + 58min = 07:58 (actual)
├─ Task 3: 08:00 + 92min = 09:32 (actual)
└─ Critical path: 09:32
  Deadline 12:15 → VALID ✓ (but tighter margin than estimated)
```

### Handling Failed/Incomplete Executions

If a task execution fails or times out:

```csharp
ExecutionDurationManifest row:
  Interface 2, 2024-03-26 08:00, Duration=NULL, Status="FAILED"

Handling:
1. No duration update imported (Status != "Completed")
2. ExecutionInstance remains with previous estimate (15 min or old actual)
3. IsPendingDurationReplacement = true
4. Next execution of same task will retry collection
5. Orchestrator flags: "Task 2 (2024-03-26 08:00): Awaiting duration data"
```

---

## Resolved Requirements (Answers to Open Questions)

### Time & Scheduling ✅ FULLY RESOLVED

1. **Time Zone Handling:** ✅ **RESOLVED** - All times are Pacific Time (no UTC conversion). DST transitions must be flagged with warnings.

2. **Cross-Period Dependencies:** ✅ **RESOLVED** - No cross-period dependencies. Each manifest context is independent; tasks can only depend on tasks within the same period/manifest.

3. **Daylight Saving Time:** ✅ **RESOLVED** - Flag in execution plan output. Any execution context crossing DST boundaries (March/November) receives a warning in the ExecutionPlan.

### Execution & Constraints ✅ FULLY RESOLVED

4. **Hard vs. Soft Deadlines:** ✅ **RESOLVED** - Hard failure. Missing an intake deadline marks the sequence as invalid.

5. **Backfilling:** ✅ **RESOLVED** - If a daily task was skipped yesterday, today's instance depends on yesterday's instance (even if it didn't run).

6. **Late Prerequisites:** ✅ **RESOLVED** - If precursor Task B misses its deadline, dependent Task A immediately fails validation (no retry or late-time re-evaluation).

### Financial Domain Specifics ✅ FULLY RESOLVED

7. **Multi-System Coordination:** ✅ **RESOLVED** - Polling/pull model. External systems poll our database; we don't push notifications.

8. **Cascading Failures:** ✅ **RESOLVED** - If Task A misses deadline, all downstream tasks that depend on A are automatically marked invalid.

9. **Transactional Integrity:** ✅ **RESOLVED** - Yes, certain periods (e.g., end-of-month close) suspend task scheduling entirely.

10. **Repeated Data Files:** ✅ **RESOLVED** - Re-run all tasks per iteration (no skip/checkpoint logic needed).

### Duration Estimation & Data Collection ✅ FULLY RESOLVED

14. **Duration Data Availability Window:** ✅ **RESOLVED** - Real-time immediate. Actual durations are available as soon as tasks complete; ExecutionDurationManifest receives real-time updates.

15. **Duration Trending:** ✅ **RESOLVED** - Simple: last execution time only. Track the most recent actual duration; use as input for next planning cycle. No moving averages or percentiles for now.

16. **Timeout Handling:** ✅ **RESOLVED** - Hard stop at deadline. If a task reaches its intake deadline before completion, execution stops; data loss is acceptable rather than exceeding deadline.

17. **Duration Variance:** ✅ **RESOLVED** - Expose confidence intervals. ExecutionPlan includes confidence intervals on all duration estimates to help downstream users understand risk.

### Orleans & Distribution ✅ FULLY RESOLVED (But Not Needed)

11. **Grain Lifetime & Persistence:** ✅ **RESOLVED** - Not applicable. This is a planning tool; no Orleans agents needed.

12. **Grain Reminders:** ✅ **RESOLVED** - Not applicable. This is a planning tool; no Orleans agents needed.

13. **Scale:** ✅ **RESOLVED** - Small deployment: <100 tasks, <10k instances/week; single console application (no Orleans cluster).

---

## ALL 17 OPEN QUESTIONS NOW RESOLVED ✅

### Phase 3: Orleans Implementation Planning

5. **Define Orleans deployment model:**
   - Single-silo for development/testing?
   - Multi-silo for production?
   - In-memory state or persistent storage (Azure Table Storage, etc.)?
   - Does ExecutionPlan persist or regenerate on demand?

6. **Specify ExecutionPlan lifecycle:**
   - Build once per day at start of processing?
   - Rebuild after each duration update?
   - Expose via REST API? Stored in database?
   - Archived for audit trail?

### Phase 4: Implementation Tasks

7. **Implement Phase 0.5: Duration Resolution**
   - Parse ExecutionDurationManifest CSV
   - Create ExecutionDuration value object
   - Default to 15 min if no data available
   - Build lookup index for fast matching

8. **Implement IExecutionInstanceGrain enhancements**
   - GetDurationAsync()
   - UpdateActualDurationAsync()
   - GetDependentTasksAsync()
   - Re-validation on duration update

9. **Implement IExecutionPlanOrchestratorGrain**
   - BuildExecutionPlanAsync()
   - Depth-first traversal of task chain
   - Critical path calculation
   - Deadline miss collection

10. **Implement ExecutionPlan model & serialization**
    - Chain linking logic
    - Summary statistics
    - JSON export for visibility

11. **Implement DurationUpdateService**
    - Watches for ExecutionDurationManifest imports
    - Matches (InterfaceNumber, Date, Time) tuples
    - Triggers grain updates in parallel
    - Notifies orchestrator for plan recalculation
