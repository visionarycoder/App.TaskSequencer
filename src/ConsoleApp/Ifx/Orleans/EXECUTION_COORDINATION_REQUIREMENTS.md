# Execution Coordination & Iterative Refinement Requirements

## Overview

This document specifies the requirements for the three core capabilities of the TaskSequencer execution planning system:

1. **Dependency Chain Analysis & Task Grouping** (Phase 2-3)
2. **Iterative Execution Order Arrangement** (ExecutionPlanCoordinator - Phase 5)
3. **Timing Adjustment & Convergence** (Refinement Loop - Phase 5)

These components work together to transform a loaded manifest into a feasible, deadline-compliant execution plan.

---

## Part 1: Dependency Chain Analysis & Task Grouping

### 1.1 Dependency Chain Resolution

**Purpose**: Understand the prerequisite relationships between tasks and establish a valid execution sequence.

**Input**:
- `IReadOnlyList<ExecutionEventDefinition>` - All tasks from manifest
- `IReadOnlyList<IntakeEventRequirement>` - Intake deadline constraints
- `DependencyResolver` service with prerequisite resolution logic

**Process**:

#### Step 1.1.1: Identify All Task Dependencies
For each `ExecutionEventDefinition`:
- Extract `PrerequisiteTaskIds` collection
- For each prerequisite task ID:
  - Find all execution events for that prerequisite task
  - Determine valid execution events:
    - **Same-day execution**: Prerequisite event must occur earlier in the day
    - **Earlier-day execution**: Any execution of prerequisite on earlier day is valid
    - **Later-day execution**: NOT valid (would require previous period continuation)

#### Step 1.1.2: Latest Feasible Prerequisite Selection
For each prerequisite relationship:
- Select the LATEST feasible prerequisite event
- Rationale: Allows later start time for dependent task, maximizing scheduling flexibility
- Implementation: `DependencyResolver.ResolvePrerequisites()`

#### Step 1.1.3: Circular Dependency Detection
Before proceeding:
- Perform circular dependency check on resolved prerequisite graph
- Algorithm: DFS from each task, detect back edges
- Result: Either confirmed DAG or list of circular dependency paths
- Action: Fail fast with detailed error message if cycles detected

#### Step 1.1.4: Build Dependency Graph
Create a directed acyclic graph (DAG) structure:
```csharp
interface IDependencyGraph
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToPrerequisites { get; }
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToDependents { get; }
    IReadOnlyList<string> TopologicalOrder { get; }
    int ComputeDepthFromRoot(string taskId);
    int ComputeDepthToLeaf(string taskId);
    IReadOnlyList<string> GetCriticalPath();
}
```

**Output of Step 1.1**:
- Validated DAG structure
- Topological sort order
- Depth metrics for each task

---

### 1.2 Task Stratification (Level Assignment)

**Purpose**: Assign execution levels to enable parallel execution where dependencies allow.

**Input**:
- Validated `IDependencyGraph`
- Topological order

**Process**:

#### Step 1.2.1: Level Assignment Algorithm
Assign each task to a stratification level based on longest path from any root:

```
ALGORITHM: AssignStratificationLevels(DAG)
  Initialize: level[task] = 0 for all tasks

  FOR each task in topological_order:
    IF task has no prerequisites:
      level[task] = 0
    ELSE:
      max_prereq_level = MAX(level[prereq] for all prereq in prerequisites)
      level[task] = max_prereq_level + 1

  RETURN: Dictionary<string, int> task_to_level
```

**Result**: 
- All tasks at Level 0 can execute in parallel
- All tasks at Level N depend only on Level 0..N-1 tasks
- Maximum level = longest dependency chain depth

#### Step 1.2.2: Level Grouping
Group tasks by assigned level:
```csharp
Dictionary<int, IReadOnlyList<string>> levelGroups = 
    tasks.GroupBy(t => stratificationLevel[t])
         .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList());
```

**Benefits**:
- Level 0: All independent tasks can execute in parallel
- Level N > 0: Can execute after Level N-1 tasks complete
- Enables parallel grain execution for tasks at same level

**Output of Step 1.2**:
- Stratification levels for all tasks
- Grouped task collections by level

---

### 1.3 Task Execution Grouping Strategy

