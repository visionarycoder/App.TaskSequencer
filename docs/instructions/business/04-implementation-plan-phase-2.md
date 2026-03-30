# Phase 2 Implementation Plan: Orleans + Aspire + Desktop GUI

**Status**: Requirements & Architecture Finalized (as of March 26, 2026)
**Version**: 1.0
**Target**: Q2 2026

---

## Phase Overview

Refactor Phase 1 console application into a distributed Orleans-based planning system with native Windows 11 desktop GUI. The system will perform iterative task scheduling calculations across multiple reprocessing rounds until convergence, with local sequence visualization per grouping.

### Phase 1 → Phase 2 Transition

**Phase 1 Deliverables** (existing):
- ✅ CSV parsing for 3 input files
- ✅ Core domain models (Task, ExecutionInstance, ExecutionPlan)
- ✅ Dependency resolution algorithm (2-phase with deadline validation)
- ✅ Console output of execution sequences
- ✅ Unit tests covering core logic

**Phase 2 Goals**:
- Distributed grain-based execution using Orleans
- Iterative refinement with convergence detection (multi-round difference sequences)
- Durable grain state persistence with SQLite
- Difference sequence tracking across reprocessing rounds
- Native Windows 11 desktop GUI for reporting and visualization
- Excel export with non-technical calendar visualization and risk analysis
- Aspire orchestration for local development

---

## Sprint Breakdown (16–17 weeks)

**Core Phase (16 weeks)**: Sprints 1–16 cover all essential Phase 2 features.
**Optional Advanced Reporting (1 week)**: Sprint 17 adds sophisticated Excel reporting (recommended for executive distribution).

### Sprint 1–2: Orleans Infrastructure & Core Grains (2 weeks)

**Goal**: Foundation for distributed execution model with persistent state

**Tasks**:
1. Create `TaskSequencer.Orleans` project
   - [ ] Orleans host configuration
   - [ ] Logging and diagnostics setup (Serilog integration)
   - [ ] SQLite grain state persistence setup
   - [ ] Grain state persistence blueprints

