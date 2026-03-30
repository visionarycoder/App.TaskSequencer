# Volatility-Based System Design

## Meta
```yaml
doc_type: system_architecture
target: ai_agent_consumption
volatility_framework: applied
optimization: token_minimal|delivery_fast
version: 1.0.0
date: 2026-03-27
```

## Core Concept

**Volatility-Based Composition**: Group system components by rate of change. Isolate volatile (frequently changing) code from stable code. Minimizes cascade effects, accelerates delivery, reduces token overhead in AI-assisted development.

## Volatility Layers

### L0: Foundation (Change Rate: <1%/sprint)
**Token Priority**: Reference only when debugging
- `Identifier<T>` base class
- `TimeOfDay` value object
- `DayOfWeek` extensions
- Core interfaces (IRepository, IGrain)

**Rationale**: Foundational types rarely change. If changing these, system architecture is wrong.

### L1: Domain Primitives (Change Rate: ~5%/sprint)
**Token Priority**: Read when modifying domain model
- `ExecutionInstance` record
- `ExecutionEventDefinition` record
- `TaskDefinitionManifest` record
- `IntakeEventRequirement` record
- `ExecutionDuration` value object
- `ExecutionStatus` enum

**Rationale**: Domain model stabilizes early. Changes driven by requirement clarifications, not feature additions.

### L2: Business Logic (Change Rate: ~15%/sprint)
**Token Priority**: Primary modification target
- `DependencyResolver`
- `DeadlineValidator`
- `ExecutionEventMatrixBuilder`
- `ManifestTransformer`

**Rationale**: Core algorithms. Optimizations, bug fixes, edge cases drive changes here. High complexity, moderate stability.

### L3: Orchestration (Change Rate: ~25%/sprint)
**Token Priority**: Frequent modification target
- `ExecutionPlanGenerator`
- `OrleansExecutionPlanGenerator`
- `IExecutionTaskGrain` implementation
- `IExecutionPlanCoordinatorGrain` implementation

**Rationale**: Coordination logic changes as features added. Integrates L2 components. High volatility.

### L4: I/O Boundaries (Change Rate: ~30%/sprint)
**Token Priority**: Modify when data format changes
- `ManifestCsvParser`
- CSV file schemas
- Export formatters (Excel, JSON)

**Rationale**: External format changes, new data sources, schema migrations occur frequently early in project lifecycle.

### L5: UI/Presentation (Change Rate: ~40%/sprint)
**Token Priority**: Modify constantly during Phase 2+
- WinUI 3 views
- ViewModels
- Desktop app shell
- Visualization components

**Rationale**: User feedback drives continuous UI iteration. Highest volatility. Isolate completely from domain/business logic.

## Component Architecture

### Stable Core (L0-L1)
```
Domain/
  ValueObjects/
    Identifier.cs         # L0
    TimeOfDay.cs         # L0
    ExecutionDuration.cs # L1
  Models/
    ExecutionInstance.cs # L1
    ExecutionEventDefinition.cs # L1
    TaskDefinitionManifest.cs # L1
```

### Business Logic Core (L2)
```
Services/
  DependencyResolver.cs         # Prerequisite chain resolution
  DeadlineValidator.cs          # Deadline feasibility checks
  ExecutionEventMatrixBuilder.cs # Event matrix generation
  ManifestTransformer.cs        # Manifest → Domain model
```

### Orchestration Layer (L3)
```
Orchestration/
  ExecutionPlanGenerator.cs        # Console app orchestration
  OrleansExecutionPlanGenerator.cs # Orleans grain orchestration
Orleans/Grains/
  ExecutionTaskGrain.cs         # Per-task actor
  ExecutionPlanCoordinatorGrain.cs # Convergence coordinator
```

### I/O Layer (L4)
```
Infrastructure/
  Persistence/
    ManifestCsvParser.cs    # CSV ingestion
    ExcelExporter.cs        # Excel output (Phase 2)
  ExternalServices/
    FileSystemWatcher.cs    # Monitor CSV changes
```

