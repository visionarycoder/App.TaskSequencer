# Manifest Loading & Orchestration Requirements

## Overview

This document specifies the complete workflow for processing a loaded manifest through dependency analysis, task grouping, and iterative time slot refinement using the ExecutionPlanCoordinator.

## Workflow Phases

### Phase 1: Manifest Loading & Validation

**Input**: 
- CSV manifest file containing task definitions and intake event requirements

**Process**:
- Parse manifest using `ManifestCsvParser`
- Transform into domain models using `ManifestTransformer`
- Validate task definitions and their properties
- Validate recurrence patterns (new feature)
- Build intake event requirements

**Output**:
- `TaskDefinitionEnhanced` collection
- `IntakeEventDefinition` collection  
- `IntakeEventManifest` collection with recurrence patterns

**Key Components**:
- `ManifestCsvParser` - Parse CSV content
- `ManifestTransformer` - Convert to domain models
- Validation services

---

### Phase 2: Dependency Chain Analysis

**Input**:
- `IReadOnlyList<ExecutionEventDefinition>` (all tasks)

**Process**:

#### 2.1 Dependency Resolution
Using `DependencyResolver`:

1. For each task, identify all prerequisite tasks
2. For each prerequisite, find all execution events for that task
3. Filter feasible prerequisites:
   - Same day: must occur earlier in the day
   - Earlier day: always feasible
   - Later day: not feasible (would need previous period)
4. Select the LATEST feasible prerequisite for each dependency

#### 2.2 Dependency Validation
- Detect circular dependencies (must fail)
- Detect missing prerequisites (should warn)
- Detect unreachable tasks (tasks with unsatisfiable prerequisites)

#### 2.3 Dependency Chain Output
- Directed Acyclic Graph (DAG) of task dependencies
- Topological sort order
- Critical path analysis

**Key Components**:
- `DependencyResolver` - Resolve prerequisites
- Circular dependency detector
- Topological sorter

**Output**:
- Dependency graph structure
- Task execution order (topological sort)
- Critical path timeline

---

### Phase 3: Task Grouping & Stratification

**Input**:
- Dependency DAG from Phase 2
- Task execution order

**Process**:

#### 3.1 Level Assignment (Stratification)
Assign each task to a level based on longest path from root:

```
Level 0 (Roots):    Tasks with no prerequisites
                    ↓
Level 1:            Tasks that only depend on Level 0
                    ↓
Level 2:            Tasks that only depend on Level 0-1
                    ↓
...
Level N:            Tasks with longest dependency chain
```

#### 3.2 Grouping Strategy
Group tasks by execution strategy:

1. **Independent Group**: No dependencies (can execute in parallel)
2. **Sequential Chain Groups**: Linear dependency chains
3. **Fan-Out Groups**: One task with multiple dependents
4. **Fan-In Groups**: Multiple tasks feeding one task
5. **Complex DAGs**: Mixed dependency patterns

#### 3.3 Criticality Analysis
- Identify critical tasks (on critical path)
- Identify slack time for non-critical tasks
- Calculate task priority/urgency

**Output**:
- Task stratification levels
- Grouped task sets
- Criticality indicators
- Slack time calculations

---

### Phase 4: Initial Timing Calculation

**Input**:
- Grouped tasks
- Task durations
- Scheduled times (from manifest)
- Intake deadline requirements

**Process**:

#### 4.1 Initial Schedule Assignment
For each task in topological order:

1. Get scheduled start time (from task definition)
2. If has prerequisites:
   - Calculate latest prerequisite completion time
   - Adjust start time = MAX(scheduled start, latest prereq completion)
3. Calculate completion time = start time + duration

#### 4.2 Deadline Checking (Initial)
For each task:
- Check if planned completion ≤ intake deadline requirement
- Flag deadline conflicts
- Calculate slack time = deadline - completion

#### 4.3 Initial Execution Plan
- `ExecutionInstanceEnhanced` for each task
- Status: Scheduled, AwaitingPrerequisites, or Invalid
- Planned start/end times

**Output**:
- `IReadOnlyList<ExecutionInstanceEnhanced>` (initial plan)
- Deadline conflict list
- Metrics (valid tasks, conflicts, etc.)

