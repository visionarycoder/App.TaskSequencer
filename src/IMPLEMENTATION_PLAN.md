# Execution Coordination Implementation Plan

## Overview
This plan outlines the implementation of the execution coordination and iterative refinement system based on the requirements documented in `EXECUTION_COORDINATION_REQUIREMENTS.md`.

The system transforms a loaded manifest through five phases into a deadline-compliant execution plan.

## Current State
- ✅ Phase 1 (Manifest Loading): Implemented in `ManifestCsvParser`, `ManifestTransformer`
- ✅ Phase 2 (Dependency Analysis): Partially implemented in `DependencyResolver`
- ❌ Phase 3 (Task Grouping & Stratification): **MISSING**
- ⏳ Phase 4 (Initial Timing): Partial (within ExecutionGrains)
- ✅ Phase 5 (Iterative Refinement): Framework in place, needs enhancement

## Implementation Strategy

### Phase 1: Update Core Abstractions (Grains & Models)

**File**: `src/ConsoleApp/Ifx/Orleans/Grains/Abstractions.cs`

**Changes**:
1. Enhance `IExecutionTaskGrain` interface:
   - Add `GetDeadlineSlackAsync()` for criticality analysis
   - Add `SetPrerequisitesAsync()` for explicit prerequisite management
   - Add status update methods

2. Enhance `IExecutionPlanCoordinatorGrain` interface:
   - Add support for tracking iteration metrics
   - Add support for convergence status
   - Add method to get validation results

**Rationale**: 
- Current interfaces lack criticality metrics
- Need explicit prerequisite management for stratification
- Need detailed convergence tracking

---

### Phase 2: Create Dependency Graph Service

**File**: `src/ConsoleApp/Ifx/Services/DependencyGraphBuilder.cs` (NEW)

**Purpose**: Build and validate the DAG structure from resolved dependencies

**Key Methods**:
```csharp
public class DependencyGraphBuilder
{
    // Build complete DAG from all execution events
    public IDependencyGraph BuildDependencyGraphAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        CancellationToken ct);

    // Detect circular dependencies (DFS-based)
    public bool HasCircularDependencies(
        IDependencyGraph graph,
        out IReadOnlyList<IReadOnlyList<string>> cycles);

    // Topological sort
    public IReadOnlyList<string> TopologicalSort(IDependencyGraph graph);
}
```

**Data Structure**: `IDependencyGraph`
```csharp
public interface IDependencyGraph
{
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToPrerequisites { get; }
    IReadOnlyDictionary<string, IReadOnlyList<string>> TaskToDependents { get; }
    IReadOnlyList<string> TopologicalOrder { get; }
}
```

---

### Phase 3: Create Task Stratifier Service

**File**: `src/ConsoleApp/Ifx/Services/TaskStratifier.cs` (NEW)

**Purpose**: Assign execution levels to tasks based on dependency depth

**Key Methods**:
```csharp
public class TaskStratifier
{
    // Assign stratification levels to all tasks
    public StratificationResult AssignStratificationLevels(
        IDependencyGraph graph,
        CancellationToken ct);
}

public record StratificationResult
{
    public Dictionary<string, int> TaskToLevel { get; init; }
    public Dictionary<int, IReadOnlyList<string>> LevelToTasks { get; init; }
    public int MaxLevel { get; init; }
}
```

**Algorithm**:
- Level 0: Tasks with no prerequisites
- Level N: MAX(level of all prerequisites) + 1

**Benefits**:
- Level 0 tasks can execute in parallel
- Enables predictable scheduling across levels

---

### Phase 4: Create Task Grouping Service

**File**: `src/ConsoleApp/Ifx/Services/TaskGrouper.cs` (NEW)

**Purpose**: Classify tasks by execution pattern for optimized scheduling

**Execution Patterns**:
1. **Independent**: No dependencies, no dependents
2. **Sequential Chain**: Linear dependency chain
3. **Fan-Out**: One task → multiple dependents
4. **Fan-In**: Multiple prerequisites → one task
5. **Complex DAG**: Mixed patterns

