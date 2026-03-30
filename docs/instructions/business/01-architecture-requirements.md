# Architecture & Business Requirements

## Overview

**App.TaskSequencer is a DISTRIBUTED PLANNING SYSTEM.** It orchestrates task execution within an interconnected financial ecosystem by:

1. **Loading all available input CSV files** in their entirety
2. **Analyzing dependencies and timing constraints** across all tasks
3. **Calculating feasible execution sequences** through iterative refinement via Orleans grains
4. **Resolving scheduling conflicts** across parallel difference calculations
5. **Detecting deadline violations** and infeasible task orderings
6. **Providing desktop-based visualization** of execution sequences per grouping

The system uses **Microsoft Orleans** for distributed, parallel processing of task scheduling calculations, and **Microsoft Aspire** for development orchestration. All execution instances are autonomous grain actors that perform independent deadline validation and coordinate through the orchestrator grain. The desktop GUI runs on Windows 11 with an embedded Orleans silo—no external services required.

---

## Technology Stack

### Core Infrastructure
- **Orleans 8.x** - Distributed actor model for parallel grain execution
  - IExecutionInstanceGrain - autonomous grain per task execution
  - IExecutionPlanOrchestratorGrain - coordination and plan aggregation
  - ISequenceGroupGrain - domain grouping/categorization
  - IReportGeneratorGrain - report composition
  
- **Microsoft Aspire** - Development orchestration
  - Orchestrates embedded Orleans silo in desktop app
  - Local development environment with optional dashboard
  - Configuration management

- **WinUI 3** - Native Windows 11 desktop GUI
  - Dashboard visualization of execution sequences
  - Grouping-based sequence reports
  - Deadline miss detection and visualizations
  - Iterative reprocessing progress tracking (multi-round visualization)
  - Timeline/Gantt chart for task scheduling view

### Data Processing
- CsvHelper (.NET) - CSV parsing for manifests
- System.Collections.Immutable - Immutable domain models

---

## Development Phases

### Phase 1 – Console Application ✅ (COMPLETED)

Core domain model and CSV parsing validated in single ConsoleApp project.

### Phase 2 – Orleans + Aspire Desktop Planning (IN PROGRESS)

Refactor console logic into Orleans grains with iterative refinement algorithm.

- IExecutionInstanceGrain - parallel deadline validation per task
- IExecutionPlanOrchestratorGrain - orchestrates plan generation
- ISequenceGroupGrain - groups tasks by domain category
- IReportGeneratorGrain - produces reporting data
- Aspire host + service architecture

### Phase 3 – Web Reporting GUI (PLANNED)

ASP.NET Core + React/Vue web application providing:
- Sequence visualization per grouping
- Execution timeline and dependency graphs
- Deadline miss highlighting
- Iterative refinement progress dashboard

---

## Business Requirements

### Business Context

App.TaskSequencer orchestrates task execution within an interconnected financial ecosystem. Tasks function as **data movers** across system boundaries:

- **Triggering Events**: Activities like payroll cascade through dependent tasks
- **Distributed Dependencies**: Tasks depend on work completed on remote systems
- **Critical Path**: Some tasks are dependencies for downstream systems
- **Cross-Boundary Data Flow**: Data transits repeatedly across system boundaries until all transactions are 100% complete
- **Deadline Constraints**: Each task must complete **before** its intake event occurs

The system calculates feasible execution schedules that respect both predecessor dependencies and intake event deadlines through **iterative refinement**, recalculating task positions across multiple passes until convergence is achieved.

---

## Phase 2: Orleans + Aspire Desktop Application (IN PROGRESS)

Refactor console logic into Orleans grains with embedded Windows 11 desktop GUI.

- IExecutionInstanceGrain - parallel deadline validation per task
- IExecutionPlanOrchestratorGrain - orchestrates plan generation
- ISequenceGroupGrain - groups tasks by domain category
- IReportGeneratorGrain - produces reporting data
- WinUI 3 Desktop Application - sequence visualization & reporting
- Embedded Orleans silo in desktop process
- Aspire orchestration for local development

### Web Stack REPLACED with Desktop Architecture

**Why Desktop Instead of Web?**

| Aspect | Desktop (Phase 2) | Web (Alternative) |
|--------|-------|---------|
| Deployment | Single .msix file | API server + Frontend hosting + Database |
| Complexity | Simplified | 3+ services to coordinate |
| Network | None required | Required |
| Authentication | Not needed | Required |
| Offline Use | ✓ Fully operational | ✗ Must connect to server |
| Development Speed | Faster (single process) | Slower (3 moving parts) |
| User Experience | Native Windows 11 | Browser-based |

