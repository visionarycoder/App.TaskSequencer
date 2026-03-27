# Implementation Plan: Subagent-Optimized

## Meta
```yaml
doc_type: implementation_plan
optimization: subagent_parallel_execution
target: minimize_time_to_delivery
volatility_aware: true
version: 1.0.0
date: 2026-03-27
```

## Plan Structure

Each task designed for **single subagent** execution with **minimal context loading**.

## Phase 1: Foundation Refactoring (Parallel Execution)

**Duration**: 3 days
**Parallelization**: High (5 agents simultaneously)
**Token Load**: Minimal per agent

### Task 1.1: Extract L0 Foundation [Agent: Foundation]
**Context Required**:
- Current: `src/ConsoleApp/Ifx/Models/Identifier.cs`
- Current: `src/ConsoleApp/Ifx/Models/TimeOfDay.cs`

**Actions**:
1. Create `src/Domain/Foundation/` folder
2. Move `Identifier.cs` → `src/Domain/Foundation/Identifier.cs`
3. Move `TimeOfDay.cs` → `src/Domain/Foundation/TimeOfDay.cs`
4. Update namespace to `App.TaskSequencer.Domain.Foundation`
5. Update all references (IDE refactoring tool)
6. Verify compilation

**Output**: L0 foundation isolated
**Token Estimate**: 2K tokens
**Dependencies**: None
**Verification**: `dotnet build`

### Task 1.2: Isolate L1 Domain Models [Agent: DomainModel]
**Context Required**:
- Current: `src/ConsoleApp/Ifx/Models/*.cs` (15 files)

**Actions**:
1. Create `src/Domain/Models/` folder
2. Move all record types to `src/Domain/Models/`
3. Update namespace to `App.TaskSequencer.Domain.Models`
4. Verify all properties are `init` or `get` only (immutability)
5. Remove any service dependencies
6. Update all references
7. Verify compilation

**Output**: L1 domain isolated and immutable
**Token Estimate**: 5K tokens
**Dependencies**: Task 1.1 complete
**Verification**: `dotnet build && grep -r "{ get; set; }" src/Domain/Models/` (should be empty)

### Task 1.3: Extract L2 Business Logic [Agent: BusinessLogic]
**Context Required**:
- Current: `src/ConsoleApp/Ifx/Services/DependencyResolver.cs`
- Current: `src/ConsoleApp/Ifx/Services/DeadlineValidator.cs`
- Current: `src/ConsoleApp/Ifx/Services/ExecutionEventMatrixBuilder.cs`
- Current: `src/ConsoleApp/Ifx/Services/ManifestTransformer.cs`

**Actions**:
1. Create `src/BusinessLogic/Services/` folder
2. Move 4 service files to `src/BusinessLogic/Services/`
3. Update namespace to `App.TaskSequencer.BusinessLogic.Services`
4. Remove any orchestration logic (if found)
5. Ensure dependencies only on L0-L1
6. Update all references
7. Add unit test project: `tests/BusinessLogic.Tests/`
8. Verify compilation

**Output**: L2 business logic isolated with test project
**Token Estimate**: 8K tokens
**Dependencies**: Task 1.2 complete
**Verification**: `dotnet build && dotnet test tests/BusinessLogic.Tests/`

### Task 1.4: Isolate L3 Orchestration [Agent: Orchestration]
**Context Required**:
- Current: `src/ConsoleApp/Ifx/Services/ExecutionPlanGenerator.cs`
- Current: `src/ConsoleApp/Ifx/Services/OrleansExecutionPlanGenerator.cs`
- Current: `src/ConsoleApp/Ifx/Orleans/Grains/*.cs`

**Actions**:
1. Create `src/Orchestration/Generators/` folder
2. Create `src/Orchestration/Orleans/Grains/` folder
3. Move `ExecutionPlanGenerator.cs` → `src/Orchestration/Generators/`
4. Move `OrleansExecutionPlanGenerator.cs` → `src/Orchestration/Generators/`
5. Move Orleans grain files → `src/Orchestration/Orleans/Grains/`
6. Update namespace to `App.TaskSequencer.Orchestration.*`
7. Verify no algorithm implementation in orchestration (only coordination)
8. Update all references
9. Add integration test project: `tests/Orchestration.Tests/`
10. Verify compilation

