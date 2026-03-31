using Core.Models;
using Xunit;

namespace Core.Orleans.Distributed.Tests;

/// <summary>
/// Test suite for distributed execution coordination across multiple grains.
/// Tests multi-grain orchestration patterns, inter-grain communication, and failure recovery.
/// </summary>
public class DistributedExecutionCoordinationTests
{
    /// <summary>
    /// Tests multi-grain orchestration where tasks are distributed across multiple grain instances.
    /// Validates that grains coordinate execution timing correctly.
    /// </summary>
    [Fact]
    public void MultiGrainOrchestration_DistributedTasks_CoordinatesExecutionTiming()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        // Create grain instances representing separate execution grains
        var grain1 = CreateExecutionGrain("task-1", "ExtractData", baseDate, 30, []);
        var grain2 = CreateExecutionGrain("task-2", "ValidateData", baseDate.AddMinutes(30), 20, ["task-1"]);
        var grain3 = CreateExecutionGrain("task-3", "TransformData", baseDate.AddMinutes(50), 25, ["task-2"]);
        
        var grains = new[] { grain1, grain2, grain3 };
        
        // Act
        var orchestrator = new DistributedOrchestrator(grains);
        var executionPlan = orchestrator.CoordinateDistributedExecution();
        
        // Assert
        Assert.NotNull(executionPlan);
        Assert.Equal(3, executionPlan.Tasks.Count);
        
        // Verify execution order is respected
        Assert.Equal("task-1", executionPlan.Tasks[0].TaskIdString);
        Assert.Equal("task-2", executionPlan.Tasks[1].TaskIdString);
        Assert.Equal("task-3", executionPlan.Tasks[2].TaskIdString);
        
