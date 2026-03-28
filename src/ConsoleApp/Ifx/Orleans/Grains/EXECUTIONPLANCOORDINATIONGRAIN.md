# ExecutionPlanCoordinationGrain - Implementation Guide

## Overview

The `ExecutionPlanCoordinationGrain` is a high-level Orleans grain that provides centralized coordination and management of execution plans. It orchestrates multiple execution plan calculations and maintains the state of task execution across planning periods.

## Purpose

- **Coordinate** multiple task execution plans
- **Manage** task lifecycle (scheduled → completed)
- **Detect** deadline conflicts
- **Rebalance** plans when durations change
- **Track** coordination metrics and status

## Architecture

```
ExecutionPlanCoordinationGrain (High-Level Coordinator)
    │
    ├─→ IExecutionPlanCoordinatorGrain (Period Coordinator)
    │    │
    │    ├─→ IExecutionTaskGrain (Task 1)
    │    ├─→ IExecutionTaskGrain (Task 2)
    │    └─→ IExecutionTaskGrain (Task N)
    │
    └─→ Task Status & Plan Management
         ├─→ Execution Plans (by date)
         └─→ Task Execution Status
```

## Key Features

### 1. **Plan Initialization**
```csharp
var executionPlan = await coordinationGrain.StartExecutionPlanningAsync(
    executionEvents,
    periodStart,
    periodEnd
);
```

- Creates execution instances for all tasks
- Initializes coordinator grain for the period
- Calculates initial plan
- Stores task status and execution plan

### 2. **Task Status Tracking**
```csharp
var task = await coordinationGrain.GetTaskExecutionStatusAsync("task-001");
var allTasks = await coordinationGrain.GetAllTasksAsync();
```

- Get individual task execution status
- Get all tasks in current plan
- Tracks task progress through execution lifecycle

### 3. **Completion Tracking**
```csharp
await coordinationGrain.MarkTaskCompletedAsync("task-001", actualCompletionTime);
```

- Marks task as completed
- Updates dependent tasks with new start times
- Cascades changes through task chain
- Triggers re-coordination

### 4. **Deadline Detection**
```csharp
var conflicts = await coordinationGrain.DetectDeadlineConflictsAsync();
```

- Identifies tasks that won't meet deadline
- Returns list of at-risk task IDs
- Helps prioritize rebalancing efforts

### 5. **Plan Rebalancing**
```csharp
await coordinationGrain.RebalancePlanAsync("task-001", newDuration);
```

- Updates task duration
- Cascades changes to dependent tasks
- Triggers re-coordination
- Updates convergence status

### 6. **Status Monitoring**
```csharp
var status = await coordinationGrain.GetCoordinationStatusAsync();
```

Returns `CoordinationStatus` with:
- Planning period dates
- Total task count
- Valid/conflict task counts
- Completed/pending task counts
- Critical path completion time
- Convergence status
- Iteration count

## Usage Example

```csharp
// Get coordination grain
var coordinationGrain = grainFactory.GetGrain<IExecutionPlanCoordinationGrain>(
    "2025-01-15"
);

// Start planning for a period
var plan = await coordinationGrain.StartExecutionPlanningAsync(
    executionEvents,
    new DateTime(2025, 1, 15),
    new DateTime(2025, 1, 22)
);

// Check for deadline conflicts
var conflicts = await coordinationGrain.DetectDeadlineConflictsAsync();
if (conflicts.Any())
    Console.WriteLine($"At-risk tasks: {string.Join(", ", conflicts)}");

// Mark task as completed
await coordinationGrain.MarkTaskCompletedAsync(
    "task-001",
    DateTime.UtcNow
);

// Get updated plan
var plan = await coordinationGrain.GetExecutionPlanAsync(new DateTime(2025, 1, 15));

// Get status
var status = await coordinationGrain.GetCoordinationStatusAsync();
Console.WriteLine($"Valid tasks: {status.ValidTaskCount}/{status.TotalTaskCount}");
Console.WriteLine($"Converged: {status.HasConverged}");
```

## Data Models

### CoordinationStatus
```csharp
public record CoordinationStatus
{
    public DateTime PlanningPeriodStart { get; init; }
    public DateTime PlanningPeriodEnd { get; init; }
    public int TotalTaskCount { get; init; }
    public int ValidTaskCount { get; init; }
    public int ConflictTaskCount { get; init; }
    public int CompletedTaskCount { get; init; }
    public int PendingTaskCount { get; init; }
    public DateTime? CriticalPathCompletion { get; init; }
    public DateTime LastUpdateTime { get; init; }
    public bool HasConverged { get; init; }
    public int IterationCount { get; init; }
}
```

## Execution Status Transitions

```
Initializing
    ↓
AwaitingPrerequisites (after plan creation)
    ↓
ReadyToExecute (if converged and no conflicts)
    ↓
Completed (when marked complete)

Invalid (if deadline cannot be met)
DeadlineMiss (if deadline exceeded)
DurationPending (when duration is adjusted)
```

## Internal State Management

### Task Execution Status Map
```csharp
private Dictionary<string, ExecutionInstanceEnhanced> _taskExecutionStatus
```

- Key: Task ID string
- Value: Current execution status
- Updated on: completion, rebalancing, duration changes