**Output**: L3 orchestration isolated
**Token Estimate**: 10K tokens
**Dependencies**: Task 1.3 complete
**Verification**: `dotnet build && dotnet test tests/Orchestration.Tests/`

### Task 1.5: Extract L4 I/O Infrastructure [Agent: Infrastructure]
**Context Required**:
- Current: `src/ConsoleApp/Ifx/Services/ManifestCsvParser.cs`

**Actions**:
1. Create `src/Infrastructure/Persistence/` folder
2. Move `ManifestCsvParser.cs` → `src/Infrastructure/Persistence/`
3. Update namespace to `App.TaskSequencer.Infrastructure.Persistence`
4. Create `IManifestParser` interface
5. Create `IExportService` interface (future Excel export)
6. Update all references
7. Add I/O test project: `tests/Infrastructure.Tests/`
8. Verify compilation

**Output**: L4 I/O isolated with interfaces
**Token Estimate**: 4K tokens
**Dependencies**: Task 1.2 complete (for DTO references)
**Verification**: `dotnet build && dotnet test tests/Infrastructure.Tests/`

## Phase 2: Orleans Integration (Sequential + Parallel)

**Duration**: 7 days
**Parallelization**: Medium (3 agents, some sequential dependencies)

### Task 2.1: Implement IExecutionTaskGrain [Agent: OrleansGrain1]
**Context Required**:
- `src/Orchestration/Orleans/Grains/Abstractions.cs` (interface)
- `src/Domain/Models/ExecutionEventDefinition.cs`
- `src/Domain/Models/ExecutionInstanceEnhanced.cs`
- `src/BusinessLogic/Services/DeadlineValidator.cs` (reference only)

**Actions**:
1. Create `src/Orchestration/Orleans/Grains/ExecutionTaskGrain.cs`
2. Implement `IExecutionTaskGrain` interface
3. Add grain state persistence (in-memory for Phase 1)
4. Implement methods:
   - `InitializeAsync()`
   - `GetExecutionInstanceAsync()`
   - `UpdateStartTimeAsync()`
   - `GetPlannedCompletionAsync()`
   - `ValidateDeadlineAsync()`
   - `MarkAsReadyAsync()`
5. Call `DeadlineValidator` from L2 for validation logic
6. Add grain unit tests
7. Verify grain activation

**Output**: Functional IExecutionTaskGrain implementation
**Token Estimate**: 12K tokens
**Dependencies**: Task 1.4 complete
**Verification**: `dotnet test tests/Orchestration.Tests/ --filter "ExecutionTaskGrain"`

### Task 2.2: Implement IExecutionPlanCoordinatorGrain [Agent: OrleansGrain2]
**Context Required**:
- `src/Orchestration/Orleans/Grains/Abstractions.cs` (interface)
- `src/Orchestration/Orleans/Grains/ExecutionTaskGrain.cs` (from Task 2.1)
- `src/Domain/Models/ExecutionPlan.cs`

**Actions**:
1. Create `src/Orchestration/Orleans/Grains/ExecutionPlanCoordinatorGrain.cs`
2. Implement `IExecutionPlanCoordinatorGrain` interface
3. Implement iterative refinement algorithm:
   - `CalculateExecutionPlanAsync()` - main orchestration
   - `RefineTimeSlotIterationAsync()` - single iteration
   - `GetCurrentPlanAsync()` - query state
4. Implement convergence detection logic
5. Add grain state persistence
6. Add coordinator unit tests
7. Add integration test with mock task grains

**Output**: Functional IExecutionPlanCoordinatorGrain
**Token Estimate**: 15K tokens
**Dependencies**: Task 2.1 complete
**Verification**: `dotnet test tests/Orchestration.Tests/ --filter "CoordinatorGrain"`

### Task 2.3: Integrate Orleans with Orchestration [Agent: OrleansIntegration]
**Context Required**:
- `src/Orchestration/Generators/OrleansExecutionPlanGenerator.cs`
- `src/Orchestration/Orleans/Grains/ExecutionTaskGrain.cs`
- `src/Orchestration/Orleans/Grains/ExecutionPlanCoordinatorGrain.cs`