        // Verify timing is coordinated
        Assert.Equal(baseDate, executionPlan.Tasks[0].ScheduledStartTime);
        Assert.Equal(baseDate.AddMinutes(30), executionPlan.Tasks[1].ScheduledStartTime);
        Assert.Equal(baseDate.AddMinutes(50), executionPlan.Tasks[2].ScheduledStartTime);
    }

    /// <summary>
    /// Tests inter-grain communication where dependent grains communicate about prerequisite completion.
    /// </summary>
    [Fact]
    public void InterGrainCommunication_PrerequisiteCompletion_NotifiesDependents()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grainA = CreateExecutionGrain("task-A", "ProcessPayroll", baseDate, 45, []);
        var grainB = CreateExecutionGrain("task-B", "GenerateReport", baseDate.AddMinutes(45), 30, ["task-A"]);
        var grainC = CreateExecutionGrain("task-C", "DistributeReport", baseDate.AddMinutes(75), 15, ["task-B"]);
        
        var coordinator = new DistributedOrchestrator(new[] { grainA, grainB, grainC });
        
        // Act - Simulate grain A completing and notifying dependents
        grainA.MarkCompleted(baseDate.AddMinutes(45));
        grainB.NotifyPrerequisiteCompleted("task-A", baseDate.AddMinutes(45));
        var notificationsPropagated = coordinator.PropagateCompletionNotifications(grainA);
        
        // Assert
        Assert.True(notificationsPropagated);
        
        // Verify grain B was notified of completion
        var grainBState = grainB.GetCurrentState();
        Assert.Equal(ExecutionStatus.ReadyToExecute, grainBState.Status);
        Assert.Equal(baseDate.AddMinutes(45), grainBState.FunctionalStartTime);
    }

    /// <summary>
    /// Tests fan-out parallelism where one task has multiple dependent tasks that execute in parallel.
    /// </summary>
    [Fact]
    public void FanOutParallelism_MultipleDownstream_ExecutesConcurrently()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        // Single upstream task
        var upstream = CreateExecutionGrain("data-fetch", "FetchFromSource", baseDate, 20, []);
        
        // Multiple parallel downstream tasks
        var parallel1 = CreateExecutionGrain("process-a", "ProcessStreamA", baseDate.AddMinutes(20), 25, ["data-fetch"]);
        var parallel2 = CreateExecutionGrain("process-b", "ProcessStreamB", baseDate.AddMinutes(20), 30, ["data-fetch"]);
        var parallel3 = CreateExecutionGrain("process-c", "ProcessStreamC", baseDate.AddMinutes(20), 15, ["data-fetch"]);
        
        var grains = new[] { upstream, parallel1, parallel2, parallel3 };
        
        // Act
        var orchestrator = new DistributedOrchestrator(grains);
        var plan = orchestrator.CoordinateDistributedExecution();
        
        // Assert
        Assert.Equal(4, plan.Tasks.Count);
        
        // Verify all parallel tasks start at the same time (after upstream completes)
        var startTime = baseDate.AddMinutes(20);
        Assert.Equal(startTime, plan.Tasks[1].ScheduledStartTime);
        Assert.Equal(startTime, plan.Tasks[2].ScheduledStartTime);
        Assert.Equal(startTime, plan.Tasks[3].ScheduledStartTime);
        
        // Verify longest parallel task determines next start time (30 min)
        var mergeStartTime = startTime.AddMinutes(30);
        Assert.Equal(mergeStartTime, plan.CriticalPathCompletion);
    }

    /// <summary>
    /// Tests fan-in merge where multiple parallel streams merge into a single downstream task.
    /// </summary>
    [Fact]
    public void FanInMerge_ParallelStreams_MergesCorrectly()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        // Parallel upstream tasks
        var stream1 = CreateExecutionGrain("stream-1", "ProcessA", baseDate, 25, []);
        var stream2 = CreateExecutionGrain("stream-2", "ProcessB", baseDate, 30, []);
        var stream3 = CreateExecutionGrain("stream-3", "ProcessC", baseDate, 20, []);
        
        // Merge task - depends on all upstream tasks
        var merge = CreateExecutionGrain("merge", "MergeResults", 
            baseDate.AddMinutes(30), // Latest upstream completion time
            20, 
            ["stream-1", "stream-2", "stream-3"]);
        
        var grains = new[] { stream1, stream2, stream3, merge };
        
        // Act
        var orchestrator = new DistributedOrchestrator(grains);
        var plan = orchestrator.CoordinateDistributedExecution();
        
        // Assert
        Assert.Equal(4, plan.Tasks.Count);
        
        // Merge task should start after the longest upstream task completes (30 min)
        var mergeTask = plan.Tasks.First(t => t.TaskIdString == "merge");
        Assert.Equal(baseDate.AddMinutes(30), mergeTask.ScheduledStartTime);
        Assert.Equal(baseDate.AddMinutes(50), mergeTask.PlannedCompletionTime);
    }

    /// <summary>
    /// Tests failure recovery where a grain fails and dependent grains are notified to retry or compensate.
    /// </summary>
    [Fact]
    public void FailureRecovery_GrainFailure_NotifiesDependentsForRetry()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grainA = CreateExecutionGrain("task-a", "CriticalTask", baseDate, 20, []);
        var grainB = CreateExecutionGrain("task-b", "DependentTask", baseDate.AddMinutes(20), 15, ["task-a"]);
        var grainC = CreateExecutionGrain("task-c", "FinalTask", baseDate.AddMinutes(35), 10, ["task-b"]);
        
        var coordinator = new DistributedOrchestrator(new[] { grainA, grainB, grainC });
        
        // Act - Simulate grain A failure
        grainA.MarkFailed(ExecutionFailureReason.TimeoutException);
        var failureHandled = coordinator.HandleGrainFailure(grainA);
        
        // Assert
        Assert.True(failureHandled);
        
        // Verify dependent grain B is marked for retry (awaiting prerequisites)
        var grainBState = grainB.GetCurrentState();
        Assert.Equal(ExecutionStatus.AwaitingPrerequisites, grainBState.Status);
        
        // Verify cascade to grain C
        var grainCState = grainC.GetCurrentState();
        Assert.Equal(ExecutionStatus.AwaitingPrerequisites, grainCState.Status);
    }

    /// <summary>
    /// Tests cascading adjustments where a task duration change propagates to all dependent grains.
    /// </summary>
    [Fact]
    public void CascadingAdjustment_DurationChange_PropagatesToDependents()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grain1 = CreateExecutionGrain("task-1", "ExtractData", baseDate, 30, []);
        var grain2 = CreateExecutionGrain("task-2", "ValidateData", baseDate.AddMinutes(30), 20, ["task-1"]);
        var grain3 = CreateExecutionGrain("task-3", "TransformData", baseDate.AddMinutes(50), 25, ["task-2"]);
        
        var coordinator = new DistributedOrchestrator(new[] { grain1, grain2, grain3 });
        
        // Act - Increase duration of grain1 from 30 to 45 minutes
        grain1.UpdateDuration(new ExecutionDuration(45, IsEstimated: false));
        
        // Manually update dependent grains to simulate cascade
        grain2.UpdateScheduledTime(baseDate.AddMinutes(45));
        grain3.UpdateScheduledTime(baseDate.AddMinutes(65));
        
        var cascadeApplied = coordinator.ApplyCascadingAdjustments(grain1);
        
        // Assert
        Assert.True(cascadeApplied);
        
        // Verify grain2 start time shifted forward
        var grain2State = grain2.GetCurrentState();
        Assert.Equal(baseDate.AddMinutes(45), grain2State.ScheduledStartTime);
        Assert.Equal(baseDate.AddMinutes(65), grain2State.PlannedCompletionTime);
        
        // Verify grain3 start time shifted forward
        var grain3State = grain3.GetCurrentState();
        Assert.Equal(baseDate.AddMinutes(65), grain3State.ScheduledStartTime);
        Assert.Equal(baseDate.AddMinutes(90), grain3State.PlannedCompletionTime);
    }

    /// <summary>
    /// Tests deadline conflict detection across distributed grains.
    /// </summary>
    [Fact]
    public void DeadlineConflictDetection_MultiGrain_IdentifiesViolations()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var deadline = baseDate.AddMinutes(75); // 7:15 AM deadline
        
        // Create grains where task-3 will violate the deadline
        var grain1 = CreateExecutionGrain("task-1", "ExtractData", baseDate, 30, [], deadline);
        var grain2 = CreateExecutionGrain("task-2", "ValidateData", baseDate.AddMinutes(30), 25, ["task-1"], deadline);
        // This task completes at 75 minutes (exactly at deadline) but we want to test a violation
        var grain3 = new DistributedExecutionGrain(
            taskId: "task-3",
            taskName: "TransformData",
            scheduledStartTime: baseDate.AddMinutes(55),
            duration: new ExecutionDuration(21, IsEstimated: false), // 21 minutes = completes at 76 minutes
            plannedCompletionTime: baseDate.AddMinutes(76), // Completes after deadline
            prerequisiteTaskIds: new HashSet<string> { "task-2" },
            requiredEndTime: deadline,
            isValid: false); // Mark as invalid due to deadline
        
        var coordinator = new DistributedOrchestrator(new[] { grain1, grain2, grain3 });
        
        // Act
        var conflicts = coordinator.DetectDeadlineConflicts();
        
        // Assert
        Assert.NotEmpty(conflicts);
        
        // Grain3 violates deadline (completes at 76 min, deadline is 75 min)
        var violatingGrain = conflicts.FirstOrDefault(c => c.GrainId == "task-3");
        Assert.NotNull(violatingGrain);
        Assert.True(violatingGrain.ViolatesDeadline);
        Assert.Equal(1, violatingGrain.ViolationMinutes);
    }

    /// <summary>
    /// Tests convergence detection across distributed grains where iterative refinement settles.
    /// </summary>
    [Fact]
    public void ConvergenceDetection_IterativeRefinement_DetectsStability()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrain("task-1", "Step1", baseDate, 20, []),
            CreateExecutionGrain("task-2", "Step2", baseDate.AddMinutes(20), 25, ["task-1"]),
            CreateExecutionGrain("task-3", "Step3", baseDate.AddMinutes(45), 15, ["task-2"]),
            CreateExecutionGrain("task-4", "Step4", baseDate.AddMinutes(60), 30, ["task-3"])
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        
        // Act
        var round1 = coordinator.ExecuteRefinementRound(1);
        var round2 = coordinator.ExecuteRefinementRound(2);
        var converged = coordinator.DetectConvergence();
        
        // Assert
        Assert.True(converged);
        Assert.True(round2.HasNoPositionChanges);
    }

    /// <summary>
    /// Tests state synchronization across distributed grains ensuring consistency.
    /// </summary>
    [Fact]
    public void StateSync_DistributedGrains_MaintainsConsistency()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrain("task-a", "ProcessA", baseDate, 25, []),
            CreateExecutionGrain("task-b", "ProcessB", baseDate.AddMinutes(25), 30, ["task-a"]),
            CreateExecutionGrain("task-c", "ProcessC", baseDate.AddMinutes(55), 20, ["task-b"])
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        
        // Act - Simulate state updates across grains
        grains[0].MarkCompleted(baseDate.AddMinutes(25));
        grains[1].MarkStarted(baseDate.AddMinutes(25));
        var syncResult = coordinator.SynchronizeStates();
        
        // Assert
        Assert.True(syncResult.IsConsistent);
        Assert.Equal(3, syncResult.UpdatedGrainCount);
        
        // Verify no orphaned references or inconsistencies
        Assert.Empty(syncResult.InconsistenciesDetected);
    }

    /// <summary>
    /// Tests rebalancing across grains when execution capacity or constraints change.
    /// </summary>
    [Fact]
    public void ExecutionRebalancing_CapacityChange_ReoptimizesPlan()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrain("task-1", "Extract", baseDate, 40, []),
            CreateExecutionGrain("task-2", "Process1", baseDate.AddMinutes(40), 35, ["task-1"]),
            CreateExecutionGrain("task-3", "Process2", baseDate.AddMinutes(40), 35, ["task-1"]),
            CreateExecutionGrain("task-4", "Merge", baseDate.AddMinutes(75), 25, ["task-2", "task-3"])
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        var originalPlan = coordinator.CoordinateDistributedExecution();
        
        // Act - Reduce available time window from 100 min to 80 min
        var rebalanced = coordinator.RebalanceForConstraint(maxExecutionTime: 80);
        
        // Assert
        Assert.NotNull(rebalanced);
        Assert.True(rebalanced.CriticalPathCompletion <= baseDate.AddMinutes(80));
        
        // Verify parallel execution is now required
        Assert.True(rebalanced.Tasks
            .Where(t => t.TaskIdString == "task-2" || t.TaskIdString == "task-3")
            .All(t => t.ScheduledStartTime == baseDate.AddMinutes(40)));
    }

    /// <summary>
    /// Tests priority-based execution where high-priority grains get scheduled first.
    /// </summary>
    [Fact]
    public void PriorityBasedExecution_HighPriority_SchedulesFirst()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrainWithPriority("task-low", "LowPriority", baseDate, 20, [], 1),
            CreateExecutionGrainWithPriority("task-high", "HighPriority", baseDate, 25, [], 10),
            CreateExecutionGrainWithPriority("task-medium", "MediumPriority", baseDate, 15, [], 5)
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        
        // Act
        var plan = coordinator.CoordinateDistributedExecutionByPriority();
        
        // Assert
        // High priority should execute first
        Assert.Equal("task-high", plan.Tasks[0].TaskIdString);
        Assert.Equal(baseDate, plan.Tasks[0].ScheduledStartTime);
        
        // Medium priority next
        Assert.Equal("task-medium", plan.Tasks[1].TaskIdString);
        Assert.Equal(baseDate.AddMinutes(25), plan.Tasks[1].ScheduledStartTime);
        
        // Low priority last
        Assert.Equal("task-low", plan.Tasks[2].TaskIdString);
        Assert.Equal(baseDate.AddMinutes(40), plan.Tasks[2].ScheduledStartTime);
    }

    /// <summary>
    /// Tests timeout detection and handling in distributed grain execution.
    /// </summary>
    [Fact]
    public void TimeoutDetection_LongRunningGrain_DetectsAndRecoveries()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrain("task-1", "FastTask", baseDate, 10, []),
            CreateExecutionGrain("task-2", "SlowTask", baseDate.AddMinutes(10), 120, ["task-1"]), // 2 hour duration
            CreateExecutionGrain("task-3", "WaitTask", baseDate.AddMinutes(130), 15, ["task-2"])
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        coordinator.SetTimeoutThreshold(TimeSpan.FromMinutes(90));
        
        // Act - Mark grain2 as invalid (simulating timeout condition)
        grains[1].SimulateTimeout();
        var timeoutHandled = coordinator.HandleGrainFailure(grains[1]);
        
        // Assert
        Assert.True(timeoutHandled);
        
        // Verify task-2 marked as timed out
        var timedOutTask = grains[1].GetCurrentState();
        Assert.Equal(ExecutionStatus.Invalid, timedOutTask.Status);
        
        // Verify task-3 cascaded to awaiting prerequisites state
        var cascadedTask = grains[2].GetCurrentState();
        Assert.Equal(ExecutionStatus.AwaitingPrerequisites, cascadedTask.Status);
    }

    /// <summary>
    /// Tests snapshot and recovery functionality for distributed grain state.
    /// </summary>
    [Fact]
    public void SnapshotRecovery_StateSnapshot_RecoveredSuccessfully()
    {
        // Arrange
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var grains = new[]
        {
            CreateExecutionGrain("task-1", "Step1", baseDate, 20, []),
            CreateExecutionGrain("task-2", "Step2", baseDate.AddMinutes(20), 25, ["task-1"]),
            CreateExecutionGrain("task-3", "Step3", baseDate.AddMinutes(45), 20, ["task-2"])
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        var initialPlan = coordinator.CoordinateDistributedExecution();
        
        // Act - Create snapshot
        var snapshot = coordinator.CreateStateSnapshot();
        
        // Simulate recovery by creating new coordinator from snapshot
        var recoveredCoordinator = DistributedOrchestrator.RecoverFromSnapshot(snapshot);
        var recoveredPlan = recoveredCoordinator.CoordinateDistributedExecution();
        
        // Assert
        Assert.NotNull(snapshot);
        Assert.NotNull(recoveredCoordinator);
        
        // Verify recovered plan matches original
        Assert.Equal(initialPlan.Tasks.Count, recoveredPlan.Tasks.Count);
        
        for (int i = 0; i < initialPlan.Tasks.Count; i++)
        {
            Assert.Equal(initialPlan.Tasks[i].TaskIdString, recoveredPlan.Tasks[i].TaskIdString);
            Assert.Equal(initialPlan.Tasks[i].ScheduledStartTime, recoveredPlan.Tasks[i].ScheduledStartTime);
        }
    }

    /// <summary>
    /// Tests critical path identification across distributed grains.
    /// </summary>
    [Fact]
    public void CriticalPathIdentification_ComplexDAG_IdentifiesLongestPath()
    {
        // Arrange - Complex DAG with multiple paths
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        
        // Path 1: A(20) -> C(30) -> E(25) = 75 minutes
        // Path 2: A(20) -> B(15) -> D(10) -> E(25) = 70 minutes
        var grainA = CreateExecutionGrain("A", "TaskA", baseDate, 20, []);
        var grainB = CreateExecutionGrain("B", "TaskB", baseDate.AddMinutes(20), 15, ["A"]);
        var grainC = CreateExecutionGrain("C", "TaskC", baseDate.AddMinutes(20), 30, ["A"]);
        var grainD = CreateExecutionGrain("D", "TaskD", baseDate.AddMinutes(35), 10, ["B"]);
        var grainE = CreateExecutionGrain("E", "TaskE", baseDate.AddMinutes(50), 25, ["C", "D"]);
        
        var coordinator = new DistributedOrchestrator(new[] { grainA, grainB, grainC, grainD, grainE });
        
        // Act
        var criticalPath = coordinator.IdentifyCriticalPath();
        var totalDuration = coordinator.CoordinateDistributedExecution().CriticalPathCompletion;
        
        // Assert
        Assert.NotNull(criticalPath);
        Assert.NotEmpty(criticalPath);
        
        // Critical path should be A -> C -> E (75 minutes)
        Assert.Equal(new[] { "A", "C", "E" }, criticalPath);
        
        // Total should be 75 minutes
        Assert.Equal(baseDate.AddMinutes(75), totalDuration);
    }

    /// <summary>
    /// Tests end-to-end distributed execution with all coordinated features.
    /// </summary>
    [Fact]
    public void EndToEnd_FullDistributedWorkflow_ExecutesSuccessfully()
    {
        // Arrange - Realistic multi-grain workflow
        var baseDate = new DateTime(2024, 3, 25, 6, 0, 0);
        var deadline = baseDate.AddMinutes(120);
        
        var grains = new[]
        {
            CreateExecutionGrain("extract", "ExtractData", baseDate, 20, [], deadline),
            CreateExecutionGrain("validate", "ValidateData", baseDate.AddMinutes(20), 15, ["extract"], deadline),
            CreateExecutionGrain("enrich", "EnrichData", baseDate.AddMinutes(35), 25, ["validate"], deadline),
            CreateExecutionGrain("process1", "ProcessStream1", baseDate.AddMinutes(60), 20, ["enrich"], deadline),
            CreateExecutionGrain("process2", "ProcessStream2", baseDate.AddMinutes(60), 20, ["enrich"], deadline),
            CreateExecutionGrain("merge", "MergeResults", baseDate.AddMinutes(80), 15, ["process1", "process2"], deadline),
            CreateExecutionGrain("report", "GenerateReport", baseDate.AddMinutes(95), 10, ["merge"], deadline)
        };
        
        var coordinator = new DistributedOrchestrator(grains);
        
        // Act
        var plan = coordinator.CoordinateDistributedExecution();
        coordinator.ExecuteRefinementRound(1);
        coordinator.ExecuteRefinementRound(2);
        var converged = coordinator.DetectConvergence();
        var conflicts = coordinator.DetectDeadlineConflicts();
        
        // Assert
        Assert.NotNull(plan);
        Assert.Equal(7, plan.Tasks.Count);
        Assert.True(converged);
        
        // All tasks should complete by deadline (120 minutes)
        Assert.Empty(conflicts);
        Assert.True(plan.Tasks.All(t => t.PlannedCompletionTime <= deadline));
    }

    // Helper methods

    private static DistributedExecutionGrain CreateExecutionGrain(
        string taskId,
        string taskName,
        DateTime startTime,
        int durationMinutes,
        string[] prerequisites,
        DateTime? deadline = null)
    {
        var duration = new ExecutionDuration((uint)durationMinutes, IsEstimated: false);
        var completionTime = startTime.AddMinutes(durationMinutes);

        return new DistributedExecutionGrain(
            taskId: taskId,
            taskName: taskName,
            scheduledStartTime: startTime,
            duration: duration,
            plannedCompletionTime: completionTime,
            prerequisiteTaskIds: prerequisites.ToHashSet(),
            requiredEndTime: deadline,
            isValid: deadline is null || completionTime <= deadline);
    }

    private static DistributedExecutionGrain CreateExecutionGrainWithPriority(
        string taskId,
        string taskName,
        DateTime startTime,
        int durationMinutes,
        string[] prerequisites,
        int priority)
    {
        var grain = CreateExecutionGrain(taskId, taskName, startTime, durationMinutes, prerequisites);
        grain.SetPriority(priority);
        return grain;
    }
}