**Purpose**: Group tasks by similar execution patterns to optimize scheduling and resource allocation.

**Input**:
- Stratified task levels
- Task definitions with duration and scheduling info
- Dependency relationships

**Process**:

#### Step 1.3.1: Categorize Execution Patterns

Analyze each task's dependency pattern:

1. **Independent Group** (Level 0, No dependents)
   - Tasks with no prerequisites and no dependent tasks
   - Can execute immediately and in parallel
   - Example: Initial data gathering, startup tasks

2. **Sequential Chain Group** (Linear dependency)
   - Task A → Task B → Task C (each with single dependency)
   - Must execute in strict order
   - Example: Report generation → validation → upload

3. **Fan-Out Group** (One-to-many)
   - Single task with multiple dependent tasks
   - Example: Master data load → multiple parallel processes

4. **Fan-In Group** (Many-to-one)
   - Multiple prerequisite tasks feeding single task
   - Example: Multiple validations → consolidation task

5. **Complex DAG Group** (Mixed patterns)
   - Tasks with multiple prerequisites from multiple paths
   - Example: Reconciliation task with inputs from multiple branches

#### Step 1.3.2: Group Creation Algorithm

```
ALGORITHM: CreateExecutionGroups(stratifiedTasks, dependencies)
  groups = []
  processed = set()

  FOR each level in order:
    level_tasks = stratifiedTasks[level]
    subgroups = []

    FOR each task in level_tasks (not in processed):
      pattern = AnalyzePattern(task, dependencies)

      SWITCH pattern:
        CASE Independent:
          Add to independent_group (batch multiple)
        CASE SequentialChain:
          Create chain_group with all linked tasks
        CASE FanOut:
          Create fanout_group with root + dependents
        CASE FanIn:
          Add to current_fanin_accumulator
        CASE Complex:
          Create isolated_group

      Add task to processed

    groups.AddAll(subgroups)

  RETURN: groups
```

#### Step 1.3.3: Group Scheduling Characteristics

For each execution group, determine:

| Group Type | Parallelizable | Order Enforced | Resource Model |
|-----------|--------|--------|--------|
| Independent | Yes | No | Parallel execution at same time |
| Sequential | No | Yes | Serial execution in order |
| Fan-Out | Yes (dependents) | No | Root then parallel dependents |
| Fan-In | Yes (prerequisites) | No | Parallel prerequisites then root |
| Complex | Partial | Mixed | Level-based parallelization |

**Output of Step 1.3**:
- Classification of each task by execution pattern
- Grouping recommendations
- Parallelization hints for scheduler

---

### 1.4 Criticality Analysis

**Purpose**: Identify tasks on the critical path and calculate scheduling slack.

**Input**:
- Dependency DAG
- Task durations
- Intake deadline requirements

**Process**:

#### Step 1.4.1: Forward Pass (Earliest Start/End Times)

```
ALGORITHM: ComputeEarliestTimes(DAG, durations, startDate)
  earliest_start[task] = startDate for all tasks

  FOR each task in topological_order:
    IF task has prerequisites:
      // Earliest start = MAX completion time of prerequisites
      earliest_start[task] = MAX(
        earliest_start[prereq] + duration[prereq] 
        for all prereq in prerequisites
      )

    earliest_end[task] = earliest_start[task] + duration[task]

  RETURN: (earliest_start, earliest_end)
```

#### Step 1.4.2: Backward Pass (Latest Start/End Times)

```
ALGORITHM: ComputeLatestTimes(DAG, durations, deadline)
  latest_end[task] = deadline for all tasks

  FOR each task in reverse_topological_order:
    IF task has dependents:
      // Latest end = MIN start time of dependents
      latest_end[task] = MIN(
        latest_start[dependent]
        for all dependent in dependents
      )

    latest_start[task] = latest_end[task] - duration[task]

  RETURN: (latest_start, latest_end)
```

#### Step 1.4.3: Slack Calculation

For each task:
```csharp
slack[task] = latest_start[task] - earliest_start[task];

// Alternatively:
slack[task] = latest_end[task] - earliest_end[task];

// Critical path: tasks where slack = 0
is_critical[task] = (slack[task] == 0);
```

#### Step 1.4.4: Critical Path Identification