---

### Phase 5: Iterative Time Slot Refinement (ExecutionPlanCoordinator)

**Input**:
- Initial execution plan from Phase 4
- All execution events
- Deadline requirements

**Process**:

#### 5.1 Coordinator Initialization
The `IExecutionPlanCoordinatorGrain`:

1. Creates `IExecutionTaskGrain` for each task
2. Initializes each grain with task definition and duration
3. Starts iterative refinement loop

#### 5.2 Single Iteration (RefineTimeSlotIterationAsync)

**Step 1: Gather Current State**
- Collect all task planned completions
- Build prerequisite completion map

**Step 2: Update All Tasks**
For each task grain (parallel):
- Call `UpdateStartTimeAsync(prerequisiteCompletions)`
- Calculate new start time = MAX(scheduled, latest prereq completion)
- Calculate new completion time = start time + duration
- Return new start time

**Step 3: Validate Deadlines**
For each task grain:
- Call `ValidateDeadlineAsync()`
- Check if completion ≤ deadline
- Mark as Valid or DeadlineMiss

**Step 4: Check Convergence**
- Compare new times to previous iteration
- All tasks valid? → Converged = True
- Any changes? → Converged = False
- Exceeded MAX_ITERATIONS? → Force convergence

**Output of Single Iteration**:
- `(HasConverged: bool, UpdateCount: int)`
- Updated task times
- Validation status

#### 5.3 Iteration Loop Control
```csharp
iteration = 0
while (!converged && iteration < MAX_ITERATIONS)
{
    (converged, updateCount) = RefineTimeSlotIterationAsync()
    iteration++
}
```

**Convergence Conditions**:
1. All tasks meet deadline requirements
2. No start times changed in last iteration
3. Maximum iterations reached

#### 5.4 Final Execution Plan
After convergence:
- Build final `ExecutionPlan` from all grains
- Include metrics:
  - Valid task count
  - Invalid task count (deadline misses)
  - Critical path completion
  - List of deadline miss tasks
  - Iteration count

**Output**:
- Final `ExecutionPlan`
- Status for each task
- Convergence status

---

## Data Models & Structures

### Input Models (Phase 1)
```csharp
// From CSV Manifest
TaskDefinitionManifest
ExecutionDurationManifest
IntakeEventManifest (with RecurrencePattern)

// Transformed to Domain Models
TaskDefinitionEnhanced
ExecutionDuration
ExecutionEventDefinition
IntakeEventRequirement
```

### Processing Models (Phases 2-3)
```csharp
// Dependency Analysis
DependencyGraph {
    TaskId -> ISet<PrerequisiteTaskIds>
    CriticalPath: List<TaskId>
    Levels: Dictionary<int, List<TaskId>>
}

// Task Groups
TaskGroup {
    GroupId: string
    Strategy: GroupStrategy (Independent, Sequential, FanOut, FanIn, Complex)
    Tasks: IList<TaskId>
    CriticalPath: bool
    SlackTime: TimeSpan
}
```

### Execution Models (Phases 4-5)
```csharp
ExecutionInstanceEnhanced {
    TaskId: string
    ScheduledStartTime: DateTime
    FunctionalStartTime: DateTime?
    RequiredEndTime: DateTime?
    PlannedCompletionTime: DateTime
    Duration: ExecutionDuration
    Status: ExecutionStatus
    IsValid: bool
    PrerequisiteTaskIds: IReadOnlySet<string>
}

ExecutionPlan {
    IncrementId: string
    IncrementStart: DateTime
    IncrementEnd: DateTime
    Tasks: IReadOnlyList<ExecutionInstanceEnhanced>
    TaskChain: IReadOnlyList<string>
    TotalValidTasks: int
    TotalInvalidTasks: int
    CriticalPathCompletion: DateTime?
    DeadlineMisses: IReadOnlyList<string>
    DSTWarnings: IReadOnlyList<string>
}
```

---

## Orchestration Flow Diagram