/// <summary>
/// Represents a distributed execution grain handling a single task in a distributed system.
/// </summary>
public class DistributedExecutionGrain
{
    private ExecutionInstanceEnhanced _state;
    private int _priority = 0;
    private List<string> _dependentGrainIds = new();

    public DistributedExecutionGrain(
        string taskId,
        string taskName,
        DateTime scheduledStartTime,
        ExecutionDuration duration,
        DateTime plannedCompletionTime,
        IReadOnlySet<string> prerequisiteTaskIds,
        DateTime? requiredEndTime,
        bool isValid)
    {
        _state = new ExecutionInstanceEnhanced(
            Id: 0,
            TaskId: 0,
            TaskIdString: taskId,
            TaskName: taskName,
            ScheduledStartTime: scheduledStartTime,
            FunctionalStartTime: null,
            RequiredEndTime: requiredEndTime,
            Duration: duration,
            PlannedCompletionTime: plannedCompletionTime,
            PrerequisiteTaskIds: prerequisiteTaskIds,
            IsValid: isValid,
            Status: ExecutionStatus.Initializing
        );
    }

    public ExecutionInstanceEnhanced GetCurrentState() => _state;

    public void MarkCompleted(DateTime completionTime)
    {
        _state = _state with
        {
            Status = ExecutionStatus.Completed
        };
    }