```
ALGORITHM: IdentifyCriticalPath(DAG, slack, durations)
  critical_tasks = [task for task in all_tasks if slack[task] = 0]

  // Critical path = longest sequence of critical tasks
  // Start from root critical tasks, follow through critical dependents

  FOR each root_task in critical_tasks (no prerequisites):
    path = TraceLinearPath(root_task, critical_tasks, dependents)
    critical_paths.Add(path)

  // Critical path completion time = MAX completion time
  critical_completion = MAX(latest_end[task] for task in critical_tasks)

  RETURN: (critical_tasks, critical_paths, critical_completion)
```

**Output of Step 1.4**:
- Slack time for each task
- Critical task identification
- Critical path sequences
- Critical path completion time

---

## Part 2: Iterative Execution Order Arrangement

### 2.1 ExecutionPlanCoordinator Initialization

**Purpose**: Set up the grain-based coordination system for iterative refinement.

**Input**:
- All execution events and definitions
- Planning period (start and end date)
- Resolved dependency graph
- Task stratification and grouping

**Process**:

#### Step 2.1.1: Grain Creation
The `ExecutionPlanCoordinator` initializes:

```csharp
ALGORITHM: InitializeCoordination(events, period)
  coordinator = IExecutionPlanCoordinatorGrain

  // Create a task grain for each execution event
  FOR each event in events:
    taskGrain = grainFactory.GetGrain<IExecutionTaskGrain>(event.TaskIdString)
    await taskGrain.InitializeAsync(
      taskDefinition: event,
      duration: event.Duration,
      scheduledStartTime: event.ScheduledTime,
      planningPeriodStart: period.Start
    )

    coordinatorGrains[event.TaskId] = taskGrain

  // Store initial execution instances
  FOR each event in events:
    instance = ExecutionInstanceEnhanced(
      Id: index,
      TaskIdString: event.TaskIdString,
      ScheduledStartTime: event.ScheduledTime.ApplyToDate(period.Start),
      Duration: event.Duration,
      PrerequisiteTaskIds: event.PrerequisiteTaskIds,
      Status: ExecutionStatus.AwaitingPrerequisites,
      IsValid: true
    )
    executionInstances[event.TaskId] = instance

  RETURN: coordinator ready for refinement
```

#### Step 2.1.2: Initial Execution Order
Establish initial task order based on topological sort:

```csharp
// From dependency DAG
executionOrder = topologicalSort(dependencyDAG);

// OR: stratified execution (level-based)
FOR each level in stratifiedLevels:
  executionOrder.AddRange(tasksAtLevel[level])
```

**Output of Step 2.1**:
- All task grains initialized
- Initial execution order established
- Coordination state ready

---

### 2.2 Iterative Refinement Loop

**Purpose**: Iteratively adjust task start times until all deadlines are met or maximum iterations reached.

**Input**:
- Initialized coordinator grains
- Initial execution instances
- Deadline requirements

**Process**:

#### Step 2.2.1: Single Iteration Structure

```csharp
ALGORITHM: SingleRefinementIteration(coordinator)
  iteration_number++
  changes_made = 0
  all_valid = true

  // PHASE A: Gather prerequisite completions
  prerequisite_completions = {}
  FOR each (taskId, instance) in executionInstances:
    FOR each prereq_task_id in instance.PrerequisiteTaskIds:
      prereq_completion = executionInstances[prereq_task_id].PlannedCompletionTime
      prerequisite_completions[taskId].Add(prereq_completion)

  // PHASE B: Update start times (can be parallel)
  FOR each taskGrain in coordinatorGrains (in parallel):
    new_start_time = await taskGrain.UpdateStartTimeAsync(
      prerequisiteCompletions: prerequisite_completions[taskId],
      scheduledTime: executionInstances[taskId].ScheduledStartTime
    )

    IF new_start_time != executionInstances[taskId].FunctionalStartTime:
      executionInstances[taskId].FunctionalStartTime = new_start_time
      executionInstances[taskId].PlannedCompletionTime = new_start_time + duration[taskId]
      changes_made++

  // PHASE C: Validate deadlines
  FOR each (taskId, instance) in executionInstances:
    (is_valid, status) = await taskGrain.ValidateDeadlineAsync(
      plannedCompletion: instance.PlannedCompletionTime,
      deadline: instance.RequiredEndTime
    )

    instance.IsValid = is_valid
    instance.Status = status

    IF !is_valid:
      all_valid = false

  // PHASE D: Check convergence
  converged = (changes_made == 0) OR all_valid

  RETURN: (converged, changes_made, all_valid)
```

