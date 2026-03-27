# App.TaskSequencer

App.TaskSequencer reads task definitions from CSV files and produces an executable plan that runs every task on time while honouring all inter-task dependencies.

## What it does

- Parses two CSV input files: an **Availability Window** file and a **Task Definition** file.
- Resolves execution order so that no task starts before its precursor tasks have completed.
- Adjusts suggested start times when dependency constraints require a later start.
- Supports on-demand tasks and scheduled tasks (one-off or recurring).

## Documentation

- [Architecture & Business Requirements](docs/architecture.md)
- [Coding Standards & Patterns](docs/readme.md)
- [Documentation Index](docs/index.md)