**Key Methods**:
```csharp
public class TaskGrouper
{
    // Classify each task by execution pattern
    public Dictionary<string, ExecutionPattern> ClassifyTasks(
        IDependencyGraph graph,
        IReadOnlyList<string> allTaskIds,
        CancellationToken ct);

    // Group tasks by execution strategy
    public IReadOnlyList<TaskExecutionGroup> CreateExecutionGroups(
        Dictionary<string, ExecutionPattern> patterns,
        Dictionary<string, int> stratificationLevels,
        CancellationToken ct);
}

public enum ExecutionPattern
{
    Independent,
    SequentialChain,
    FanOut,
    FanIn,
    ComplexDAG
}

public record TaskExecutionGroup
{
    public string GroupId { get; init; }
    public ExecutionPattern Pattern { get; init; }
    public IReadOnlyList<string> TaskIds { get; init; }
    public int StratificationLevel { get; init; }
    public bool IsParallelizable { get; init; }
}
```

---

### Phase 5: Create Criticality Analyzer Service

**File**: `src/ConsoleApp/Ifx/Services/CriticalityAnalyzer.cs` (NEW)

**Purpose**: Calculate critical path, slack time, and task criticality

**Algorithms**:
- Forward pass: Earliest start/end times
- Backward pass: Latest start/end times
- Slack calculation: Latest - Earliest
- Critical path: Tasks with slack = 0

**Key Methods**:
```csharp
public class CriticalityAnalyzer
{
    // Compute earliest times for all tasks (forward pass)
    public Dictionary<string, (DateTime Start, DateTime End)> ComputeEarliestTimes(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime periodStart,
        CancellationToken ct);

    // Compute latest times for all tasks (backward pass)
    public Dictionary<string, (DateTime Start, DateTime End)> ComputeLatestTimes(
        IDependencyGraph graph,
        IReadOnlyDictionary<string, ExecutionDuration> durations,
        DateTime deadline,
        CancellationToken ct);

    // Calculate slack for all tasks
    public Dictionary<string, TimeSpan> CalculateSlack(
        Dictionary<string, (DateTime Start, DateTime End)> earliestTimes,
        Dictionary<string, (DateTime Start, DateTime End)> latestTimes,
        CancellationToken ct);

    // Identify critical path
    public IReadOnlyList<string> IdentifyCriticalPath(
        Dictionary<string, TimeSpan> slack,
        CancellationToken ct);
}
```

---

### Phase 6: Create Execution Plan Orchestrator

**File**: `src/ConsoleApp/Ifx/Services/ExecutionPlanOrchestrator.cs` (NEW)

**Purpose**: Coordinate all phases 2-5 of the execution planning workflow

**Key Methods**:
```csharp
public class ExecutionPlanOrchestrator
{
    // Execute complete workflow: analyze → group → calculate timing
    public async Task<ExecutionPlanAnalysis> AnalyzeAndPlanAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        IReadOnlyList<IntakeEventRequirement> intakeRequirements,
        DateTime planningPeriodStart,
        DateTime planningPeriodEnd,
        CancellationToken ct);
}

public record ExecutionPlanAnalysis
{
    public IDependencyGraph DependencyGraph { get; init; }
    public StratificationResult Stratification { get; init; }
    public IReadOnlyList<TaskExecutionGroup> ExecutionGroups { get; init; }
    public CriticalityMetrics CriticalityInfo { get; init; }
    public ValidationResult ValidationResult { get; init; }
}

public record CriticalityMetrics
{
    public IReadOnlyList<string> CriticalTasks { get; init; }
    public DateTime CriticalPathCompletion { get; init; }
    public Dictionary<string, TimeSpan> TaskSlack { get; init; }
}
```

---

### Phase 7: Enhance Execution Grain Implementation

**File**: `src/ConsoleApp/Ifx/Orleans/Grains/ExecutionGrains.cs`

**Changes**:
1. Update `ExecutionTaskGrain` to use CancellationToken throughout
2. Add support for prerequisite-aware start time calculation
3. Add deadline slack calculation
4. Enhanced logging for debugging iterations

---

### Phase 8: Update Abstractions with CancellationToken

**File**: `src/ConsoleApp/Ifx/Orleans/Grains/Abstractions.cs`

**Critical Change**: Add `CancellationToken ct` as last parameter to all async methods

**Example**:
```csharp
// OLD
Task<ExecutionPlan> CalculateExecutionPlanAsync(
    IReadOnlyList<ExecutionEventDefinition> executionEvents,
    IReadOnlyList<ExecutionInstanceEnhanced> initialInstances,
    DateTime periodStartDate);

// NEW
Task<ExecutionPlan> CalculateExecutionPlanAsync(
    IReadOnlyList<ExecutionEventDefinition> executionEvents,
    IReadOnlyList<ExecutionInstanceEnhanced> initialInstances,
    DateTime periodStartDate,
    CancellationToken ct);
```