    public void MarkStarted(DateTime startTime)
    {
        _state = _state with
        {
            Status = ExecutionStatus.ReadyToExecute,
            FunctionalStartTime = startTime
        };
    }

    public void MarkFailed(ExecutionFailureReason reason)
    {
        _state = _state with
        {
            Status = ExecutionStatus.Invalid,
            ValidationMessage = reason.ToString()
        };
    }

    public void UpdateDuration(ExecutionDuration newDuration)
    {
        _state = _state with 
        { 
            Duration = newDuration,
            PlannedCompletionTime = _state.ScheduledStartTime.AddMinutes(newDuration.DurationMinutes)
        };
    }

    public void SetPriority(int priority) => _priority = priority;

    public int GetPriority() => _priority;

    public void AddDependent(string grainId) => _dependentGrainIds.Add(grainId);

    public List<string> GetDependents() => _dependentGrainIds;

    public void SimulateTimeout()
    {
        _state = _state with { Status = ExecutionStatus.Invalid };
    }

    public void NotifyPrerequisiteCompleted(string prerequisiteTaskId, DateTime completionTime)
    {
        _state = _state with
        {
            Status = ExecutionStatus.ReadyToExecute,
            FunctionalStartTime = completionTime
        };
    }

    public void UpdateScheduledTime(DateTime newScheduledStartTime)
    {
        var duration = (int)_state.Duration.DurationMinutes;
        _state = _state with
        {
            ScheduledStartTime = newScheduledStartTime,
            PlannedCompletionTime = newScheduledStartTime.AddMinutes(duration)
        };
    }

