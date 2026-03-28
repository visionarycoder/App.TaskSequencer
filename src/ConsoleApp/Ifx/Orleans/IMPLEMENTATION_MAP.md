# Orchestration Requirements Implementation Map

## Overview

This document maps the orchestration requirements to existing code components and identifies gaps that need to be addressed.

## Phase-by-Phase Mapping

### Phase 1: Manifest Loading & Validation

**Requirements**:
- Parse CSV manifest
- Transform to domain models
- Validate task definitions
- Validate recurrence patterns
- Build intake event requirements

**Current Implementation**:
| Component | Status | Location |
|-----------|--------|----------|
| ManifestCsvParser | ✅ Exists | `Services/ManifestCsvParser.cs` |
| ManifestTransformer | ✅ Exists | `Services/ManifestTransformer.cs` |
| RecurrencePattern | ✅ NEW | `Models/RecurrencePattern.cs` |
| RecurrenceCalculationService | ✅ NEW | `Services/RecurrenceCalculationService.cs` |
| Task Validation | ⚠️ Partial | Various model validators |
| Intake Event Requirements | ✅ Exists | `Models/IntakeEventRequirement.cs` |

**Gaps**:
- [ ] Comprehensive validation orchestrator for Phase 1
- [ ] Integration of recurrence pattern with manifest loading
- [ ] Validation error aggregation and reporting

---

### Phase 2: Dependency Chain Analysis

**Requirements**:
- Dependency resolution
- Circular dependency detection
- Missing prerequisite detection
- Topological sorting
- Critical path analysis

**Current Implementation**:
| Component | Status | Location |
|-----------|--------|----------|
| DependencyResolver | ✅ Exists | `Services/DependencyResolver.cs` |
| Circular Dependency Detection | ⚠️ Partial | In ExecutionSequencingEngine |
| Topological Sort | ✅ Exists | In ExecutionSequencingEngine |
| Critical Path Analysis | ⚠️ Partial | In ExecutionPlan metrics |

**Key Method**:
```csharp
DependencyResolver.ResolvePrerequisites()
// Finds latest feasible prerequisite execution events
```

**Gaps**:
- [ ] Standalone dependency analysis orchestrator
- [ ] Missing prerequisite detection and reporting
- [ ] Explicit DAG structure (currently implicit)
- [ ] Critical path calculation clarity
- [ ] Visualization/export of dependency graph

---

### Phase 3: Task Grouping & Stratification

**Requirements**:
- Level assignment (stratification)
- Group by execution strategy
- Criticality analysis
- Slack time calculation

**Current Implementation**:
| Component | Status | Location |
|-----------|--------|----------|
| Stratification | ❌ Missing | N/A |
| Task Grouping | ❌ Missing | N/A |
| Criticality Analysis | ⚠️ Partial | In execution planning |
| Slack Calculation | ❌ Missing | N/A |

**Gaps**:
- [ ] **MAJOR**: No task stratification/leveling implementation
- [ ] **MAJOR**: No task grouping strategy implementation
- [ ] **MAJOR**: No explicit criticality calculation
- [ ] **MAJOR**: No slack time calculation

---

### Phase 4: Initial Timing Calculation

**Requirements**:
- Calculate start/end times for initial schedule
- Check deadlines
- Flag conflicts
- Create initial ExecutionPlan

**Current Implementation**:
| Component | Status | Location |
|-----------|--------|----------|
| ExecutionPlanGenerator | ✅ Exists | `Services/ExecutionPlanGenerator.cs` |
| TimeSlot Calculation | ✅ Exists | ExecutionSequencingEngine |
| Deadline Checking | ✅ Exists | Various validators |
| ExecutionInstanceEnhanced | ✅ Exists | `Models/ExecutionInstanceEnhanced.cs` |
| ExecutionPlan | ✅ Exists | `Models/ExecutionPlan.cs` |

**Gaps**:
- [ ] Clear separation of Phase 4 logic
- [ ] Initial conflict reporting
- [ ] Integration with recurrence patterns

---

### Phase 5: Iterative Time Slot Refinement

**Requirements**:
- Create task grains
- Implement iteration loop
- Update times based on prerequisites
- Validate deadlines each iteration
- Check convergence
- Build final plan