### Execution Plans Map
```csharp
private Dictionary<string, ExecutionPlan> _executionPlans
```

- Key: Date string (yyyy-MM-dd)
- Value: Complete execution plan
- One plan per planning period

### Coordinator Grains Map
```csharp
private Dictionary<string, IExecutionPlanCoordinatorGrain> _coordinatorGrains
```

- Key: Date string
- Value: Period coordinator grain reference
- One coordinator per planning period

## Cascade Effects

### Task Completion Cascade
When a task is marked complete:
1. Task marked as `Completed`
2. All dependent tasks found
3. For each dependent with `ScheduledStartTime` before `actualCompletionTime`:
   - `FunctionalStartTime` set to `actualCompletionTime`
   - `PlannedCompletionTime` recalculated
   - Status updated to `AwaitingPrerequisites`
4. Re-coordination triggered

### Duration Change Cascade
When a task duration is updated:
1. Task marked as `DurationPending`
2. All dependent tasks traversed (depth-first)
3. For each dependent with earlier `ScheduledStartTime`:
   - `FunctionalStartTime` adjusted
   - `PlannedCompletionTime` recalculated
   - Status marked as `DurationPending`
4. Traversal continues through entire dependency chain
5. Re-coordination triggered

## Metrics & Convergence

### Convergence Definition
- All task deadlines can be met
- All tasks have valid start/end times
- No re-planning required

### Tracking Metrics
- **IterationCount**: Number of re-coordination cycles
- **HasConverged**: Whether plan is stable
- **LastUpdateTime**: When state last changed
- **ValidTaskCount**: Tasks with valid times
- **ConflictTaskCount**: Tasks with deadline conflicts

## Error Handling

### Exception Cases
1. **Task Not Found**: Thrown if task ID doesn't exist
2. **Invalid Date Range**: If start >= end
3. **Null Arguments**: If required parameters are null

### Recovery Options
- `ResetAsync()`: Clear all state and start fresh
- Get status and retry with different parameters

## Performance Considerations

### Complexity
- **Plan Creation**: O(N) where N = number of tasks
- **Task Completion**: O(N) for cascade to dependents
- **Duration Rebalancing**: O(N) for dependency traversal
- **Status Query**: O(1) lookup or O(N) for all tasks

### Optimization Tips
1. Use `GetTaskExecutionStatusAsync()` for single task queries
2. Call `DetectDeadlineConflictsAsync()` before `RebalancePlanAsync()`
3. Batch multiple duration updates before re-coordination
4. Reset periodically for long-running simulations

## Thread Safety

- Orleans grain serialization handles thread safety
- All operations are async and non-blocking
- Internal dictionaries are accessed single-threaded per grain instance
- Safe for concurrent calls from multiple callers

## Testing Considerations

### Unit Test Scenarios
```csharp
// Test plan initialization
var plan = await grain.StartExecutionPlanningAsync(...);
Assert.NotNull(plan);
Assert.True(plan.Tasks.Count > 0);

// Test task completion
await grain.MarkTaskCompletedAsync("task-001", completionTime);
var status = await grain.GetTaskExecutionStatusAsync("task-001");
Assert.Equal(ExecutionStatus.Completed, status.Status);

// Test cascade effects
var dependent = await grain.GetTaskExecutionStatusAsync("task-002");
Assert.True(dependent.FunctionalStartTime >= completionTime);

// Test deadline detection
var conflicts = await grain.DetectDeadlineConflictsAsync();
// Verify expected conflicts

// Test rebalancing
await grain.RebalancePlanAsync("task-001", newDuration);
var coordination = await grain.GetCoordinationStatusAsync();
Assert.False(coordination.HasConverged);
```

## Integration with Orleans

### Grain Registration
```csharp
services.AddOrleans(builder =>
{
    builder
        .AddMemoryGrainStorage("execution-plans")
        .ConfigureApplicationParts(parts =>
        {
            parts.AddApplicationPart(typeof(ExecutionPlanCoordinationGrain).Assembly)
                 .WithReferences();
        });
});
```

### Usage in Application
```csharp
public class ExecutionPlanner
{
    private readonly IGrainFactory _grainFactory;

    public ExecutionPlanner(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<ExecutionPlan> PlanPeriodAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        DateTime periodStart)
    {
        var grain = _grainFactory.GetGrain<IExecutionPlanCoordinationGrain>(
            periodStart.ToString("yyyy-MM-dd")
        );

        return await grain.StartExecutionPlanningAsync(
            events,
            periodStart,
            periodStart.AddDays(7)
        );
    }
}
```

## Related Components

- **IExecutionPlanCoordinatorGrain**: Period-specific coordinator
- **IExecutionTaskGrain**: Individual task grain
- **ExecutionPlan**: Result model
- **ExecutionInstanceEnhanced**: Task execution details
- **ExecutionEventDefinition**: Task definition

## Files

- **Implementation**: `src/ConsoleApp/Ifx/Orleans/Grains/ExecutionPlanCoordinationGrain.cs`
- **Grain Classes**: `src/ConsoleApp/Ifx/Orleans/Grains/ExecutionGrains.cs`
- **Abstractions**: `src/ConsoleApp/Ifx/Orleans/Grains/Abstractions.cs`

---

**Last Updated**: Today  
**Status**: ✅ Production Ready