```
┌─────────────────────────────────────────────────────────┐
│ Phase 1: Manifest Loading & Validation                  │
│ Input: CSV Manifest File                                │
│ Output: Task & Intake Event Definitions                 │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│ Phase 2: Dependency Analysis                            │
│ - Identify prerequisites per task                       │
│ - Build dependency DAG                                  │
│ - Detect cycles & missing dependencies                  │
│ Output: Dependency Graph, Topological Sort, Critical Path
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│ Phase 3: Task Grouping & Stratification                 │
│ - Assign levels (stratification)                        │
│ - Group by execution strategy                           │
│ - Calculate criticality                                 │
│ Output: Stratified Task Groups, Criticality Indicators  │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│ Phase 4: Initial Timing Calculation                     │
│ - Calculate start/end times                             │
│ - Check deadlines (initial)                             │
│ - Identify conflicts                                    │
│ Output: Initial ExecutionPlan, Conflict List            │
└──────────────────┬──────────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────────┐
│ Phase 5: Iterative Refinement (ExecutionPlanCoordinator)│
│                                                         │
│ ┌─────────────────────────────────────────────────┐   │
│ │ For each iteration (until converged):           │   │
│ │ 1. Gather current completion times             │   │
│ │ 2. Update all task start times (parallel)      │   │
│ │ 3. Validate deadlines for all tasks            │   │
│ │ 4. Check convergence:                          │   │
│ │    - All valid? → Converged = True             │   │
│ │    - No changes? → Converged = True            │   │
│ │    - Max iterations? → Force convergence       │   │
│ └─────────────────────────────────────────────────┘   │
│                                                         │
│ Output: Final ExecutionPlan with metrics               │
└──────────────────┬──────────────────────────────────────┘
                   │
                   ▼
         ┌─────────────────────┐
         │ Final ExecutionPlan  │
         │ Ready for Execution  │
         └─────────────────────┘
```

---

## Key Algorithms

### Algorithm 1: Dependency Resolution
```
For each ExecutionEvent E:
  For each PrerequisiteTaskId P in E.Prerequisites:
    Find all ExecutionEvents for task P
    Filter for feasible events:
      - Same day: occurs earlier
      - Earlier day: always feasible
    Select latest feasible event
    Add to resolved prerequisites
```

### Algorithm 2: Topological Sort
```
Graph: Task -> Dependencies

Result: Ordered list where each task appears before its dependents
Used for: Determining execution sequence
```

### Algorithm 3: Stratification (Leveling)
```
For each Task T:
  If T has no prerequisites:
    Level[T] = 0
  Else:
    Level[T] = 1 + MAX(Level[P] for P in prerequisites[T])

Result: Levels with independent tasks at each level
```

### Algorithm 4: Iterative Refinement
```
iteration = 0
converged = false

While not converged and iteration < MAX_ITERATIONS:

  completionTimes = {}
  For each Task T (parallel):
    Gather latest prerequisite completion time
    newStart = MAX(scheduledStart, latestPrereqCompletion)
    newEnd = newStart + duration
    completionTimes[T] = newEnd

  For each Task T:
    isValid = completionTimes[T] <= deadline[T]

  converged = ALL(isValid) OR iteration >= MAX_ITERATIONS - 1
  iteration++

Return convergence status and final times
```

---

## Convergence Criteria

A plan is considered converged when:

1. **Deadline Compliance**: All tasks meet their intake deadline requirements
   - `plannedCompletion ≤ requiredEndTime` for all tasks

2. **Stability**: Start times no longer change between iterations
   - New start times ≈ previous start times (within threshold)

3. **Iteration Limit**: Maximum iterations reached (failsafe)
   - Prevents infinite loops
   - Default: MAX_ITERATIONS = 100

**Convergence Result**:
- `HasConverged = True`: All conditions satisfied
- `HasConverged = False`: Convergence not reached (but plan is final)

---

## Error & Conflict Handling

### Validation Errors (Phase 2)
- **Circular Dependencies**: Detected by cycle detection algorithm
  - Result: Plan marked as invalid
  - Action: Require user to resolve cycles

- **Missing Prerequisites**: Prerequisite task not in manifest
  - Result: Warning, task may be unreachable
  - Action: Continue with partial dependencies