#### Step 2.2.2: Update Start Time Logic (Per Task Grain)

```csharp
ALGORITHM: UpdateStartTimeAsync(taskGrain, prerequisiteCompletions, scheduledTime)
  IF prerequisiteCompletions.IsEmpty:
    // No prerequisites, use scheduled time
    new_start = scheduledTime
  ELSE:
    // Start = MAX(scheduled, latest prerequisite completion)
    latest_prereq_completion = MAX(prerequisiteCompletions)
    new_start = MAX(scheduledTime, latest_prereq_completion)

  taskGrain.FunctionalStartTime = new_start
  taskGrain.PlannedCompletionTime = new_start + taskGrain.Duration

  RETURN: new_start
```

#### Step 2.2.3: Deadline Validation (Per Task Grain)

```csharp
ALGORITHM: ValidateDeadlineAsync(taskGrain)
  IF plannedCompletion <= deadline:
    status = ExecutionStatus.ReadyToExecute
    slack = deadline - plannedCompletion
    RETURN: (true, status)
  ELSE:
    status = ExecutionStatus.DeadlineMiss
    deficit = plannedCompletion - deadline
    RETURN: (false, status)
```

**Output of Single Iteration**:
- Updated start times for all tasks
- Validation status for all tasks
- Change count
- Convergence indicator

---

### 2.3 Iteration Loop Control

**Purpose**: Manage the overall refinement iterations until convergence or termination.

**Input**:
- Initialized coordinator
- Maximum iteration limit (default: 100)

**Process**:

```csharp
ALGORITHM: ExecuteRefinementLoop(coordinator, maxIterations)
  iteration = 0
  converged = false

  WHILE iteration < maxIterations AND NOT converged:
    (converged, changesCount, allValid) = 
        SingleRefinementIteration(coordinator)

    iteration++

    // Termination conditions:
    IF converged:
      // All tasks valid OR no changes made
      break

    IF allValid AND changesCount == 0:
      // Bonus: Early convergence detection
      converged = true
      break

    // Log iteration progress
    LogProgress(
      iteration: iteration,
      changes: changesCount,
      valid_tasks: CountValidTasks(),
      invalid_tasks: CountInvalidTasks()
    )

  // Forced convergence if max iterations exceeded
  IF iteration >= maxIterations:
    converged = true
    status = "CONVERGED_BY_MAX_ITERATIONS"
  ELSE:
    status = "CONVERGED_ORGANICALLY"

  RETURN: (converged, iteration, status)
```

#### Convergence Conditions

A refinement loop converges when ANY of:

1. **All tasks valid**: Every task meets its deadline
2. **No changes**: No start time changed in last iteration
3. **Max iterations**: Reached `MAX_ITERATIONS` (force convergence)

#### Convergence Metrics

Track during loop:
- Total iterations performed
- Tasks with deadline misses (if any)
- Average changes per iteration
- Convergence time

**Output of Iteration Loop**:
- Final converged status
- Iteration count
- Task validation summary
- Deadline conflict list (if any)

---

## Part 3: Timing Adjustment & Convergence

### 3.1 Timing Adjustment Mechanisms

**Purpose**: Adjust task start times to maximize schedule feasibility and deadline compliance.

#### 3.1.1: Prerequisite-Based Adjustment

When a task has prerequisites:

```
Task T depends on Task P

Original Constraint:
  T.ScheduledStart = [user-specified time]

Adjusted by Prerequisites:
  IF P.PlannedCompletion > T.ScheduledStart:
    T.FunctionalStart = P.PlannedCompletion
    (push T later to wait for P)
  ELSE:
    T.FunctionalStart = T.ScheduledStart
    (T can start as scheduled)
```

**Effect**: Maintains prerequisite ordering while minimizing delay.

#### 3.1.2: Deadline-Aware Adjustment

When a task would miss its deadline:

