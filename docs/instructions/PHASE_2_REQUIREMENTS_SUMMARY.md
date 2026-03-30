# Phase 2 Requirements Summary (Desktop + Orleans)

**Status**: Requirements Complete - Ready for Implementation  
**Date**: March 26, 2026  
**Version**: 1.0

---

## Executive Decision: Desktop Application (NOT Web)

### User Request
> "I do not want to build a WebApp or WebApi. I want a desktop application that runs on my Windows 11 computer."

### Solution: WinUI 3 + Orleans (Embedded) 

**Single-Process Desktop Architecture**:
- **OS**: Windows 11 (build 22621+)
- **Framework**: .NET 8 with WinUI 3 native UI
- **Backend**: Orleans 8.x (embedded in desktop process)
- **No web server, no database, no API → Single .exe/.msix file**

---

## Architecture Overview

### Three Core Components

1. **Orleans Silo** (Embedded)
   - 4 grain types handle distributed calculation
   - In-process, no remote calls
   - Memory-only state (Phase 2)
   - Optional: LocalHost dashboard for monitoring (port 8080)

2. **Desktop GUI** (WinUI 3)
   - Native Windows 11 Fluent Design
   - Dashboard, Timeline (Gantt), Violations, Settings windows
   - MVVM pattern with Community Toolkit
   - Direct grain client access

3. **CSV Processing** (Reused from Phase 1)
   - Task Definitions CSV
   - Intake Events CSV
   - Duration Manifest CSV (optional)

### File → Execution Flow

```
User selects 3 CSVs
    ↓
CSVParser (Phase 1 reused)
    ↓
ExecutionEventDefinition Matrix
    ↓
Create OrchestratorGrain + 150 InstanceGrains
    ↓
Persist grain state to SQLite
    ↓
ROUND 1: All grains validate in parallel
    ↓
Convergence check (any positions changed?)
    ↓
    ├─ NO changes → CONVERGED (display results, save state)
    └─ YES changes → ROUND 2 (continue iterating)
    ↓
Build ExecutionPlan (valid instances + chains)
    ↓
Display in Dashboard, Timeline, Violations views
    ↓
Optional: Export as Excel (calendar visualization for non-technical staff)
Optional: Export as CSV
```

---

## Key Features

### Dashboard Window
- Execution statistics (total, valid, invalid tasks)
- **Convergence progress** - shows Round 1, 2, 3...
- Deadline violations summary
- Quick links to business groupings

### Timeline Window (Gantt Chart)
- Horizontal scrollable task timeline
- Color-coded: Green (valid), Red (invalid), Yellow (critical path)
- Right-click for task details & prerequisites
- Deadline lines at scheduled deadline times

### Violations Window
- Table of deadline misses
- Sortable by severity (Task ID, Miss Amount, etc.)
- Export to CSV
- Filter by business grouping

### Convergence Tracking
- **Real-time visibility** into multi-round refinement
- Display round-by-round position changes
- Show "Task 2 shifted +45 minutes due to upstream change"
- Cancel button if calculation taking too long

---

## Technology Stack (Confirmed)

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Backend: Distributed** | Orleans | 8.x | Parallel grain execution |
| **Backend: Persistence** | SQLite (Orleans.Persistence.AdoNet) | 3.x | Durable grain state in `data/TaskSequencer.db` |
| **Backend: Orchestration** | .NET Hosting | 8.x | Orleans silo in desktop app |
| **Frontend: Native GUI** | WinUI 3 | 1.6.x | Windows 11 desktop app |
| **Frontend: UI Patterns** | MVVM Toolkit | 8.x | ViewModel binding |
| **Frontend: Data Grid** | WinUI DataGrid | 8.x | Task table + timeline columns |
| **Data: CSV Parsing** | CsvHelper | 31.x | Parse 3 CSV formats |
| **Export: Excel** | ClosedXML + DocumentFormat.OpenXml | 0.21+ | Generate Excel with calendar visualization |

### Project Structure

```
TaskSequencer/
├─ src/
│  ├─ Orleans/
│  │  └─ Grain implementations (4 types)
│  ├─ DesktopApp/          ← NEW: WinUI 3 project
│  │  ├─ Views/             (XAML windows)
│  │  ├─ ViewModels/        (MVVM pattern)
│  │  ├─ Services/          (GrainClient, file, etc.)
│  │  └─ Program.cs         (Orleans silo init + App startup)
│  └─ ConsoleApp/           (Phase 1: kept for validation)
├─ tests/
│  └─ Orleans + Desktop tests
└─ docs/
   └─ All requirements (updated)
```

