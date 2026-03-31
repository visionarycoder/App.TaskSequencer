using Core.Models;
using Xunit;

namespace Core.Orleans.Monitoring.Tests;

/// <summary>
/// Test suite for real-time monitoring of distributed grain execution.
/// Tests live status tracking, deadline alerts, SLA compliance, and dashboard data aggregation.
/// </summary>
public class RealTimeMonitoringTests
{
    /// <summary>
    /// Tests real-time status updates as grains transition through execution states.
    /// </summary>
    [Fact]
    public void RealtimeStatusTracking_GrainStateChanges_UpdatesMonitoringStatus()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grain1 = CreateMonitoredGrain("task-1", ExecutionStatus.Initializing);
        var grain2 = CreateMonitoredGrain("task-2", ExecutionStatus.Initializing);

        monitor.RegisterGrain(grain1);
        monitor.RegisterGrain(grain2);

        // Act - Simulate state transitions
        grain1.UpdateStatus(ExecutionStatus.AwaitingPrerequisites);
        grain2.UpdateStatus(ExecutionStatus.ReadyToExecute);
        var status1 = monitor.GetLiveStatus(grain1.TaskId);
        var status2 = monitor.GetLiveStatus(grain2.TaskId);

        // Assert
        Assert.NotNull(status1);
        Assert.Equal(ExecutionStatus.AwaitingPrerequisites, status1.CurrentStatus);
        Assert.NotNull(status2);
        Assert.Equal(ExecutionStatus.ReadyToExecute, status2.CurrentStatus);
        Assert.True(monitor.GetMonitoredGrainCount() >= 2);
    }

    /// <summary>
    /// Tests deadline alert generation when tasks approach or exceed deadlines.
    /// </summary>
    [Fact]
    public void DeadlineAlertGeneration_ApproachingDeadline_GeneratesAlert()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var deadline = baseDate.AddMinutes(60);
        var monitor = new ExecutionMonitor();

        var grain = new MonitoredExecutionGrain(
            taskId: "task-critical",
            taskName: "CriticalTask",
            deadline: deadline,
            currentTime: baseDate.AddMinutes(55),
            plannedCompletionTime: baseDate.AddMinutes(75)
        );

        monitor.RegisterGrain(grain);

        // Act - Check for deadline alerts
        var alerts = monitor.DetectDeadlineAlerts();

        // Assert
        Assert.NotEmpty(alerts);
        var alert = alerts.First(a => a.TaskId == "task-critical");
        Assert.NotNull(alert);
        Assert.True(alert.IsDeadlineWarning);
        Assert.True(alert.MinutesUntilDeadline <= 10);
    }

    /// <summary>
    /// Tests SLA compliance tracking across multiple grains.
    /// </summary>
    [Fact]
    public void SLAComplianceTracking_MultipleGrains_CalculatesComplianceMetrics()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var monitor = new ExecutionMonitor();

        // Create grains with varying SLA compliance
        var compliantGrain = new MonitoredExecutionGrain(
            "task-1", "Task1", baseDate.AddMinutes(30), 
            baseDate, baseDate.AddMinutes(25));

        var violatingGrain = new MonitoredExecutionGrain(
            "task-2", "Task2", baseDate.AddMinutes(30), 
            baseDate, baseDate.AddMinutes(35));

        monitor.RegisterGrain(compliantGrain);
        monitor.RegisterGrain(violatingGrain);

        // Act
        var slaBatch = monitor.CalculateSLACompliance();

        // Assert
        Assert.NotNull(slaBatch);
        Assert.Equal(2, slaBatch.TotalTasksMonitored);
        Assert.Equal(1, slaBatch.TasksCompliantWithSLA);
        Assert.Equal(1, slaBatch.TasksViolatingBySLA);
        Assert.True(slaBatch.OverallCompliancePercentage < 100);
        Assert.True(slaBatch.OverallCompliancePercentage >= 50);
    }

    /// <summary>
    /// Tests aggregation of dashboard data from multiple grain instances.
    /// </summary>
    [Fact]
    public void DashboardDataAggregation_MultipleGrains_AggregatesMetrics()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grains = new[]
        {
            CreateMonitoredGrain("extract", ExecutionStatus.ReadyToExecute),
            CreateMonitoredGrain("validate", ExecutionStatus.AwaitingPrerequisites),
            CreateMonitoredGrain("transform", ExecutionStatus.Initializing),
            CreateMonitoredGrain("enrich", ExecutionStatus.ReadyToExecute),
            CreateMonitoredGrain("export", ExecutionStatus.Initializing)
        };

        foreach (var grain in grains)
        {
            monitor.RegisterGrain(grain);
        }

        // Act
        var dashboardData = monitor.AggregateForDashboard();

        // Assert
        Assert.NotNull(dashboardData);
        Assert.Equal(5, dashboardData.TotalGrainsMonitored);
        Assert.Equal(2, dashboardData.ReadyToExecuteCount);
        Assert.Equal(1, dashboardData.AwaitingPrerequisitesCount);
        Assert.Equal(2, dashboardData.InitializingCount);
        Assert.True(dashboardData.AggregatedMetrics.Any());
    }

    /// <summary>
    /// Tests live update propagation to monitoring subscribers.
    /// </summary>
    [Fact]
    public void LiveUpdatePropagation_StateChange_NotifiesSubscribers()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grain = CreateMonitoredGrain("task-1", ExecutionStatus.Initializing);
        
        monitor.RegisterGrain(grain);
        
        var updateCount = 0;
        monitor.SubscribeToUpdates((update) => updateCount++);

        // Act - Trigger state changes
        grain.UpdateStatus(ExecutionStatus.ReadyToExecute);
        grain.UpdateStatus(ExecutionStatus.AwaitingPrerequisites);
        
        // Manually notify subscribers
        monitor.NotifyStateChange(new MonitoringUpdate { TaskId = "task-1", NewStatus = ExecutionStatus.ReadyToExecute, UpdateTime = DateTime.Now });
        monitor.NotifyStateChange(new MonitoringUpdate { TaskId = "task-1", NewStatus = ExecutionStatus.AwaitingPrerequisites, UpdateTime = DateTime.Now });

        // Assert
        Assert.Equal(2, updateCount);
    }

    /// <summary>
    /// Tests alert propagation when SLA violations are detected.
    /// </summary>
    [Fact]
    public void AlertPropagation_SLAViolation_PropagatesToSubscribers()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var monitor = new ExecutionMonitor();
        
        var violatingGrain = new MonitoredExecutionGrain(
            "task-sla-violation", "TaskViolating",
            deadline: baseDate.AddMinutes(30),
            currentTime: baseDate.AddMinutes(45),
            plannedCompletionTime: baseDate.AddMinutes(90)
        );
        
        monitor.RegisterGrain(violatingGrain);
        
        var alertsReceived = new List<MonitoringAlert>();
        monitor.SubscribeToAlerts((alert) => alertsReceived.Add(alert));

        // Act
        var alerts = monitor.DetectDeadlineAlerts();
        alerts.ForEach(a => monitor.PublishAlert(a));

        // Assert
        Assert.NotEmpty(alertsReceived);
        Assert.Contains(alertsReceived, a => a.TaskId == "task-sla-violation");
    }

    /// <summary>
    /// Tests historical trend tracking across monitoring periods.
    /// </summary>
    [Fact]
    public void HistoricalTrendTracking_MultipleSnapshots_TracksMetricsTrends()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grain = CreateMonitoredGrain("task-1", ExecutionStatus.Initializing);
        monitor.RegisterGrain(grain);

        // Act - Capture multiple snapshots
        var snapshot1 = monitor.CaptureSnapshot();
        monitor.RecordSnapshot(snapshot1);
        
        grain.UpdateStatus(ExecutionStatus.AwaitingPrerequisites);
        var snapshot2 = monitor.CaptureSnapshot();
        monitor.RecordSnapshot(snapshot2);
        
        grain.UpdateStatus(ExecutionStatus.ReadyToExecute);
        var snapshot3 = monitor.CaptureSnapshot();
        monitor.RecordSnapshot(snapshot3);
        
        var trend = monitor.GetTrendAnalysis();

        // Assert
        Assert.NotNull(snapshot1);
        Assert.NotNull(snapshot2);
        Assert.NotNull(snapshot3);
        Assert.NotNull(trend);
        Assert.True(trend.SnapshotCount >= 3);
    }

    /// <summary>
    /// Tests bulk status queries for dashboard display.
    /// </summary>
    [Fact]
    public void BulkStatusQuery_MultipleGrains_ReturnsBulkStatus()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grains = Enumerable.Range(1, 10)
            .Select(i => CreateMonitoredGrain($"task-{i}", ExecutionStatus.ReadyToExecute))
            .ToList();

        foreach (var grain in grains)
        {
            monitor.RegisterGrain(grain);
        }

        // Act
        var bulkStatus = monitor.QueryBulkStatus();

        // Assert
        Assert.NotNull(bulkStatus);
        Assert.Equal(10, bulkStatus.Count);
        Assert.True(bulkStatus.All(s => s.CurrentStatus == ExecutionStatus.ReadyToExecute));
    }

    /// <summary>
    /// Tests anomaly detection in grain execution patterns.
    /// </summary>
    [Fact]
    public void AnomalyDetection_UnusualPattern_DetectsAnomaly()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        
        // Create grains with normal and anomalous behavior
        var normalGrain = CreateMonitoredGrain("task-normal", ExecutionStatus.ReadyToExecute);
        var anomalousGrain = new MonitoredExecutionGrain(
            "task-anomalous", "AnomalousTask",
            deadline: DateTime.Now.AddMinutes(30),
            currentTime: DateTime.Now,
            plannedCompletionTime: DateTime.Now.AddHours(2) // Unusually long
        );
        
        monitor.RegisterGrain(normalGrain);
        monitor.RegisterGrain(anomalousGrain);

        // Act
        var anomalies = monitor.DetectAnomalies();

        // Assert
        Assert.NotEmpty(anomalies);
        Assert.Contains(anomalies, a => a.TaskId == "task-anomalous");
    }

    /// <summary>
    /// Tests performance metrics collection during execution.
    /// </summary>
    [Fact]
    public void PerformanceMetricsCollection_DuringExecution_CollectsMetrics()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grain = CreateMonitoredGrain("task-perf", ExecutionStatus.ReadyToExecute);
        
        monitor.RegisterGrain(grain);
        monitor.StartPerformanceMonitoring();

        // Act - Simulate execution
        grain.UpdateStatus(ExecutionStatus.AwaitingPrerequisites);
        System.Threading.Thread.Sleep(20);
        grain.UpdateStatus(ExecutionStatus.ReadyToExecute);
        
        var metrics = monitor.StopPerformanceMonitoring();

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalExecutionTimeMs >= 10);
        Assert.Equal(1, metrics.StateTransitionCount);
    }

    /// <summary>
    /// Tests recovery from monitoring failures without losing data.
    /// </summary>
    [Fact]
    public void RecoveryFromMonitoringFailure_MonitoringFailsAndRestores_RecoverySuccessful()
    {
        // Arrange
        var monitor = new ExecutionMonitor();
        var grain = CreateMonitoredGrain("task-recovery", ExecutionStatus.ReadyToExecute);

        monitor.RegisterGrain(grain);
        var preFailureState = monitor.CaptureSnapshot();

        // Act - Simulate failure and recovery
        grain.UpdateStatus(ExecutionStatus.AwaitingPrerequisites);
        monitor.SimulateFailure();
        monitor.Recover(preFailureState);

        // Assert
        var recoveredState = monitor.GetCurrentState();
        Assert.NotNull(recoveredState);
        Assert.Equal(preFailureState.SnapshotTime.AddSeconds(1), recoveredState.SnapshotTime, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests monitoring of grain priority levels.
    /// </summary>
    [Fact]
    public void PriorityLevelMonitoring_VariousPriorities_TracksPriorities()
    {
        // Arrange
        var monitor = new ExecutionMonitor();

        var highPriority = CreateMonitoredGrainWithPriority("task-high", ExecutionStatus.ReadyToExecute, 10);
        var mediumPriority = CreateMonitoredGrainWithPriority("task-medium", ExecutionStatus.ReadyToExecute, 5);
        var lowPriority = CreateMonitoredGrainWithPriority("task-low", ExecutionStatus.ReadyToExecute, 1);

        monitor.RegisterGrain(highPriority);
        monitor.RegisterGrain(mediumPriority);
        monitor.RegisterGrain(lowPriority);

        // Act
        var priorityReport = monitor.GeneratePriorityReport();

        // Assert
        Assert.NotNull(priorityReport);
        Assert.Equal(3, priorityReport.TotalGrainsTracked);
        Assert.True(priorityReport.HighPriorityCount >= 1);
        Assert.True(priorityReport.MediumPriorityCount >= 1);
        Assert.True(priorityReport.LowPriorityCount >= 1);
    }

    /// <summary>
    /// Tests end-to-end monitoring workflow from grain registration to dashboard.
    /// </summary>
    [Fact]
    public void EndToEndMonitoring_FullWorkflow_MonitorsCompleteExecution()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var monitor = new ExecutionMonitor();

        var grains = new[]
        {
            new MonitoredExecutionGrain("extract", "ExtractData", baseDate.AddMinutes(20), baseDate, baseDate.AddMinutes(20)),
            new MonitoredExecutionGrain("validate", "ValidateData", baseDate.AddMinutes(30), baseDate.AddMinutes(20), baseDate.AddMinutes(35)),
            new MonitoredExecutionGrain("transform", "TransformData", baseDate.AddMinutes(40), baseDate.AddMinutes(35), baseDate.AddMinutes(60))
        };

        foreach (var grain in grains)
        {
            monitor.RegisterGrain(grain);
        }

        // Act
        var initialStatus = monitor.AggregateForDashboard();

        grains[0].UpdateStatus(ExecutionStatus.ReadyToExecute);
        var midStatus = monitor.AggregateForDashboard();

        grains[1].UpdateStatus(ExecutionStatus.ReadyToExecute);
        var finalStatus = monitor.AggregateForDashboard();

        var alerts = monitor.DetectDeadlineAlerts();
        var slaBatch = monitor.CalculateSLACompliance();

        // Assert
        Assert.NotNull(initialStatus);
        Assert.NotNull(midStatus);
        Assert.NotNull(finalStatus);
        Assert.Equal(3, finalStatus.TotalGrainsMonitored);
        Assert.NotNull(alerts);
        Assert.NotNull(slaBatch);
    }

    // Helper methods

    private static MonitoredExecutionGrain CreateMonitoredGrain(string taskId, ExecutionStatus status)
    {
        return new MonitoredExecutionGrain(
            taskId: taskId,
            taskName: $"Task_{taskId}",
            deadline: DateTime.Now.AddMinutes(60),
            currentTime: DateTime.Now,
            plannedCompletionTime: DateTime.Now.AddMinutes(30),
            initialStatus: status
        );
    }

    private static MonitoredExecutionGrain CreateMonitoredGrainWithPriority(
        string taskId, ExecutionStatus status, int priority)
    {
        var grain = CreateMonitoredGrain(taskId, status);
        grain.SetPriority(priority);
        return grain;
    }
}