### Presentation Layer (L5)
```
DesktopApp/
  Views/
    MainWindow.xaml         # Dashboard shell
    TimelineView.xaml       # Gantt chart
    DeadlineView.xaml       # Violations report
  ViewModels/
    DashboardViewModel.cs   # Presentation state
```

## Dependency Flow

```
L5 (UI) → L3 (Orchestration) → L2 (Business Logic) → L1 (Domain) → L0 (Foundation)
         ↓
       L4 (I/O)
```

**Rules**:
- No upward dependencies (L2 cannot depend on L3)
- L4 (I/O) only touches L3 (orchestration) and L1 (domain DTOs)
- L5 (UI) only touches L3 (orchestration) via interfaces
- Cross-layer calls must go through abstractions

## AI Agent Optimization

### Token Budget Allocation

**High Volatility (L3-L5)**: 70% of token budget
- These change most frequently
- Agents must load full context
- Example: When modifying `ExecutionPlanGenerator`, load all L3 context + relevant L2 services

**Medium Volatility (L2)**: 20% of token budget
- Load only when algorithm changes required
- Example: Deadline validation bug → load `DeadlineValidator` + `ExecutionInstance`

**Low Volatility (L0-L1)**: 10% of token budget
- Reference documentation only
- Load full files only when debugging foundation issues
- Example: `TimeOfDay` parsing error → load `TimeOfDay.cs`

### Agent Work Patterns

**Feature Addition** (Most Common):
1. Agent loads L3 orchestration + L5 UI (high volatility)
2. Agent references L2 interfaces (no full load)
3. Agent minimal references L1 domain (documentation only)
4. Agent implements changes in L3-L5 layers only

**Algorithm Optimization**:
1. Agent loads L2 business logic (full context)
2. Agent loads L1 domain models (interface contracts)
3. Agent minimal reference L3 orchestration (understand callers)
4. Agent implements changes in L2 only

**Bug Fix**:
1. Agent loads stacktrace layer + adjacent layers
2. If bug in L2, load L2 + L1 models + L3 caller
3. If bug in L3, load L3 + L2 interfaces
4. Agent implements fix in single layer when possible

## Volatility Metrics

### Predicted Change Frequency (16-week sprint)

| Layer | Files Changed/Sprint | Lines Changed/Sprint | Agent Invocations/Sprint |
|-------|---------------------|---------------------|-------------------------|
| L5 UI | 12 files | 800 lines | 25 |
| L4 I/O | 4 files | 200 lines | 8 |
| L3 Orchestration | 6 files | 400 lines | 15 |
| L2 Business Logic | 3 files | 150 lines | 6 |
| L1 Domain | 2 files | 50 lines | 2 |
| L0 Foundation | 0 files | 0 lines | 0 |

**Total Token Savings**: ~40% compared to monolithic context loading

## Implementation Isolation

### Example: Add Excel Export Feature

**Traditional Approach** (No Volatility Isolation):
1. Modify `ExecutionPlanGenerator` (L3) to add export call
2. Modify `ExecutionPlan` model (L1) to add export metadata
3. Create `ExcelExporter` (L4)
4. Update UI (L5) to trigger export
5. **Token Load**: L0+L1+L2+L3+L4+L5 = Full context

**Volatility-Based Approach**:
1. Create `ExcelExporter` in L4 (isolated)
2. Add export interface to L3 orchestration (minimal change)
3. Update UI (L5) to call interface
4. **Token Load**: L3 interface + L4 implementation + L5 UI = 30% of full context

### Example: Optimize Dependency Resolution

**Traditional Approach**:
1. Agent loads all of `ExecutionPlanGenerator` (L3)
2. Agent loads all callers to understand impact
3. Modify `DependencyResolver` (L2)
4. **Token Load**: L2+L3+UI = 60% of codebase

