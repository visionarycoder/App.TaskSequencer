# Implementation Gap Analysis & Plan

## Executive Summary

The Task Sequencer solution has architectural documentation and service layer structure in place, but the **core business logic for dependency chain calculation and execution plan generation** is incomplete or stubbed out. The documentation describes three key CSV input files that drive the system, but the implementation for processing these into executable dependency graphs and time-constrained execution plans is missing.

**Focus**: Build unit-tested, core business rule implementations for dependency chain calculation FIRST, then execution plan generation, before adding scheduling/time allocation.

---

## Gap Analysis: Core Business Rules

### Gap 1: Dependency Graph Construction from Task Definitions
**Current State**: `DependencyGraphBuilder.cs` exists but lacks complete implementation
**Missing**: 
- Algorithm to parse task interface numbers and dependencies from CSV
- Graph construction logic (topological sorting validation)
- Cycle detection in dependency chains
- Unit tests for graph building

**Impact**: Cannot determine which tasks must run before others

**Business Rule**:
```
For each task in CSV:
1. Extract InterfaceNumber (task ID)
2. Extract Dependencies (pipe-separated interface numbers)
3. Build directed graph: Task → Dependent tasks
4. Validate: No cycles (circular dependencies)
5. Output: Topologically sorted task list
```

### Gap 2: Execution Sequencing (Dependency Chain Scheduling)
**Current State**: Service exists but missing core sequencing logic
**Missing**:
- Algorithm to calculate when each task can start (based on upstream completion + intake deadlines)
- Deadline backtracking: Working backwards from intake times to earliest start times
- Concurrency detection: Identifying tasks that can run in parallel
- Conflict resolution: Multiple instances of same task on same day

**Impact**: Cannot generate valid execution plans that respect deadlines

**Business Rule**:
```
For each task's execution instance:
1. Identify all upstream task dependencies
2. Find latest completion time of all upstreams
3. Backtrack from intake deadline:
   - If intake time exists for this task on this day
   - Calculate: latest_start = intake_time - duration
   - Check: Can task complete before deadline?
4. Calculate: Can earliest_start (max of: current_time + downstream_buffer)
5. Output: Valid execution window or conflict flag
```

### Gap 3: Duration Estimation & Allocation
**Current State**: CSV import structure exists; duration calculation missing
**Missing**:
- Default duration assignment (15 minutes per CSV spec)
- Historical data import and averaging
- Execution time calculation for compound tasks (task groups with multiple subtasks)
- Buffer allocation for dependencies

**Impact**: Cannot estimate if tasks will complete before intake deadlines

**Business Rule**:
```
For each execution instance:
1. If historical data exists for this task/date/time:
   - Use actual_duration from completed execution
   - Set IsEstimated = false
2. Else:
   - Use default: 15 minutes
   - Set IsEstimated = true
3. For grouped tasks:
   - Sum all subtask durations
   - Add 10% buffer for inter-task overhead
4. Output: Task duration with confidence flag
```

### Gap 4: Deadline Validation & Conflict Detection
**Current State**: `DeadlineValidator.cs` exists; incomplete implementation
**Missing**:
- Algorithm to check if execution plan meets all intake deadlines
- Conflict reporting: Which tasks violate deadlines
- Feasibility analysis: Can this day's tasks complete in time?
- Recommendation generation: Suggest task reordering or earlier start times

**Impact**: Cannot determine if proposed schedule is valid

**Business Rule**:
```
For each execution instance scheduled for a day:
1. Check intake deadlines:
   - If task marked X for this day, must complete by intake_time
2. Calculate: actual_completion = start_time + duration
3. Validate: actual_completion ≤ intake_time
4. If multiple instances same day:
   - All must fit within available window
   - Check for cascading delays
5. Output: List of conflicts or "Valid"
```

### Gap 5: Execution Plan Generation (Orchestration)
**Current State**: `ExecutionPlanGenerator.cs` service interface exists
**Missing**:
- Algorithm to combine: dependency chain + deadline validation + duration estimation
- Task grouping logic (stratification by criticality/dependency level)
- Orchestration plan output structure
- Integration with Orleans grain coordination

**Impact**: Cannot generate comprehensive, ready-to-execute plans

**Business Rule**:
```
To generate an execution plan:
1. Build dependency graph (Gap 1)
2. Calculate task durations (Gap 3)
3. Calculate execution windows (Gap 2)
4. Validate deadlines (Gap 4)
5. Group tasks by execution level (stratification)
6. Output: ExecutionPlan with:
   - Task sequence
   - Execution instances (with times)
   - Dependencies between instances
   - Deadline compliance status
   - Criticality flags
```

---

## Test-First Implementation Strategy

### Phase 1: Unit Tests for Core Rules (NO IMPLEMENTATION CODE YET)