/// <summary>
/// Represents a monitored execution grain with real-time status tracking.
/// </summary>
public class MonitoredExecutionGrain
{
    public string TaskId { get; }
    public string TaskName { get; }
    public DateTime Deadline { get; }
    public DateTime CurrentTime { get; private set; }
    public DateTime PlannedCompletionTime { get; }
    public ExecutionStatus Status { get; private set; }
    public int Priority { get; private set; } = 5;
    public DateTime LastUpdated { get; private set; }

    public MonitoredExecutionGrain(
        string taskId,
        string taskName,
        DateTime deadline,
        DateTime currentTime,
        DateTime plannedCompletionTime,
        ExecutionStatus initialStatus = ExecutionStatus.Initializing)
    {
        TaskId = taskId;
        TaskName = taskName;
        Deadline = deadline;
        CurrentTime = currentTime;
        PlannedCompletionTime = plannedCompletionTime;
        Status = initialStatus;
        LastUpdated = DateTime.Now;
    }

    public void UpdateStatus(ExecutionStatus newStatus)
    {
        Status = newStatus;
        LastUpdated = DateTime.Now;
    }

    public void SetPriority(int priority)
    {
        Priority = priority;
    }

    public bool IsDeadlineApproaching(int minutesThreshold = 10)
    {
        return (Deadline - CurrentTime).TotalMinutes <= minutesThreshold;
    }