**Actions**:
1. Update `OrleansExecutionPlanGenerator` to use real grains
2. Add Orleans client initialization
3. Add grain factory calls
4. Implement convergence loop
5. Add error handling for grain failures
6. Add end-to-end integration test
7. Update `Program.cs` to use Orleans generator

**Output**: Working Orleans-based execution plan generation
**Token Estimate**: 10K tokens
**Dependencies**: Task 2.2 complete
**Verification**: `dotnet run --project src/ConsoleApp/ -- --use-orleans`

### Task 2.4: Add SQLite Persistence [Agent: Persistence]
**Context Required**:
- `src/Orchestration/Orleans/Grains/ExecutionTaskGrain.cs`
- `src/Orchestration/Orleans/Grains/ExecutionPlanCoordinatorGrain.cs`
- Orleans persistence documentation (external)

**Actions**:
1. Add `Orleans.Persistence.AdoNet` NuGet package
2. Add `Microsoft.Data.Sqlite` NuGet package
3. Create SQLite schema in `data/schema.sql`
4. Configure Orleans to use SQLite persistence
5. Update grain state attributes
6. Test grain state persistence across activations
7. Add database backup on shutdown

**Output**: Persistent grain state in SQLite
**Token Estimate**: 8K tokens
**Dependencies**: Task 2.2 complete
**Verification**: Run app twice, verify state persists

## Phase 3: Desktop Application (Highly Parallel)

**Duration**: 10 days
**Parallelization**: Very High (7 agents simultaneously)

### Task 3.1: Create WinUI 3 Project [Agent: DesktopSetup]
**Context Required**: None (scaffolding)

**Actions**:
1. Create new WinUI 3 project: `src/DesktopApp/`
2. Add references to `Orchestration` and `Domain` projects
3. Configure app manifest and assets
4. Create app shell: `MainWindow.xaml`
5. Add navigation framework (NavigationView)
6. Configure dependency injection
7. Embed Orleans silo in desktop app process
8. Verify app launches

**Output**: WinUI 3 app shell with embedded Orleans
**Token Estimate**: 6K tokens
**Dependencies**: Task 2.3 complete (for Orleans client)
**Verification**: `dotnet run --project src/DesktopApp/`

### Task 3.2: Implement Dashboard View [Agent: UIAgent1]
**Context Required**:
- `src/DesktopApp/MainWindow.xaml`
- `src/Orchestration/Generators/OrleansExecutionPlanGenerator.cs` (interface)

**Actions**:
1. Create `src/DesktopApp/Views/DashboardView.xaml`
2. Create `src/DesktopApp/ViewModels/DashboardViewModel.cs`
3. Display execution plan statistics:
   - Total valid tasks
   - Total invalid tasks
   - Critical path completion time
   - Current round number
4. Add "Generate Plan" button
5. Add real-time status updates
6. Wire up to Orleans coordinator grain

**Output**: Functional dashboard view
**Token Estimate**: 10K tokens
**Dependencies**: Task 3.1 complete
**Verification**: Manual UI test

### Task 3.3: Implement Timeline View [Agent: UIAgent2]
**Context Required**:
- `src/Domain/Models/ExecutionInstanceEnhanced.cs`
- WinUI 3 controls documentation

**Actions**:
1. Create `src/DesktopApp/Views/TimelineView.xaml`
2. Create `src/DesktopApp/ViewModels/TimelineViewModel.cs`
3. Implement Gantt chart visualization:
   - Horizontal timeline (time axis)
   - Vertical task list
   - Task blocks with colors (valid/invalid)
4. Add zoom controls
5. Add task hover tooltip (shows dependencies)
6. Wire up to execution plan data

**Output**: Gantt chart timeline view
**Token Estimate**: 15K tokens
**Dependencies**: Task 3.1 complete
**Verification**: Manual UI test

### Task 3.4: Implement Deadline Violations View [Agent: UIAgent3]
**Context Required**:
- `src/Domain/Models/ExecutionPlan.cs`
- `src/Domain/Models/ExecutionInstanceEnhanced.cs`