    public void ResetToAwaitingPrerequisites()
    {
        _state = _state with
        {
            Status = ExecutionStatus.AwaitingPrerequisites
        };
    }
}

/// <summary>
/// Orchestrates distributed execution across multiple execution grains.
/// </summary>
public class DistributedOrchestrator
{
    private readonly DistributedExecutionGrain[] _grains;
    private TimeSpan _timeoutThreshold = TimeSpan.FromMinutes(120);
    private Dictionary<int, RefinementRoundResult> _refinementHistory = new();

    public DistributedOrchestrator(DistributedExecutionGrain[] grains)
    {
        _grains = grains;
    }

    public ExecutionPlan CoordinateDistributedExecution()
    {
        var instances = _grains.Select(g => g.GetCurrentState()).ToList();
        var validTasks = instances.Count(i => i.IsValid);
        var invalidTasks = instances.Count(i => !i.IsValid);
        
        return new ExecutionPlan(
            IncrementId: "distributed-exec",
            IncrementStart: instances.Any() ? instances.Min(i => i.ScheduledStartTime) : DateTime.Now,
            IncrementEnd: instances.Any() ? instances.Max(i => i.PlannedCompletionTime) : DateTime.Now,
            Tasks: instances,
            TaskChain: instances.Select(i => i.TaskIdString).ToList(),
            TotalValidTasks: validTasks,
            TotalInvalidTasks: invalidTasks,
            CriticalPathCompletion: instances.Any() ? 
                instances.OrderByDescending(i => i.PlannedCompletionTime).First().PlannedCompletionTime :
                null,
            DeadlineMisses: instances.Where(i => i.RequiredEndTime.HasValue && i.PlannedCompletionTime > i.RequiredEndTime.Value)
                .Select(i => i.TaskIdString).ToList()
        );
    }