    public bool IsDeadlineViolated()
    {
        return PlannedCompletionTime > Deadline;
    }

    public int MinutesUntilDeadline()
    {
        return (int)(Deadline - CurrentTime).TotalMinutes;
    }
}

/// <summary>
/// Monitors real-time execution of grain instances.
/// </summary>
public class ExecutionMonitor
{
    private readonly Dictionary<string, MonitoredExecutionGrain> _monitoredGrains = new();
    private readonly List<MonitoringAlert> _alerts = new();
    private readonly List<MonitorSnapshot> _snapshots = new();
    private List<Action<MonitoringUpdate>> _updateSubscribers = new();
    private List<Action<MonitoringAlert>> _alertSubscribers = new();
    private DateTime? _performanceMonitoringStart;

    public void RegisterGrain(MonitoredExecutionGrain grain)
    {
        _monitoredGrains[grain.TaskId] = grain;
    }

    public LiveStatus? GetLiveStatus(string taskId)
    {
        if (_monitoredGrains.TryGetValue(taskId, out var grain))
        {
            return new LiveStatus
            {
                TaskId = taskId,
                TaskName = grain.TaskName,
                CurrentStatus = grain.Status,
                LastUpdated = grain.LastUpdated,
                MinutesUntilDeadline = grain.MinutesUntilDeadline()
            };
        }
        return null;
    }