**Volatility-Based Approach**:
1. Agent loads `DependencyResolver` (L2) only
2. Agent loads `ExecutionEventDefinition` (L1) interface
3. Agent runs unit tests to verify no breaks
4. **Token Load**: L2+L1 = 15% of codebase

## Change Propagation Matrix

| Change Origin | L0 | L1 | L2 | L3 | L4 | L5 |
|---------------|----|----|----|----|----|----|
| L0 Foundation | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| L1 Domain | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| L2 Business Logic | — | — | ✓ | ✓ | — | — |
| L3 Orchestration | — | — | — | ✓ | — | ✓ |
| L4 I/O | — | — | — | ✓ | ✓ | ✓ |
| L5 UI | — | — | — | — | — | ✓ |

**Interpretation**:
- ✓ = Potential impact (must verify)
- — = Isolated (no impact)
- Foundation changes cascade everywhere (avoid!)
- UI changes isolated to UI layer (safe to iterate)

## Testing Strategy by Layer

### L0-L1: Contract Tests
- Verify interfaces unchanged
- Run when foundation modified (rare)
- **Agent Task**: "Verify L0-L1 contracts unchanged"

### L2: Unit Tests
- Test algorithms in isolation
- Mock all dependencies
- **Agent Task**: "Test DependencyResolver algorithm correctness"

### L3: Integration Tests
- Test orchestration flow
- Real L2 services, mocked I/O
- **Agent Task**: "Test ExecutionPlanGenerator end-to-end with mocked CSV"

### L4: I/O Tests
- Test CSV parsing accuracy
- Test Excel export correctness
- **Agent Task**: "Test CSV parser with sample_data.csv"

### L5: UI Tests
- Manual testing primarily
- Snapshot tests for layouts
- **Agent Task**: Not applicable (manual QA)

## Subagent Assignment Strategy

### By Volatility Layer

**Foundation Agent (L0-L1)**: "Modify domain model or value objects"
- Invoked: <2 times per 16-week sprint
- Context: Full L0-L1 codebase
- Restrictions: No dependencies on L2+

**Algorithm Agent (L2)**: "Optimize business logic algorithms"
- Invoked: ~6 times per sprint
- Context: L2 services + L1 interfaces
- Restrictions: No UI knowledge, no I/O knowledge

**Orchestration Agent (L3)**: "Modify execution plan generation flow"
- Invoked: ~15 times per sprint
- Context: L3 + L2 interfaces + L1 DTOs
- Restrictions: No direct I/O, no UI

**I/O Agent (L4)**: "Modify CSV parsing or Excel export"
- Invoked: ~8 times per sprint
- Context: L4 + L1 DTOs
- Restrictions: No business logic, no UI

**UI Agent (L5)**: "Modify desktop application UI"
- Invoked: ~25 times per sprint
- Context: L5 + L3 interfaces
- Restrictions: No business logic, no I/O

### By Feature Domain

**CSV Processing Agent**: L4 + L1
**Dependency Resolution Agent**: L2 only
**Deadline Validation Agent**: L2 only
**Orleans Integration Agent**: L3 only
**Desktop UI Agent**: L5 only
**Reporting Agent**: L4 + L5

## Architectural Principles

1. **Volatility Segregation**: Never mix high-volatility and low-volatility code in same file
2. **Dependency Inversion**: High-volatility layers depend on abstractions from low-volatility layers
3. **Interface Buffering**: Use interfaces at layer boundaries to prevent cascade
4. **Immutable Domain**: L0-L1 should be immutable records (prevents cascade)
5. **Orchestration Isolation**: L3 should only coordinate, never implement algorithms
6. **UI Independence**: L5 must not know about L2 (no business logic in UI)

## Token Optimization Rules

1. **Never load L0 unless debugging foundation** (save ~5% tokens)
2. **Reference L1 interfaces, don't load full files** (save ~10% tokens)
3. **Load L2 only when algorithm changes required** (save ~15% tokens)
4. **Always load L3 context for feature work** (necessary)
5. **Load L4 only for I/O changes** (save ~10% tokens)
6. **Load L5 only for UI changes** (save ~15% tokens)

