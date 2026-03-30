# Orleans & Aspire Architecture (Phase 2+)

## Overview

App.TaskSequencer Phase 2 introduces **Microsoft Orleans** as the distributed execution engine and **Microsoft Aspire** for development orchestration and deployment.

---

## Orleans Grain Architecture

### Grain Interfaces

#### 1. IExecutionInstanceGrain
**Purpose**: Autonomous actor grain representing a single task execution instance

**Grain Key**: `InterfaceNumber_DayOfWeek_HHmmss` (string-based)

**Responsibilities**:
- Hold execution event definition and constraints
- Calculate resolved start time accounting for prerequisites and deadlines
- Validate deadline feasibility
- Track duration (estimated or actual)
- Notify orchestrator of validation results
- Handle duration updates from execution manifest imports
- Notify dependent grains on updates (cascading recalculation)

**State**:
```csharp
public record ExecutionInstanceGrainState
{
    public ExecutionEventDefinition EventDefinition { get; set; }
    public IReadOnlySet<string> PrerequisiteGrainKeys { get; set; }
    public DateTime? IntakeDeadline { get; set; }
    public ExecutionDuration Duration { get; set; }
    
    // Calculation results
    public DateTime CalculatedStartTime { get; set; }
    public DateTime CalculatedEndTime { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationMessage { get; set; }
    
    // Difference sequence tracking
    public DateTime PriorRoundStartTime { get; set; }
    public TimeSpan PositionDelta { get; set; }
    public bool ConvergedThisRound { get; set; }
    
    // Dependent grains that must be recalculated
    public IReadOnlySet<string> DependentGrainKeys { get; set; }
}
```

#### 2. IExecutionPlanOrchestratorGrain
**Purpose**: Orchestrates multi-round calculation and convergence

**Grain Key**: `ExecutionPlan_{IncrementId}` (e.g., `ExecutionPlan_2024-03-25`)

**Responsibilities**:
- Trigger Round N of execution instance grain evaluations
- Collect validation results from all instance grains
- Detect convergence (no grains changed position)
- Initiate Round N+1 if needed
- Generate final ExecutionPlan with task chains
- Pass results to ReportGeneratorGrain
- Manage reprocessing timeout/max iterations

**State**:
```csharp
public record ExecutionPlanOrchestratorGrainState
{
    public string IncrementId { get; set; }
    public DateTime IncrementStart { get; set; }
    public DateTime IncrementEnd { get; set; }
    
    // Iterative refinement tracking
    public int CurrentRound { get; set; }
    public int MaxRounds { get; set; } = 10;  // Safety limit
    public bool HasConverged { get; set; }
    
    // All execution instance grains in this increment
    public IReadOnlyList<string> ExecutionInstanceGrainKeys { get; set; }
    
    // Per-round tracking
    public Dictionary<int, List<string>> GrainsChangedPerRound { get; set; }  // Position changes
    public Dictionary<int, List<string>> GrainFailuresPerRound { get; set; }  // New deadline violations
    
    // Final execution plan
    public ExecutionPlan? FinalPlan { get; set; }
}
```

#### 3. ISequenceGroupGrain
**Purpose**: Organizes execution instances by business domain/category

**Grain Key**: `SequenceGroup_{GroupId}` (e.g., `SequenceGroup_PAYROLL`, `SequenceGroup_SETTLEMENT`)

**Responsibilities**:
- Aggregate execution instances by domain category
- Track grouping-specific deadline requirements
- Generate grouping-specific reports
- Manage group-level metrics (critical path, total duration, failure count)

**State**:
```csharp
public record SequenceGroupGrainState
{
    public string GroupId { get; set; }
    public string GroupName { get; set; }
    public IReadOnlySet<string> ExecutionInstanceGrainKeys { get; set; }
    public ExecutionPlan? GroupExecutionPlan { get; set; }
    
    // Group-level metrics
    public DateTime? CriticalPathCompletion { get; set; }
    public int ValidTaskCount { get; set; }
    public int InvalidTaskCount { get; set; }
    public IReadOnlyList<string> DeadlineMisses { get; set; }
}
```

#### 4. IReportGeneratorGrain
**Purpose**: Produces reporting data for web GUI consumption

**Grain Key**: `ReportGenerator_{ReportType}_{Timestamp}` (e.g., `ReportGenerator_Daily_2024-03-25`)

**Responsibilities**:
- Aggregate difference sequences across all rounds
- Calculate per-group statistics
- Format timeline data for web dashboard
- Generate sequence-vs-deadline violation reports
- Track convergence metrics and refinement iterations