---

## Four Grain Types

### 1. IExecutionInstanceGrain
- **One per task execution** (150+ grains for typical plan)
- Validates deadline constraints independently
- Calculates resolved start time accounting for prerequisites
- Tracks position changes between rounds
- Returns: (IsValid, PlannedCompletion, PositionDelta)

### 2. IExecutionPlanOrchestratorGrain
- **One per scheduling increment** (e.g., "Monday")
- Coordinates multi-round refinement
- Detects convergence (when no positions change)
- Aggregates results into ExecutionPlan
- Passes results to ReportGeneratorGrain

### 3. ISequenceGroupGrain
- **One per business domain** (~20 grains for typical org: PAYROLL, SETTLEMENT, REPORTING, etc.)
- Aggregates task instances by grouping
- Calculates group-specific metrics & critical paths
- Filters execution plan to group's tasks

### 4. IReportGeneratorGrain
- **One per report** (~1-2 grains)
- Aggregates all DifferenceSequences
- Formats data for UI consumption
- Tracks convergence metrics (rounds, time, changes/round)

---

## Iterative Refinement Algorithm

### Multi-Round Calculation Until Convergence

**Round 1**:
- All InstanceGrains initialize with provisional times
- Each independently validates against deadline
- Mark position changes

**Round 2+** (if Round 1 had changes):
- Affected tasks recalculate based on new upstream completions
- Position deltas tracked and passed to dependents
- Continue until: no new position changes → **CONVERGED**

**Difference Sequence** = Sequence of position adjustments per round:
```
Round 1 → Task 2 adjusted +45min due to Task 1 longer duration
Round 2 → Task 3 adjusted +15min due to Task 2's adjustment
Round 3 → No adjustments → CONVERGED
```

---

## Implementation Timeline

**16–17 Sprints, 16–17 Weeks**

| Phase | Sprints | Duration | Focus |
|-------|---------|----------|-------|
| Infra | 1–2 | 2 weeks | Orleans + SQLite persistence + 4 grain types |
| Initialization | 3 | 1 week | CSV → Matrix → Grains |
| Grain Logic | 4–9 | 5 weeks | InstanceGrain, OrchestratorGrain, GroupGrain, ReporterGrain |
| **Desktop GUI** | **10–11** | **2 weeks** | **WinUI 3 windows, MVVM, Orleans client, Excel export** |
| **Convergence & Visualization** | **12** | **1 week** | **Real-time round tracking, difference sequences** |
| Testing | 13–14 | 2 weeks | E2E validation, error handling |
| Packaging | 15 | 1 week | .msix package, user docs |
| Optimization | 16 | 1 week | Performance tuning |
| **(Optional) Advanced Reporting** | **17** | **1 week** | **Excel calendar grid, risk analysis, executive export** |

---

## New Phase 2 Features: SQLite & Excel Export

### SQLite Persistence
- **Auto-created database**: `<AppPath>\data\TaskSequencer.db`
- **Automatic backup**: `TaskSequencer.db.backup.<date>` on app shutdown (keeps last 10)
- **Grain state fully persistent**: All 150+ grains saved to SQLite automatically
- **Recovery**: Grains restore state on app restart
- **Performance**: ~5-10ms per grain state write (batched, async)
- **No external database needed**: Everything local & portable

### Excel Export for Non-Technical Staff
- **File Format**: Modern Excel (.xlsx) with rich formatting
- **5 Analysis Tabs**:
  1. **Summary Tab** - Plan metadata, statistics, deadline violations count
  2. **Timeline Calendar Tab** - Time-based grid showing all tasks, groups, parallel execution
  3. **Task Details Tab** - Complete task listing with dependencies, deadlines, violations
  4. **Dependency Chains Tab** - Text-based chain visualization showing task sequences
  5. **Risk Analysis Tab** - Identifies bottleneck tasks, at-risk forecasts, mitigation recommendations

- **Visual Indicators**:
  - 🟢 Green = Task on schedule
  - 🟡 Yellow = Task at-risk (may miss deadline)
  - 🔴 Red = Task deadline violated

- **Key Benefits**:
  - Multiple tasks can be shown in same time block (stacked/wrapped)
  - Simple tasks with no dependencies clearly marked "standalone"
  - Dependent chains show "Task A → Task B → Task C" flow
  - Non-technical user guide embedded in Excel
  - Exportable to PowerPoint, email, reporting systems
  - Professional presentation for executive/management review