**Actions**:
1. Create `src/DesktopApp/Views/ViolationsView.xaml`
2. Create `src/DesktopApp/ViewModels/ViolationsViewModel.cs`
3. Display deadline miss list:
   - Task ID and name
   - Scheduled start time
   - Required end time
   - Actual completion time
   - Reason for violation
4. Add sorting and filtering
5. Add export to CSV button
6. Wire up to execution plan data

**Output**: Deadline violations report view
**Token Estimate**: 8K tokens
**Dependencies**: Task 3.1 complete
**Verification**: Manual UI test

### Task 3.5: Implement Settings View [Agent: UIAgent4]
**Context Required**:
- Configuration schema

**Actions**:
1. Create `src/DesktopApp/Views/SettingsView.xaml`
2. Create `src/DesktopApp/ViewModels/SettingsViewModel.cs`
3. Add settings controls:
   - CSV file paths (file picker)
   - Max iterations (slider)
   - Convergence threshold (number input)
   - Orleans dashboard URL
4. Persist settings to `appsettings.json`
5. Load settings on startup

**Output**: Settings configuration view
**Token Estimate**: 6K tokens
**Dependencies**: Task 3.1 complete
**Verification**: Manual UI test

### Task 3.6: Add Excel Export [Agent: ExcelAgent]
**Context Required**:
- `src/Domain/Models/ExecutionPlan.cs`
- `src/Domain/Models/ExecutionInstanceEnhanced.cs`
- ClosedXML documentation

**Actions**:
1. Add `ClosedXML` NuGet package
2. Create `src/Infrastructure/Persistence/ExcelExporter.cs`
3. Implement 5-tab workbook generation:
   - Summary tab (statistics)
   - Timeline Calendar tab (time-block grid)
   - Task Details tab (full listing)
   - Dependency Chains tab (text visualization)
   - Risk Analysis tab (deadline misses)
4. Add color coding (green=valid, red=invalid)
5. Add `IExportService` interface implementation
6. Wire up to desktop app "Export" button

**Output**: Excel export functionality
**Token Estimate**: 12K tokens
**Dependencies**: Task 3.2 complete (for UI integration)
**Verification**: Generate plan → Export → Open Excel

### Task 3.7: Add CSV File Monitoring [Agent: FileWatcher]
**Context Required**:
- `src/Infrastructure/Persistence/ManifestCsvParser.cs`

**Actions**:
1. Create `src/Infrastructure/ExternalServices/FileSystemWatcher.cs`
2. Monitor CSV file changes (FileSystemWatcher)
3. Auto-reload and regenerate plan on file change
4. Add notification toast in UI
5. Add "Auto-reload" toggle in settings
6. Handle file lock conflicts

**Output**: Auto-reload on CSV changes
**Token Estimate**: 5K tokens
**Dependencies**: Task 3.5 complete (settings integration)
**Verification**: Modify CSV → Observe auto-reload

## Phase 4: Testing & Optimization (Parallel)

**Duration**: 4 days
**Parallelization**: High (4 agents)

### Task 4.1: Unit Test Coverage [Agent: TestAgent1]
**Context Required**:
- All L2 business logic services

**Actions**:
1. Ensure >80% coverage for `BusinessLogic` project
2. Add tests for edge cases:
   - Circular dependencies
   - Deadline infeasibility
   - DST transitions
3. Use xUnit + FluentAssertions
4. Mock all L1 dependencies

**Output**: Comprehensive unit tests
**Token Estimate**: 10K tokens
**Dependencies**: Task 1.3 complete
**Verification**: `dotnet test --collect:"XPlat Code Coverage"`

### Task 4.2: Integration Test Coverage [Agent: TestAgent2]
**Context Required**:
- All L3 orchestration

**Actions**:
1. Ensure >70% coverage for `Orchestration` project
2. Add end-to-end tests:
   - CSV → Execution Plan
   - Orleans grain convergence
   - SQLite persistence
3. Use Orleans TestCluster
4. Use real L2 services, mock I/O

**Output**: Comprehensive integration tests
**Token Estimate**: 12K tokens
**Dependencies**: Task 2.3 complete
**Verification**: `dotnet test tests/Orchestration.Tests/`