```
Deadline Conflict Scenario:
  T.FunctionalStart + T.Duration > T.Deadline

Possible Adjustments (in priority order):
  1. Can't adjust earlier (prerequisites prevent)
     → CONFLICT: Mark as DeadlineMiss

  2. Can adjust prerequisite tasks earlier:
     → Trigger rebalance of prerequisite chain
     → Re-run iteration to allow P to start earlier

  3. Can shorten duration (if flexible):
     → May require action item or escalation
     → Outside scope of this algorithm
```

#### 3.1.3: Cascading Adjustments

When one task changes, effects cascade:

```
SEQUENCE: Task B changes start time
  1. All tasks depending on B must reconsider their start times
  2. Prerequisite chain propagates forward through graph
  3. May trigger deadline conflicts downstream
  4. Next iteration addresses new conflicts
```

**Implementation**: Queue-based traversal (already implemented in `CascadeDurationChangesAsync`)

---

### 3.2 Convergence Criteria

**Purpose**: Define precisely when refinement is complete.

#### 3.2.1: Optimal Convergence

```csharp
CONDITION: OptimalConvergence
  All tasks meet deadlines:
    FOR ALL tasks: 
      PlannedCompletionTime <= RequiredDeadline

  AND no further changes needed:
    FOR ALL tasks:
      FunctionalStartTime == PreviousIteration.FunctionalStartTime
```

**State**: CONVERGED_SUCCESS

#### 3.2.2: Partial Convergence with Conflicts

```csharp
CONDITION: PartialConvergence
  Some tasks meet deadlines, others don't:
    EXISTS task where PlannedCompletionTime > RequiredDeadline
    EXISTS task where PlannedCompletionTime <= RequiredDeadline

  AND no further changes can be made:
    No start time changes in last N iterations
```

**State**: CONVERGED_WITH_CONFLICTS
**Action**: Return conflict list for resolution

#### 3.2.3: Force Convergence

```csharp
CONDITION: ForceConvergence
  Reached maximum iteration limit:
    iteration >= MAX_ITERATIONS

  OR reached time budget:
    elapsed_time >= TIME_BUDGET
```

**State**: CONVERGED_BY_LIMIT
**Action**: Return best-effort plan; escalate conflicts

---

### 3.3 Convergence Behavior & Guarantees

#### 3.3.1: Monotonicity Guarantee

The iteration algorithm maintains monotonicity:

```
PROPERTY: Non-Increasing Invalid Task Count

Iteration 0: invalid_count = I0
Iteration 1: invalid_count = I1, where I1 <= I0
Iteration 2: invalid_count = I2, where I2 <= I1
...
Iteration N: invalid_count = IN, where IN <= I(N-1)

PROOF SKETCH:
  Each iteration adjusts start times to satisfy prerequisites.
  This moves tasks LATER (never earlier by design).
  Later start times can only increase deadline compliance.
  Once valid, a task remains valid (no backward movement).
```

#### 3.3.2: Convergence Guarantee (Under Normal Conditions)

```
THEOREM: Convergence in finite iterations

Given:
  - Acyclic dependency graph (DAG property)
  - No retroactive scheduling (always move tasks forward/later)
  - Monotonic improvement (tracked invalid count)

Conclusion:
  Algorithm MUST converge in <= N iterations
  where N = number of tasks (worst case: chain of N tasks)
```

#### 3.3.3: Known Non-Convergence Scenarios

```
SCENARIO 1: Impossible Deadlines
  Task requires 8 hours but deadline is 4 hours away
  Result: Flags as DeadlineMiss; impossible to satisfy
  Action: Manual intervention required

SCENARIO 2: Circular Timing Dependencies (rare)
  Task A depends on B depends on A
  Result: Detected in Phase 2 (circular dependency check)
  Preventive: Circular dependencies blocked before Phase 5

SCENARIO 3: Missing Precedence Information
  Task A depends on Task B but B has no execution in plan
  Result: Cannot satisfy prerequisite
  Action: Phase 2 validation should detect; if missed, marked Invalid
```

---

### 3.4 Final Execution Plan Construction

**Purpose**: Build final plan after convergence with all metrics.

**Input**:
- Final task states after iteration loop
- Convergence status and iteration count
- Deadline validation results