**Desktop Decision**: For an internal planning tool on Windows 11, a desktop application provides superior user experience, simpler deployment, and faster development—without requiring a web architecture.

---

## Phase 3 – Real Execution Integration (PLANNED)

Monitor actual silo execution and integrate duration updates:

- Real-time monitoring of actual task executions
- Duration manifest periodic imports → plan recalculation
- Persistent state store (SQL/SQLite/Azure)
- Historical trending & predictive analytics
- Multi-user support (if needed)

---

## Input Files

The system consumes **three CSV files** that together fully describe the work to be sequenced.

### File 1: Task Definition CSV

Contains the full definition of each task with execution schedule.

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Unique task identifier |
| `Interface Name` | string | Human-readable task name |
| `Execution Type` | enum | `OnDemand` or `Scheduled` |
| `Schedule Type` | enum | `OneOff` or `Recurring` |
| `Duration Minutes` | uint | Expected execution duration |
| `Suggested Start Time` | time | Initial timing suggestion |
| `Precursor Interface Numbers` | csv-list | Comma-separated task IDs that must complete first |
| `Execution Days` | pipe-list | Pipe-separated day names (e.g., `Monday\|Wednesday\|Friday`) |
| `Execution Times` | pipe-list | Pipe-separated execution times (e.g., `06:00:00\|14:00:00`) |

### File 2: Availability Window / Intake Event Requirements CSV

Defines completion deadlines for each task.

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Task identifier (links to Task Definition CSV) |
| `Monday` through `Sunday` | enum | `X` marks days when task must be completed |
| `Intake Time` | time | Deadline time on those days by which task must complete |

### File 3: Execution Duration Manifest (Optional, Imported Periodically)

Captures actual execution times as tasks complete in the real world.

| Column | Type | Description |
|--------|------|-------------|
| `Interface Number` | string | Task identifier |
| `Execution Date` | date | Date when task executed |
| `Execution Time` | time | Scheduled start time for this execution |
| `Actual Duration Minutes` | uint | Real elapsed time |
| `Status` | enum | `Completed`, `Failed`, `Timeout`, etc. |

---

## Task Identity

- The **Interface Number** is the sole definition of uniqueness for a task.
- It is a legacy identifier carried over from the mainframe system being replaced.
- The term *Interface* and *Interface Number* are used interchangeably in source materials; both refer to the same concept.

---

## Execution Model (Planning Phase Only)

### Stateless Planning

- **Input**: Load all three CSV files completely in a single batch operation
- **Processing**: Analyze dependencies, timing conflicts, and feasibility for all sequences
- **Output**: Consolidated execution plan with all sequences marked valid or failed
- **No Real-Time Execution**: The app does not execute, monitor, or track actual task runs
- **No State Persistence**: No grains, agents, or persistent state; everything is derived from CSV inputs during planning
- **No Execution Management**: Planning is a one-time, batch operation with no redo, retry, or rebasing logic

### Calendar & Sequencing

- **Calendar Blocks**: Each task occupies a 15-minute block in a unified calendar
- **Block Granularity**: A 60-minute task occupies 4 consecutive blocks; a 90-minute task occupies 6 blocks
- **Time Continuity**: Once a sequence starts, it continues uninterrupted until completion
  - Tasks do not have to fit within a 24-hour interval
  - If a 60-minute task starts at 23:30 PST, it spans: 23:30–23:45, 23:45–00:00, 00:00–00:15, 00:15–00:30 (next day)
  - All time will use Pacific Time; DST transitions must be flagged
- **Single Consolidated Calendar**: All sequences from all execution contexts occupy the same unified calendar (no parallel silos)

### Execution Isolation & Matrix Concept

- **Sequence**: A top-level execution context representing one scheduled manifestation of a set of tasks
  - Example: "Monday Payroll 06:00", "Wednesday Payroll 08:00", "Friday Payroll 06:00"
  - Each sequence corresponds to a row in the ExecutionEventDefinition → ExecutionInstance matrix
  - Each sequence is a distinct node in the execution tree

- **Execution Instance**: An individual task execution within a sequence
  - Represents one task at one scheduled time in one sequence context
  - Contains: resolved start time, end time, validity status, reason for failure (if any)
  - Parent: The sequence
  - Children: Execution instances of dependent tasks within same sequence