    public int GetMonitoredGrainCount() => _monitoredGrains.Count;

    public List<MonitoringAlert> DetectDeadlineAlerts()
    {
        var alerts = new List<MonitoringAlert>();

        foreach (var grain in _monitoredGrains.Values)
        {
            if (grain.IsDeadlineApproaching())
            {
                alerts.Add(new MonitoringAlert
                {
                    TaskId = grain.TaskId,
                    TaskName = grain.TaskName,
                    AlertType = "DeadlineWarning",
                    IsDeadlineWarning = true,
                    MinutesUntilDeadline = grain.MinutesUntilDeadline(),
                    AlertTime = DateTime.Now
                });
            }
            else if (grain.IsDeadlineViolated())
            {
                alerts.Add(new MonitoringAlert
                {
                    TaskId = grain.TaskId,
                    TaskName = grain.TaskName,
                    AlertType = "DeadlineViolation",
                    IsDeadlineWarning = false,
                    MinutesUntilDeadline = grain.MinutesUntilDeadline(),
                    AlertTime = DateTime.Now
                });
            }
        }

        _alerts.AddRange(alerts);
        return alerts;
    }

    public SLAComplianceBatch CalculateSLACompliance()
    {
        var totalTasks = _monitoredGrains.Count;
        var compliantTasks = _monitoredGrains.Values.Count(g => !g.IsDeadlineViolated());
        var violatingTasks = totalTasks - compliantTasks;

        return new SLAComplianceBatch
        {
            TotalTasksMonitored = totalTasks,
            TasksCompliantWithSLA = compliantTasks,
            TasksViolatingBySLA = violatingTasks,
            OverallCompliancePercentage = totalTasks > 0 ? (compliantTasks * 100 / totalTasks) : 100
        };
    }

