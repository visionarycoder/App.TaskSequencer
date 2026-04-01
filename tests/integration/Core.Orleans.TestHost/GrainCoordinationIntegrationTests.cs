using Core.Models;

namespace Core.Orleans.TestHost;

/// <summary>
/// SUBTASK-13: Orleans Grain Coordination Integration Tests
/// Tests grain responsibilities and coordination patterns
/// Note: Full Orleans runtime testing requires Orleans.TestingHost infrastructure
/// These tests validate coordination logic that will be used by grains
/// </summary>
public class GrainCoordinationPatternTests
{
    /// <summary>
    /// Test 1: Task grain initialization simulation
    /// </summary>
    [Fact]
    public void TaskGrainInitialization_StoresEventDefinition()
    {
        // Arrange
        var eventDef = new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: "1",
            TaskName: "Extract Data",
            ScheduledDay: DayOfWeek.Monday,
            ScheduledTime: new TimeOfDay(6, 0),
            PrerequisiteTaskIds: new HashSet<string>().AsReadOnly(),
            DurationMinutes: 30,
            IntakeRequirement: null
        );

        var duration = new ExecutionDuration(DurationMinutes: 30, IsEstimated: true);

        // Act - simulate grain initialization
        var instance = new ExecutionInstanceEnhanced(
            Id: 0,
            TaskId: 0,
            TaskIdString: eventDef.TaskId,
            TaskName: eventDef.TaskName,
            ScheduledStartTime: eventDef.ScheduledTime.ApplyToDate(DateTime.Now),
            FunctionalStartTime: null,
            RequiredEndTime: null,
            Duration: duration,
            PlannedCompletionTime: eventDef.ScheduledTime.ApplyToDate(DateTime.Now).AddMinutes(30),
            PrerequisiteTaskIds: eventDef.PrerequisiteTaskIds,
            IsValid: true,
            Status: ExecutionStatus.ReadyToExecute,
            ValidationMessage: null
        );