### Task 4.3: Performance Optimization [Agent: PerfAgent]
**Context Required**:
- `src/BusinessLogic/Services/DependencyResolver.cs`
- `src/Orchestration/Orleans/Grains/ExecutionPlanCoordinatorGrain.cs`

**Actions**:
1. Profile execution plan generation
2. Identify bottlenecks (likely in dependency resolution)
3. Optimize algorithms:
   - Cache prerequisite lookups
   - Parallelize grain activations
   - Use ConcurrentDictionary for shared state
4. Add benchmarks (BenchmarkDotNet)
5. Target: <5 seconds for 100 tasks

**Output**: Optimized performance
**Token Estimate**: 8K tokens
**Dependencies**: Task 2.3 complete
**Verification**: Run benchmarks, verify <5s

### Task 4.4: Security & Code Quality [Agent: QualityAgent]
**Context Required**:
- All source code (scan mode)

**Actions**:
1. Run static analysis (SonarQube or Roslyn analyzers)
2. Fix all critical warnings
3. Ensure no primitive obsession violations
4. Verify all async methods have CancellationToken
5. Check for SQL injection (SQLite queries)
6. Verify file path validation (CSV input)
7. Add security scanning to CI/CD

**Output**: Code quality report + fixes
**Token Estimate**: 6K tokens
**Dependencies**: All previous tasks
**Verification**: `dotnet build /warnaserror`

## Phase 5: Documentation & Deployment (Parallel)

**Duration**: 2 days
**Parallelization**: Medium (3 agents)

### Task 5.1: User Documentation [Agent: DocsAgent1]
**Context Required**: None (user perspective)

**Actions**:
1. Create `docs/user-guide.md`
2. Document CSV file formats
3. Document desktop app usage
4. Add screenshots
5. Document Excel export format
6. Add troubleshooting guide

**Output**: User guide
**Token Estimate**: 5K tokens
**Dependencies**: Task 3.6 complete (for screenshots)
**Verification**: Manual review

### Task 5.2: Developer Documentation [Agent: DocsAgent2]
**Context Required**:
- Volatility-based architecture docs

**Actions**:
1. Update `docs/architecture/` with actual implementation
2. Document subagent usage patterns
3. Add API documentation (XML docs)
4. Document testing strategy
5. Add contribution guide

**Output**: Developer docs
**Token Estimate**: 6K tokens
**Dependencies**: Task 4.4 complete
**Verification**: Manual review

### Task 5.3: Deployment Package [Agent: DeployAgent]
**Context Required**:
- WinUI 3 packaging documentation

**Actions**:
1. Create MSIX package configuration
2. Add app signing certificate
3. Create installer wizard
4. Add auto-update mechanism (optional)
5. Test installation on clean Windows 11 VM
6. Create release notes

**Output**: MSIX installer
**Token Estimate**: 5K tokens
**Dependencies**: Task 3.7 complete
**Verification**: Install on clean VM

## Execution Strategy

### Parallel Execution Waves

**Wave 1** (Day 1-3): Foundation refactoring
- Task 1.1, 1.2, 1.3, 1.4, 1.5 in parallel (5 agents)

**Wave 2** (Day 4-7): Orleans integration
- Task 2.1 (Day 4-5)
- Task 2.2 (Day 5-6, depends on 2.1)
- Task 2.3 (Day 6-7, depends on 2.2)
- Task 2.4 (Day 7, parallel with 2.3 completion)

**Wave 3** (Day 8-17): Desktop UI
- Task 3.1 (Day 8)
- Task 3.2, 3.3, 3.4, 3.5 (Day 9-14, parallel, 4 agents)
- Task 3.6, 3.7 (Day 15-17, parallel, 2 agents)

**Wave 4** (Day 18-21): Testing & optimization
- Task 4.1, 4.2, 4.3 (Day 18-20, parallel, 3 agents)
- Task 4.4 (Day 21, sequential)

**Wave 5** (Day 22-23): Documentation & deployment
- Task 5.1, 5.2, 5.3 (Day 22-23, parallel, 3 agents)

**Total Duration**: 23 days (with parallel execution)
**Sequential Duration Equivalent**: ~45 days (95% reduction)