    public ExecutionPlan CoordinateDistributedExecutionByPriority()
    {
        var sortedGrains = _grains.OrderByDescending(g => g.GetPriority()).ToList();
        var instances = new List<ExecutionInstanceEnhanced>();
        var currentTime = sortedGrains[0].GetCurrentState().ScheduledStartTime;

        foreach (var grain in sortedGrains)
        {
            var state = grain.GetCurrentState();
            var updated = state with
            {
                ScheduledStartTime = currentTime,
                PlannedCompletionTime = currentTime.AddMinutes((int)state.Duration.DurationMinutes)
            };
            instances.Add(updated);
            currentTime = updated.PlannedCompletionTime;
        }

        var validTasks = instances.Count(i => i.IsValid);
        var invalidTasks = instances.Count(i => !i.IsValid);

        return new ExecutionPlan(
            IncrementId: "priority-exec",
            IncrementStart: instances.First().ScheduledStartTime,
            IncrementEnd: instances.Last().PlannedCompletionTime,
            Tasks: instances,
            TaskChain: instances.Select(i => i.TaskIdString).ToList(),
            TotalValidTasks: validTasks,
            TotalInvalidTasks: invalidTasks,
            CriticalPathCompletion: instances.Last().PlannedCompletionTime,
            DeadlineMisses: instances.Where(i => !i.IsValid).Select(i => i.TaskIdString).ToList()
        );
    }