- **Matrix of Sequences with Child Executions**: 
  - Rows: Task definitions (Interface Numbers)
  - Columns: Scheduled execution contexts (e.g., "Monday 06:00", "Wednesday 08:00")
  - Cells: Execution instances (resolved start times, end times, validity status)
  - Tree Structure: Parent sequence → Child execution instances → grandchild dependents

- **Context Isolation (CRITICAL)**:
  - Each sequence context is **completely independent**
  - Task A in "Monday Payroll" does not interact with Task A in "Wednesday Payroll"
  - No shared state between sequences
  - No cascading side effects across sequences
  - Each sequence's failure (deadline miss) is isolated to that sequence only

### No Redo / No Execution Management

- **No Retry Logic**: If a sequence's configuration makes deadlines impossible, it is **flagged as FAILED** immediately
- **No Rebasing**: Once the plan is generated, there is no mechanism to adjust timing
- **No Persistent State**: All information is derived fresh from CSV inputs; no state survives between planning runs
- **No Progress Tracking**: The app does not track real-world task completions or adapt based on actual execution
- **Frozen Schedule**: ExecutionPlan is a snapshot; it does not update or recalculate

### Failure Flagging & Validation

- **Timing Conflict**: If precursor task chains + intake deadline constraints make a sequence impossible, flag it as **FAILED**
- **Feasibility Check**: For each execution instance, validate:
  - Can all precursor tasks complete before this task's required start time?
  - Can this task complete before its intake deadline?
  - Is the calculated start time ≥ the suggested start time?
- **ExecutionPlan Output**:
  - ✅ **Valid**: Execution instances with optimized start times (resolved conflicts, met deadlines)
  - ❌ **Failed**: Execution instances marked invalid with reason (e.g., "Deadline infeasible due to precursor chain exceeding intake deadline")
- **No Partial Recovery**: A sequence either is valid or marked as failed; no partial completion or workarounds

---

## Task Identity

- The **Interface Number** is the sole definition of uniqueness for a task.
- It is a legacy identifier carried over from the mainframe system being replaced.
- The term *Interface* and *Interface Number* are used interchangeably in source materials; both refer to the same concept.

---

## Dependency Rules & Scheduling Conflicts

### Core Dependency Rule

- A task may declare one or more **precursor tasks** (other Interface Numbers that must finish first).
- A task **must not begin execution** until every one of its precursor tasks has completed successfully.
- The suggested start time is overridden whenever dependency constraints require a later start.
- Tasks with no precursors may start as soon as their scheduled time (if any) is reached.

### Precursor Task Scheduling Problem (Core Reason for This System)

**The Problem**: All precursor tasks in a manifest are scheduled with the **same start time**. If multiple independent tasks are scheduled to start at, say, 06:00 AM, but one of them depends on another, there will be logical failures.

**Example of Conflict**:
```
Task A: Scheduled 06:00 AM (no dependencies)
Task B: Scheduled 06:00 AM, depends on Task A
Task C: Scheduled 06:00 AM, depends on Task A
```
Both Task B and Task C are scheduled to start at 06:00 AM, but Task A must finish first. Without re-scheduling, both fail.

**The Solution**: The sequencer must identify these conflicts and re-schedule dependent tasks to start **after** their precursor tasks complete, while respecting intake deadlines and other constraints.