**Purpose**: Define business rules precisely through tests

#### Test Suite 1: Dependency Graph Construction
```csharp
// Tests/Core.Services.Tests/DependencyGraphBuilderTests.cs
- BuildGraph_SimpleChain_SortsTopologically()
- BuildGraph_MultipleBranches_MergesCorrectly()
- BuildGraph_HasCycle_ThrowsInvalidOperationException()
- BuildGraph_MissingDependency_ThrowsArgumentException()
- BuildGraph_EmptyTask_ReturnsEmpty()
```

#### Test Suite 2: Duration Estimation
```csharp
// Tests/Core.Services.Tests/ExecutionDurationCalculatorTests.cs
- GetDuration_NoHistory_DefaultsFifteenMinutes()
- GetDuration_WithHistory_UsesActual()
- GetDuration_MultipleHistoricalValues_AveragesOrLatest()
- GetDuration_GroupedTask_SumsPlusTenPercentBuffer()
- GetDuration_IsEstimated_FlagsCorrectly()
```

#### Test Suite 3: Execution Window Calculation
```csharp
// Tests/Core.Services.Tests/ExecutionWindowCalculatorTests.cs
- CalculateWindow_NoDependencies_StartsImmediately()
- CalculateWindow_WithDependency_StartsAfterUpstream()
- CalculateWindow_WithDeadline_BacktracksFromIntake()
- CalculateWindow_MultipleDependencies_UsesLatestCompletion()
- CalculateWindow_ConflictingDeadlines_ReturnsNull()
```

#### Test Suite 4: Deadline Validation
```csharp
// Tests/Core.Services.Tests/DeadlineValidatorTests.cs
- Validate_CompletesBeforeDeadline_ReturnsValid()
- Validate_CompletesAfterDeadline_ReturnsFailed()
- Validate_NoDeadlineThisDay_SkipsValidation()
- Validate_MultipleInstancesSameDay_ChecksAll()
- Validate_CascadingFailure_ReportsChainImpact()
```

#### Test Suite 5: Execution Plan Generation
```csharp
// Tests/Core.Services.Tests/ExecutionPlanGeneratorTests.cs
- GeneratePlan_SimpleTasks_CreatesSequential()
- GeneratePlan_WithDependencies_RespectsSorting()
- GeneratePlan_WithDeadlines_MeetsAllIntakeTimes()
- GeneratePlan_UnfeasibleSchedule_ReturnsFailureWithReason()
- GeneratePlan_MultiDaySchedule_GroupsByExecution Level()
```

---

## Implementation Roadmap

### SUBTASK-1: Unit Tests for Dependency Graph Building
**Type**: Testing (Write tests first, implementation second)
**Tokens**: Medium (1000-1500)
**Dependencies**: None
**Input**: Existing DependencyGraphContracts.cs for test data types
**Output**: DependencyGraphBuilderTests.cs with 5+ test cases
**Constraints**: #.NET10, #C#14+, #TestFirstApproach, Tests should fail initially

### SUBTASK-2: Unit Tests for Duration Estimation
**Type**: Testing
**Tokens**: Medium (1000-1200)
**Dependencies**: SUBTASK-1 (for test patterns)
**Input**: ExecutionInstance.cs, ExecutionDurationDefinition CSV spec
**Output**: ExecutionDurationCalculatorTests.cs with 5+ test cases
**Constraints**: #.NET10, #C#14+, #NoMocks (use real data structures)

### SUBTASK-3: Unit Tests for Execution Window Calculation
**Type**: Testing
**Tokens**: Medium (1200-1500)
**Dependencies**: SUBTASK-1, SUBTASK-2
**Input**: ExecutionEventDefinition.cs, deadline semantics from doc
**Output**: ExecutionWindowCalculatorTests.cs with 5+ test cases
**Constraints**: #.NET10, #C#14+, #IntakeDatetimeLogic

### SUBTASK-4: Unit Tests for Deadline Validation
**Type**: Testing
**Tokens**: Medium (1000-1300)
**Dependencies**: SUBTASK-1, SUBTASK-2, SUBTASK-3
**Input**: DeadlineValidator.cs interfaces, validation rules from doc
**Output**: DeadlineValidatorTests.cs with 5+ test cases
**Constraints**: #.NET10, #C#14+, #TimeArithmetic

### SUBTASK-5: Unit Tests for Execution Plan Generation
**Type**: Testing
**Tokens**: Medium (1200-1500)
**Dependencies**: SUBTASK-1 through SUBTASK-4
**Input**: ExecutionPlanGenerator interface, orchestration semantics
**Output**: ExecutionPlanGeneratorTests.cs with 5+ test cases
**Constraints**: #.NET10, #C#14+, #EndToEndLogic