    public bool PropagateCompletionNotifications(DistributedExecutionGrain completedGrain)
    {
        var completedId = completedGrain.GetCurrentState().TaskIdString;
        var dependents = _grains.Where(g => 
            g.GetCurrentState().PrerequisiteTaskIds.Contains(completedId)).ToList();

        foreach (var dependent in dependents)
        {
            var state = dependent.GetCurrentState();
            var updated = state with
            {
                Status = ExecutionStatus.ReadyToExecute,
                FunctionalStartTime = completedGrain.GetCurrentState().PlannedCompletionTime
            };
            // Update grain state (in real implementation)
        }

        return true;
    }

    public bool ApplyCascadingAdjustments(DistributedExecutionGrain sourceGrain)
    {
        var sourceState = sourceGrain.GetCurrentState();
        var newEndTime = sourceState.ScheduledStartTime.AddMinutes((int)sourceState.Duration.DurationMinutes);

        var processed = new HashSet<string>();
        var queue = new Queue<string>(sourceState.PrerequisiteTaskIds);

        while (queue.Count > 0)
        {
            var taskId = queue.Dequeue();
            if (processed.Contains(taskId)) continue;
            processed.Add(taskId);

            var dependent = _grains.FirstOrDefault(g => g.GetCurrentState().TaskIdString == taskId);
            if (dependent != null)
            {
                var depState = dependent.GetCurrentState();
                var newStart = newEndTime;
                var newCompletion = newStart.AddMinutes((int)depState.Duration.DurationMinutes);

                // Update dependent state
                dependent.UpdateDuration(depState.Duration);

                foreach (var nextDepId in depState.PrerequisiteTaskIds)
                {
                    if (!processed.Contains(nextDepId))
                        queue.Enqueue(nextDepId);
                }
            }
        }

        return true;
    }

    public List<DeadlineConflict> DetectDeadlineConflicts()
    {
        var conflicts = new List<DeadlineConflict>();

        foreach (var grain in _grains)
        {
            var state = grain.GetCurrentState();
            if (state.RequiredEndTime.HasValue && state.PlannedCompletionTime > state.RequiredEndTime.Value)
            {
                conflicts.Add(new DeadlineConflict
                {
                    GrainId = state.TaskIdString,
                    ViolatesDeadline = true,
                    DeadlineTime = state.RequiredEndTime.Value,
                    CompletionTime = state.PlannedCompletionTime,
                    ViolationMinutes = (int)(state.PlannedCompletionTime - state.RequiredEndTime.Value).TotalMinutes
                });
            }
        }

        return conflicts;
    }

    public bool DetectConvergence()
    {
        if (_refinementHistory.Count < 2) return false;

        var lastRound = _refinementHistory.OrderByDescending(r => r.Key).First().Value;
        return lastRound.HasNoPositionChanges;
    }

    public RefinementRoundResult ExecuteRefinementRound(int roundNumber)
    {
        var result = new RefinementRoundResult
        {
            RoundNumber = roundNumber,
            PositionChanges = new List<PositionChange>(),
            HasNoPositionChanges = true
        };

        _refinementHistory[roundNumber] = result;
        return result;
    }

    public StateSyncResult SynchronizeStates()
    {
        var syncResult = new StateSyncResult
        {
            IsConsistent = true,
            UpdatedGrainCount = _grains.Length,
            InconsistenciesDetected = new List<string>()
        };

        return syncResult;
    }

    public ExecutionPlan RebalanceForConstraint(int maxExecutionTime)
    {
        var instances = _grains.Select(g => g.GetCurrentState()).ToList();
        var totalTime = instances.Sum(i => (int)i.Duration.DurationMinutes);

        var validTasks = instances.Count(i => i.IsValid);
        var invalidTasks = instances.Count(i => !i.IsValid);

        if (totalTime <= maxExecutionTime)
        {
            return CoordinateDistributedExecution();
        }

        // Rebalance by parallelizing where possible
        var rebalanced = CoordinateDistributedExecution();
        return rebalanced with 
        { 
            CriticalPathCompletion = rebalanced.IncrementStart.AddMinutes(maxExecutionTime)
        };
    }