**Current Implementation**:
| Component | Status | Location |
|-----------|--------|----------|
| IExecutionTaskGrain | ✅ Exists | `Grains/ExecutionGrains.cs` |
| ExecutionTaskGrain | ✅ Exists | `Grains/ExecutionGrains.cs` |
| IExecutionPlanCoordinatorGrain | ✅ Exists | `Grains/Abstractions.cs` |
| ExecutionPlanCoordinatorGrain | ✅ Exists | `Grains/ExecutionGrains.cs` |
| IExecutionPlanCoordinationGrain | ✅ NEW | `Grains/ExecutionPlanCoordinationGrain.cs` |
| ExecutionPlanCoordinationGrain | ✅ NEW | `Grains/ExecutionPlanCoordinationGrain.cs` |

**Key Methods**:
```csharp
IExecutionPlanCoordinatorGrain.CalculateExecutionPlanAsync()
// Main orchestration method

IExecutionPlanCoordinatorGrain.RefineTimeSlotIterationAsync()
// Single iteration implementation

IExecutionPlanCoordinationGrain.StartExecutionPlanningAsync()
// High-level coordination
```

**Gaps**:
- [ ] Clear documentation of iteration algorithm
- [ ] Convergence criteria clarity
- [ ] Integration with recurrence patterns
- [ ] Metrics collection during refinement

---

## Data Flow Mapping

### Current Flow (Partial)
```
CSV Manifest
    ↓
ManifestCsvParser
    ↓
ManifestTransformer
    ↓
ExecutionEventDefinition[]
    ↓
ExecutionPlanGenerator (or ExecutionSequencingEngine)
    ↓
ExecutionPlan
```

### Required Flow (Complete with Phases)
```
CSV Manifest
    ↓ (Phase 1)
[ManifestCsvParser] → Parse & Load
[ManifestTransformer] → Transform to Domain
[Validators] → Validate
RecurrenceCalculationService → Calculate Occurrences
    ↓
TaskDefinitionEnhanced[], IntakeEventDefinition[]
    ↓ (Phase 2)
[DependencyResolver] → Build DAG
[TopologicalSort] → Determine Order
[CycleDetector] → Detect Cycles
[CriticalPathCalculator] → Analyze Path
    ↓
DependencyGraph, ExecutionOrder
    ↓ (Phase 3)
[Stratifier] → Assign Levels → MISSING
[TaskGrouper] → Group Tasks → MISSING
[CriticalityAnalyzer] → Calculate Criticality → MISSING
[SlackCalculator] → Calculate Slack → MISSING
    ↓
StratifiedTasks, TaskGroups
    ↓ (Phase 4)
[InitialScheduler] → Calculate Times
[DeadlineChecker] → Check Deadlines
    ↓
ExecutionInstanceEnhanced[]
    ↓ (Phase 5)
[ExecutionPlanCoordinatorGrain]
  ├→ Creates ExecutionTaskGrains
  ├→ Iteration Loop:
  │  ├→ UpdateStartTimes
  │  ├→ ValidateDeadlines
  │  └→ CheckConvergence
  └→ BuildFinalPlan
    ↓
ExecutionPlan (Final)
```

---

## Component Status Summary

### ✅ Fully Implemented (5 components)
1. ManifestCsvParser - CSV parsing
2. ManifestTransformer - Domain transformation
3. ExecutionSequencingEngine - Time calculation core
4. ExecutionTaskGrain - Individual task grain
5. ExecutionPlanCoordinatorGrain - Period coordinator & new ExecutionPlanCoordinationGrain - High-level coordinator

### ⚠️ Partially Implemented (4 components)
1. DependencyResolver - Basic resolution (missing cycle detection detail)
2. Validation - Scattered across models (needs orchestration)
3. Critical Path Analysis - In ExecutionPlan (needs formalization)
4. Recurrence Integration - New pattern support (needs full integration)

### ❌ Not Implemented (4 components)
1. **Task Stratification** - Level assignment algorithm
2. **Task Grouping** - Execution strategy grouping
3. **Criticality Calculation** - Task priority/urgency
4. **Slack Calculation** - Time slack for non-critical tasks

---

## Implementation Priorities

### Priority 1: Orchestration Orchestrator (Critical)
**Purpose**: Coordinate all 5 phases
**Location**: New class `ExecutionOrchestrator.cs`
**Responsibility**:
- Call phases in sequence
- Handle phase errors
- Aggregate results
- Provide metrics

### Priority 2: Task Stratification (Important)
**Purpose**: Assign execution levels to tasks
**Location**: New class `TaskStratifier.cs`
**Algorithm**: BFS/DFS level assignment based on dependencies

### Priority 3: Criticality Analysis (Important)
**Purpose**: Identify critical tasks and slack
**Location**: New class `CriticalityAnalyzer.cs`
**Calculation**: 
- Critical path identification
- Float/slack time calculation
- Criticality scoring