**State**:
```csharp
public record ReportGeneratorGrainState
{
    public string ReportId { get; set; }
    public string ReportType { get; set; }  // "Daily", "Weekly", "AdHoc"
    public DateTime GeneratedAt { get; set; }
    
    // Aggregated data for reporting
    public IReadOnlyList<DifferenceSequence> AllDifferenceSequences { get; set; }
    public Dictionary<string, SequenceGroupReport> GroupReports { get; set; }
    public ExecutionPlan OverallPlan { get; set; }
    
    // Convergence tracking
    public int TotalRounds { get; set; }
    public TimeSpan TotalCalculationTime { get; set; }
    public bool FullyConverged { get; set; }
}

public record DifferenceSequence(
    int RoundNumber,
    DateTime CalculatedAt,
    IReadOnlyList<PositionAdjustment> Adjustments,
    bool NewViolationsDetected
);

public record PositionAdjustment(
    string ExecutionInstanceGrainKey,
    DateTime PreviousCalculatedStart,
    DateTime NewCalculatedStart,
    TimeSpan PositionDelta,
    string Reason  // "Upstream completion updated", "Deadline conflict resolved", etc.
);

public record SequenceGroupReport(
    string GroupId,
    string GroupName,
    IReadOnlyList<string> ExecutionSequence,
    DateTime CriticalPathCompletion,
    IReadOnlyList<string> DeadlineMisses,
    Dictionary<int, int> ChangesPerRound
);
```

---

## Aspire Integration

### Desktop Application Architecture

With Aspire, the Orleans silo runs **embedded in the WinUI 3 desktop application**, not as a separate service:

```csharp
// Program.cs - Single integrated process
var host = new HostBuilder()
    .ConfigureServices(services =>
    {
        // Add Orleans Silo (in-process)
        services.AddOrleans("TaskSequencerSilo", (context, siloBuilder) =>
        {
            siloBuilder
                .UseLocalhostClustering()
                .ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(ExecutionInstanceGrain).Assembly)
                         .WithReferences();
                })
                .UseDashboard(options => { options.Port = 8080; });
        });
        
        // Add desktop app services
        services.AddSingleton<App>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<DashboardViewModel>();
    })
    .Build();

// Start Orleans silo first
await host.StartAsync();

// Launch desktop app window
var app = host.Services.GetRequiredService<App>();
app.RegisterFrameworkElementsForLayoutCycleErrorDetection();
// ... WinUI app startup
```

### Single-Process Model

```
┌───────────────────────────────────────────┐
│   Windows 11 Process (Single .exe/.msix)  │
├───────────────────────────────────────────┤
│                                           │
│  WinUI 3 UI Thread                       │
│  ├─ Main Window (Dashboard)              │
│  ├─ Timeline View                        │
│  ├─ Violations Report                    │
│  └─ Settings Window                      │
│                                           │
│  Orleans Worker Threads (shared pool)    │
│  ├─ ExecutionInstanceGrain[0..N]        │
│  ├─ ExecutionPlanOrchestratorGrain      │
│  ├─ SequenceGroupGrain[0..M]            │
│  └─ ReportGeneratorGrain                 │
│                                           │
│  Local File I/O                          │
│  └─ CSV parsing, export                  │
│                                           │
└───────────────────────────────────────────┘
```

### Development Experience

**Local startup** (one command):
```bash
dotnet run --project DesktopApp/DesktopApp.csproj
# Starts Windows app with embedded Orleans silo
# Orleans dashboard available at http://localhost:8080
```

**No deployment complexity**:
- No app server to configure
- No separate database
- No API gateway
- No authentication infrastructure
- Single file (.msix) for distribution

---

## Grain Communication Flow

### Startup & Initialization Phase

```
┌─────────────────────┐
│ Main Entry Point    │
│ Load 3 CSV files    │
└────────────┬────────┘
             │
             ↓
┌─────────────────────────────────────────────────────┐
│ CSV Parser Service                                  │
│ - Task Definitions CSV → TaskDefinition records    │
│ - Intake Events CSV → IntakeEventRequirement       │
│ - Duration Manifest → ExecutionDuration lookups    │
└─────────────┬───────────────────────────────────────┘
              │
              ↓
┌─────────────────────────────────────────────────────┐
│ ExecutionEventDefinition Matrix Builder             │
│ For each TaskDef, generate all execution instances  │
│ (cartesian product of days × times)                 │
│                                                     │
│ Example: Task 1 Mon/Wed/Fri @ 06:00, 14:00         │
│ Generates: 6 ExecutionEventDefinition instances    │
└─────────────┬───────────────────────────────────────┘
              │
              ↓
┌─────────────────────────────────────────────────────┐
│ Orchestrator Initialization                         │
│ Create IExecutionPlanOrchestratorGrain              │
│ Key = "ExecutionPlan_{IncrementId}"                 │
│ Store all ExecutionEventDefinition instances        │
└─────────────┬───────────────────────────────────────┘
              │
              ↓
┌─────────────────────────────────────────────────────┐
│ ROUND 1: Parallel Instance Grain Creation          │
│ For each ExecutionEventDefinition:                 │
│   • Get IExecutionInstanceGrain via factoryKey      │
│   • Call Initialize(eventDef, prereqs, deadline)   │
│   • Store grain reference                          │
│                                                    │
│ All → 100+ grains created & initialized in parallel│
└─────────────┬───────────────────────────────────────┘
```