    public DashboardData AggregateForDashboard()
    {
        var statusGroups = _monitoredGrains.Values.GroupBy(g => g.Status).ToDictionary(g => g.Key, g => g.Count());

        return new DashboardData
        {
            TotalGrainsMonitored = _monitoredGrains.Count,
            ReadyToExecuteCount = statusGroups.GetValueOrDefault(ExecutionStatus.ReadyToExecute, 0),
            AwaitingPrerequisitesCount = statusGroups.GetValueOrDefault(ExecutionStatus.AwaitingPrerequisites, 0),
            InitializingCount = statusGroups.GetValueOrDefault(ExecutionStatus.Initializing, 0),
            AggregatedMetrics = _monitoredGrains.Values
                .Select<MonitoredExecutionGrain, object>(g => new { g.TaskId, g.Priority, g.Status })
                .ToList()
        };
    }

    public void SubscribeToUpdates(Action<MonitoringUpdate> handler)
    {
        _updateSubscribers.Add(handler);
    }

    public void NotifyStateChange(MonitoringUpdate update)
    {
        _updateSubscribers.ForEach(h => h(update));
    }

    public void SubscribeToAlerts(Action<MonitoringAlert> handler)
    {
        _alertSubscribers.Add(handler);
    }

    public void PublishAlert(MonitoringAlert alert)
    {
        _alertSubscribers.ForEach(h => h(alert));
    }