**Sequencing Logic**:
- Group tasks by their precursor relationships
- For each group, adjust the start times to resolve conflicts
- Dependent tasks start after their precursors finish (start time = precursor's end time)
- Validate that the re-scheduled completion time does not violate the intake deadline
- If re-scheduling violates a deadline, mark the sequence as **FAILED**

### Time Zone & Daylight Saving Time

- **All times are Pacific Time** (no UTC conversion; local PT only)
- **DST Transitions**: Any task schedule that crosses DST boundaries (March & November) must be flagged with a warning
  - When clocks "spring forward" (2:00 AM → 3:00 AM), a task scheduled for 2:30 AM becomes ambiguous
  - When clocks "fall back" (2:00 AM occurs twice), a task could run twice
  - The sequencer alerts on any execution context crossing a DST boundary

### Contextual Timing (Manifest-Scoped)

- **Timing Context**: All times for a given manifest are contextualized within that manifest's calendar period
- **Multiple Contexts**: A task may run differently in different contexts throughout a calendar year (e.g., weekly payroll vs. monthly close)
- **No Conflicts Within Manifest**: There will be no time conflicts or logical contradictions within any single manifest
- **Different Manifest, Different Rules**: When manifests change, the timing rules may also change; each manifest is treated independently

### Suggested Start Time

- Provided in the Task Definition File by a human author
- The author has **no visibility into dependencies** when this value is set; it is advisory only
- The sequencer must adjust actual start times to satisfy all dependency constraints
- Adjusted start time must be ≥ suggested start time

---

## Intake Event Constraints

- **Intake Event**: A downstream system deadline requiring the task's output at a specific time
- **Deadline**: Task execution must complete before the intake event occurs
- **Hard Constraint**: Missing an intake deadline is a validation failure; sequence marked invalid
- **Cascading Failure (CRITICAL)**: If Task A misses its deadline, all downstream tasks that depend on Task A are **automatically marked invalid** immediately
- **Root Cause**: The downstream task cannot complete successfully if its required input (from Task A) is not available before Task A's deadline

---

## Key Constraints and Invariants

| Constraint | Description |
|------------|-------------|
| Unique identifier | Interface Number uniquely identifies a task across all CSV files |
| Dependency ordering | No task runs before all its precursors are complete |
| Suggested time is advisory | Actual start time ≥ suggested start time, subject to dependency satisfaction |
| Intake deadline | Task must complete before intake event; failure marks sequence as invalid |
| Time zone | All times in Pacific Time; flag DST transitions with warnings |
| Manifest scope | Timing rules contextualized per manifest; no conflicts within manifest |
| Conflict resolution | Re-schedule dependent tasks to avoid same-start-time conflicts |
| Precursor chain | All precursor tasks have same advisory start time; sequencer must chain them |
| Backfill dependency | If a daily task was skipped/missed, today's instance depends on yesterday's instance (even if it didn't run) |
| Sequence isolation | Each sequence context is fully independent; failures do not cascade across sequences |
| Stateless planning | No persistent state; all data derived from CSV inputs; plan is frozen output |
| Calendar blocks | Each task occupies 15-minute blocks; sequences continue uninterrupted across 24h boundaries |
| No redo | No retry, rebase, or progress tracking; plan is generated once and output |
| Late precursor | If precursor Task B misses deadline, dependent Task A immediately fails validation (no retry) |
| Multi-system coordination | External systems poll our database ("pull model"); we don't push notifications |
| Transactional blackout | Certain periods (e.g., end-of-month close) suspend all task scheduling (no data in flight) |
| Repeated data iterations | Data cycles repeatedly through system boundaries; re-run all tasks each iteration |

---

## Architecture Notes: No Orleans Needed for Planning

**This is a PLANNING-ONLY tool. Orleans grains are not required.**

The initial requirement to use Orleans was based on the assumption that the system would manage real-time task executions and maintain state. Since App.TaskSequencer is a batch planning tool that:

1. Loads all CSV files once
2. Analyzes all sequences simultaneously
3. Outputs a frozen execution plan
4. Does not track or execute tasks

**Orleans components are not necessary**. The system can be implemented as a simple Console application with:

- CSV parsing services
- Domain model (ExecutionInstance, ExecutionPlan, etc.)
- Dependency resolution engine
- Planning algorithm
- Calendar output

**Possible future use of Orleans**: If a separate **execution engine** is built to actually run the calendar generated by TaskSequencer, that engine could use Orleans grains to manage real-time execution state. But that would be a separate application.

---

## Glossary

| Term | Definition |
|------|------------|
| Interface | A schedulable unit of work; equivalent to *task* in this system |
| Interface Number | The unique numeric identifier for an Interface; legacy term from the replaced mainframe |
| Sequence | A top-level execution context representing one scheduled manifestation of tasks (e.g., "Monday 06:00") |
| Execution Instance | An individual task execution within a sequence; contains resolved start/end times and validity status |
| Precursor | A task that must complete before a dependent task may begin |
| Suggested Start Time | Advisory start time provided in CSV; adjusted by sequencer to resolve conflicts |
| Intake Event | Downstream system deadline requiring task output at a specific time |
| Intake Deadline | Time by which a task must complete to satisfy its intake event |
| Execution Plan | Output containing all valid and failed execution sequences with resolved start/end times |
| Calendar Blocks | 15-minute time slots assigned to each task within a unified calendar |
| Feasibility | Whether a sequence can meet all intake deadlines given dependency constraints |
| Valid Execution | Sequence with all tasks scheduled, dependencies resolved, deadlines met |
| Failed Execution | Sequence where timing conflicts prevent meeting intake deadlines |
| Context Isolation | Independence of each sequence; failures in one do not affect others |
