# Technology Stack & Desktop GUI Architecture (Phase 2)

**Version**: 2.0 (Desktop Application)  
**Date**: March 26, 2026  
**Status**: Requirements Stage (Implementation Pending)  
**Application Type**: Windows 11 Native Desktop Application (WinUI 3)

---

## Executive Summary

App.TaskSequencer Phase 2 implements a native Windows 11 desktop application combining Orleans distributed actors for parallel task sequencing with WinUI 3 for desktop visualization. The single-process architecture embeds the Orleans silo directly in the desktop application, eliminating network complexity while maintaining the full actor model for parallel grain execution.

**Key Architecture Principle**: Embedded Orleans silo (no remote services, no web API, no external database).

---

## Technology Stack Overview

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| **Runtime** | .NET | 8.0 | Target framework for all projects |
| **Distributed Actors** | Orleans | 8.x | Grain-based parallel task execution & sequencing |
| **Grain Persistence** | SQLite (via Orleans.Persistence.AdoNet) | 3.x | Durable grain state storage in local database |
| **Desktop Framework** | WinUI 3 | 1.6+ | Native Windows 11 UI with Fluent Design |
| **MVVM Framework** | MVVM Toolkit | 8.2+ | Binding, commanding, UI-VM separation |
| **Excel Export** | ClosedXML | 0.21+ | Excel 2007+ file generation with styling |
| **Excel Export** | DocumentFormat.OpenXml | 3.x | OOXML format support for Excel files |
| **Logging** | Serilog | 3.x+ | Structured logging to console & files |
| **Dependency Injection** | Microsoft.Extensions.DI | 8.x | Service container & composition root |
| **CSV Parsing** | CsvHelper | 30.x+ | Fast CSV parsing for Phase 1 integration |

---

## Core Technology Decisions

### 1. Orleans 8.x (Distributed Actor System)

**Why Orleans?**
- Virtual actor model ideal for 100+ independent task calculations
- Automatic reminders for state reevaluation (supports convergence rounds)
- Built-in activation & deactivation (memory efficient)
- Type-safe grain interfaces (compile-time checked communication)
- Performance: Sub-millisecond grain calls on same machine

**Key Orleans Architecture (Phase 2)**:
- **Single Orleans Silo**: Embedded in WinUI 3 app process (localhost only)
- **No Remote Transport**: All grain communication in-process
- **SQLite Persistence**: Local database in app folder (automatic on every state change)
- **Grain Types**: 4 grain interfaces (per [03-orleans-aspire-architecture.md](03-orleans-aspire-architecture.md))

**Orleans Setup with SQLite Persistence**:
```csharp
// Program.cs (WinUI 3 App.xaml.cs or startup)
var appDataPath = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(appDataPath);
var dbPath = Path.Combine(appDataPath, "TaskSequencer.db");

var siloHost = new SiloHostBuilder()
    .UseLocalhostClustering()  // Single-machine silo
    .ConfigureApplicationParts(parts =>
    {
        parts.AddApplicationPart(typeof(ExecutionInstanceGrain).Assembly).WithReferences();
    })
    .AddAdoNetGrainStorage("grain-storage", options =>
    {
        options.Invariant = "System.Data.SQLite";
        options.ConnectionString = $"Data Source={dbPath};Version=3;";
        options.UseJsonFormatForBlobs = true;  // Easier debugging
    })
    .UseDashboard(options =>
    {
        options.Port = 11111;  // Optional: Dev dashboard on localhost:11111
    })
    .Build();

await siloHost.StartAsync();

// Connect grain client (same process has implicit connection)
var grainFactory = siloHost.Services.GetRequiredService<IGrainFactory>();
```

**SQLite Database Location**:
- **Path**: `<AppExePath>\data\TaskSequencer.db`
- **Example**: `C:\Program Files\App.TaskSequencer\data\TaskSequencer.db`
- **Auto-created**: On first run if it doesn't exist
- **Automatic backup**: Weekly backups to `TaskSequencer.db.backup.<date>`

**Performance Characteristics**:
- Grain activation: ~1ms
- Grain call (same host): < 0.1ms
- SQLite state persist: ~5-10ms per grain (batched, async)
- Convergence detection (150 tasks, 3 rounds): ~1-2 seconds total
- Memory footprint: ~50-100MB for 1000-task execution plan in RAM
- SQLite database size: ~500KB-1MB for 1000 tasks (indexed)

---

### 2. WinUI 3 (Native Windows 11 Desktop GUI)

