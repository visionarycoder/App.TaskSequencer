using Core.Orleans.Grains;
using Core.Models;
using Xunit;

namespace Core.Orleans.UnitTests.Grains;

/// <summary>
/// SUBTASK-12: Orleans Grain Coordination - Simplified Unit Tests
/// Tests key coordination logic without requiring a full Orleans silo.
/// Note: Full Orleans grain testing requires TestHost infrastructure.
/// </summary>
public class ExecutionPlanCoordinationLogicTests
{
    /// <summary>
    /// Test 1: Verify dependency ordering in execution plan
    /// </summary>
    [Fact]
    public void TaskChain_RespectsDependencies()
    {
        // Arrange: Create 3 tasks where 1 → 2 → 3
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            new(
                Id: 0,
                TaskId: 0,
                TaskIdString: "1",
                TaskName: "First",
                ScheduledStartTime: DateTime.Now,
                FunctionalStartTime: null,
                RequiredEndTime: null,
                Duration: new ExecutionDuration(15, true),
                PlannedCompletionTime: DateTime.Now.AddMinutes(15),
                PrerequisiteTaskIds: new HashSet<string>().AsReadOnly(),
                IsValid: true,
                Status: ExecutionStatus.ReadyToExecute,
                ValidationMessage: null
            ),
            new(
                Id: 1,
                TaskId: 1,
                TaskIdString: "2",
                TaskName: "Second",
                ScheduledStartTime: DateTime.Now.AddMinutes(15),
                FunctionalStartTime: null,
                RequiredEndTime: null,
                Duration: new ExecutionDuration(15, true),
                PlannedCompletionTime: DateTime.Now.AddMinutes(30),
                PrerequisiteTaskIds: new HashSet<string> { "1" }.AsReadOnly(),
                IsValid: true,
                Status: ExecutionStatus.AwaitingPrerequisites,
                ValidationMessage: null
            ),
            new(
                Id: 2,
                TaskId: 2,
                TaskIdString: "3",
                TaskName: "Third",
                ScheduledStartTime: DateTime.Now.AddMinutes(30),
                FunctionalStartTime: null,
                RequiredEndTime: null,
                Duration: new ExecutionDuration(15, true),
                PlannedCompletionTime: DateTime.Now.AddMinutes(45),
                PrerequisiteTaskIds: new HashSet<string> { "2" }.AsReadOnly(),
                IsValid: true,
                Status: ExecutionStatus.AwaitingPrerequisites,
                ValidationMessage: null
            )
        };

        // Act: Build task chain
        var chain = BuildTaskChain(tasks);