### Priority 4: Task Grouping (Enhancement)
**Purpose**: Group tasks by execution pattern
**Location**: New class `TaskGrouper.cs`
**Strategies**: Independent, Sequential, FanOut, FanIn, Complex

### Priority 5: Enhanced Metrics (Nice to Have)
**Purpose**: Better monitoring and reporting
**Location**: Metrics collection throughout phases
**Metrics**: Timing per phase, conflict counts, etc.

---

## Interface/Contract Requirements

### Missing Interface: IPhaseOrchestrator
```csharp
public interface IPhaseOrchestrator
{
    // Phase 1: Load & Validate
    Task<ManifestData> LoadAndValidateAsync(string manifestPath);

    // Phase 2: Analyze Dependencies
    Task<DependencyGraph> AnalyzeDependenciesAsync(ManifestData data);

    // Phase 3: Group Tasks
    Task<TaskGroups> GroupTasksAsync(DependencyGraph graph);

    // Phase 4: Initial Timing
    Task<ExecutionInstanceEnhanced[]> CalculateInitialTimingAsync(
        ManifestData data,
        DependencyGraph graph);

    // Phase 5: Iterative Refinement
    Task<ExecutionPlan> RefineExecutionPlanAsync(
        ExecutionInstanceEnhanced[] initialPlan);

    // Full orchestration
    Task<ExecutionPlan> OrchestrateMasterPlanAsync(string manifestPath);
}
```

### Missing Interface: ITaskStratifier
```csharp
public interface ITaskStratifier
{
    Dictionary<int, IReadOnlyList<string>> StratifyTasks(
        IReadOnlyList<string> tasks,
        Dictionary<string, IReadOnlySet<string>> dependencies);
}
```

### Missing Interface: ICriticalityAnalyzer
```csharp
public interface ICriticalityAnalyzer
{
    CriticalityAnalysis Analyze(
        IReadOnlyList<ExecutionInstanceEnhanced> plan,
        Dictionary<string, IReadOnlySet<string>> dependencies);
}
```

---

## Integration Points with ExecutionPlanCoordinator

### Current (Phase 5 Only)
```
ExecutionEventDefinition[]
    ↓
IExecutionPlanCoordinatorGrain.CalculateExecutionPlanAsync()
    ↓
ExecutionPlan
```

### Required (Full Pipeline)
```
CSV Manifest
    ↓
Phase 1: Load & Validate
    ↓
Phase 2: Analyze Dependencies  
    ↓
Phase 3: Group & Stratify (NEW)
    ↓
Phase 4: Initial Timing
    ↓
Phase 5: ExecutionPlanCoordinator
    ├→ CreateTaskGrains()
    ├→ CalculateExecutionPlanAsync()
    ├→ RefineTimeSlotIterationAsync() (iteratively)
    └→ BuildExecutionPlanAsync()
    ↓
ExecutionPlan (Final)
```

---

## Recurrence Pattern Integration Points

### Phase 1: Manifest Loading
- Parse `RecurrencePattern` from manifest row
- Store in `IntakeEventManifest.RecurrencePattern`
- Calculate all occurrences within period

### Phase 2: Dependency Analysis
- Treat each recurrence instance as separate task
- Resolve dependencies for each instance
- Build DAG including all instances

### Phase 3: Task Grouping
- Group instances by pattern type
- Apply grouping strategy per instance set

### Phase 4: Initial Timing
- Apply timing rules to each instance
- Each instance inherits recurrence time from pattern

### Phase 5: Iterative Refinement
- Each instance participates in refinement
- Convergence applies to all instances

---

## Summary of Gaps

| Gap | Severity | Impact | Priority |
|-----|----------|--------|----------|
| No Phase Orchestrator | HIGH | Cannot coordinate all 5 phases | P1 |
| No Task Stratification | HIGH | Missing execution level assignment | P1 |
| No Criticality Calculation | MEDIUM | Cannot identify critical tasks | P2 |
| No Task Grouping | MEDIUM | Cannot group by strategy | P2 |
| No Slack Calculation | LOW | Limited optimization capability | P3 |
| Incomplete Validation Orchestration | MEDIUM | Validation scattered | P2 |
| Missing Recurrence Integration | MEDIUM | New feature not fully integrated | P2 |

---

## Next Steps (No Code Changes Yet)

1. **Review** this requirements document
2. **Confirm** the five-phase workflow is correct
3. **Validate** existing component mapping
4. **Prioritize** implementation order
5. **Design** missing components (stratification, grouping, etc.)
6. **Plan** code changes in separate document

**Current Status**: Requirements documented, no code changes made yet.