**Cumulative Savings**: ~40-50% token reduction per agent invocation

## Migration Path from Current Code

### Current Structure (Monolithic)
```
src/ConsoleApp/Ifx/
  Models/          # Mixed L0, L1
  Services/        # Mixed L2, L3
  Orleans/Grains/  # Mixed L3, L4
  Utils.cs         # Mixed L0, L2
```

### Target Structure (Volatility-Based)
```
src/
  Domain/              # L0-L1
    Foundation/        # L0
    Models/           # L1
  BusinessLogic/       # L2
    Services/
  Orchestration/       # L3
    Generators/
    Orleans/Grains/
  Infrastructure/      # L4
    Persistence/
    ExternalServices/
  DesktopApp/         # L5
```

### Migration Steps (Prioritized)

1. **Extract L0 Foundation** (1 day)
   - Move `Identifier<T>` to `Domain/Foundation/`
   - Move `TimeOfDay` to `Domain/Foundation/`
   - Update namespaces
   - **Agent**: "Extract foundation value objects to Domain/Foundation/"

2. **Isolate L1 Domain Models** (2 days)
   - Move all record types to `Domain/Models/`
   - Ensure immutability (all properties `init` only)
   - Remove dependencies on services
   - **Agent**: "Move domain models to Domain/Models/ and ensure immutability"

3. **Extract L2 Business Logic** (3 days)
   - Move `DependencyResolver`, `DeadlineValidator`, `ExecutionEventMatrixBuilder` to `BusinessLogic/Services/`
   - Ensure no orchestration logic
   - Add unit tests
   - **Agent**: "Extract business logic services to BusinessLogic/Services/"

4. **Isolate L3 Orchestration** (4 days)
   - Move `ExecutionPlanGenerator`, `OrleansExecutionPlanGenerator` to `Orchestration/Generators/`
   - Move grain implementations to `Orchestration/Orleans/Grains/`
   - Ensure only coordination, no algorithms
   - **Agent**: "Move orchestration to Orchestration/ and ensure no algorithm implementation"

5. **Extract L4 I/O** (2 days)
   - Move `ManifestCsvParser` to `Infrastructure/Persistence/`
   - Create `ExcelExporter` in `Infrastructure/Persistence/`
   - Add I/O interfaces
   - **Agent**: "Extract I/O to Infrastructure/Persistence/"

6. **Create L5 Desktop App** (ongoing Phase 2)
   - Create `DesktopApp` project
   - Implement WinUI 3 views
   - Use L3 interfaces only
   - **Agent**: "Create desktop app with WinUI 3"

## Success Metrics

### Development Velocity
- **Target**: 30% reduction in time per feature
- **Measurement**: Track time from requirement to deployment per feature
- **Mechanism**: Fewer files loaded per agent invocation = faster context understanding

### Token Efficiency
- **Target**: 40% reduction in tokens per agent session
- **Measurement**: Track average tokens per completed task
- **Mechanism**: Volatility-based context loading

### Bug Rate
- **Target**: 20% reduction in cross-layer bugs
- **Measurement**: Track bugs that span multiple layers
- **Mechanism**: Change isolation prevents cascade failures

### Test Stability
- **Target**: 50% reduction in test churn
- **Measurement**: Track test file modifications per sprint
- **Mechanism**: Layer isolation means L5 UI changes don't break L2 algorithm tests

## Conclusion

Volatility-based composition optimizes for **AI-assisted development speed**. By grouping components by change rate and isolating volatile from stable code:

1. AI agents load minimal context (40% token savings)
2. Changes isolated to single layer (30% faster delivery)
3. Fewer cascade failures (20% fewer bugs)
4. Test stability improved (50% less test churn)

**Next**: See implementation plan with subagent assignments per layer.