### Iterative Refinement Phase (Rounds 1, 2, 3...)

```
┌──────────────────────────────────────────┐
│ ROUND N: Parallel Validation             │
│                                          │
│ Orchestrator.TriggerRound(N)             │
│   ├─→ ExecutionInstanceGrain[0..N]       │
│   │   + GetDuration()                    │
│   │   + CalculateResolvedStartTime()     │
│   │   + ValidateExecutability()          │
│   │   + Return: (IsValid, StartTime,     │
│   │             EndTime, PositionDelta)  │
│   │                                      │
│   ├─→ ExecutionInstanceGrain[N..M]       │
│   │   [same parallel operations]         │
│   │                                      │
│   └─→ [etc. - all grains in parallel]    │
│                                          │
│ Result: RoundResults {                   │
│   ValidInstances: [],                    │
│   InvalidInstances: [],                  │
│   PositionChanges: { key→delta },        │
│   NewViolations: []                      │
│ }                                        │
└────────┬─────────────────────────────────┘
         │
         ↓
┌──────────────────────────────────────────┐
│ Convergence Check                        │
│                                          │
│ if PositionChanges.Count == 0:           │
│   HasConverged = true                    │
│ else:                                    │
│   TriggerRound(N+1)                      │
│                                          │
│ if CurrentRound >= MaxRounds:            │
│   HasConverged = true (via timeout)      │
└────────┬─────────────────────────────────┘
         │
         ↓ [only if converged]
┌──────────────────────────────────────────┐
│ Final Plan Generation                    │
│                                          │
│ Build ExecutionPlan {                    │
│   IncrementId, Start, End,               │
│   Tasks: [...all valid],                 │
│   TaskChain: [root→leaf],                │
│   CriticalPathCompletion,                │
│   DeadlineMisses: [...]                  │
│ }                                        │
│                                          │
│ Pass to ReportGeneratorGrain             │
└────────────────────────────────────────┘
```

### Difference Sequence Tracking

Each grain maintains:

```csharp
private Dictionary<int, DateTime> _startTimePerRound;

public async Task CalculateResolvedStartTimeAsync(int round)
{
    // Fetch prerequisite grains' completion times
    var prereqComplete = await GetPrerequisiteCompletionTimeAsync();
    
    // Constraint 1: Scheduled time
    var scheduledStart = EventDefinition.ScheduledTime;
    
    // Constraint 2: Prerequisites complete
    var afterPrereqs = prereqComplete;
    
    // Constraint 3: Must complete by intake deadline
    var intakeDeadline = IntakeRequirement?.GetIntakeDeadline(EventDefinition.ScheduledDay);
    var mustStartBy = intakeDeadline.HasValue
        ? intakeDeadline.Value.Subtract(Duration.DurationMinutes)
        : DateTime.MaxValue;
    
    // Resolved start = latest of constraints
    var resolvedStart = new[] { scheduledStart, afterPrereqs, Duration.EstimatedStartBuffer }
        .Max();
    
    // Track position change
    if (_startTimePerRound.TryGetValue(round - 1, out var priorStart))
    {
        PositionDelta = resolvedStart - priorStart;
    }
    
    _startTimePerRound[round] = resolvedStart;
    ConvergedThisRound = (PositionDelta == TimeSpan.Zero);
    
    return resolvedStart;
}
```

---

## Reporting & GUI Integration

### Data Flow to Web Frontend

```
ReportGeneratorGrain
├─ Aggregates all DifferenceSequences
├─ Computes per-group metrics
├─ Formats timeline visualization data
│
↓ HTTP API
│
ASP.NET Core Controllers
├─ GET /api/reports/{reportId}
├─ GET /api/groups/{groupId}
├─ GET /api/timeline/{incrementId}
└─ GET /api/deadline-misses/{incrementId}
│
↓ JSON payloads
│
React Web App
├─ Dashboard (overall status)
├─ Groups Tab (sequences per grouping)
├─ Timeline View (task execution Gantt)
└─ Violations Tab (deadline misses)
```