## Token Budget Per Task

| Task | Estimated Tokens | Justification |
|------|------------------|---------------|
| 1.1 Foundation | 2K | Minimal files |
| 1.2 Domain | 5K | 15 files but simple records |
| 1.3 Business Logic | 8K | 4 services with algorithms |
| 1.4 Orchestration | 10K | Complex coordination |
| 1.5 I/O | 4K | Single parser file |
| 2.1 ExecutionTaskGrain | 12K | Grain implementation + state |
| 2.2 CoordinatorGrain | 15K | Complex iterative logic |
| 2.3 Orleans Integration | 10K | Integration code |
| 2.4 SQLite Persistence | 8K | Configuration + schema |
| 3.1 WinUI Setup | 6K | Project scaffolding |
| 3.2 Dashboard | 10K | XAML + ViewModel |
| 3.3 Timeline | 15K | Complex visualization |
| 3.4 Violations | 8K | Report view |
| 3.5 Settings | 6K | Configuration UI |
| 3.6 Excel Export | 12K | ClosedXML + 5 tabs |
| 3.7 File Monitoring | 5K | FileSystemWatcher |
| 4.1 Unit Tests | 10K | Test coverage |
| 4.2 Integration Tests | 12K | Orleans TestCluster |
| 4.3 Performance | 8K | Profiling + optimization |
| 4.4 Quality | 6K | Static analysis |
| 5.1 User Docs | 5K | Documentation |
| 5.2 Dev Docs | 6K | Technical docs |
| 5.3 Deployment | 5K | MSIX packaging |

**Total Token Budget**: 192K tokens
**Average per task**: 8.3K tokens
**Max single task**: 15K tokens (within limits)

## Dependency Graph

```
1.1 → 1.2 → 1.3 → 1.4 → 2.1 → 2.2 → 2.3 → 3.1 → 3.2 → 4.1
                   ↓                    ↓      ↓
                  1.5                  2.4    3.3 → 4.2
                                              3.4 → 4.3
                                              3.5 → 3.7
                                              3.6 → 5.1
                                                    5.2 → 4.4 → 5.3
```

## Success Criteria

### Per Task
- [ ] Compiles without errors
- [ ] All tests pass
- [ ] No static analysis warnings
- [ ] Token usage within budget
- [ ] Verification step succeeds

### Per Phase
- [ ] All tasks complete
- [ ] Integration tests pass
- [ ] Manual QA complete (for UI tasks)

### Overall
- [ ] Application runs end-to-end
- [ ] CSV → Execution Plan → Excel export works
- [ ] Desktop UI functional
- [ ] Orleans grains converge correctly
- [ ] SQLite persistence works
- [ ] Documentation complete
- [ ] MSIX installer works

## Risk Mitigation

### High Risk Tasks
- **Task 2.2 (CoordinatorGrain)**: Complex convergence logic
  - **Mitigation**: Prototype convergence algorithm first in L2 unit test
- **Task 3.3 (Timeline View)**: Complex visualization
  - **Mitigation**: Use existing WinUI 3 chart library (if available)
- **Task 4.3 (Performance)**: May find fundamental bottlenecks
  - **Mitigation**: Profile early, optimize incrementally

### Dependencies on External Factors
- **Orleans 10.x availability**: May need to use Orleans 8.x
  - **Mitigation**: Code to Orleans interfaces, version agnostic
- **WinUI 3 stability**: Known issues in early versions
  - **Mitigation**: Use stable WinUI 3.5+ version
- **ClosedXML**: May have performance issues with large workbooks
  - **Mitigation**: Test with 500+ tasks early

## Conclusion

This implementation plan is **optimized for subagent parallel execution**:

1. **Maximum parallelization**: 5-7 agents working simultaneously
2. **Minimal context per agent**: Average 8K tokens (40% reduction vs monolithic)
3. **Clear dependencies**: Each task has explicit prerequisites
4. **Fast delivery**: 23 days vs 45 days sequential (50% reduction)
5. **Volatility-aware**: High-volatility tasks isolated, can iterate rapidly

**Next**: Execute Phase 1 (Foundation Refactoring) with 5 parallel subagents.