**Process**:

```csharp
ALGORITHM: ConstructFinalExecutionPlan(coordinator, convergenceInfo)
  plan = new ExecutionPlan()

  // Add all task instances
  FOR each (taskId, instance) in executionInstances:
    finalInstance = ExecutionInstanceEnhanced(
      Id: instance.Id,
      TaskIdString: instance.TaskIdString,
      TaskName: instance.TaskName,
      ScheduledStartTime: instance.ScheduledStartTime,
      FunctionalStartTime: instance.FunctionalStartTime,
      RequiredEndTime: instance.RequiredEndTime,
      Duration: instance.Duration,
      PlannedCompletionTime: instance.PlannedCompletionTime,
      Status: instance.Status,
      IsValid: instance.IsValid
    )

    plan.Instances.Add(finalInstance)

  // Calculate metrics
  plan.Metrics = new ExecutionPlanMetrics
  {
    TotalTaskCount: executionInstances.Count,
    ValidTaskCount: CountValidTasks(),
    InvalidTaskCount: CountInvalidTasks(),
    CriticalPathCompletion: CalculateCriticalPathCompletion(),
    ConflictingTasks: GetConflictingTaskIds(),
    ConvergenceStatus: convergenceInfo.Status,
    IterationCount: convergenceInfo.IterationCount,
    PlanningStartTime: DateTime.UtcNow - convergenceInfo.ElapsedTime,
    PlanningEndTime: DateTime.UtcNow
  }

  RETURN: plan
```

**Output of Final Plan**:
- Complete execution schedule with all timings
- Task status for each task
- Metrics and convergence information
- Conflict list (if any)

---

## Part 4: Integration & Orchestration

### 4.1 End-to-End Workflow

The three parts work together:

```
PHASE 2: DEPENDENCY ANALYSIS
  Input: Raw execution events
    ↓
  [1.1] Resolve dependency chains
  [1.1] Build dependency graph (DAG)
    ↓
  Output: Validated DAG with topological order

PHASE 3: TASK GROUPING & STRATIFICATION
  Input: Dependency DAG
    ↓
  [1.2] Assign stratification levels
  [1.3] Group by execution pattern
  [1.4] Calculate criticality and slack
    ↓
  Output: Stratified, grouped tasks with criticality info

PHASE 4: INITIAL TIMING (not covered here, but prerequisite)
  Input: Grouped tasks
    ↓
  Calculate initial start/end times
  Check initial deadline feasibility
    ↓
  Output: Initial execution plan with known conflicts

PHASE 5: ITERATIVE REFINEMENT
  Input: Initial execution plan
    ↓
  [2.1] Initialize coordination grains
  [2.2] Execute refinement iterations
    [3.1] Apply timing adjustments
    [3.2] Check convergence
    [3.3] Collect metrics
  [3.4] Construct final plan
    ↓
  Output: Final executable plan with all metrics
```

### 4.2 Error Handling Strategy

| Error Type | Detection Point | Handling | Recovery |
|-----------|---------|----------|----------|
| Circular Dependencies | Phase 2 | Fail fast | User reviews manifest |
| Missing Prerequisites | Phase 2 | Warn, continue | Mark task as Invalid |
| Impossible Deadlines | Phase 5 (iteration) | Flag conflict | Manual resolution |
| Orphaned Tasks | Phase 1-2 | Warn | Mark as DeadlineMiss |

---

## Part 5: Success Criteria

### 5.1 Functional Requirements Met

- ✅ Dependency chains fully resolved and validated
- ✅ Task stratification assigned to all tasks
- ✅ Execution order iteratively arranged by coordinator
- ✅ Start times adjusted to satisfy prerequisites
- ✅ Deadlines validated for each task
- ✅ Convergence detected and reported
- ✅ Final plan with metrics generated

### 5.2 Performance Requirements

- ⏱️ Dependency analysis: < 100ms for typical manifest (< 1000 tasks)
- ⏱️ Single iteration: < 50ms (including grain calls)
- ⏱️ Full refinement loop: < 2 seconds for 100% convergence
- ⏱️ Memory: < 50MB for 1000-task plan

### 5.3 Quality Requirements

