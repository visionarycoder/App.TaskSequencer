# AI Agent Quick Reference: Volatility Framework

## Meta
```yaml
doc_type: agent_quick_reference
format: lookup_table
target: ai_consumption
token_priority: critical_paths_only
```

## Decision Tree: Which Context to Load?

```
USER REQUEST TYPE
│
├─ "Modify UI" / "Change desktop app"
│   → LOAD: L5 (Views/ViewModels) + L3 interfaces only
│   → SKIP: L0, L1, L2, L4
│   → TOKEN LOAD: 15K
│
├─ "Fix deadline validation bug" / "Optimize algorithm"
│   → LOAD: L2 (specific service) + L1 interfaces
│   → SKIP: L0, L3, L4, L5
│   → TOKEN LOAD: 10K
│
├─ "Add CSV export" / "Change file format"
│   → LOAD: L4 (I/O) + L1 DTOs
│   → SKIP: L0, L2, L3, L5
│   → TOKEN LOAD: 8K
│
├─ "Add new feature" / "Integrate component"
│   → LOAD: L3 (Orchestration) + L2 interfaces + L1 DTOs
│   → SKIP: L0, L4, L5
│   → TOKEN LOAD: 20K
│
├─ "Fix grain coordination" / "Orleans issue"
│   → LOAD: L3 (Orleans grains) + L1 DTOs
│   → SKIP: L0, L2, L4, L5
│   → TOKEN LOAD: 15K
│
└─ "Foundation change" / "Value object bug"
    → LOAD: L0 + L1 + all usage references (rare!)
    → TOKEN LOAD: 50K (avoid if possible!)
```

## Layer Identification

### L0: Foundation
**Files**: `Identifier.cs`, `TimeOfDay.cs`
**Characteristics**: Generic, reusable, <100 LOC per file
**Change Frequency**: Almost never
**When to load**: Only if debugging core value object behavior

### L1: Domain Models
**Files**: All `record` types (ExecutionInstance, ExecutionPlan, etc.)
**Characteristics**: Immutable, no logic, only data
**Change Frequency**: Early project only
**When to load**: When modifying domain contracts

### L2: Business Logic
**Files**: DependencyResolver, DeadlineValidator, ExecutionEventMatrixBuilder
**Characteristics**: Pure algorithms, no I/O, no UI
**Change Frequency**: Medium (bug fixes, optimizations)
**When to load**: When modifying scheduling algorithms

### L3: Orchestration
**Files**: ExecutionPlanGenerator, Orleans grains
**Characteristics**: Coordinates L2 services, no algorithms
**Change Frequency**: High (feature additions)
**When to load**: When adding features or integrating components

### L4: I/O
**Files**: ManifestCsvParser, ExcelExporter
**Characteristics**: External system interfaces
**Change Frequency**: High early, stabilizes later
**When to load**: When modifying data ingestion/export

### L5: UI
**Files**: XAML views, ViewModels
**Characteristics**: Presentation logic only
**Change Frequency**: Very high (user feedback)
**When to load**: When modifying UI

## Token Optimization Rules

### Rule 1: Load Minimal Context
**Before volatility awareness**:
```
Load: All files in project
Token cost: 80K tokens
```

**With volatility awareness**:
```
Identify layer → Load only that layer + interfaces
Token cost: 8-20K tokens
Savings: 60K-72K tokens (75-90%)
```

### Rule 2: Reference Don't Load
**Don't load**:
- L0 files (just know they exist)
- L1 files (just reference interfaces)
- Unrelated layers

**Do load**:
- Target layer (where change happens)
- Adjacent layer interfaces (dependencies)
- Test files for target layer

### Rule 3: Load Order Priority
1. **Primary**: Layer where change occurs (full load)
2. **Secondary**: Adjacent layer interfaces (reference only)
3. **Tertiary**: DTOs used by primary (structure only)
4. **Never**: Unrelated layers

## Context Loading Matrix

| Your Task | Load L0 | Load L1 | Load L2 | Load L3 | Load L4 | Load L5 |
|-----------|---------|---------|---------|---------|---------|---------|
| Fix UI bug | ❌ | ❌ | ❌ | Interface | ❌ | ✅ Full |
| Optimize algorithm | ❌ | Interface | ✅ Full | ❌ | ❌ | ❌ |
| Add CSV field | ❌ | DTO only | ❌ | ❌ | ✅ Full | ❌ |
| Add feature | ❌ | DTO only | Interface | ✅ Full | ❌ | ❌ |
| Fix grain bug | ❌ | DTO only | ❌ | ✅ Full | ❌ | ❌ |
| Change value object | ✅ Full | ✅ Full | Scan usage | Scan usage | Scan usage | Scan usage |

Legend:
- ✅ Full = Load complete files
- Interface = Load signatures only
- DTO only = Load record definitions only
- Scan usage = Search for references, don't load
- ❌ = Completely skip

## Common Scenarios

