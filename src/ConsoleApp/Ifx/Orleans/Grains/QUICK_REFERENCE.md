# ExecutionPlanCoordinationGrain - Quick Reference

## File Location
```
src/ConsoleApp/Ifx/Orleans/Grains/ExecutionPlanCoordinationGrain.cs
```

## Interface
```csharp
public interface IExecutionPlanCoordinationGrain : IGrainWithStringKey
```

## Key Methods

### 1. Start Planning
```csharp
Task<ExecutionPlan> StartExecutionPlanningAsync(
    IReadOnlyList<ExecutionEventDefinition> executionEvents,
    DateTime planningPeriodStart,
    DateTime planningPeriodEnd
)
```
**Usage**: Initialize plan for a period with task definitions

### 2. Get Plan
```csharp
Task<ExecutionPlan?> GetExecutionPlanAsync(DateTime planDate)
```
**Usage**: Retrieve execution plan for a specific date

### 3. Get Task Status
```csharp
Task<ExecutionInstanceEnhanced?> GetTaskExecutionStatusAsync(string taskId)
```
**Usage**: Get execution status of specific task

### 4. Get All Tasks
```csharp
Task<IReadOnlyList<ExecutionInstanceEnhanced>> GetAllTasksAsync()
```
**Usage**: Get all tasks in current plan

### 5. Mark Complete
```csharp
Task MarkTaskCompletedAsync(string taskId, DateTime actualCompletionTime)
```
**Usage**: Mark task complete and cascade changes to dependents

### 6. Detect Conflicts
```csharp
Task<IReadOnlyList<string>> DetectDeadlineConflictsAsync()
```
**Usage**: Find all tasks that won't meet deadline

### 7. Rebalance Plan
```csharp
Task<bool> RebalancePlanAsync(string taskId, ExecutionDuration newDuration)
```
**Usage**: Update duration and rebalance dependent tasks

### 8. Get Status
```csharp
Task<CoordinationStatus> GetCoordinationStatusAsync()
```
**Usage**: Get coordination metrics and status

### 9. Reset
```csharp
Task ResetAsync()
```
**Usage**: Clear all state and reset grain

## Quick Start Example

```csharp
// 1. Get grain
var grain = grainFactory.GetGrain<IExecutionPlanCoordinationGrain>("2025-01-15");

// 2. Start planning
var plan = await grain.StartExecutionPlanningAsync(
    events,
    new DateTime(2025, 1, 15),
    new DateTime(2025, 1, 22)
);

// 3. Check status
var status = await grain.GetCoordinationStatusAsync();
Console.WriteLine($"Tasks: {status.ValidTaskCount}");

// 4. Mark task complete
await grain.MarkTaskCompletedAsync("task-001", DateTime.UtcNow);

// 5. Check conflicts
var conflicts = await grain.DetectDeadlineConflictsAsync();

// 6. Get plan
var updatedPlan = await grain.GetExecutionPlanAsync(new DateTime(2025, 1, 15));
```

## CoordinationStatus Properties

```csharp
public DateTime PlanningPeriodStart         // Start of planning window
public DateTime PlanningPeriodEnd           // End of planning window
public int TotalTaskCount                   // Total tasks
public int ValidTaskCount                   // Tasks with valid times
public int ConflictTaskCount                // Tasks with deadline conflicts
public int CompletedTaskCount               // Completed tasks
public int PendingTaskCount                 // Tasks still pending
public DateTime? CriticalPathCompletion     // Latest completion time
public DateTime LastUpdateTime              // When status was updated
public bool HasConverged                    // Is plan stable?
public int IterationCount                   // How many re-coordinations?
```

## State Transitions

```
Create Plan
    ↓
AwaitingPrerequisites (initial state)
    ↓
ReadyToExecute (if no conflicts)
    ├─→ DurationPending (if duration changes)
    └─→ Completed (when finished)

Invalid/DeadlineMiss (if conflicts)
```

## Error Handling

```csharp
try
{
    await grain.MarkTaskCompletedAsync("task-001", completionTime);
}
catch (ArgumentNullException)
{
    // Task ID was null
}
catch (InvalidOperationException ex)
{
    // Task not found: "Task not found: task-001"
}
```

## Integration Pattern

```csharp
public class ExecutionService
{
    private readonly IGrainFactory _grainFactory;

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

## Related Types

| Type | Purpose |
|------|---------|
| `IExecutionPlanCoordinatorGrain` | Period coordinator |
| `IExecutionTaskGrain` | Individual task grain |
| `ExecutionPlan` | Plan result |
| `ExecutionInstanceEnhanced` | Task details |
| `CoordinationStatus` | Status metrics |
| `ExecutionEventDefinition` | Task definition |

## Documentation Files

| File | Purpose |
|------|---------|
| `ExecutionPlanCoordinationGrain.cs` | Implementation |
| `EXECUTIONPLANCOORDINATIONGRAIN.md` | Full documentation |
| `CREATION_SUMMARY.md` | Creation summary |
| `QUICK_REFERENCE.md` | This file |

## Performance Tips

1. **Batch Operations**: Update multiple durations before re-coordinating
2. **Selective Queries**: Use `GetTaskExecutionStatusAsync()` for single queries
3. **Periodic Reset**: Clear old periods with `ResetAsync()`
4. **Status Caching**: Cache `CoordinationStatus` between updates

## Tested Features

✅ Plan initialization
✅ Task status tracking
✅ Completion marking
✅ Cascade effects
✅ Deadline detection
✅ Rebalancing
✅ Status queries
✅ Error handling
✅ Reset functionality

---

**Build Status**: ✅ Success
**Documentation**: ✅ Complete  
**Ready for Use**: ✅ Yes