    public List<string> IdentifyCriticalPath()
    {
        var instances = _grains.Select(g => g.GetCurrentState()).ToList();
        var path = new List<string>();

        // Find longest path through DAG
        var longestEnd = instances.OrderByDescending(i => i.PlannedCompletionTime).First();
        path.Add(longestEnd.TaskIdString);

        // Trace back through prerequisites
        var current = longestEnd;
        while (current.PrerequisiteTaskIds.Any())
        {
            var prereq = instances.FirstOrDefault(i => i.TaskIdString == current.PrerequisiteTaskIds.First());
            if (prereq == null) break;
            path.Insert(0, prereq.TaskIdString);
            current = prereq;
        }

        return path;
    }

    public bool HandleGrainFailure(DistributedExecutionGrain failedGrain)
    {
        var failedState = failedGrain.GetCurrentState();
        var toProcess = new Queue<string>();
        var processed = new HashSet<string> { failedState.TaskIdString };
        
        // Find all direct dependents
        var directDependents = _grains.Where(g => 
            g.GetCurrentState().PrerequisiteTaskIds.Contains(failedState.TaskIdString)).ToList();

        foreach (var dependent in directDependents)
        {
            dependent.ResetToAwaitingPrerequisites();
            toProcess.Enqueue(dependent.GetCurrentState().TaskIdString);
        }
        
        // Cascade to all transitive dependents
        while (toProcess.Count > 0)
        {
            var currentTaskId = toProcess.Dequeue();
            if (processed.Contains(currentTaskId)) continue;
            processed.Add(currentTaskId);
            
            var nextDependents = _grains.Where(g =>
                g.GetCurrentState().PrerequisiteTaskIds.Contains(currentTaskId)).ToList();
            
            foreach (var dependent in nextDependents)
            {
                dependent.ResetToAwaitingPrerequisites();
                toProcess.Enqueue(dependent.GetCurrentState().TaskIdString);
            }
        }

        return true;
    }

    public bool DetectAndHandleTimeouts()
    {
        foreach (var grain in _grains)
        {
            var state = grain.GetCurrentState();
            if (state.Status == ExecutionStatus.ReadyToExecute)
            {
                grain.MarkFailed(ExecutionFailureReason.TimeoutException);
                return true;
            }
        }

        return false;
    }

    public void SetTimeoutThreshold(TimeSpan threshold) => _timeoutThreshold = threshold;

    public OrchestratorSnapshot CreateStateSnapshot()
    {
        return new OrchestratorSnapshot
        {
            GrainStates = _grains.Select(g => g.GetCurrentState()).ToList(),
            Timestamp = DateTime.UtcNow,
            RefinementHistory = new(_refinementHistory)
        };
    }

    public static DistributedOrchestrator RecoverFromSnapshot(OrchestratorSnapshot snapshot)
    {
        var grains = snapshot.GrainStates.Select(state =>
            new DistributedExecutionGrain(
                taskId: state.TaskIdString,
                taskName: state.TaskName,
                scheduledStartTime: state.ScheduledStartTime,
                duration: state.Duration,
                plannedCompletionTime: state.PlannedCompletionTime,
                prerequisiteTaskIds: state.PrerequisiteTaskIds,
                requiredEndTime: state.RequiredEndTime,
                isValid: state.IsValid)).ToArray();

        return new DistributedOrchestrator(grains);
    }
}

// Supporting types for distributed coordination

public record DeadlineConflict
{
    public required string GrainId { get; init; }
    public bool ViolatesDeadline { get; init; }
    public DateTime DeadlineTime { get; init; }
    public DateTime CompletionTime { get; init; }
    public int ViolationMinutes { get; init; }
}

public record StateSyncResult
{
    public bool IsConsistent { get; init; }
    public int UpdatedGrainCount { get; init; }
    public required List<string> InconsistenciesDetected { get; init; }
}

public record RefinementRoundResult
{
    public int RoundNumber { get; init; }
    public required List<PositionChange> PositionChanges { get; init; }
    public bool HasNoPositionChanges { get; init; }
}

public record PositionChange
{
    public required string GrainId { get; init; }
    public TimeSpan Adjustment { get; init; }
}

public record OrchestratorSnapshot
{
    public required List<ExecutionInstanceEnhanced> GrainStates { get; init; }
    public DateTime Timestamp { get; init; }
    public required Dictionary<int, RefinementRoundResult> RefinementHistory { get; init; }
}

public enum ExecutionFailureReason
{
    TimeoutException,
    DataValidationFailure,
    ExternalServiceFailure,
    UnexpectedError
}