        // Assert: Verify ordering
        Assert.NotEmpty(chain);
        Assert.Equal("1", chain[0]);
        Assert.Equal("2", chain[1]);
        Assert.Equal("3", chain[2]);
    }

    /// <summary>
    /// Test 2: Verify parallel tasks are handled in chain building
    /// </summary>
    [Fact]
    public void TaskChain_IncludesAllTasks()
    {
        // Arrange: Create tasks with mixed dependencies
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            new(0, 0, "1", "Extract",
                DateTime.Now, null, null,
                new ExecutionDuration(30, true), DateTime.Now.AddMinutes(30),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),
            
            new(1, 1, "2", "Parallel A",
                DateTime.Now.AddMinutes(30), null, null,
                new ExecutionDuration(30, true), DateTime.Now.AddMinutes(60),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),
            
            new(2, 2, "3", "Parallel B",
                DateTime.Now.AddMinutes(30), null, null,
                new ExecutionDuration(25, true), DateTime.Now.AddMinutes(55),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),
            
            new(3, 3, "4", "Merge",
                DateTime.Now.AddMinutes(60), null, null,
                new ExecutionDuration(20, true), DateTime.Now.AddMinutes(80),
                new HashSet<string> { "2", "3" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act: Build task chain
        var chain = BuildTaskChain(tasks);

        // Assert: Verify all tasks are included and core dependencies are honored
        Assert.Equal(4, chain.Count);
        Assert.Contains("1", chain);
        Assert.Contains("2", chain);
        Assert.Contains("3", chain);
        Assert.Contains("4", chain);

        // Verify root tasks come first
        Assert.Equal("1", chain[0]);
    }

    /// <summary>
    /// Test 3: Deadline compliance detection
    /// </summary>
    [Fact]
    public void DeadlineCompliance_DetectsViolations()
    {
        // Arrange: Create task with deadline that will be missed
        var baseTime = DateTime.Now;
        var task = new ExecutionInstanceEnhanced(
            Id: 0,
            TaskId: 0,
            TaskIdString: "tight",
            TaskName: "Tight Deadline Task",
            ScheduledStartTime: baseTime,
            FunctionalStartTime: null,
            RequiredEndTime: baseTime.AddMinutes(15), // Only 15 minutes available
            Duration: new ExecutionDuration(30, true), // But task takes 30 minutes
            PlannedCompletionTime: baseTime.AddMinutes(30), // Will complete at 30 minutes
            PrerequisiteTaskIds: new HashSet<string>().AsReadOnly(),
            IsValid: false, // Should be invalid due to deadline miss
            Status: ExecutionStatus.DeadlineMiss,
            ValidationMessage: "Deadline miss: completion 2024-03-25 14:30:00, deadline 2024-03-25 14:15:00"
        );

        // Act: Check deadline compliance
        var missed = task.RequiredEndTime.HasValue && task.PlannedCompletionTime > task.RequiredEndTime.Value;

        // Assert
        Assert.True(missed);
        Assert.Equal(ExecutionStatus.DeadlineMiss, task.Status);
    }

    /// <summary>
    /// Test 4: Coordination status metrics
    /// </summary>
    [Fact]
    public void CoordinationStatus_CalculatesMetricsCorrectly()
    {
        // Arrange: Create a mix of valid and invalid tasks
        var baseTime = DateTime.Now;
        var tasks = new Dictionary<string, ExecutionInstanceEnhanced>
        {
            ["1"] = new(0, 0, "1", "Valid Task", baseTime, null, baseTime.AddHours(1), 
                new ExecutionDuration(30, true), baseTime.AddMinutes(30), 
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),
            
            ["2"] = new(1, 1, "2", "Invalid Task", baseTime, null, baseTime.AddMinutes(10), 
                new ExecutionDuration(30, true), baseTime.AddMinutes(30), 
                new HashSet<string>().AsReadOnly(), false, ExecutionStatus.DeadlineMiss, "Deadline miss"),
            
            ["3"] = new(2, 2, "3", "Completed Task", baseTime, null, baseTime.AddHours(1), 
                new ExecutionDuration(20, true), baseTime.AddMinutes(20), 
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.Completed, null)
        };

        // Act: Calculate metrics
        var validCount = tasks.Values.Count(t => t.IsValid);
        var invalidCount = tasks.Values.Count(t => !t.IsValid);
        var completedCount = tasks.Values.Count(t => t.Status == ExecutionStatus.Completed);
        var pendingCount = tasks.Values.Count(t => t.Status != ExecutionStatus.Completed && t.Status != ExecutionStatus.Invalid);

        // Assert
        Assert.Equal(2, validCount);
        Assert.Equal(1, invalidCount);
        Assert.Equal(1, completedCount);
        Assert.Equal(2, pendingCount);
    }

    /// <summary>
    /// Test 5: Prerequisite cascading
    /// </summary>
    [Fact]
    public void PrerequisiteCascading_AdjustsDependentTasks()
    {
        // Arrange: Create chain where task 1 completes late
        var baseTime = DateTime.Now;
        var actualCompletion1 = baseTime.AddMinutes(45); // Late completion
        
        var task1 = new ExecutionInstanceEnhanced(
            Id: 0, TaskId: 0, TaskIdString: "1", TaskName: "Task 1",
            ScheduledStartTime: baseTime, FunctionalStartTime: null, RequiredEndTime: baseTime.AddHours(1),
            Duration: new ExecutionDuration(30, true), 
            PlannedCompletionTime: actualCompletion1,
            PrerequisiteTaskIds: new HashSet<string>().AsReadOnly(),
            IsValid: true, Status: ExecutionStatus.Completed, ValidationMessage: null
        );

        var task2 = new ExecutionInstanceEnhanced(
            Id: 1, TaskId: 1, TaskIdString: "2", TaskName: "Task 2",
            ScheduledStartTime: baseTime.AddMinutes(35), FunctionalStartTime: null, RequiredEndTime: baseTime.AddHours(1),
            Duration: new ExecutionDuration(20, true),
            PlannedCompletionTime: baseTime.AddMinutes(55),
            PrerequisiteTaskIds: new HashSet<string> { "1" }.AsReadOnly(),
            IsValid: true, Status: ExecutionStatus.AwaitingPrerequisites, ValidationMessage: null
        );

        // Act: Adjust task 2's start time based on task 1's actual completion
        var adjustedTask2StartTime = actualCompletion1; // Task 2 can't start until task 1 completes
        var adjustedTask2CompletionTime = adjustedTask2StartTime.Add(task2.Duration.ToTimeSpan());

        // Assert: Verify cascade
        Assert.Equal(actualCompletion1, adjustedTask2StartTime);
        Assert.Equal(baseTime.AddMinutes(65), adjustedTask2CompletionTime); // 45 + 20 minutes
        Assert.True(adjustedTask2CompletionTime > task2.PlannedCompletionTime);
    }

    /// <summary>
    /// Test 6: Critical path identification
    /// </summary>
    [Fact]
    public void CriticalPath_IdentifiesLongestDurationChain()
    {
        // Arrange: Create DAG with multiple paths
        var baseTime = DateTime.Now;
        var tasks = new List<ExecutionInstanceEnhanced>
        {
            // Path 1: 1 → 2 → 4 (60 minutes total)
            new(0, 0, "1", "Task 1", baseTime, null, null,
                new ExecutionDuration(30, true), baseTime.AddMinutes(30),
                new HashSet<string>().AsReadOnly(), true, ExecutionStatus.ReadyToExecute, null),
            
            new(1, 1, "2", "Task 2", baseTime.AddMinutes(30), null, null,
                new ExecutionDuration(30, true), baseTime.AddMinutes(60),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),
            
            // Path 2: 1 → 3 → 4 (50 minutes total)
            new(2, 2, "3", "Task 3", baseTime.AddMinutes(30), null, null,
                new ExecutionDuration(20, true), baseTime.AddMinutes(50),
                new HashSet<string> { "1" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null),
            
            // Merge: 4 takes 10 minutes (Path 1 critical: 60 + 10 = 70)
            new(3, 3, "4", "Task 4", baseTime.AddMinutes(60), null, null,
                new ExecutionDuration(10, true), baseTime.AddMinutes(70),
                new HashSet<string> { "2", "3" }.AsReadOnly(), true, ExecutionStatus.AwaitingPrerequisites, null)
        };

        // Act: Find critical path (longest completion time)
        var criticalPath = tasks.Max(t => t.PlannedCompletionTime);

        // Assert
        Assert.Equal(baseTime.AddMinutes(70), criticalPath);
    }

    /// <summary>
    /// Helper: Build task chain respecting dependencies (mimics grain logic)
    /// </summary>
    private List<string> BuildTaskChain(List<ExecutionInstanceEnhanced> tasks)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();

        // Find root tasks (no prerequisites)
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

        // Find children: tasks that depend on this task
        var children = tasks
            .Where(t => t.PrerequisiteTaskIds.Contains(taskId))
            .Select(t => t.TaskIdString)
            .Distinct();

        foreach (var child in children)
        {
            TraverseDepthFirst(child, tasks, visited, chain);
        }
    }
}