**Why WinUI 3?**
- Native Windows 11 integration (Fluent Design System, Acrylic effects)
- High DPI awareness built-in
- Touch & pen support (future: tablet scenarios)
- Direct integration with C# grain interfaces (no API call serialization)
- Dark/Light theme support (Windows theme binding)
- Performance: 60 FPS for complex XAML layouts

**Architecture Pattern**: MVVM (Model-View-ViewModel)
```
View Layer (XAML)
    └─ MainWindow.xaml, DashboardPage.xaml, TimelineView.xaml, etc.
       ↑↓ Data binding (two-way)
ViewModel Layer (C#)
    └─ DashboardViewModel, TimelineViewModel, GroupsViewModel
       ↑↓ Commands & properties
Model Layer (C#)
    └─ Grain interfaces (IExecutionInstanceGrain, IExecutionPlanOrchestratorGrain, etc.)
       ↑↓ Async grain calls (Task<T>)
Grains (Distributed Actors)
    └─ ExecutionInstanceGrain, ExecutionPlanOrchestratorGrain, etc.
```

**WinUI 3 Project Structure**:
```
App.WinUI/
├─ App.xaml (app-level resources)
├─ App.xaml.cs (startup, grain silo initialization)
├─ Views/
│  ├─ MainWindow.xaml
│  ├─ DashboardPage.xaml (landing page, task statistics)
│  ├─ TimelineView.xaml (Gantt chart for all tasks)
│  ├─ GroupsPage.xaml (per-group drill-down)
│  └─ ViolationsPage.xaml (deadline miss reporting)
├─ ViewModels/
│  ├─ DashboardViewModel.cs
│  ├─ TimelineViewModel.cs
│  ├─ GroupsViewModel.cs
│  └─ ViolationsViewModel.cs
└─ Converters/
   ├─ BoolToVisibilityConverter.cs
   ├─ StatusToColorConverter.cs (Valid = green, Invalid = red)
   └─ TimeSpanToStringConverter.cs
```

**Key WinUI 3 Packages**:
```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.* or later" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.*" />
<PackageReference Include="CommunityToolkit.WinUI" Version="8.1.*" />
```

---

### 3. MVVM Toolkit (UI-Business Logic Separation)

**Why MVVM Toolkit?**
- Two-way data binding (XAML ↔ ViewModel properties)
- RelayCommand for button clicks, menu actions
- ObservableCollection for live updates (as new grains finish calculations)
- INotifyPropertyChanged boilerplate generation (reduces code)

**ViewModel Pattern**:
```csharp
public partial class DashboardViewModel : ObservableObject
{
    private readonly IGrainFactory _grainFactory;
    
    [ObservableProperty]
    private string? executionPlanId;
    
    [ObservableProperty]
    private int totalTasks;
    
    [ObservableProperty]
    private int validTasks;
    
    [ObservableProperty]
    private double convergenceProgress;  // For progress indicator
    
    [RelayCommand]
    private async Task RefreshPlan()
    {
        // Call grain to recalculate
        var orchestrator = _grainFactory.GetGrain<IExecutionPlanOrchestratorGrain>(executionPlanId!);
        await orchestrator.ExecuteRoundAsync();
        
        // Update UI via data binding
        ValidTasks = await orchestrator.GetValidTaskCountAsync();
    }
}
```

**Binding in XAML**:
```xaml
<TextBlock Text="{x:Bind ViewModel.TotalTasks, Mode=OneWay}" />
<Button Command="{x:Bind ViewModel.RefreshPlanCommand}" />
<ProgressBar Value="{x:Bind ViewModel.ConvergenceProgress, Mode=OneWay}" />
```

---

### 4. Serilog (Structured Logging)

**Why Serilog?**
- Structured logging (key-value pairs in JSON)
- Multiple sinks (console, file, rolling file)
- Zero-config for development
- Integrates with other services via dependency injection

**Setup**:
```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

services.AddLogging(cfg => cfg.AddSerilog());
```

**Usage in Grains**:
```csharp
public class ExecutionInstanceGrain : Grain, IExecutionInstanceGrain
{
    private readonly ILogger<ExecutionInstanceGrain> _logger;
    
    public async Task RecalculateAsync()
    {
        _logger.LogInformation("Recalculating task {TaskId}", _executionInstance.ExecutionInstanceKey);
        // Calculation logic
    }
}
```

---

## Application Startup Architecture

