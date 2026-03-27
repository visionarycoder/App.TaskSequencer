# Architecture

## Overview

App.TaskSequencer is a scheduling and dependency-resolution tool that reads task definitions from CSV files and produces an executable plan that respects all inter-task dependencies while honoring time constraints.

The initial development phase uses a single **Console application** as the host. This keeps the feedback loop short and avoids infrastructure overhead while the core domain logic is being designed and validated.

---

## Development Approach

### Phase 1 – Console Application

All logic is developed and exercised inside a single `ConsoleApp` project. There are no additional layers, services, or infrastructure projects during this phase. This lets the team:

- Iterate quickly on the domain model.
- Validate CSV parsing and dependency resolution without deployment complexity.
- Defer architectural decisions (e.g., REST API, background service) until the domain is stable.

---

## Business Requirements

### Input Files

The system consumes **two CSV files** that together fully describe the work to be sequenced.

#### 1. Availability Window File

Defines when the remote system expects **all input files for a task** to be ready.

| Column | Description |
|--------|-------------|
| Interface Number | Unique task identifier (see [Task Identity](#task-identity)) |
| Expected Availability Time | The deadline by which all input data for this task must be present |

#### 2. Task Definition File

Contains the full definition of each task.

| Column | Description |
|--------|-------------|
| Interface Number | Unique task identifier |
| Interface Name | Human-readable name for the task (also called the *Interface*) |
| Execution Type | `OnDemand` or `Scheduled` |
| Schedule Type | `OneOff` or `Recurring` (applicable when Execution Type is `Scheduled`) |
| Suggested Start Time | A human-estimated start time; provided without knowledge of dependencies |
| Precursor Interface Numbers | Comma-separated list of Interface Numbers that must complete before this task may run (may be empty) |

---

### Task Identity

- The **Interface Number** is the sole definition of uniqueness for a task.
- It is a legacy identifier carried over from the mainframe system being replaced.
- The term *Interface* and *Interface Number* are used interchangeably in source materials; both refer to the same concept.

---

### Execution Model

#### On-Demand Tasks

- Run when explicitly triggered.
- Are not bound to a scheduled time.

#### Scheduled Tasks

- **One-off**: Runs once at a specified time and does not repeat.
- **Recurring**: Repeats on a defined cadence.

#### Suggested Start Time

- Provided in the Task Definition File by a human author.
- The author has **no visibility into dependencies** when this value is set; it is advisory only.
- The sequencer must adjust actual start times to satisfy all dependency constraints.

---

### Dependency Rules

- A task may declare one or more **precursor tasks** (other Interface Numbers that must finish first).
- A task **must not begin execution** until every one of its precursor tasks has completed successfully.
- The suggested start time is overridden whenever dependency constraints require a later start.
- Tasks with no precursors may start as soon as their input data is available and their scheduled time (if any) is reached.

---

## Key Constraints and Invariants

| Constraint | Description |
|------------|-------------|
| Unique identifier | Interface Number uniquely identifies a task across both CSV files |
| Dependency ordering | No task runs before all its precursors are complete |
| Suggested time is advisory | Actual start time ≥ suggested start time, subject to dependency satisfaction |
| Input availability | A task may not start before all required input files are available (per the Availability Window File) |

---

## Glossary

| Term | Definition |
|------|------------|
| Interface | A schedulable unit of work; equivalent to *task* in this system |
| Interface Number | The unique numeric identifier for an Interface; legacy term from the replaced mainframe |
| Precursor | A task that must complete before a dependent task may begin |
| Suggested Start Time | Advisory start time provided by a human; may be adjusted by the sequencer |
| Availability Window | The time by which all input data for a task must be present |