**Rationale**: Follows user preference for async method signature consistency

---

## Iteration Plan (Execute in Order)

### Iteration 1: Update Abstractions & Models
**Files to Update**:
- [ ] `Abstractions.cs` - Add CancellationToken, enhance interfaces
- [ ] Create `IDependencyGraph` interface
- [ ] Create supporting record types for services

**Verify**: Build succeeds, existing tests pass

### Iteration 2: Implement Dependency Graph Builder
**File**:
- [ ] `DependencyGraphBuilder.cs` (NEW) - Build & validate DAG

**Dependencies**: `DependencyResolver` (already exists)

**Verify**: Build succeeds, unit tests for graph construction

### Iteration 3: Implement Task Stratifier
**File**:
- [ ] `TaskStratifier.cs` (NEW) - Assign levels to tasks

**Dependencies**: `IDependencyGraph`

**Verify**: Build succeeds, unit tests for level assignment

### Iteration 4: Implement Task Grouper
**File**:
- [ ] `TaskGrouper.cs` (NEW) - Classify & group tasks

**Dependencies**: `IDependencyGraph`, `TaskStratifier`

**Verify**: Build succeeds, unit tests for grouping

### Iteration 5: Implement Criticality Analyzer
**File**:
- [ ] `CriticalityAnalyzer.cs` (NEW) - Calculate critical path

**Dependencies**: `IDependencyGraph`, `ExecutionDuration`

**Verify**: Build succeeds, unit tests for critical path

### Iteration 6: Implement Execution Plan Orchestrator
**File**:
- [ ] `ExecutionPlanOrchestrator.cs` (NEW) - Coordinate phases

**Dependencies**: All Phase 2-5 services

**Verify**: Build succeeds, integration tests for end-to-end flow

### Iteration 7: Update Execution Grain Implementation
**File**:
- [ ] `ExecutionGrains.cs` - Add CancellationToken, enhance logic

**Verify**: Build succeeds, existing tests still pass

### Iteration 8: Integration Testing & Documentation
**Files**:
- [ ] Create integration test scenarios
- [ ] Update documentation with examples
- [ ] Verify end-to-end workflow

---

## Key Design Principles

1. **Separation of Concerns**: Each service handles one phase
2. **Async Throughout**: CancellationToken on all async methods
3. **DAG-First Design**: Dependency graph is foundation for all subsequent work
4. **Fail-Fast Validation**: Circular dependencies detected early
5. **Monotonic Improvement**: Iteration guarantees non-increasing invalid count
6. **Comprehensive Metrics**: Full traceability through all phases

---

## Success Criteria

### Functional
- ✅ Dependency chains fully resolved and validated
- ✅ Circular dependencies detected and reported
- ✅ Task stratification assigned to all tasks
- ✅ Execution groups created with pattern classification
- ✅ Critical path identified and calculated
- ✅ Slack time computed for all tasks
- ✅ Iterative refinement converges on deadline-compliant plan
- ✅ Final plan includes all metrics

### Code Quality
- ✅ All async methods use CancellationToken
- ✅ Build succeeds with zero warnings
- ✅ Existing tests continue to pass
- ✅ New code covered by unit tests
- ✅ Integration tests demonstrate end-to-end flow

### Performance
- ⏱️ Dependency analysis: < 100ms for 1000 tasks
- ⏱️ Single iteration: < 50ms
- ⏱️ Full refinement: < 2 seconds for typical manifest

---

## Notes

- Orleans grains already exist for task execution; we're enhancing their coordination
- `DependencyResolver` already exists; `DependencyGraphBuilder` wraps it
- Phase 1 (Manifest Loading) already working
- Phase 4 (Initial Timing) embedded in existing implementations
- This plan focuses on explicit Phase 2-3 orchestration and Phase 5 enhancement

---

## File Dependencies

```
Phase 2-3 Services:
  DependencyGraphBuilder
    ↓
  TaskStratifier, TaskGrouper, CriticalityAnalyzer
    ↓
  ExecutionPlanOrchestrator
    ↓
  IExecutionPlanCoordinatorGrain (Phase 5)
    ↓
  IExecutionTaskGrain (Phase 5, existing grains)
```

All services are stateless and can be registered as singletons in DI.
