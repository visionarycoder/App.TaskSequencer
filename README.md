# App.TaskSequencer

**Distributed task sequencing system** that orchestrates parallel execution of interdependent tasks using **Microsoft Orleans** for parallel processing and **WinUI 3** for visualization (Phase 2).

## What it does

- **Phase 1 (Console App)**: Reads task definitions and availability windows from CSV files, resolves execution order respecting all inter-task dependencies, and outputs executable plans with precise start times.
- **Phase 2 (Desktop App)**: Refactors Phase 1 into an Orleans-based distributed planning system with:
  - **Orleans grains** for parallel task execution calculations
  - **SQLite persistence** for durable grain state via local database
  - **Windows 11 desktop GUI** for sequence visualization and reporting
  - **Excel export** with calendar visualization, dependency chains, and risk analysis for non-technical stakeholders

### Current Status

- ✅ **Phase 1**: Complete – Console application with dependency resolution and CSV parsing
- 🔄 **Phase 2**: In planning (16–17 week implementation sprint beginning Q2 2026)
  - Orleans infrastructure
  - Desktop GUI (WinUI 3)
  - Excel export with non-technical calendar visualization
  - SQLite grain state persistence

## Quick Links

### Documentation Structure

- **[Business & Architecture](docs/business/)** – Requirements, Orleans architecture, implementation plan
  - [Architecture & Business Requirements](docs/business/01-architecture-requirements.md)
  - [Execution Sequencing Pipeline](docs/business/02-execution-sequencing-pipeline.md)
  - [Orleans + Aspire Architecture](docs/business/03-orleans-aspire-architecture.md)
  - [Phase 2 Implementation Plan](docs/business/04-implementation-plan-phase-2.md)
  - [Technology Stack & Desktop GUI](docs/business/05-technology-stack-desktop-gui.md)

- **[Standards](docs/standards/)** – Coding standards, async requirements, quality architecture
- **[Patterns](docs/patterns/)** – DDD, validation, and testing patterns
- **[Master Index](docs/index.md)** – Complete navigation

### Phase 2 Planning Documents

- [Phase 2 Requirements Summary](docs/PHASE_2_REQUIREMENTS_SUMMARY.md) – SQLite persistence and Excel export specifications
- [Phase 2 Implementation Summary](docs/PHASE_2_IMPLEMENTATION_SUMMARY.md) – Sprint breakdown and resource allocation

## Technology Stack

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Framework** | .NET 10 | Target runtime for Console and Desktop apps |
| **Distributed Actors** | Orleans 10.x | Parallel grain execution for task sequencing |
| **Desktop UI** | WinUI 3 | Native Windows 11 dashboard (Phase 2) |
| **Persistence** | SQLite (Orleans.Persistence.AdoNet) | Durable grain state in local database |
| **Excel Export** | ClosedXML | Calendar export with color coding and risk analysis |
| **CSV Parsing** | CsvHelper 30.x | Task and availability window file parsing |
| **Dependency Injection** | Microsoft.Extensions.DI | Service container and composition |

## Project Structure

```
src/
├── ConsoleApp/                          # Phase 1: Console runner
│   └── Ifx/
│       ├── Models/                      # 15 domain models (tasks, instances, plans)
│       ├── Orleans/Grains/              # Grain interfaces and implementations
│       └── Services/                    # Business logic services
│
tests/
└── ConsoleApp.Tests/                    # Unit tests for Phase 1 & grain contracts

docs/
├── business/                            # Architecture, Orleans, implementation
├── standards/                           # Coding standards and quality
├── patterns/                            # DDD, validation, testing patterns
├── index.md                             # Navigation hub
└── readme.md                            # Detailed instruction manual

data/
└── execution_durations.csv              # Sample data for execution timing
```

## Getting Started

### Phase 1: Run the Console Application

```bash
cd src/ConsoleApp
dotnet run
```

Input CSV files at the default location or specify via configuration.

### Phase 2: WinUI 3 Desktop Application (Under Development)

Desktop GUI will provide:
- Interactive sequence timeline (Gantt chart style)
- Grouping-based execution reports
- Excel export for stakeholder distribution
- Multi-round convergence visualization

See [Phase 2 Implementation Plan](docs/business/04-implementation-plan-phase-2.md) for Sprint breakdown.

## Key Features

### Dependency Resolution

- Two-phase algorithm: feasibility check and deadline validation
- Resolves circular dependencies and infeasible orderings
- Adjusts task start times when constraints require later execution

### Parallel Execution (Orleans Grains – Phase 2)

- Independent grain actors per execution instance
- Automatic state persistence to SQLite
- Convergence detection across reprocessing rounds
- Sub-millisecond inter-grain communication

### Excel Export (Phase 2)

Five-tab workbook generated for each execution plan:
1. **Summary** – Plan statistics and metadata
2. **Timeline Calendar** – Time-block grid showing all parallel tasks
3. **Task Details** – Complete listing with dependencies and durations
4. **Dependency Chains** – Text visualization of dependency paths
5. **Risk Analysis** – Bottleneck detection and deadline miss forecasts

## Testing

Unit tests cover:
- CSV parsing and manifest validation (Phase 1)
- Dependency resolution algorithm correctness
- Orleans grain activation and persistence (Phase 2)
- Excel export formatting and data accuracy

Run tests:
```bash
dotnet test tests/ConsoleApp.Tests/ConsoleApp.Tests.csproj
```

## Documentation

For detailed information, see the [Documentation Index](docs/index.md) which provides:
- Complete architecture overview
- Orleans grain contracts and design patterns
- Implementation roadmap with sprint estimates
- Code quality and testing standards

---

**Last Updated**: March 26, 2026 (Phase 2 design finalized, SQLite + Excel export specifications complete)