### Export File Example
```
ExecutionPlan_2024-03-26.xlsx
├─ Summary
│  └─ Total: 145 tasks, 143 valid, 2 violations
├─ Timeline Calendar
│  └─ 06:00-18:00 grid shows PAYROLL, SETTLEMENT, REPORTING groupings
├─ Task Details
│  └─ Full task list with all constraints & deadlines
├─ Dependency Chains
│  └─ Text chains: "Task 1 → Task 2 → Task 3..."
├─ Risk Analysis
│  └─ Bottlenecks (Task 2 has 15 dependents), at-risk tasks
└─ Instructions (for readers)
   └─ How to read the report (hidden tab for experienced users)
```

---

## Deployment Model

### Phase 2: Local Development
```bash
dotnet run --project DesktopApp/DesktopApp.csproj
# Starts WinUI 3 app with embedded Orleans silo
# Orleans dashboard optional: http://localhost:8080
```

### Phase 2: Distribution  
```bash
dotnet publish -c Release -p:WindowsPackageVersion=1.0.0.0
# Creates: TaskSequencer_1.0.0.0_x64.msix (~180 MB)
# User double-clicks → MSIX installer → App installed
# Future: .appinstaller file for auto-updates (Phase 3)
```

### System Requirements
- **OS**: Windows 11 build 22621 or later
- **RAM**: 4 GB minimum, 8 GB recommended  
- **Storage**: ~150 MB (includes .NET 8 runtime in package)
- **Display**: 1920x1080 minimum recommended

---

## Key Decision Points Locked In

✅ **Confirmed**:
- [x] Orleans grains for parallel calculation
- [x] WinUI 3 for Windows 11 desktop GUI (NOT web)
- [x] Embedded Orleans silo (NOT remote services)
- [x] **SQLite for durable grain persistence** (local `data/TaskSequencer.db`)
- [x] **Excel export with calendar visualization for non-technical staff**
- [x] Async iterative refinement until convergence
- [x] Multi-round difference sequence tracking
- [x] 4 grain types (Instance, Orchestrator, Group, Report)
- [x] Aspire for local dev orchestration
- [x] Desktop-only → no API, no database, no web server

❌ **NOT in scope Phase 2**:
- Web API or REST endpoints
- React frontend
- ASP.NET Core API layer
- External database
- Multi-user authentication
- Network communication
- Kubernetes/cloud deployment

**Phase 3+ (future)**:
- Persistent state storage (SQL/SQLite)
- Real execution monitoring
- Multi-user support
- Cloud deployment
- Webhook notifications

---

## Files Updated

✅ **Documentation Complete**:
- [ ] `01-architecture-requirements.md` - Updated for desktop + Orleans
- [ ] `03-orleans-aspire-architecture.md` - Desktop-focused grain architecture
- [ ] `04-implementation-plan-phase-2.md` - 16-week sprint breakdown (desktop)
- [ ] `05-technology-stack-web-gui.md` - WinUI 3 instead of web stack (NEW)

✅ **New Documents Created**:
- [x] `03-orleans-aspire-architecture.md` - Grain types & communication patterns
- [x] `04-implementation-plan-phase-2.md` - Detailed sprint breakdown
- [x] `05-technology-stack-web-gui.md` - Technology decisions (WinUI 3)

---

## Success Criteria

Phase 2 is **COMPLETE** when:

- [ ] Single .msix file installs cleanly on Windows 11
- [ ] CSV files load and calculate plan in < 3 seconds
- [ ] Timeline renders 150+ tasks smoothly with horizontal scrolling
- [ ] Convergence progress shows Rounds 1, 2, 3... in real-time
- [ ] Deadline violations highlighted in red with miss amounts
- [ ] Dark mode / Light mode toggle works
- [ ] Recent files remembered across app restarts
- [ ] Export to CSV works
- [ ] Help documentation accessible from app
- [ ] All Orleans grains tested with 5 test scenarios
- [ ] Performance: < 500ms UI update latency

---

## Ready for Implementation

**All requirements documented. No code changes until Phase 2 development begins.**

**Next Step**: Approval to proceed with Sprint 1 (Orleans infrastructure).

---

## Contact & Clarification

**Questions on requirements?** Review these documents in order:
1. `01-architecture-requirements.md` - High-level overview
2. `03-orleans-aspire-architecture.md` - Technical architecture
3. `04-implementation-plan-phase-2.md` - Sprint-by-sprint breakdown
4. `05-technology-stack-web-gui.md` - Technology choices explained