### SUBTASK-6: Implement Dependency Graph Builder
**Type**: Code Generation (Implementation driven by tests)
**Tokens**: Medium (1500-2000)
**Dependencies**: SUBTASK-1 complete (tests must exist and fail)
**Input**: DependencyGraphBuilderTests.cs (red tests)
**Output**: DependencyGraphBuilder.cs fully implemented and green tests
**Constraints**: #.NET10, #C#14+, #TopologicalSort, #CycleDetection

### SUBTASK-7: Implement Duration Calculator
**Type**: Code Generation
**Tokens**: Medium (1200-1500)
**Dependencies**: SUBTASK-2, SUBTASK-6
**Input**: ExecutionDurationCalculatorTests.cs (red tests)
**Output**: ExecutionDurationCalculator.cs implementation
**Constraints**: #.NET10, #C#14+, #CsvParsing, #AggregateHistoricalData

### SUBTASK-8: Implement Execution Window Calculator
**Type**: Code Generation
**Tokens**: Medium (1500-1800)
**Dependencies**: SUBTASK-3, SUBTASK-6, SUBTASK-7
**Input**: ExecutionWindowCalculatorTests.cs (red tests)
**Output**: ExecutionWindowCalculator.cs implementation
**Constraints**: #.NET10, #C#14+, #TimeArithmetic, #BacktrackingLogic

### SUBTASK-9: Implement Deadline Validator
**Type**: Code Generation
**Tokens**: Medium (1200-1500)
**Dependencies**: SUBTASK-4, SUBTASK-8
**Input**: DeadlineValidatorTests.cs (red tests)
**Output**: DeadlineValidator.cs complete implementation
**Constraints**: #.NET10, #C#14+, #ConflictReporting

### SUBTASK-10: Implement Execution Plan Generator
**Type**: Code Generation
**Tokens**: High (2000-2500)
**Dependencies**: All SUBTASK-6 through SUBTASK-9
**Input**: ExecutionPlanGeneratorTests.cs (red tests), all previous implementations
**Output**: ExecutionPlanGenerator.cs orchestrating all prior services
**Constraints**: #.NET10, #C#14+, #Orchestration, #EndToEndIntegration

### SUBTASK-11: Integration Testing with CSV Data
**Type**: Testing
**Tokens**: High (2000-2500)
**Dependencies**: SUBTASK-10
**Input**: Sample CSV files (task definitions, intake events, durations)
**Output**: ExecutionPlanGeneratorIntegrationTests.cs with real CSV scenarios
**Constraints**: #.NET10, #C#14+, #RealWorldScenarios, #TestDataValidation

### SUBTASK-12: Orleans Grain Coordination Integration
**Type**: Code Generation + Integration
**Tokens**: High (2500-3000)
**Dependencies**: SUBTASK-11
**Input**: ExecutionPlanCoordinationGrain.cs interface, ExecutionPlan data
**Output**: Grain implementation coordinating plan execution
**Constraints**: #.NET10, #C#14+, #OrleansAsync, #DistributedCoordination

---

## Optimization Notes

**Parallelizable**: 
- SUBTASK-1, SUBTASK-2, SUBTASK-3, SUBTASK-4, SUBTASK-5 can run in parallel (independent test suites)
- SUBTASK-6, SUBTASK-7, SUBTASK-8, SUBTASK-9, SUBTASK-10 must run sequentially (dependency chain)

**Sequential Requirements**:
- All tests (1-5) should complete before any implementations (6-10)
- All implementations (6-10) should complete before integration (11)
- Integration should complete before Orleans coordination (12)

**Token-Saving Opportunities**:
- Reuse test fixtures and builders across all test suites
- Reference ExecutionInstance, ExecutionPlan DTOs (don't redefine)
- Use real CSV data, not mocks, to validate business rules
- Keep service implementations focused: one algorithm per service

**Reusable Components**:
- Common test data builders (TaskBuilder, ExecutionInstanceBuilder)
- CSV fixtures for all scenarios
- Datetime utility functions (all in one place)

---

## Success Criteria

✅ All 5 test suites written and initially RED
✅ All 5 test suites passing (implementations complete)
✅ Integration tests passing with real CSV data
✅ Build verified (dotnet build)
✅ No TODOs or NotImplementedException left
✅ Orleans grain can execute coordinated plans
✅ Documentation updated with implementation details

---

## Ready for Code Agent

Execute subtasks in order: **1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12**

Parallel-safe: **SUBTASK-1 through SUBTASK-5 can run simultaneously**; then sequential 6-12.

**Critical Path**: Tests first (SUBTASKs 1-5), then implementations (SUBTASKs 6-10), then integration (SUBTASKs 11-12).

**Cleanup**: Delete this plan file after execution is complete.