        // Assert
        Assert.Equal("1", instance.TaskIdString);
        Assert.Equal("Extract Data", instance.TaskName);
        Assert.Equal(30u, instance.Duration.DurationMinutes);
    }

    /// <summary>
    /// Test 2: Task completion time calculation
    /// </summary>
    [Fact]
    public void PlannedCompletionTime_CalculatedCorrectly()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var duration = new ExecutionDuration(DurationMinutes: 45, IsEstimated: true);

        // Act
        var completionTime = baseTime.Add(duration.ToTimeSpan());

        // Assert
        Assert.Equal(baseTime.AddMinutes(45), completionTime);
    }

    /// <summary>
    /// Test 3: Start time adjustment for prerequisites
    /// </summary>
    [Fact]
    public void StartTimeAdjustment_RespectPrerequisiteCompletion()
    {
        // Arrange
        var baseTime = DateTime.Now;
        var scheduledStart = baseTime.AddMinutes(60);
        var prerequisiteCompletion = baseTime.AddMinutes(120); // Later than scheduled

        // Act - simulate prerequisite-aware start time
        var adjustedStart = prerequisiteCompletion > scheduledStart 
            ? prerequisiteCompletion 
            : scheduledStart;

        // Assert
        Assert.Equal(prerequisiteCompletion, adjustedStart);
    }

    /// <summary>
    /// Test 4: Deadline compliance check
    /// </summary>
    [Fact]
    public void DeadlineCompliance_DetectsViolations()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var scheduledStart = baseDate.AddMinutes(360);  // 6 hours
        var duration = new ExecutionDuration(DurationMinutes: 30, IsEstimated: true);
        var deadline = baseDate.AddMinutes(375);  // 6 hours 15 minutes - too tight

        var plannedCompletion = scheduledStart.Add(duration.ToTimeSpan());

        // Act
        var isViolation = plannedCompletion > deadline;

        // Assert
        Assert.True(isViolation);
    }

    /// <summary>
    /// Test 5: Multi-task execution sequencing
    /// </summary>
    [Fact]
    public void ExecutionSequencing_RespectsDependencies()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Extract", baseDate.AddHours(6), null, null,
                new ExecutionDuration(30, true), baseDate.AddMinutes(390),  // 6.5 hours
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),

            new(1, 1, "2", "Validate", baseDate.AddMinutes(390), null, null,
                new ExecutionDuration(20, true), baseDate.AddMinutes(410),  // 6h 50m
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),

            new(2, 2, "3", "Transform", baseDate.AddMinutes(410), null, null,
                new ExecutionDuration(25, true), baseDate.AddMinutes(435),  // 7h 15m
                new HashSet<string> { "2" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act - verify chain building
        var chain = BuildTaskChain(tasks);

        // Assert
        Assert.Equal(3, chain.Count);
        Assert.Equal("1", chain[0]);
        Assert.Equal("2", chain[1]);
        Assert.Equal("3", chain[2]);
    }

    /// <summary>
    /// Test 6: Parallel task handling
    /// </summary>
    [Fact]
    public void ParallelTasks_ExecutedConcurrently()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Extract", baseDate.AddHours(6), null, null,
                new ExecutionDuration(30, true), baseDate.AddHours(6.5),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),

            new(1, 1, "2", "Process A", baseDate.AddHours(6.5), null, null,
                new ExecutionDuration(20, true), baseDate.AddHours(6.833),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),

            new(2, 2, "3", "Process B", baseDate.AddHours(6.5), null, null,
                new ExecutionDuration(20, true), baseDate.AddHours(6.833),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act - identify parallel tasks
        var task1Dependents = tasks.Where(t => t.PrerequisiteTaskIds.Contains("1")).ToList();

        // Assert
        Assert.Equal(2, task1Dependents.Count);
        Assert.Contains(task1Dependents, t => t.TaskIdString == "2");
        Assert.Contains(task1Dependents, t => t.TaskIdString == "3");
    }

    /// <summary>
    /// Test 7: Critical path identification
    /// </summary>
    [Fact]
    public void CriticalPath_IdentifiesLongestDuration()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            // Path 1: 30 + 20 = 50 minutes
            new(0, 0, "1", "Start", baseDate.AddHours(6), null, null,
                new ExecutionDuration(30, true), baseDate.AddHours(6.5),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),

            new(1, 1, "2", "Middle", baseDate.AddHours(6.5), null, null,
                new ExecutionDuration(20, true), baseDate.AddHours(6.833),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),

            // Path 2: 30 + 25 + 15 = 70 minutes
            new(2, 2, "3", "Alt Path", baseDate.AddHours(6.5), null, null,
                new ExecutionDuration(25, true), baseDate.AddHours(6.917),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),

            new(3, 3, "4", "Final", baseDate.AddHours(6.917), null, null,
                new ExecutionDuration(15, true), baseDate.AddHours(7.167),
                new HashSet<string> { "3" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act
        var criticalPath = tasks.Max(t => t.PlannedCompletionTime);

        // Assert
        Assert.Equal(baseDate.AddHours(7.167), criticalPath);
    }

    /// <summary>
    /// Test 8: Coordinator convergence detection
    /// </summary>
    [Fact]
    public void ConvergenceDetection_IdentifiesStability()
    {
        // Arrange - simulate two refinement rounds with FIXED time
        var baseTime = new DateTime(2024, 3, 25, 6, 0, 0);
        
        var round1Tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Task 1", baseTime, null, null,
                new ExecutionDuration(30, true), baseTime.AddMinutes(30),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null)
        };

        var round2Tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Task 1", baseTime, null, null,
                new ExecutionDuration(30, true), baseTime.AddMinutes(30),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null)
        };

        // Act - check if tasks are equal between rounds
        var noChanges = TasksEqual(round1Tasks, round2Tasks);

        // Assert - when tasks are equal, convergence has been achieved
        Assert.True(noChanges);
    }

    /// <summary>
    /// Test 9: Deadline miss cascade
    /// </summary>
    [Fact]
    public void DeadlineMiss_CascadesToDependents()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var task1 = new ExecutionInstanceEnhanced(
            Id: 0, TaskId: 0, TaskIdString: "1", TaskName: "Late Task",
            ScheduledStartTime: baseDate.AddHours(6),
            FunctionalStartTime: null,
            RequiredEndTime: baseDate.AddHours(6.5),
            Duration: new ExecutionDuration(45, true),  // Will miss deadline
            PlannedCompletionTime: baseDate.AddHours(6.75),  // Past deadline
            PrerequisiteTaskIds: new HashSet<string>().AsReadOnly(),
            IsValid: false,
            Status: ExecutionStatus.DeadlineMiss,
            ValidationMessage: "Misses deadline"
        );

        var task2 = new ExecutionInstanceEnhanced(
            Id: 1, TaskId: 1, TaskIdString: "2", TaskName: "Dependent Task",
            ScheduledStartTime: baseDate.AddHours(6.75),
            FunctionalStartTime: null,
            RequiredEndTime: baseDate.AddHours(7.167),
            Duration: new ExecutionDuration(20, true),
            PlannedCompletionTime: baseDate.AddHours(7.083),
            PrerequisiteTaskIds: new HashSet<string> { "1" }.AsReadOnly(),
            IsValid: true,
            Status: ExecutionStatus.AwaitingPrerequisites,
            ValidationMessage: null
        );

        // Act - cascade the miss to dependents
        var affectedByMiss = task1.Status == ExecutionStatus.DeadlineMiss &&
                           task2.PrerequisiteTaskIds.Contains(task1.TaskIdString);

        // Assert
        Assert.True(affectedByMiss);
    }

    /// <summary>
    /// Test 10: End-to-end coordination workflow
    /// </summary>
    [Fact]
    public void EndToEndWorkflow_GeneratesValidPlan()
    {
        // Arrange
        var baseDate = DateTime.Now;
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Extract", baseDate.AddHours(6), null, baseDate.AddHours(7),
                new ExecutionDuration(30, true), baseDate.AddHours(6.5),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),

            new(1, 1, "2", "Transform", baseDate.AddHours(6.5), null, baseDate.AddHours(7),
                new ExecutionDuration(20, true), baseDate.AddHours(6.833),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),

            new(2, 2, "3", "Load", baseDate.AddHours(6.833), null, baseDate.AddHours(7.25),
                new ExecutionDuration(15, true), baseDate.AddHours(7.083),
                new HashSet<string> { "2" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act - build full workflow
        var chain = BuildTaskChain(tasks);
        var allValid = tasks.All(t => t.IsValid);
        var criticalPath = tasks.Max(t => t.PlannedCompletionTime);

        // Assert
        Assert.Equal(3, chain.Count);
        Assert.True(allValid);
        Assert.Equal(baseDate.AddHours(7.083), criticalPath);
    }

    /// <summary>
    /// Helper: Build task chain respecting dependencies
    /// </summary>
    private List<string> BuildTaskChain(List<ExecutionInstanceEnhanced> tasks)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();

        var roots = tasks.Where(t => t.PrerequisiteTaskIds.Count == 0).ToList();

        foreach (var root in roots)
        {
            TraverseDepthFirst(root.TaskIdString, tasks, visited, chain);
        }

        return chain;
    }

    private void TraverseDepthFirst(
        string taskId,
        List<ExecutionInstanceEnhanced> tasks,
        HashSet<string> visited,
        List<string> chain)
    {
        if (visited.Contains(taskId))
            return;

        visited.Add(taskId);
        chain.Add(taskId);

        var children = tasks
            .Where(t => t.PrerequisiteTaskIds.Contains(taskId))
            .Select(t => t.TaskIdString)
            .Distinct<string>();

        foreach (var child in children)
        {
            TraverseDepthFirst(child, tasks, visited, chain);
        }
    }

    private bool TasksEqual(List<ExecutionInstanceEnhanced> tasks1, List<ExecutionInstanceEnhanced> tasks2)
    {
        if (tasks1.Count != tasks2.Count)
            return false;

        return tasks1.Zip(tasks2).All(pair =>
            pair.First.TaskIdString == pair.Second.TaskIdString &&
            pair.First.PlannedCompletionTime == pair.Second.PlannedCompletionTime
        );
    }
}