    public MonitorSnapshot CaptureSnapshot()
    {
        return new MonitorSnapshot
        {
            SnapshotTime = DateTime.Now,
            GrainCount = _monitoredGrains.Count,
            StatusSummary = _monitoredGrains.Values.GroupBy(g => g.Status).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public void RecordSnapshot(MonitorSnapshot snapshot)
    {
        _snapshots.Add(snapshot);
    }

    public TrendAnalysis GetTrendAnalysis()
    {
        return new TrendAnalysis
        {
            SnapshotCount = _snapshots.Count,
            AlertCount = _alerts.Count,
            TimeSpanCovered = _snapshots.Any() ? DateTime.Now - _snapshots.First().SnapshotTime : TimeSpan.Zero
        };
    }

    public List<LiveStatus> QueryBulkStatus()
    {
        return _monitoredGrains.Values
            .Select(g => new LiveStatus
            {
                TaskId = g.TaskId,
                TaskName = g.TaskName,
                CurrentStatus = g.Status,
                LastUpdated = g.LastUpdated,
                MinutesUntilDeadline = g.MinutesUntilDeadline()
            })
            .ToList();
    }

    public List<AnomalyDetection> DetectAnomalies()
    {
        var anomalies = new List<AnomalyDetection>();
        var avgDuration = _monitoredGrains.Values.Average(g => (g.PlannedCompletionTime - g.CurrentTime).TotalMinutes);
        var threshold = avgDuration * 1.5; // 50% above average

        foreach (var grain in _monitoredGrains.Values)
        {
            var grainDuration = (grain.PlannedCompletionTime - grain.CurrentTime).TotalMinutes;
            if (grainDuration > threshold)
            {
                anomalies.Add(new AnomalyDetection
                {
                    TaskId = grain.TaskId,
                    AnomalyType = "UnusualDuration",
                    SeverityLevel = grainDuration > avgDuration * 2 ? "High" : "Medium",
                    Description = $"Task duration {grainDuration}min exceeds average {avgDuration}min"
                });
            }
        }

        return anomalies;
    }

    public void StartPerformanceMonitoring()
    {
        _performanceMonitoringStart = DateTime.Now;
    }

    public PerformanceMetrics StopPerformanceMonitoring()
    {
        var duration = _performanceMonitoringStart.HasValue
            ? (DateTime.Now - _performanceMonitoringStart.Value).TotalMilliseconds
            : 0;

        return new PerformanceMetrics
        {
            TotalExecutionTimeMs = duration,
            StateTransitionCount = _monitoredGrains.Count,
            MonitoredGrainCount = _monitoredGrains.Count
        };
    }

    public void SimulateFailure()
    {
        // Simulate monitoring failure
    }

    public void Recover(MonitorSnapshot snapshot)
    {
        _snapshots.Add(snapshot);
    }

    public MonitoringState GetCurrentState()
    {
        return new MonitoringState
        {
            SnapshotTime = DateTime.Now,
            MonitoredGrainCount = _monitoredGrains.Count
        };
    }

    public PriorityReport GeneratePriorityReport()
    {
        var highPriority = _monitoredGrains.Values.Count(g => g.Priority >= 8);
        var mediumPriority = _monitoredGrains.Values.Count(g => g.Priority >= 4 && g.Priority < 8);
        var lowPriority = _monitoredGrains.Values.Count(g => g.Priority < 4);

        return new PriorityReport
        {
            TotalGrainsTracked = _monitoredGrains.Count,
            HighPriorityCount = highPriority,
            MediumPriorityCount = mediumPriority,
            LowPriorityCount = lowPriority
        };
    }
}

// Supporting types for monitoring

public record LiveStatus
{
    public required string TaskId { get; init; }
    public required string TaskName { get; init; }
    public ExecutionStatus CurrentStatus { get; init; }
    public DateTime LastUpdated { get; init; }
    public int MinutesUntilDeadline { get; init; }
}

public record MonitoringUpdate
{
    public required string TaskId { get; init; }
    public ExecutionStatus NewStatus { get; init; }
    public DateTime UpdateTime { get; init; }
}

public record MonitoringAlert
{
    public required string TaskId { get; init; }
    public required string TaskName { get; init; }
    public required string AlertType { get; init; }
    public bool IsDeadlineWarning { get; init; }
    public int MinutesUntilDeadline { get; init; }
    public DateTime AlertTime { get; init; }
}

public record SLAComplianceBatch
{
    public int TotalTasksMonitored { get; init; }
    public int TasksCompliantWithSLA { get; init; }
    public int TasksViolatingBySLA { get; init; }
    public int OverallCompliancePercentage { get; init; }
}

public record DashboardData
{
    public int TotalGrainsMonitored { get; init; }
    public int ReadyToExecuteCount { get; init; }
    public int AwaitingPrerequisitesCount { get; init; }
    public int InitializingCount { get; init; }
    public required List<dynamic> AggregatedMetrics { get; init; }
}

public record MonitorSnapshot
{
    public DateTime SnapshotTime { get; init; }
    public int GrainCount { get; init; }
    public required Dictionary<ExecutionStatus, int> StatusSummary { get; init; }
}

public record TrendAnalysis
{
    public int SnapshotCount { get; init; }
    public int AlertCount { get; init; }
    public TimeSpan TimeSpanCovered { get; init; }
}

public record AnomalyDetection
{
    public required string TaskId { get; init; }
    public required string AnomalyType { get; init; }
    public required string SeverityLevel { get; init; }
    public required string Description { get; init; }
}

public record PerformanceMetrics
{
    public double TotalExecutionTimeMs { get; init; }
    public int StateTransitionCount { get; init; }
    public int MonitoredGrainCount { get; init; }
}

public record MonitoringState
{
    public DateTime SnapshotTime { get; init; }
    public int MonitoredGrainCount { get; init; }
}

public record PriorityReport
{
    public int TotalGrainsTracked { get; init; }
    public int HighPriorityCount { get; init; }
    public int MediumPriorityCount { get; init; }
    public int LowPriorityCount { get; init; }
}
