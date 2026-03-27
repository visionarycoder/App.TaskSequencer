# Business Requirements & Architecture

Documentation covering the financial domain, business logic, system architecture, and Phase 2 Orleans + desktop GUI implementation.

---

## 📄 Documents in This Section

### 1. [Architecture & Business Requirements](01-architecture-requirements.md)
**Read First**: ✅ YES  
**Purpose**: Understand what the system does, business context, and technology choices

**Contains**:
- Financial services domain context
- CSV input file specifications (Task Definition, Intake Events, Duration History)
- Task identity and execution model
- Dependency rules and intake event constraints
- Orleans actor-based architecture overview
- WinUI 3 desktop GUI approach
- Key constraints and glossary

**When to Read**:
- Starting work on any feature
- Before implementing domain models
- To understand legacy mainframe integration (Interface Numbers)
- Understanding Phase 1 → Phase 2 transition

---

### 2. [Execution Sequencing Pipeline](02-execution-sequencing-pipeline.md)
**Read First**: ✅ YES (if implementing Orleans features)  
**Purpose**: Detailed design of the execution planning engine

**Contains**:
- Complete transformation pipeline (6 phases)
- Execution instance matrix generation
- Dependency resolution algorithm (2-phase with deadline validation)
- Execution plan concept (task chain linking from first to last task)
- Orleans grain architecture & lifecycle
- Duration estimation & refinement strategy (15-min defaults with incremental updates)
- Open questions requiring business clarification

**When to Read**:
- Implementing Orleans-based execution planning
- Working on dependency resolution
- Understanding how execution instances are created and validated
- Designing grain state management

---

### 3. [Orleans & Aspire Architecture](03-orleans-aspire-architecture.md)
**Read First**: ✅ YES (if implementing Phase 2)  
**Purpose**: Technical architecture for distributed grain-based planning

**Contains**:
- Four Orleans grain types with state contracts:
  - IExecutionInstanceGrain (per task execution)
  - IExecutionPlanOrchestratorGrain (convergence orchestration)
  - ISequenceGroupGrain (business domain grouping)
  - IReportGeneratorGrain (reporting aggregation)
- Grain lifecycle and communication patterns
- Single-process desktop model (embedded Orleans silo)
- Iterative refinement algorithm (multi-round convergence)
- Difference sequence tracking
- Aspire integration for local development

**When to Read**:
- Implementing Orleans grains
- Understanding multi-round convergence algorithm
- Grain state persistence & recovery
- Local development orchestration

---

### 4. [Phase 2 Implementation Plan](04-implementation-plan-phase-2.md)
**Read First**: ✅ YES (for planning Phase 2 work)  
**Purpose**: Sprint-by-sprint breakdown of 16-week Phase 2 development

**Contains**:
- 16 sprints across 4 phases: Infrastructure, Initialization, Grain Logic, Desktop GUI
- Detailed task breakdown per sprint
- Performance targets and success criteria
- Risk mitigation strategies
- Desktop app specific features (WinUI 3, MSIX packaging, file pickers)
- Testing scenarios and acceptance criteria
- Deployment mechanism (.msix package)

**When to Read**:
- Planning Phase 2 sprints
- Estimating work for team members
- Understanding dependencies between sprints
- Preparing testing strategy

---

### 5. [Technology Stack & Desktop GUI](05-technology-stack-desktop-gui.md)
**Read First**: ✅ YES (before implementation starts)  
**Purpose**: Technology choices, desktop architecture, and GUI design specifications

**Contains**:
- Orleans 8.x, WinUI 3, .NET 8 stack rationale
- Embedded Orleans silo architecture (single-process desktop app)
- MVVM pattern with MVVM Toolkit
- GUI screens: Dashboard, Timeline (Gantt), Groups, Violations
- WinUI 3 native Windows 11 integration
- Data binding architecture (ViewModel → XAML)
- Performance targets and scalability limits
- Startup architecture and dependency injection
- Security model (local-only desktop app, no authentication)
- Explicit rejection of web architecture for Phase 2
- Deployment model (.exe or .msix package for later)

**When to Read**:
- Before starting desktop app development
- Understanding embedded Orleans configuration
- Planning WinUI 3 GUI implementation
- Setting up MVVM binding patterns
- Defining performance targets

---

## 📚 Phase 2 Planning Documents

### [Phase 2 Requirements Summary](../PHASE_2_REQUIREMENTS_SUMMARY.md)
**Quick reference** for Phase 2 architecture decisions and technology stack.

---

## 🎯 Quick Navigation by Task

| Task | Documents |
|------|-----------|
| "Understand what we're building" | 01-architecture-requirements.md |
| "Implement Orleans grains" | 03-orleans-aspire-architecture.md, 02-execution-sequencing-pipeline.md |
| "Design CSV parsers" | 01-architecture-requirements.md (file specs section) |
| "Build dependency resolver" | 02-execution-sequencing-pipeline.md (algorithm section) |
| "Implement execution plans" | 02-execution-sequencing-pipeline.md (execution plan section) |
| "Build desktop GUI" | 05-technology-stack-desktop-gui.md, 04-implementation-plan-phase-2.md (Sprints 10-12) |
| "Plan Phase 2 sprints" | 04-implementation-plan-phase-2.md |
| "Quick Phase 2 overview" | ../PHASE_2_REQUIREMENTS_SUMMARY.md |

---

## 📊 Key Concepts at a Glance

### Three Input CSV Files
1. **Task Definition** - What tasks exist and their schedule
2. **Intake Events** - Deadline constraints per task per day
3. **Duration History** - Actual execution times (optional, imported periodically)

### Transformation Phases
1. Load & parse CSV files
2. Resolve execution durations (15-min default → actual data)
3. Build intake event requirements
4. Create task definitions with type safety
5. Generate execution matrix (days × times combinations)
6. Resolve dependencies with deadline validation
7. Create execution instances (valid/invalid marked)
8. Build execution plans (task chains with critical path)

### Orleans Grain Model (Phase 2)
- **IExecutionInstanceGrain**: One per task execution (150+ grains typical)
- **IExecutionPlanOrchestratorGrain**: One per scheduling increment (coordinates convergence)
- **ISequenceGroupGrain**: One per business domain (PAYROLL, SETTLEMENT, etc.)
- **IReportGeneratorGrain**: One per report (aggregates results for UI)
- **Multi-round convergence**: Iterative refinement until no position changes

### Desktop GUI (Phase 2)
- **Dashboard**: Statistics, convergence progress, violations summary
- **Timeline**: Gantt chart with task positioning across time
- **Violations**: Table of deadline misses with export/sort
- **Settings**: User preferences, recent files, theme selection

### Day 1 Strategy
- All tasks get **15-minute default duration**
- Plans generated immediately with estimates
- Actual durations imported later
- Plans recalculated with refined data via difference sequences