2. Configure SQLite Persistence
   - [ ] Add `Orleans.Persistence.AdoNet` NuGet package
   - [ ] Add `System.Data.SQLite` NuGet package
   - [ ] Create data directory structure (`<AppPath>\data\`)
   - [ ] Configure OrleansAdoNetPersistence with SQLite connection string
   - [ ] Configure auto-backup mechanism
   - [ ] Create `TaskSequencer.db` initialization script

3. Define grain interfaces (abstract, in separate project)
   - [ ] `IExecutionInstanceGrain` interface + state contract
   - [ ] `IExecutionPlanOrchestratorGrain` interface + state contract
   - [ ] `ISequenceGroupGrain` interface + state contract
   - [ ] `IReportGeneratorGrain` interface + state contract
   - [ ] Supporting value objects (PositionAdjustment, DifferenceSequence, etc.)

4. Implement basic grain classes with stub methods
   - [ ] ExecutionInstanceGrain (skeleton, no logic yet)
   - [ ] ExecutionPlanOrchestratorGrain (skeleton, no logic yet)
   - [ ] SequenceGroupGrain (skeleton)
   - [ ] ReportGeneratorGrain (skeleton)
   - [ ] Configure grain state persistence attributes

5. Create Orleans grain factory helpers
   - [ ] Grain key generation utilities
   - [ ] Grain reference helpers
   - [ ] Grain state serialization helpers (JSON format)

**Deliverable**: Compiling Orleans solution with all grain contracts defined and SQLite persistence configured

**Tests**:
- [ ] Grain instantiation via factory
- [ ] Grain state persists to SQLite after state change
- [ ] Grain state loads from SQLite on reactivation

---

### Sprint 3: CSV Processing & Initialization (1 week)

**Goal**: Bridge Phase 1 CSV parsing with grain initialization

**Tasks**:
1. Refactor Phase 1 CSV parsing into reusable service
   - [ ] Extract into `TaskSequencer.Shared` project
   - [ ] Create `ICsvManifestLoader` interface
   - [ ] Implement: TaskDefinitionCsvLoader, IntakeEventCsvLoader, DurationManifestCsvLoader

2. Build ExecutionEventDefinition matrix generator
   - [ ] `IExecutionEventMatrixGenerator` interface
   - [ ] Given TaskDefinition, generate all ExecutionEventDefinition instances
   - [ ] Apply Execution Days × Execution Times cartesian product
   - [ ] Link to IntakeEventRequirement from Availability Window CSV

3. Create orchestrator initialization service
   - [ ] `IOrchestrationInitializer` interface
   - [ ] Load 3 CSVs
   - [ ] Build ExecutionEventDefinition matrix
   - [ ] Create executor grains
   - [ ] Create orchestrator grain
   - [ ] Return initialization summary

4. Create grain key utilities
   - [ ] ExecutionInstanceGrain key format: `InterfaceNumber_DayOfWeek_HHmmss`
   - [ ] OrchestratorGrain key format: `ExecutionPlan_{IncrementId}`
   - [ ] Convert between ExecutionEventDefinition and grain keys

**Deliverable**: CSV data successfully loaded and ExecutionEventDefinition matrix generated

**Tests**:
- [ ] CSV parsing for all 3 files
- [ ] ExecutionEventDefinition matrix generation matches expected size
- [ ] Grain key generation is deterministic

---

### Sprint 4–5: IExecutionInstanceGrain Implementation (2 weeks)

**Goal**: Implement autonomous task validation grain

**Key Algorithm**: Dependency resolution + deadline feasibility

**Tasks**:
1. Implement grain state initialization
   - [ ] `InitializeAsync()` method
   - [ ] Store EventDefinition, prerequisites, intake deadline
   - [ ] Store empty duration (will fetch on first use)

2. Implement duration resolution
   - [ ] `GetDurationAsync()` method
   - [ ] Lookup duration from Shared DurationManifestCache (built from Phase 1)
   - [ ] Default to 15 minutes if no history
   - [ ] Track: estimated vs. actual duration

3. Implement prerequisite resolution
   - [ ] `GetResolvedPrerequisitesAsync()` method
   - [ ] For each prerequisite InterfaceNumber:
     - Find corresponding grain key (same day, preceding time or latest prior day)
     - Call grain's `GetPlannedCompletionTimeAsync()`
   - [ ] Return list of prerequisite grain state/completion times
   - [ ] **Important**: Handle cases where prerequisite not scheduled on this day

4. Implement deadline validation
   - [ ] `CheckDeadlineAsync()` method
   - [ ] If IntakeRequirement exists for this day:
     - Get deadline time from IntakeEventRequirement
     - Calculate total of prerequisites' completion times + own duration
     - Check if completion ≤ deadline
   - [ ] Return (MeetsDeadline, PlannedCompletion, Deadline)

5. Implement resolved start time calculation
   - [ ] `CalculateResolvedStartTimeAsync()` method
   - [ ] Constraint 1: Scheduled start time (from EventDefinition)
   - [ ] Constraint 2: Latest prerequisite completion time (from Step 3)
   - [ ] Constraint 3: Must complete by deadline (from Step 4)
   - [ ] Resolved Start = MAX(scheduled, prereq complete, optional buffer for deadline)
   - [ ] Store in state for convergence tracking
   - [ ] Calculate PositionDelta from prior round

6. Implement validation orchestration
   - [ ] `ValidateExecutabilityAsync()` method
   - [ ] Call GetDuration(), GetResolvedPrerequisites(), CheckDeadline()
   - [ ] Aggregate results into IsValid, ValidationMessage
   - [ ] Set state flags for orchestrator consumption

7. Implement dependent grain notification
   - [ ] `GetDependentTasksAsync()` method (query orchestrator)
   - [ ] `UpdateActualDurationAsync()` method
     - Update Duration in state
     - Trigger re-validation of this grain
     - Get dependent grain keys from orchestrator
     - Call ValidateExecutabilityAsync() on each dependent
     - Cascade notifications upstream

**Deliverable**: Grain independently validates execution feasibility and tracks position changes

**Tests**:
- [ ] Single grain calculates correct deadline for simple task
- [ ] Prerequisite resolution finds correct prior execution
- [ ] Deadline validation catches infeasible sequences
- [ ] Position delta correctly calculated between rounds
- [ ] Duration update triggers dependent recalculation

---

### Sprint 6–7: IExecutionPlanOrchestratorGrain Implementation (2 weeks)

**Goal**: Orchestrate multi-round refinement and convergence

**Key Algorithm**: Iterative refinement until no position changes detected

**Tasks**:
1. Implement round orchestration
   - [ ] `TriggerRoundAsync(roundNumber)` method
   - [ ] For each stored ExecutionInstanceGrain key:
     - Get grain reference
     - Call CalculateResolvedStartTimeAsync(round)
     - Call ValidateExecutabilityAsync()
   - [ ] Collect all results (changes, violations, valid/invalid counts)
   - [ ] Store results in state indexed by round number

2. Implement convergence detection
   - [ ] After TriggerRound(), check if any grains changed position
   - [ ] If no changes: Set HasConverged = true, stop iteration
   - [ ] If changes exist: Schedule TriggerRound(N+1)
   - [ ] If CurrentRound >= MaxRounds: Set HasConverged = true (timeout)

3. Implement difference sequence aggregation
   - [ ] Build DifferenceSequence list from round results
   - [ ] For each grain that moved: Create PositionAdjustment record
     - Include reason (e.g., "Upstream completion updated by X minutes")
   - [ ] Store DifferenceSequence in state per round

4. Implement final plan generation
   - [ ] `BuildExecutionPlanAsync()` method (called when converged)
   - [ ] Filter ExecutionInstance list to valid instances only
   - [ ] Identify root tasks (no prerequisites or all prereqs invalid)
   - [ ] Perform depth-first traversal to build task chain
   - [ ] Calculate critical path = max(last task completion time)
   - [ ] List deadline misses (all invalid instances with deadline violation reason)
   - [ ] Create ExecutionPlan record
   - [ ] Store in state

5. Implement grouping aggregation
   - [ ] `AggregateBySequenceGroupAsync()` method
   - [ ] Load sequence group mapping (from config or Phase 1 data)
   - [ ] For each group: Create SequenceGroupGrain
   - [ ] Pass filtered execution instances to group grains
   - [ ] Receive group-specific ExecutionPlan objects

6. Implement reporting handoff
   - [ ] After convergence & plan generation:
     - Call IReportGeneratorGrain.SetPlanDataAsync()
     - Pass ExecutionPlan, DifferenceSequences, GroupPlans
   - [ ] Log completion summary

7. Implement state recovery & idempotency
   - [ ] If initialization already occurred: Return existing ExecutionInstanceGrainKeys
   - [ ] If round N already run: Return cached results
   - [ ] Ensure multiple calls to same method don't duplicate work

**Deliverable**: Orchestrator executes multi-round refinement and detects convergence

**Tests**:
- [ ] Single-round execution (all tasks valid first try)
- [ ] Multi-round execution (adjustments required, then converges)
- [ ] Convergence detection after 2–3 rounds
- [ ] Deadline misses correctly identified
- [ ] Difference sequences accurately recorded

---

### Sprint 8: ISequenceGroupGrain & Report Data Prep (1 week)

**Goal**: Aggregate execution data by business domain groupings

**Tasks**:
1. Define grouping schema
   - [ ] Map TaskDefinition.InterfaceNumber → GroupId
   - [ ] Source from configuration or Phase 1 hardcoded mappings
   - [ ] Example: Tasks 1–10 = PAYROLL, Tasks 11–20 = SETTLEMENT

2. Implement SequenceGroupGrain
   - [ ] `SetExecutionInstancesAsync(grainKeys[])` method
   - [ ] Store filtered ExecutionInstance list for this group
   - [ ] Calculate group-specific metrics: valid count, invalid count, critical path

3. Implement group report generation
   - [ ] `BuildGroupExecutionPlanAsync()` method
   - [ ] Build ExecutionPlan filtered to group's instances
   - [ ] Return plan with group-scoped task chain

**Deliverable**: Execution instances aggregated and grouped

**Tests**:
- [ ] Correct instances assigned to each group
- [ ] Group-scoped critical path matches subset of overall plan

---

### Sprint 9: IReportGeneratorGrain & Reporting API (1 week)

**Goal**: Prepare reporting data for web GUI consumption

**Tasks**:
1. Implement ReportGeneratorGrain
   - [ ] `SetPlanDataAsync(plan, sequences, groupPlans)` method
   - [ ] Aggregate all DifferenceSequence objects
   - [ ] Transform ExecutionInstance → JSON-serializable format
   - [ ] Store report state

2. Implement reporting data accessors
   - [ ] `GetFullReportAsync()` - overall execution plan
   - [ ] `GetGroupReportAsync(groupId)` - group-specific data
   - [ ] `GetTimelineAsync(incrementId)` - Gantt chart data
   - [ ] `GetDeadlineMissesAsync()` - violation summary
   - [ ] `GetConvergenceMetricsAsync()` - rounds, time, changes per round

3. Create ASP.NET Core Web API project
   - [ ] Controllers for reporting endpoints
   - [ ] GET /api/reports/{reportId}
   - [ ] GET /api/groups/{groupId}
   - [ ] GET /api/timeline/{incrementId}
   - [ ] GET /api/deadline-misses
   - [ ] JSON DTOs for web consumption

4. Integrate grain client into Web API
   - [ ] AddOrleansClient() in DI
   - [ ] Service layer wrapping grain calls

**Deliverable**: Reporting API ready to serve web frontend

**Tests**:
- [ ] API endpoints return correct JSON structures
- [ ] Grain data correctly transformed to DTO format

---

### Sprint 10–11: Aspire AppHost & Service Orchestration (2 weeks)

**Goal**: Configure distributed deployment model with local development experience

**Tasks**:
1. Create Aspire AppHost project
   - [ ] Reference Orleans host, Web API, frontend projects
   - [ ] Configure Orleans silo as service
   - [ ] Configure Web API as service
   - [ ] Configure frontend as service

2. Set up Orleans silo configuration in Aspire
   - [ ] Localhost clustering for development
   - [ ] Configure grain assembly scanning
   - [ ] Set up logging & diagnostics
   - [ ] Add health checks

3. Configure Aspire environment variables
   - [ ] Orleans cluster name, endpoint URLs
   - [ ] Web API base URL
   - [ ] CORS policies for frontend
   - [ ] Development vs. production profiles

4. Implement local startup script
   - [ ] PowerShell/Bash script to start full stack
   - [ ] Health check loops before launching frontend
   - [ ] Auto-open dashboard and web app in browser
   - [ ] Docker Compose alternative for containerized local dev

5. Test local development workflow
   - [ ] Start Aspire host
   - [ ] Verify Orleans silo online
   - [ ] Verify Web API online
   - [ ] Verify frontend can call API

**Deliverable**: Local Aspire setup working end-to-end

**Tests**:
- [ ] Aspire AppHost starts all services
- [ ] Orleans silo accepts grain activations
- [ ] Web API receives calls from frontend
- [ ] Dashboard displays service health

---

### Sprint 10–11: Desktop Application (WinUI 3) (2 weeks)

**Goal**: Build native Windows 11 GUI for sequence visualization and reporting

**Technology**: WinUI 3, MVVM Community Toolkit, Windows App SDK

**Tasks**:
1. Set up WinUI 3 project structure
   - [ ] Create `DesktopApp` project with WinUI 3 template
   - [ ] Add MVVM Community Toolkit
   - [ ] Configure Package.appxmanifest for Windows 11
   - [ ] Add Windows App SDK dependencies

2. Implement Main Dashboard window
   - [ ] MainWindow.xaml + MainViewModel
   - [ ] Execution statistics display (total, valid, invalid tasks)
   - [ ] Convergence progress visualization (Round 1, 2, 3...)
   - [ ] Deadline violations summary (top violations)
   - [ ] Sequence groups quick links

3. Implement Timeline/Gantt view window
   - [ ] TimelineWindow.xaml + TimelineViewModel
   - [ ] DataGrid with task list and visual timeline columns
   - [ ] Time scrolling (horizontal pan)
   - [ ] Right-click context menu for task details
   - [ ] Color coding: green (valid), red (invalid), yellow (critical path)
   - [ ] Deadline lines drawn at scheduled deadline times

4. Implement Violations report window
   - [ ] ViolationsWindow.xaml + ViolationsViewModel
   - [ ] DataGrid of deadline misses
   - [ ] Sortable columns (Task ID, Grouping, Deadline, Miss Amount)
   - [ ] Filter dropdown (by grouping)
   - [ ] Export to CSV button
   - [ ] Print button

5. Implement Settings & File Management
   - [ ] SettingsWindow.xaml for user preferences
   - [ ] File picker for CSV files (Browse button)
   - [ ] Drag-and-drop CSV files onto main window
   - [ ] Remember recent file paths
   - [ ] File → Open Recent menu
   - [ ] Dark mode / Light mode toggle

6. Integrate Orleans Grain Client into desktop app
   - [ ] Create GrainClientService wrapper
   - [ ] Add Orleans client initialization to Program.cs
   - [ ] Dependency inject grain factory into ViewModels
   - [ ] Async task handling for long-running grain calls
   - [ ] Show loading indicators during calculation

7. Implement CSV File Loading Flow
   - [ ] CSV file picker dialog
   - [ ] Progress indication while parsing
   - [ ] Parse Task Definitions CSV
   - [ ] Parse Intake Events CSV
   - [ ] Parse Duration Manifest CSV (optional)
   - [ ] Show validation errors if CSV format invalid

8. Implement Excel Export Feature
   - [ ] Add ClosedXML NuGet package
   - [ ] Add DocumentFormat.OpenXml package
   - [ ] Create `IExcelExportService` interface
   - [ ] Create ExcelExportService implementation
   - [ ] Implement Summary Tab (plan metadata, statistics)
   - [ ] Implement Timeline Calendar Tab (task placement by time block)
   - [ ] Implement Task Details Tab (complete task listing)
   - [ ] Implement Dependency Chains Tab (text-based chain visualization)
   - [ ] Implement Risk Analysis Tab (at-risk tasks, bottlenecks)
   - [ ] Add "Export to Excel" button to Dashboard
   - [ ] Add FileSavePicker for Excel file location
   - [ ] Add progress indicator for large exports
   - [ ] Include color coding (green=on-time, yellow=at-risk, red=violation)
   - [ ] Include helpful instructions tab for non-technical users

**Deliverable**: Functional WinUI 3 desktop app with embedded Orleans silo and Excel export

**Tests**:
- [ ] App starts without errors
- [ ] CSV files load and display results
- [ ] Timeline renders 100+ tasks smoothly
- [ ] Dashboard statistics match grain calculations
- [ ] Filter/sort on violations table works
- [ ] Recent files remembered across app restarts
- [ ] Dark mode theme applies correctly
- [ ] Excel export generates without errors
- [ ] Excel file contains all 5 tabs with correct data
- [ ] Tasks display correctly in Timeline Calendar with parallel execution
- [ ] Dependency chains show correct precursor relationships
- [ ] Color coding applied to at-risk and violated tasks

---

### Sprint 12: Convergence Visualization & Progress Tracking (1 week)

**Goal**: Real-time visualization of iterative refinement process

**Tasks**:
1. Implement convergence progress UI
   - [ ] Show "Round 1", "Round 2", "Round 3..." as rounds complete
   - [ ] Display count of tasks changed in each round
   - [ ] Show progress bar toward convergence
   - [ ] Show estimated time to completion
   - [ ] Add cancel button if calculation takes too long

2. Implement difference sequence visualization
   - [ ] Collect PositionAdjustment data from orchestrator
   - [ ] Display round-by-round position changes
   - [ ] Show "Task 2 shifted +45 minutes due to upstream change"
   - [ ] Optional: Animate task positions shifting across rounds

3. Implement task detail panel
   - [ ] Right-click task → Show details popup
   - [ ] Display task ID, name, duration, dependencies
   - [ ] Show prerequisite tasks and their completion times
   - [ ] Show intake deadline if present
   - [ ] Show validation message if invalid

**Deliverable**: Real-time convergence tracking visible to user

**Tests**:
- [ ] Convergence progress updates as rounds complete
- [ ] Position changes correctly calculated and displayed
- [ ] Task detail panel shows correct prerequisite chain

**Goal**: End-to-end validation of full system

**Tasks**:
1. Create end-to-end test scenarios
   - [ ] Scenario 1: Simple linear dependency (Task A → B → C)
   - [ ] Scenario 2: Diamond dependency (Task B & C depend on A, Task D depends on B & C)
   - [ ] Scenario 3: Multiple days with daily recurring tasks
   - [ ] Scenario 4: Deadline violation (task can't fit before deadline)
   - [ ] Scenario 5: Duration update causing cascade recalculation

2. Execute scenarios through full stack
   - [ ] Load test CSV files
   - [ ] Trigger Orleans execution via orchestrator
   - [ ] Monitor grain execution via Orleans dashboard
   - [ ] Verify final execution plan correct
   - [ ] Check web UI displays results

3. Debug & fix issues
   - [ ] Orleans grain state not persisting correctly
   - [ ] Grain communication timeouts
   - [ ] API JSON serialization issues
   - [ ] Frontend rendering bugs

4. Performance profiling
   - [ ] Measure time for 100 tasks with 5-round convergence
   - [ ] Measure grain memory overhead per instance
   - [ ] Identify bottlenecks

**Deliverable**: Full system tested and working end-to-end

**Tests**:
- [ ] All 5 scenarios execute successfully
- [ ] Plans match expected dependency sequences
- [ ] Performance acceptable (< 5 seconds for 100 tasks)

---

### Sprint 14: Error Handling & Robustness (1 week)

**Goal**: Graceful handling of edge cases and failures

**Tasks**:
1. Implement error handling in grains
   - [ ] Grain method timeouts
   - [ ] Missing prerequisite grain (null reference handling)
   - [ ] Invalid CSV data (missing columns, bad date formats)
   - [ ] Circular dependency detection (if possible in this domain)

2. Implement error handling in API
   - [ ] 404 when report not found
   - [ ] 400 for bad API requests
   - [ ] 500 with error details for server errors
   - [ ] Timeout handling

3. Frontend error UI
   - [ ] Error toast notifications
   - [ ] Fallback UI when API unavailable
   - [ ] Retry buttons

4. Add logging & diagnostics
   - [ ] Structured logging in grains
   - [ ] Request tracing across layers
   - [ ] Orleans dashboard trace correlation

**Deliverable**: System handles errors gracefully

**Tests**:
- [ ] Missing CSV file → clear error message
- [ ] Grain crashes → orchestrator recovers
- [ ] API timeout → frontend shows retry UI

---

### Sprint 15: Documentation & Transition (1 week)

**Goal**: Complete documentation and prepare for Phase 3

**Tasks**:
1. Update documentation
   - [ ] Orleans grain implementation guide
   - [ ] API endpoint documentation (OpenAPI/Swagger)
   - [ ] Frontend development guide
   - [ ] Deployment guide (cloud to Kubernetes)
   - [ ] Troubleshooting guide

2. Record video tutorials
   - [ ] Local development setup (Aspire startup)
   - [ ] Web dashboard walkthrough
   - [ ] Adding new domain groupings
   - [ ] Interpreting difference sequences

3. Prepare Phase 1 → Phase 2 migration
   - [ ] Archive Phase 1 console app (it still works for validation)
   - [ ] Migrate any remaining business logic
   - [ ] Ensure backward compatibility for testing

4. Assess Phase 3 requirements
   - [ ] Real execution integration (monitor actual task runs)
   - [ ] Persistent state store (Azure Table Storage, SQL, etc.)
   - [ ] Multi-tenant support (multiple business entities)
   - [ ] Authentication & authorization
   - [ ] Audit trails

**Deliverable**: Phase 2 complete and documented, Phase 3 planned

---

### Sprint 16: Performance Tuning & Optimization (1 week)

**Goal**: Optimize for production scale

**Tasks**:
1. Profile Orleans execution
   - [ ] Identify slow grains (GetDuration calls slow?)
   - [ ] Reduce grain state size if possible
   - [ ] Cache prerequisite lookups

2. Optimize web API response times
   - [ ] Add caching (Redis) for frequently accessed reports
   - [ ] Compress API responses (gzip)
   - [ ] Batch API calls from frontend

3. Optimize frontend rendering
   - [ ] Virtual scrolling for large task lists
   - [ ] Lazy-load Gantt chart (don't render all tasks at once)
   - [ ] Memoize React components

4. Load testing
   - [ ] Simulate 1000 tasks, multi-round convergence
   - [ ] Measure p95 latencies
   - [ ] Check infrastructure scaling needs

**Deliverable**: Performance benchmarks documented

---

### Sprint 17: Excel Export & Executive Reporting (Optional, 1 week)

**Goal**: Advanced Excel reporting with sophisticated visualizations for non-technical stakeholders

**Note**: This sprint is optional but highly recommended for user adoption. Can be deferred post-launch if needed.

**Tasks**:
1. Enhance Excel Export with Advanced Features
   - [ ] Add conditional formatting (gradient colors for at-risk severity)
   - [ ] Add data validation for risk level dropdowns
   - [ ] Implement frozen panes (time scale always visible while scrolling)
   - [ ] Add printable page layout (Timeline Calendar on single/multiple pages)

2. Implement Calendar Grid Visualization (Complex)
   - [ ] Generate dynamic Excel table with time blocks as columns
   - [ ] Place tasks in correct time block cells (with merging for multi-block tasks)
   - [ ] Wrap text in cells to show multiple tasks in same block
   - [ ] Add task dependency arrows/connectors (using Excel shape drawing)
   - [ ] Color cells by group (PAYROLL=blue, SETTLEMENT=green, etc.)

3. Create Non-Technical User Guide
   - [ ] Add "How to Read This Report" sheet with instructions
   - [ ] Add legend sheet (color meanings, symbols, conventions)
   - [ ] Add assumption notes (data as of <date>, based on forecasts, etc.)

4. Implement Dependency Chain Visualization
   - [ ] Add visual chain diagrams in Dependency Chains tab
   - [ ] Use text art or embedded shapes to show flow
   - [ ] Highlight critical path in bold/color

5. Implement Risk Analysis Dashboard
   - [ ] Identify bottleneck tasks (many dependencies)
   - [ ] Create risk score matrix (task vs. deadline buffer)
   - [ ] Recommend mitigation actions (text cells with suggestions)
   - [ ] Show cascading failure scenarios ("if Task X is delayed by N minutes, then...")

6. Add Export Options
   - [ ] Encrypt export with password option
   - [ ] Add watermark "CONFIDENTIAL" or "DRAFT"
   - [ ] Generate timestamp and user info footer
   - [ ] Add "Export All Groupings" vs. "Export Single Grouping" option

**Deliverable**: Enhanced Excel reporting ready for executive/management distribution

**Tests**:
- [ ] Excel export handles all features without corruption
- [ ] Dependency chains render without overlapping
- [ ] Calendar grid accurately places 150+ tasks
- [ ] Color coding applies consistently  
- [ ] Risk analysis identifies correct bottlenecks
- [ ] User guide is clear to non-technical readers
- [ ] Encrypted exports open only with password
- [ ] Export file size reasonable (< 5MB for 200-task plan)

---

## Dependency Chains & Risk Mitigation

### Critical Path Dependencies

```
Sprint 1–2 (Orleans Infra + SQLite)
    ↓
Sprint 3 (CSV Processing) + Sprint 4–5 (ExecutionInstanceGrain)
    ↓
Sprint 6–7 (OrchestratorGrain)
    ↓
Sprint 8–9 (Grouping + Reporting)
    ↓
Sprint 10–11 (Desktop App + Basic Excel Export)
    ↓
Sprint 12 (Convergence Visualization)
    ↓
Sprint 13–16 (Integration, Testing, Optimization)
    ↓
Sprint 17 (Advanced Excel Reporting - Optional)
```

### Risk Mitigation

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Orleans clustering issues in local dev | Medium | High | Use localhost clustering; fallback to in-memory grain store |
| SQLite concurrency/locking issues | Low | Medium | Use OptimisticConcurrency for grains; test multi-threaded writes early |
| Grain state persistence complexity | Medium | Medium | Start with memory-only; migrate to SQLite in Sprint 1-2; comprehensive tests |
| Excel export calendar grid rendering issues | Medium | High | Prototype calendar layout early; test with varying task counts |
| ClosedXML cell/style limitations | Low | Medium | Fallback to simpler table format if needed; use alternative (Aspose) only if ClosedXML insufficient |
| WinUI 3 desktop app performance | Medium | Medium | Early performance testing with large task lists; profile early & often |
| Multiple tasks in same time block display | Medium | Medium | Implement text wrapping & cell merging early; test with clustered tasks |
| Performance issues at scale (1000+ tasks) | Medium | High | Early profiling in Sprint 13; optimize hot paths immediately |
| Third-party library bugs (Orleans, WinUI, ClosedXML) | Low | High | Maintain compatibility with LTS library versions; test updates early |

---

## Success Criteria

- [ ] All 16 user stories pass acceptance tests
- [ ] Orleans silo embedding works correctly in WinUI 3 desktop app
- [ ] SQLite database auto-created and persists grain state correctly
- [ ] Grain state recovers correctly on app restart
- [ ] Aspire local development setup works in < 5 minutes (single `dotnet run`)
- [ ] Desktop app renders correctly for 100+ task execution plans
- [ ] Convergence detection works for multi-round refinement
- [ ] Difference sequences accurately tracked and visualized
- [ ] Excel export generates non-technical calendar visualization
- [ ] Excel shows task dependencies, parallel execution, and no-dependency tasks clearly
- [ ] Excel includes all 5 tabs: Summary, Timeline Calendar, Task Details, Dependency Chains, Risk Analysis
- [ ] Performance: < 5 seconds end-to-end for 100-task plans (3 rounds avg)
- [ ] Performance: Excel export completes in < 2 seconds for 150 tasks
- [ ] Documentation complete & reviewed
- [ ] Phase 1 console app validation suite still passing (regression)
- [ ] Excel exports readable by non-technical staff (user guide included)

---

## Post-Phase 2: Phase 3 Forward Looking

**Not in scope for Phase 2**, but anticipated for Phase 3–4:

1. **Real Execution Integration**
   - Monitor actual silo execution
   - Import real duration data (Phase 1 Duration Manifest → live updates)
   - Update plans based on observed timings

2. **Persistent State Store**
   - Orleans grain state → SQL/MongoDB
   - Historical plan snapshots
   - Audit trail of changes

3. **REST API for External Partners**
   - Clients submit CSV files → receive execution plan
   - Polling for plan updates
   - Webhook notifications on deadline violations

4. **Advanced Reporting**
   - Export plans to ICS calendar format
   - Integration with workflow engines
   - Predictive analytics (if task durations vary by day-of-week)

5. **Multi-Silo Distributed Deployment**
   - Azure Container Apps or Kubernetes
   - Geographic distribution
   - Disaster recovery

---

**Approval Sign-Off**: Awaiting review and confirmation before implementation begins.