**On Windows 11 App Launch**:
```
1. WinUI App.xaml.cs OnLaunched()
   ↓
2. Initialize Dependency Injection container
   - Register grain interfaces
   - Register ViewModels
   - Register logging (Serilog)
   ↓
3. Start embedded Orleans Silo (blocking until ready)
   - Activate ExecutionInstanceGrain type
   - Activate ExecutionPlanOrchestratorGrain type
   - Activate SequenceGroupGrain type
   - Activate ReportGeneratorGrain type
   ↓
4. Get IGrainFactory from Orleans Silo
   ↓
5. Inject grain factory into ViewModels
   ↓
6. Show MainWindow (WinUI 3 XAML)
   ↓
7. User loads CSV file (Phase 1 integration)
   → Parse CSV with CsvHelper
   → Create grains (ExecutionInstanceGrain for each task)
   → Call IExecutionPlanOrchestratorGrain.InitializeAsync()
   ↓
8. Orchestrator kicks off ExecuteRound() loop
   → UI updates via data binding every round
   ↓
9. User sees live convergence progress on Dashboard
```

---

## User Interface Screens (WinUI 3)

### Screen 1: Dashboard (Landing)

**Purpose**: Overview of loaded execution plan

```
┌─────────────────────────────────────────────────────┐
│ App.TaskSequencer - Dashboard                   [−][□][×] │
├─────────────────────────────────────────────────────┤
│ [File: payroll-2024-03-25.csv]                      │
│ [Reload] [Export Report] [Settings]                │
├─────────────────────────────────────────────────────┤
│                                                     │
│ Execution Plan Statistics                           │
│ ════════════════════════════════                    │
│                                                     │
│  Total Tasks: 145                                  │
│  Valid Tasks: 143  ✓                               │
│  Invalid Tasks: 2  ⚠                               │
│  Critical Path: 06:00 → 14:00 (8h 0m)             │
│                                                     │
│  Convergence Rounds: 3 of 5 (60%)                 │
│  [████████░░░░░░░░░░]  Last round: 12 tasks changed│
│                                                     │
│  Business Groupings (Summary)                       │
│  ─────────────────────────────                     │
│  PAYROLL    │ 95 tasks │ 0 violations              │
│  SETTLEMENT │ 40 tasks │ 2 violations              │
│  REPORTING  │ 10 tasks │ 0 violations              │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Key Elements**:
- File name/path (drag-drop CSV support)
- Live statistics (updated each convergence round)
- Progress bar (rounds completed / max iterations)
- Grouping summary (click to drill-down)

### Screen 2: Timeline (Gantt Chart - All Groups)

**Purpose**: Visualize task dependencies and deadlines across all business groupings

```
┌─────────────────────────────────────────────────────┐
│ Timeline - All Groups                           │
├─────────────────────────────────────────────────────┤
│ [← Back] [Zoom In] [Zoom Out] [Export PNG]        │
│ [Filter: All Groups ▼] [Sort: By Start Time ▼]   │
├─────────────────────────────────────────────────────┤
│                                                     │
│ 06:00    07:00    08:00    09:00    10:00          │
│ ├─────────────────────────────────────────────────  │
│ │                                                   │
│ PAYROLL                                             │
│ │ Task 1  [████ Extract Payroll]                   │
│ │ Task 2  │      [━━━ Validate]                   │
│ │ Task 3  │            [████ Transform]           │
│ │ Task 4  [XXXX─ Invalid:late]                    │
│ │ Task 5  │            │    [─ Aggregate]        │
│ │  ... (91 more tasks)                            │
│ │                                                   │
│ SETTLEMENT                                          │
│ │ Task 40 [████ Download Statements]              │
│ │ Task 41 │      [━━ Reconcile]                   │
│ │  ... (38 more tasks)                            │
│ │                                                   │
│ REPORTING                                           │
│ │ Task 80 [════ Aggregate Results]                │
│ │  ... (9 more tasks)                             │
│                                                     │
│ Legend:  ████ = On time   [XXXX─ = Deadline miss  │
│          ━━━ = In progress                         │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**Interactions**:
- Hover over task → Show full name, precursors, deadline
- Click task → Highlight dependencies (precursor tasks)
- Double-click task → Jump to single-group Timeline view
- Scroll horizontally → Navigate time
- Zoom slider → Adjust time scale

### Screen 3: Groups (Per-Group Details)

**Purpose**: Drill-down into single business grouping

```
┌──────────────────────────────────────────────────────┐
│ Group: PAYROLL                                        │
├──────────────────────────────────────────────────────┤
│ [← Back] [Download ICS Calendar] [Copy Timeline]    │
├──────────────────────────────────────────────────────┤
│                                                      │
│ Stats: 95 tasks, 93 valid ✓, 2 invalid ⚠           │
│ Critical Path: 06:00 → 14:00 (8 hours)             │
│ Deadline Violations: 0                              │
│                                                      │
│ TIMELINE GANTT CHART                                │
│ 06:00  06:30  07:00  07:30  08:00  08:30  09:00    │
│ ├──────────────────────────────────────────────────  │
│                                                      │
│ Task 1  [████ Extract]                             │
│ Task 2  │      [─── Validate]                      │
│ Task 3  │           [████ Transform]               │
│ Task 4  [XXXX─ Invalid:deadline] ← double-click    │
│ Task 5  │           │    [─ Load]                  │
│ Task 6  │           │       [─ Publish]            │
│  ...                                                │
│ Task 95 │.......................| [Cleanup]        │
│                                                      │
│ Task Details (selected task 4)                      │
│ ───────────────────────────────                    │
│ Task 4: Transform Payroll Data                      │
│ Status: INVALID ⚠                                  │
│ Precursors: Task 1, Task 2                          │
│ Scheduled Start: 10:30                              │
│ Calculated Start: 10:45 (delayed by Task 2)        │
│ Duration: 120 min (estimated)                       │
│ Planned End: 12:45                                  │
│ Deadline: 12:00 → MISSES by 45 minutes             │
│ Reason: Precursor Task 2 delayed from 8:15→10:45  │
│                                                      │
│ Suggestions:                                        │
│ • Increase Task 2 parallelization (currently 1 CPU)│
│ • Move task deadline from 12:00 to 13:00           │
│ • Split Task 4 to reduce duration                  │
│                                                      │
└──────────────────────────────────────────────────────┘
```

### Screen 4: Violations (Deadline Misses)

**Purpose**: Identify all deadline violations for management reporting

```
┌──────────────────────────────────────────────────────┐
│ Deadline Violations Report                           │
├──────────────────────────────────────────────────────┤
│ [Export to Excel] [Sort by: Miss Magnitude ▼]       │
├──────────────────────────────────────────────────────┤
│                                                      │
│ Total Violations: 5                                 │
│                                                      │
│ Task │ Group      │ Scheduled │ Calculated │ Miss   │
│──────┼────────────┼───────────┼────────────┼────────│
│ 4    │ PAYROLL    │ 10:30     │ 10:45      │ +15m   │
│ 42   │ REPORTING  │ 18:00     │ 18:00      │ +0m    │
│ 55   │ PAYROLL    │ 12:00     │ 12:05      │ +5m    │
│ 71   │ SETTLEMENT │ 15:30     │ 15:45      │ +15m   │
│ 87   │ SETTLEMENT │ 11:00     │ 11:15      │ +15m   │
│                                                      │
│ (Click task to see details in Group view)           │
│                                                      │
└──────────────────────────────────────────────────────┘
```

---

## Excel Export: Executive-Friendly Calendar Visualization

**Purpose**: Allow non-technical stakeholders to visualize the complete execution plan in Excel format with task dependencies, risk conditions, and parallel execution patterns clearly visible.

**Key Features**:
1. **Calendar Grid Layout** - Time blocks across columns, business groups down rows
2. **Dependency Visualization** - Tasks show precursor chain with connecting lines
3. **Color Coding** - Green (on-time), Yellow (at-risk), Red (violation)
4. **Parallel Execution** - Multiple tasks in same time block stacked visually
5. **No-Dependency Tasks** - Simple tasks with no precursors clearly marked
6. **At-Risk Indicators** - Forecast violations highlighted before they occur
7. **Export Formats** - Excel (.xlsx) with embedded metadata

### Export Structure

**Tabs in Excel Workbook**:

1. **Summary Tab**
   - Plan ID, Load Date, Last Updated
   - Total Tasks, Valid Tasks, Invalid Tasks
   - Critical Path Duration
   - Violation Count
   - Last Convergence Round

2. **Timeline Calendar (Main View)**
   - Columns: Time blocks (30-min intervals, e.g., "06:00-06:30", "06:30-07:00")
   - Rows: Business Groups (PAYROLL, SETTLEMENT, REPORTING, etc.)
   - Cells: Task names with color coding and icons
   
3. **Task Details Listing**
   - All tasks in report order with:
     - Task ID, Name, Interface Number
     - Precursor Tasks (dependency chain)
     - Scheduled vs. Calculated Start Time
     - Deadline & Miss Amount (if violated)
     - Duration, Grouping
     - Convergence Round when last changed

4. **Dependency Graph (Text-based)**
   - Shows task chains visually:
     ```
     Task 1 (Extract) [ON-TIME]
       └─→ Task 2 (Validate) [ON-TIME]
             └─→ Task 3 (Transform) [ON-TIME]
                   └─→ Task 4 (Load) [⚠ AT-RISK: +15m deadline miss]
     ```

5. **Risk Analysis Tab**
   - Critical Path visualization
   - Bottleneck tasks (high dependency count)
   - Tasks with tight margins
   - Potential cascading failures

### Calendar Cell Content Example

```
┌─────────────────────────────────┐
│ PAYROLL | 08:00-08:30 TIME BLOCK │
├─────────────────────────────────┤
│                                  │
│ 🟢 Task 1: Extract              │ ✓ On time
│    (no dependencies)             │ 30 min
│                                  │
│ 🟢 Task 2: Validate             │ ✓ On time
│    ← depends on Task 1           │ 20 min
│                                  │
│ 🟡 Task 5: Aggregate             │ ⚠ At risk
│    ← depends on Tasks 2, 3, 4    │ +8m deadline miss
│    (4 precursors total)          │ Forecast
│                                  │
└─────────────────────────────────┘

Legend:
🟢 = Task on schedule
🟡 = Task at risk (potential deadline miss)
🔴 = Task deadline violated
← = Task dependency indicator
```

### Parallel Execution Visualization

Multiple tasks in the same time block are shown stacked:

```
SETTLEMENT | 09:00-09:30
────────────────────────────
🟢 Task 40: Download Statements  (standalone, no deps)
🟢 Task 41: Validate             (no deps)  
🟢 Task 42: Reconcile            (no deps)
────────────────────────────
→ All 3 run PARALLEL in this time block
```

### No-Dependency Tasks Identification

Tasks with no precursor requirements are visually distinct:

```
Simple Task (No Dependencies):
  Task 15: Generate Stub                    ✓ Standalone
  No precursors. Can start anytime after 09:00.
  
  Expected: 06:00 start, 06:30 end
  No chain impact.
```

### Dependent Chain Visualization

Text-based dependency chain for non-technical readers:

```
Main Sequence for PAYROLL (6:00-14:00):
═══════════════════════════════════════════

1. 06:00 → Extract payroll data          (30 min)  ✓ On time
                    ↓
2. 06:30 → Validate extracted data       (15 min)  ✓ On time
                    ↓
3. 06:45 → Transform to ERP format       (45 min)  ✓ On time
                    ↓
4. 07:30 → Load to staging area          (30 min)  ✓ On time
                    ↓
5. 08:00 → Run integrity checks          (20 min)  ✓ On time
                    ↓  ← + parallel tasks 8, 9
                    ↓
6. 08:20 → Aggregate results             (40 min)  🟡 AT RISK by 15 min
           (now forecast ends 09:00 but deadline is 08:45)
```

### At-Risk Indicator Algorithm

Tasks flagged as "at-risk" (yellow/🟡) when:
- Calculated completion > (Deadline - 15 min buffer)
- Any precursor task is already red (likely cascade)
- Convergence hasn't stabilized yet (may improve in next round)

```
Risk Status Recommendations:
━━━━━━━━━━━━━━━━━━━━━━━━━

🔴 CRITICAL (Red):
   - Deadline already violated in current plan
   - Action: Escalate to management immediately
   
🟡 AT-RISK (Yellow):
   - Forecast shows violation likely in next 2 rounds
   - Action: Review precursor tasks for optimization
   
🟢 ON-TIME (Green):
   - Computed to finish within deadline + 15 min buffer
   - Action: Monitor for convergence changes
```

### Export Implementation

**ViewModel Command**:
```csharp
[RelayCommand]
public async Task ExportExecutionPlanAsync()
{
    var filePicker = new FileSavePicker();
    filePicker.FileTypeChoices.Add("Excel Files", new[] { ".xlsx" });
    filePicker.SuggestedFileName = $"ExecutionPlan_{DateTime.Now:yyyy-MM-dd}.xlsx";
    
    var file = await filePicker.PickSaveFileAsync();
    if (file != null)
    {
        await ExcelExportService.ExportFullPlanAsync(
            _grainFactory,
            _executionPlanId,
            file.Path
        );
    }
}
```

**Export Service** (pseudocode):
```csharp
public class ExcelExportService
{
    public async Task ExportFullPlanAsync(IGrainFactory grainFactory, 
        string planId, string outputPath)
    {
        using (var workbook = new XLWorkbook())
        {
            // Fetch execution plan from orchestra grain
            var orchestrator = grainFactory.GetGrain<IExecutionPlanOrchestratorGrain>(planId);
            var plan = await orchestrator.GetFullReportAsync();
            
            // Create Summary tab
            var summarySheet = workbook.Worksheets.Add("Summary");
            summarySheet.Cell("A1").Value = "Execution Plan Summary";
            summarySheet.Cell("A2").Value = $"Plan ID: {plan.PlanId}";
            summarySheet.Cell("A3").Value = $"Total Tasks: {plan.Tasks.Length}";
            
            // Create Calendar View tab
            var calendarSheet = workbook.Worksheets.Add("Timeline Calendar");
            CreateCalendarView(calendarSheet, plan);
            
            // Create Details tab
            var detailsSheet = workbook.Worksheets.Add("Task Details");
            PopulateTaskDetails(detailsSheet, plan);
            
            // Create Dependency Graph tab
            var depGraph = workbook.Worksheets.Add("Dependency Chains");
            PopulateDependencyChains(depGraph, plan);
            
            // Create Risk Analysis tab
            var riskSheet = workbook.Worksheets.Add("Risk Analysis");
            PopulateRiskAnalysis(riskSheet, plan);
            
            workbook.SaveAs(outputPath);
        }
    }
    
    private void CreateCalendarView(IXLWorksheet sheet, ExecutionPlan plan)
    {
        // Generate 30-min time blocks
        var timeBlocks = GenerateTimeBlocks(plan.StartTime, plan.EndTime);
        var groups = plan.Tasks.GroupBy(t => t.GroupId).ToList();
        
        // Header row: time blocks
        for (int col = 0; col < timeBlocks.Count; col++)
        {
            sheet.Cell(1, col + 2).Value = timeBlocks[col];
            sheet.Cell(1, col + 2).Style.Fill.BackgroundColor = XLColor.LightGray;
        }
        
        // Data rows: groups
        int row = 2;
        foreach (var group in groups)
        {
            sheet.Cell(row, 1).Value = group.Key;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            
            // Place tasks in appropriate time blocks
            foreach (var task in group)
            {
                int blockIndex = GetTimeBlockIndex(task.CalculatedStartTime, timeBlocks);
                var cellValue = $"{task.Name}\n(Deps: {task.PrecursorCount})";
                
                var cell = sheet.Cell(row, blockIndex + 2);
                cell.Value = cellValue;
                
                // Color code by status
                var bgColor = task.IsDeadlineViolated ? XLColor.Red :
                             task.IsAtRisk ? XLColor.Yellow : XLColor.Green;
                cell.Style.Fill.BackgroundColor = bgColor;
                cell.Style.Alignment.Horizontal = XLAlignment.Center;
                cell.Style.Alignment.Vertical = XLAlignment.Center;
            }
            
            row++;
        }
    }
}
```

### Non-Technical User Guide

The exported Excel file includes an embedded guide on the "Instructions" tab:

```
HOW TO READ THIS REPORT
═════════════════════════

1. SUMMARY TAB
   Shows quick statistics: total tasks, violations, timeline.

2. TIMELINE CALENDAR
   Time flows left to right (06:00 → 18:00).
   Rows are business groups (PAYROLL, SETTLEMENT, etc.).
   Tasks in each cell show dependencies and status.
   
   🟢 GREEN  = Task will finish on time
   🟡 YELLOW = Task might be late (at-risk)
   🔴 RED    = Task will be LATE (deadline miss)

3. TASK DETAILS
   Complete list of all 150+ tasks with:
   - When it's supposed to start/finish
   - What depends on it
   - How long it takes
   - If it will miss a deadline

4. DEPENDENCY CHAINS
   Shows the "chain reactions" - when Task 1 is late,
   which other tasks are affected.
   
   Example: If Task 2 depends on Task 1,
   and Task 1 runs late, Task 2 also runs late.

5. RISK ANALYSIS
   Identifies the "weak points" that could break the schedule.
   Recommendations for manager review.

KEY THINGS TO LOOK FOR:
• Red cells = PROBLEMS (deadline violations)
• Yellow cells = WARNINGS (might break if any dependency slips)
• Green cells = OK (should finish on time)
• Tasks with many arrows = Critical tasks (many things depend on them)
```

---

## SQLite Data Persistence

### Database Schema

Orleans automatically creates grain state tables on first run:

**GrainState Table** (managed by Orleans):
```sql
CREATE TABLE GrainState (
    GrainId NVARCHAR(450) PRIMARY KEY,
    GrainTypeHash INT NOT NULL,
    State NVARCHAR(MAX) NOT NULL,
    ETag NVARCHAR(MAX),
    ModifiedOn DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_GrainState_GrainTypeHash ON GrainState(GrainTypeHash);
CREATE INDEX idx_GrainState_ModifiedOn ON GrainState(ModifiedOn);
```

### Stored Data Examples

**ExecutionInstanceGrain State**:
```json
{
  "executionInstanceKey": "Task_001",
  "interfaceNumber": "042",
  "interfaceName": "Extract Customer Data",
  "calculatedStartTime": "2024-03-26T06:30:00Z",
  "plannedCompletionTime": "2024-03-26T07:00:00Z",
  "intakeDeadline": "2024-03-26T07:15:00Z",
  "isValid": true,
  "precursorInstanceKeys": ["Task_001", "Task_003"]
}
```

**ExecutionPlanOrchestratorGrain State**:
```json
{
  "executionPlanId": "ExecutionPlan_2024-03-26",
  "executionInstances": { /* 150 task instances */ },
  "currentRound": 3,
  "maxRounds": 5,
  "hasConverged": false,
  "taskChain": ["Task_001", "Task_002", ..., "Task_150"],
  "deadlineMisses": ["Task_042", "Task_055"]
}
```

### Data Persistence Behavior

- **Auto-persist on write**: Every grain state change automatically saved to SQLite
- **Lazy-load on activation**: Grains load their state from SQLite when activated
- **Concurrent access**: SQLite handles single-process concurrent reads/writes
- **Transactional**: Orleans uses transactions to ensure data consistency

### Backup Strategy

Automatic backup happens on application shutdown:
```csharp
siloHost.Stopped += async (sender) =>
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "data", "TaskSequencer.db");
    var backupPath = $"{dbPath}.backup.{DateTime.Now:yyyy-MM-dd-HH-mm-ss}";
    File.Copy(dbPath, backupPath);
    
    // Keep only last 10 backups
    var backups = Directory.GetFiles(Path.GetDirectoryName(dbPath), "TaskSequencer.db.backup.*")
        .OrderByDescending(f => f)
        .Skip(10);
    foreach (var backup in backups) File.Delete(backup);
};
```

---

## Data Binding Architecture

### Dashboard → Grain Calls (Live Updates)

**ViewModel property** → **XAML binding** → **Display**

```csharp
// ViewModel
[ObservableProperty]
private int validTaskCount;

[ObservableProperty]
private double convergenceProgress;

public async void OnConvergenceRoundCompleted()
{
    var orchestrator = _grainFactory.GetGrain<IExecutionPlanOrchestratorGrain>("plan-1");
    ValidTaskCount = await orchestrator.GetValidTaskCountAsync();
    ConvergenceProgress = await orchestrator.GetConvergenceProgressAsync();
    OnPropertyChanged(nameof(ValidTaskCount));  // Triggers UI update
    OnPropertyChanged(nameof(ConvergenceProgress));
}
```

**XAML**:
```xaml
<TextBlock Text="{x:Bind ViewModel.ValidTaskCount, Mode=OneWay}" />
<ProgressBar Value="{x:Bind ViewModel.ConvergenceProgress, Mode=OneWay}" />
```

---

## Performance Targets

| Operation | Target | Notes |
|-----------|--------|-------|
| App startup (silo init + SQLite) | < 3 seconds | Grain types pre-compiled, DB loaded |
| Load CSV + parse 150 tasks | < 500ms | CsvHelper is optimized |
| Initialize grains from CSV | < 1 second | 150 grains created in parallel |
| Persist grain state to SQLite | ~5-10ms per grain | Batched, async writes |
| ExecuteRound (all grains) | < 500ms | In-process, highly parallelized |
| Detect convergence (5 rounds) | < 3 seconds | Aggregation only |
| Export to Excel (150 tasks) | < 2 seconds | ClosedXML + memory buffer |
| Dashboard render | < 100ms | WinUI 3 virtualizing lists |
| Timeline Gantt render (150 tasks) | < 200ms | XAML virtualization |

---

## Security & Deployment

### Phase 2 (Local Development)

- **No authentication** required (single-user desktop app)
- **No network exposure** (all in-process)
- **localhost-only** grain communication
- **SQLite Database**: `<AppPath>\data\TaskSequencer.db` (auto-created)
- **File system access**: 
  - **CSV input**: `%USERPROFILE%\Documents\TaskSequencer\` (default input location)
  - **Excel output**: Configurable via File Save dialog (default: Desktop)
  - **Logs**: `%LOCALAPPDATA%\TaskSequencer\logs\` (Serilog)
  - **Backups**: `<AppPath>\data\TaskSequencer.db.backup.*` (weekly auto-backup)

### Phase 3+ (Enterprise Distribution)

- Code signing for Windows Defender SmartScreen
- MSIX packaging for Windows App Store (future: enterprise deployment)
- SQLite database encryption (AES-256 optional in settings)
- Excel export encryption option (password-protected worksheets)
- Audit logging (all plan calculations logged to `%LOCALAPPDATA%\TaskSequencer\logs\`)
- Multi-user read-only mode (shared database with locking)

---

## Testing Strategy

### Unit Tests (Grain Logic)

```csharp
// Tests for grain state transitions
[Fact]
public async Task RecalculateAsync_UpdatesCalculatedStartTime()
{
    // Arrange
    var grain = new ExecutionInstanceGrain();
    var instance = new ExecutionInstance { /* ... */ };
    
    // Act
    await grain.RecalculateAsync();
    var result = await grain.GetAsync();
    
    // Assert
    result.CalculatedStartTime.Should().BeAfter(DateTime.Now.AddHours(-1));
}
```

### UI Tests (WinUI 3 Integration)

```csharp
// XAML framework integration test
[Fact]
public async Task DashboardPage_RefreshButton_UpdatesStatistics()
{
    // Arrange
    var viewModel = new DashboardViewModel(mockGrainFactory);
    var page = new DashboardPage() { DataContext = viewModel };
    
    // Act
    await viewModel.RefreshPlanCommand.ExecuteAsync(null);
    
    // Assert
    page.FindName("ValidTasksText").Should().HaveText("143");
}
```

### End-to-End Tests

- Load sample CSV → Verify all grains initialize
- Click "Refresh" → Verify convergence rounds execute
- Export timeline → Verify PNG file created with correct chart
- Reload app → Verify initial state restored

---

## Success Criteria (Phase 2)

- ✅ App launches on Windows 11 in < 3 seconds (includes SQLite silo init)
- ✅ Dashboard displays live task statistics updated per convergence round
- ✅ Timeline renders 150+ tasks with smooth scrolling and async loading
- ✅ Convergence rounds complete in < 1 second each
- ✅ Groups drill-down shows deadline violations and at-risk tasks correctly
- ✅ SQLite database auto-created and persists grain state
- ✅ Excel export generates non-technical calendar visualization < 2 seconds
- ✅ Excel shows task dependencies, parallel execution, and at-risk indicators
- ✅ Multiple tasks visible in same time block on calendar
- ✅ No-dependency tasks clearly marked as standalone
- ✅ Export includes 5 tabs: Summary, Timeline Calendar, Task Details, Dependencies, Risk Analysis
- ✅ Orleans silo embedded with SQLite persistence (no external services)
- ✅ All 4 grain types functional with durable state (ExecutionInstance, Orchestrator, SequenceGroup, ReportGenerator)

---

## What Was NOT Chosen: Alternatives Rejected for Phase 2

The following were explicitly considered and rejected for Phase 2:

| Option | Why Rejected |
|--------|-------------|
| **ASP.NET Core Web API** | Not needed: No network calls required for single-machine desktop app |
| **React / Vue.js** | Not needed: WinUI 3 provides native Windows integration; web framework adds complexity |
| **SQL Server / EF Core** | Overkill: SQLite sufficient for local state persistence; no multi-user server deployment |
| **Aspire multi-service** | Simplified: Aspire still used for development orchestration, but all services embedded in single exe |
| **Remote OAuth2** | Unnecessary: Single-user desktop app (no authentication needed) |
| **OpenPyXL / pandas (Python)** | Wrong tech stack: C#/.NET required for Orleans integration; Python adds complexity |
| **Google Sheets / OneDrive sync** | Not applicable: Offline-first executive reporting, no cloud dependency desired |
| **PowerBI / Tableau** | Overkill: Custom WinUI 3 UI + ClosedXML Excel export sufficient for Phase 2 |
| **Aspose.Cells** | Too expensive: ClosedXML provides same Excel generation at no cost |
| **WPF instead of WinUI 3** | Outdated: WinUI 3 is modern Windows 11 standard, WPF not suitable for new apps |

**Rationale**: Phase 2 prioritizes **simplicity and performance** over extensibility. Single-process, in-memory architecture keeps code complexity low and eliminates network round-trips. Phase 3+ can add distributed services later if required for multi-machine scenarios.

---

**Ready for Phase 2 Implementation: AWAITING APPROVAL**