### Scenario A: "Add export to PDF"
**Traditional approach**:
1. Load full codebase
2. Find all export-related code
3. Implement PDF export
4. **Token cost**: 80K

**Volatility approach**:
1. Identify as L4 (I/O) task
2. Load `ExcelExporter.cs` as template
3. Load `ExecutionPlan.cs` as DTO
4. Implement `PdfExporter.cs` in L4
5. **Token cost**: 12K
6. **Savings**: 68K (85%)

### Scenario B: "Fix deadline validation bug"
**Traditional approach**:
1. Load full codebase to understand context
2. Find `DeadlineValidator`
3. Load all callers
4. Fix bug
5. **Token cost**: 50K

**Volatility approach**:
1. Identify as L2 (Business Logic) task
2. Load `DeadlineValidator.cs` only
3. Load `ExecutionEventDefinition.cs` (DTO)
4. Fix algorithm
5. Run unit tests
6. **Token cost**: 8K
7. **Savings**: 42K (84%)

### Scenario C: "Add new grouping feature"
**Traditional approach**:
1. Load full codebase
2. Understand all layers
3. Implement feature across layers
4. **Token cost**: 80K

**Volatility approach**:
1. Identify as L3 (Orchestration) task
2. Load `ExecutionPlanGenerator.cs`
3. Load L2 service interfaces (not implementations)
4. Load L1 DTOs
5. Implement orchestration
6. **Token cost**: 25K
7. **Savings**: 55K (69%)

## File Classification Shortcuts

### Quick Classification
**Look at imports**:
- Only imports from `Domain.Foundation` or `Domain.Models`? → **L2**
- Imports Orleans? → **L3**
- Imports CsvHelper or ClosedXML? → **L4**
- Imports WinUI? → **L5**

**Look at filename**:
- Ends with `Grain.cs`? → **L3**
- Ends with `ViewModel.cs`? → **L5**
- Contains `Parser` or `Exporter`? → **L4**
- Contains `Resolver` or `Validator`? → **L2**
- Is a `record` or `enum`? → **L1**
- Is `Identifier` or base class? → **L0**

## Token Budget Guidelines

### By Task Type
- **Simple bug fix**: 5-10K tokens
- **Algorithm optimization**: 10-15K tokens
- **Feature addition**: 20-30K tokens
- **UI change**: 15-25K tokens
- **I/O modification**: 8-12K tokens
- **Architectural refactor**: 40-60K tokens (rare)

### Context Loading Budget
- **L0 Foundation**: 2K tokens (load rarely)
- **L1 Domain**: 8K tokens (load interfaces only)
- **L2 Business Logic**: 12K tokens per service (load specific service)
- **L3 Orchestration**: 15K tokens (load relevant orchestrator)
- **L4 I/O**: 6K tokens per component (load specific parser/exporter)
- **L5 UI**: 20K tokens (load specific view + viewmodel)

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: "Load Everything for Context"
```
Agent loads: L0 + L1 + L2 + L3 + L4 + L5
Token cost: 80K
Reasoning: "I need to understand the full system"
```
**Why wrong**: Most of that context is irrelevant to the specific task.

**Correct approach**:
```
Agent identifies: This is an L3 orchestration change
Agent loads: L3 files + L2 interfaces + L1 DTOs
Token cost: 20K
Savings: 60K (75%)
```

### ❌ Anti-Pattern 2: "Load Implementation When Interface Sufficient"
```
Agent loads: Full DependencyResolver.cs (500 lines)
To understand: What method to call
```
**Why wrong**: Only need method signature, not implementation.

**Correct approach**:
```
Agent loads: IDependencyResolver interface (50 lines)
Token cost: 10x less
```

### ❌ Anti-Pattern 3: "Load Layer for Information Available in Docs"
```
Agent loads: Identifier.cs, TimeOfDay.cs
To understand: How value objects work
```
**Why wrong**: Generic patterns documented in architecture docs.

**Correct approach**:
```
Agent references: Architecture doc (already in context)
Token cost: 0 (already loaded)
```

## Verification Checklist

Before starting work, ask:
- [ ] What layer does this task target? (L0-L5)
- [ ] Do I need full implementation or just interfaces?
- [ ] Are there cross-layer dependencies?
- [ ] Can I reference architecture docs instead of loading code?
- [ ] Is my token budget <30K for this task?

## Summary: Token Optimization

**Key Insight**: Load only what you'll **modify**, reference what you'll **call**.

**Token Savings by Layer Isolation**:
- Foundation refactor: 40% savings (load L0-L1, skip rest)
- Algorithm fix: 85% savings (load L2 only)
- Feature addition: 70% savings (load L3, reference L2)
- UI change: 80% savings (load L5, reference L3)
- I/O modification: 85% savings (load L4 only)

**Average savings**: 70-85% token reduction per task

**Delivery time impact**: 50% faster (less context to process)

**Quality impact**: Fewer cross-layer bugs (changes isolated)