- 📊 All deadline conflicts accurately identified
- 📊 Critical path correctly calculated
- 📊 Slack time accurately computed
- 📊 No task left without valid status
- 📊 All metrics consistent and traceable

---

## Part 6: Data Structures & Contracts

### 6.1 Core Interfaces Needed

```csharp
/// Phase 2-3: Dependency and grouping analysis
interface IDependencyResolver
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> ResolvePrerequisites(
        IReadOnlyList<ExecutionEventDefinition> events);

    bool HasCircularDependencies(IDependencyGraph dag, out IReadOnlyList<IReadOnlyList<string>> cycles);

    IReadOnlyList<string> TopologicalSort(IDependencyGraph dag);
}

interface ITaskStratifier
{
    Dictionary<string, int> AssignStratificationLevels(IDependencyGraph dag);
    Dictionary<int, IReadOnlyList<string>> GroupByLevel(Dictionary<string, int> levels);
}

interface ICriticalityAnalyzer
{
    (Dictionary<string, DateTime>, Dictionary<string, DateTime>) ComputeEarliestTimes(
        IDependencyGraph dag, 
        IReadOnlyDictionary<string, ExecutionDuration> durations);

    (Dictionary<string, DateTime>, Dictionary<string, DateTime>) ComputeLatestTimes(
        IDependencyGraph dag, 
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime deadline);

    Dictionary<string, TimeSpan> CalculateSlack(
        Dictionary<string, DateTime> earliestStart,
        Dictionary<string, DateTime> latestStart);

    IReadOnlyList<string> IdentifyCriticalPath(Dictionary<string, TimeSpan> slack);
}

/// Phase 5: Iterative refinement coordination
interface IExecutionPlanCoordinator
{
    Task<ExecutionPlan> ExecuteRefinementLoopAsync(
        IReadOnlyList<ExecutionInstanceEnhanced> initialPlan,
        int maxIterations,
        CancellationToken ct);

    Task<(bool converged, int changes, bool allValid)> SingleRefinementIterationAsync(
        CancellationToken ct);

    Task<ConvergenceInfo> GetConvergenceInfoAsync();
}

/// Phase 5: Per-task grain contracts
interface IExecutionTaskGrain
{
    Task UpdateStartTimeAsync(
        IReadOnlyDictionary<string, DateTime> prerequisiteCompletions);

    Task<(bool isValid, ExecutionStatus status)> ValidateDeadlineAsync();
}
```

### 6.2 Data Models

```csharp
// Dependency graph structure
interface IDependencyGraph
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToPrerequisites { get; }
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToDependents { get; }
    IReadOnlyList<string> TopologicalOrder { get; }
}

// Stratification result
record StratificationResult
{
    Dictionary<string, int> TaskToLevel { get; init; }
    Dictionary<int, IReadOnlyList<string>> LevelToTasks { get; init; }
    int MaxLevel { get; init; }
}

// Convergence information
record ConvergenceInfo
{
    bool Converged { get; init; }
    int IterationCount { get; init; }
    ConvergenceReason Reason { get; init; } // Organic, MaxIterations, TimeLimit
    List<string> ConflictingTasks { get; init; }
    DateTime CompletionTime { get; init; }
}

enum ConvergenceReason
{
    Organic,           // All valid or no changes
    MaxIterations,     // Hit iteration limit
    TimeLimit,         // Hit time budget
    ForceConverged     // Manual force
}
```

---

## Summary

This requirements document specifies three core capabilities working together:

1. **Dependency Chain Analysis & Task Grouping**
   - Resolve prerequisite relationships into a DAG
   - Assign stratification levels for parallel execution
   - Group tasks by execution pattern
   - Analyze criticality and calculate slack

2. **Iterative Execution Order Arrangement**
   - Initialize coordination grains for all tasks
   - Execute refinement iterations in parallel where possible
   - Update start times based on prerequisite completions
   - Validate deadlines for each task

3. **Timing Adjustment & Convergence**
   - Apply prerequisite-based adjustments (push tasks later as needed)
   - Apply deadline-aware adjustments (mark conflicts)
   - Monitor for convergence (organic or forced)
   - Guarantee monotonic improvement and finite termination

All components integrate within the five-phase workflow to transform a manifest into a complete, deadline-compliant execution plan.