### Scheduling Conflicts (Phase 5)
- **Deadline Misses**: Task cannot complete by deadline
  - Detection: `plannedCompletion > requiredEndTime`
  - Result: Task marked as DeadlineMiss
  - Action: Reported in ExecutionPlan

- **Impossible Constraints**: Cannot resolve even after iterations
  - Result: Plan converges with DeadlineMiss tasks
  - Action: Identify critical bottlenecks for user

### Recovery Options
- **Duration Adjustment**: Reduce task duration
- **Deadline Extension**: Push out intake deadline
- **Parallelization**: Restructure dependencies
- **Resource Addition**: (Future) Add more capacity

---

## Metrics & Monitoring

### Phase Metrics
- **Phase 1**: Tasks parsed, validation errors, warnings
- **Phase 2**: Dependency count, critical path length, levels
- **Phase 3**: Group count, group sizes, criticality distribution
- **Phase 4**: Initial conflicts, conflict count by severity
- **Phase 5**: Convergence status, iteration count, final conflicts

### Execution Plan Metrics (Final Output)
```csharp
ExecutionPlan {
    TotalValidTasks: int              // Tasks meeting deadline
    TotalInvalidTasks: int            // Tasks with deadline miss
    CriticalPathCompletion: DateTime? // Longest chain duration
    DeadlineMisses: IReadOnlyList<string> // Tasks exceeding deadline
    IterationCount: int               // Refinement iterations
    HasConverged: bool                // Plan convergence status
}
```

### Monitoring Points
- Manifest parsing time
- Dependency analysis time
- Grouping completion time
- Initial planning time
- Each iteration duration
- Total convergence time
- Conflict resolution rate

---

## Recurrence Pattern Integration (NEW)

From the new recurrence support feature:

### Manifest Loading (Phase 1)
- Parse `RecurrencePattern` from CSV
- Support `MonthlyDays: "1,15,-1"` for multiple days
- Calculate all occurrences within planning period

### Dependency Analysis (Phase 2)
- Handle recurring task instances
- Resolve dependencies for each occurrence
- Support inter-occurrence dependencies

### Task Grouping (Phase 3)
- Group recurring instances by pattern
- Identify static vs. dynamic patterns

### Scheduling (Phases 4-5)
- Apply timing rules to all recurrence instances
- Ensure all instances meet their respective deadlines

---

## Integration Points

### Phase 1 → Phase 2
- Parsed tasks feed into dependency analyzer
- Validation errors halt progression

### Phase 2 → Phase 3
- Dependency DAG structure drives grouping
- Topological sort determines traversal order

### Phase 3 → Phase 4
- Task groups determine initial schedule assignment
- Criticality indicates priority for time adjustment

### Phase 4 → Phase 5
- Initial plan provides starting state for coordinator
- Deadline requirements guide convergence

### Coordinator (ExecutionPlanCoordinationGrain)
- Orchestrates Phase 5 iteratively
- Manages period-based coordination
- Cascades completion updates
- Supports rebalancing

---

## Success Criteria

A manifest processing workflow is successful when:

✅ **Phase 1**: All tasks parsed and validated
✅ **Phase 2**: Dependency DAG correctly built, no cycles
✅ **Phase 3**: Tasks properly stratified and grouped
✅ **Phase 4**: Initial plan created with deadline analysis
✅ **Phase 5**: Convergence reached within iteration limit
✅ **Final**: ExecutionPlan produced with valid or documented conflicts

---

## Summary

The complete workflow transforms a raw CSV manifest into an executable plan through five phases:

1. **Load & Validate** - Parse manifest into domain models
2. **Analyze Dependencies** - Build DAG and identify sequence
3. **Group Tasks** - Stratify and group by execution pattern
4. **Initial Schedule** - Calculate start/end times and deadlines
5. **Iteratively Refine** - Converge on valid schedule (or document conflicts)

The **ExecutionPlanCoordinator** orchestrates Phase 5, managing grain instances to iteratively refine start times until all tasks can complete within requirements or maximum iterations are reached.

The result is a deterministic **ExecutionPlan** ready for actual task execution, with clear metrics on validity, conflicts, and critical path information.