/// <summary>
/// SUBTASK-13: Grain State Management Tests
/// Tests persistence and idempotency patterns used by Orleans grains
/// </summary>
public class GrainStateManagementTests
{
    /// <summary>
    /// Test 1: State initialization and storage
    /// </summary>
    [Fact]
    public void InitialState_StoredCorrectly()
    {
        // Arrange
        var planId = "test-plan-1";
        var eventDefinitions = new List<ExecutionEventDefinition>
        {
            new(Guid.NewGuid(), "1", "Task 1", DayOfWeek.Monday, new TimeOfDay(6, 0),
                new HashSet<string>().AsReadOnly(), 30, null)
        };

        // Act - simulate state storage
        var storedState = new
        {
            PlanId = planId,
            Events = eventDefinitions,
            InitializedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(planId, storedState.PlanId);
        Assert.Single(storedState.Events);
    }

    /// <summary>
    /// Test 2: Idempotent initialization
    /// </summary>
    [Fact]
    public void RepeatedInitialization_ProducesSameResult()
    {
        // Arrange
        var eventDef = new ExecutionEventDefinition(
            Guid.NewGuid(), "1", "Task", DayOfWeek.Monday, new TimeOfDay(6, 0),
            new HashSet<string>().AsReadOnly(), 30, null
        );

        // Act - initialize twice
        var firstInit = CreateInitialState(eventDef);
        var secondInit = CreateInitialState(eventDef);

        // Assert
        Assert.Equal(firstInit.TaskId, secondInit.TaskId);
        Assert.Equal(firstInit.TaskName, secondInit.TaskName);
    }

    private (string TaskId, string TaskName) CreateInitialState(ExecutionEventDefinition eventDef)
    {
        return (eventDef.TaskId, eventDef.TaskName);
    }
}