### GUI Components

1. **Dashboard Home**
   - Overall execution statistics
   - Convergence progress (round N of M)
   - Calculation time elapsed
   - Critical path summary

2. **Groups Tab**
   - List all sequence groupings (PAYROLL, SETTLEMENT, etc.)
   - Per-group task count, validity, critical path
   - Drill-down to group-specific timeline

3. **Timeline Visualization**
   - Gantt chart of execution instances
   - Task boxes colored by: valid/invalid, on-time/late, estimated/actual duration
   - Hover: shows prerequisite chain
   - Difference sequence overlay (show position changes per round)

4. **Violations & Deadlines**
   - Deadline miss list with reasons
   - Tasks forced to reschedule (with delta timeline)
   - Feasibility assessment recommendations

---

## Implementation Roadmap (Phase 2)

### 2.1: Core Orleans Infrastructure
- [ ] Orleans host project setup (`TaskSequencer.Orleans`)
- [ ] Define grain interfaces (4 types above)
- [ ] Implement grain state management
- [ ] Add Orleans configuration to Aspire

### 2.2: Grain Implementations
- [ ] IExecutionInstanceGrain
- [ ] IExecutionPlanOrchestratorGrain
- [ ] ISequenceGroupGrain
- [ ] IReportGeneratorGrain

### 2.3: CSV Processing & Initialization
- [ ] Extend CSV parser to generate ExecutionEventDefinition matrix
- [ ] Orchestrator initialization logic
- [ ] Grain factory & key generation

### 2.4: Iterative Refinement Algorithm
- [ ] Round N execution pipeline
- [ ] Convergence detection
- [ ] Difference sequence tracking
- [ ] Error recovery (max iterations, timeout)

### 2.5: Web API & Aspire Host
- [ ] ASP.NET Core project with reporting controllers
- [ ] Grain client integration
- [ ] Aspire AppHost configuration
- [ ] Health checks & diagnostics

### 2.6: Web Frontend
- [ ] React app (Vite setup)
- [ ] Dashboard component
- [ ] Groups & timeline views
- [ ] Violation reporting UI

### 2.7: Testing & Validation
- [ ] Unit tests for grain logic
- [ ] Integration tests (Orleans test silos)
- [ ] End-to-end scenarios
- [ ] Performance profiling

---

## Configuration & Environment

### Local Development (Aspire)

```json
{
  "Orleans": {
    "ClusterName": "TaskSequencerCluster",
    "ServiceId": "TaskSequencer",
    "Clustering": "Localhost",
    "DashboardUrl": "http://localhost:8080"
  },
  "Logging": {
    "LogLevel": {
      "Orleans": "Information",
      "Orleans.Runtime": "Debug"
    }
  }
}
```

### Phase 2: Local Persistence with SQLite

**Grain State Storage**:
- **Database**: Local SQLite (auto-created in `<AppPath>\data\TaskSequencer.db`)
- **Orleans Integration**: Uses `Orleans.Persistence.AdoNet` with SQLite provider
- **Schema**: Auto-created by Orleans (single `GrainState` table)
- **Persistence Model**: Automatic state save on every grain mutation
- **Backup**: Automatic backup to `TaskSequencer.db.backup.<date>` on app shutdown

**SQLite Configuration**:
```json
{
  "Orleans": {
    "GrainStorage": "sqlite",
    "StorageConnectionString": "Data Source=data/TaskSequencer.db;Version=3;",
    "UseJsonFormat": true
  }
}
```

### Production Deployment (Phase 3+)

- **Orleans**: Elastic scaling on Azure Container Instances or Kubernetes
- **State Store**: Azure Table Storage or Redis for grain state (replaces local SQLite)
- **Dashboard**: Hosted via Aspire cloud projection or separate monitoring service
- **Web API**: Azure App Service or Container Apps
- **Frontend**: Static hosting (Azure Static Web Apps) with CDN

---

## Next Steps

**Do NOT implement until:**
1. ✅ Requirements confirmed (Orleans + Aspire + web reporting + difference sequences)
2. ✅ Grain types confirmed (4 types: Instance, Orchestrator, Group, Reporter)
3. ⏳ All documentation updated (this file + main business docs)
4. ⏳ Implementation plan finalized and reviewed
5. ⏳ Phase 1 console app unit tests passing

See `04-implementation-plan-phase-2.md` for detailed sprint breakdown.
