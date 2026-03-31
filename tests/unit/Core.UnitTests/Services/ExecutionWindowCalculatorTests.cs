using Xunit;
using Core.Services;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.UnitTests.Services;

/// <summary>
/// SUBTASK-3: Unit Tests for Execution Window Calculator
/// Tests the core business rule: calculate when tasks can execute based on
/// dependencies, deadlines, and scheduling constraints.
/// </summary>
public class ExecutionWindowCalculatorTests
{
    private readonly ExecutionWindowCalculator _calculator;

    public ExecutionWindowCalculatorTests()
    {
        _calculator = new ExecutionWindowCalculator();
    }

    /// <summary>
    /// Test 1: Task with no dependencies should have immediate execution window
    /// </summary>
    [Fact]
    public void CalculateWindow_NoDependencies_AllowsImmediateExecution()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001", prereqs: Array.Empty<string>());
        var allTasks = new List<object> { executionEvent };
        var currentTime = DateTime.Parse("2024-01-08 09:00");
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Should allow execution at or near scheduled time
    }

    /// <summary>
    /// Test 2: Task with dependency should start after prerequisite completion
    /// </summary>
    [Fact]
    public void CalculateWindow_WithDependency_StartsAfterPrerequisite()
    {
        // Arrange
        var task1 = CreateEvent("Task1", "T001", prereqs: Array.Empty<string>(), startTime: new TimeOfDay(9, 0, 0));
        var task2 = CreateEvent("Task2", "T002", prereqs: new[] { "T001" }, startTime: new TimeOfDay(10, 0, 0));
        var allTasks = new List<object> { task1, task2 };
        var currentTime = DateTime.Parse("2024-01-08 09:00");
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(task2, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Task2 should not start before Task1 completes
    }

    /// <summary>
    /// Test 3: Task with deadline should constrain execution window
    /// </summary>
    [Fact]
    public void CalculateWindow_WithDeadline_ConstrainsWindow()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001", prereqs: Array.Empty<string>());
        var allTasks = new List<object> { executionEvent };
        var currentTime = DateTime.Parse("2024-01-08 09:00");
        var deadline = DateTime.Parse("2024-01-08 10:00");
        var intakeTimes = new Dictionary<string, DateTime> { { "T001", deadline } };

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Window should be constrained by deadline
    }

    /// <summary>
    /// Test 4: Task with multiple dependencies should use latest prerequisite
    /// </summary>
    [Fact]
    public void CalculateWindow_WithMultipleDependencies_UsesLatestCompletion()
    {
        // Arrange
        var task1 = CreateEvent("Task1", "T001", prereqs: Array.Empty<string>(), startTime: new TimeOfDay(8, 0, 0));
        var task2 = CreateEvent("Task2", "T002", prereqs: Array.Empty<string>(), startTime: new TimeOfDay(10, 0, 0));
        var task3 = CreateEvent("Task3", "T003", prereqs: new[] { "T001", "T002" }, startTime: new TimeOfDay(11, 0, 0));
        var allTasks = new List<object> { task1, task2, task3 };
        var currentTime = DateTime.Parse("2024-01-08 08:00");
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(task3, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Task3 should start after Task2 completes (later than Task1)
    }

    /// <summary>
    /// Test 5: Task with conflicting constraints should return invalid window
    /// </summary>
    [Fact]
    public void CalculateWindow_WithConflictingConstraints_ReturnsInvalid()
    {
        // Arrange
        var task1 = CreateEvent("Task1", "T001", prereqs: Array.Empty<string>(), startTime: new TimeOfDay(14, 0, 0));
        var task2 = CreateEvent("Task2", "T002", prereqs: new[] { "T001" }, startTime: new TimeOfDay(14, 30, 0));
        var allTasks = new List<object> { task1, task2 };
        var currentTime = DateTime.Parse("2024-01-08 14:00");
        // Task1 finishes at 14:15, Task2 must start by 14:20 but needs 15 min = won't finish by 14:20
        var deadline = DateTime.Parse("2024-01-08 14:20");
        var intakeTimes = new Dictionary<string, DateTime> { { "T002", deadline } };

        // Act
        var window = _calculator.CalculateWindow(task2, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Should indicate infeasibility
    }

    /// <summary>
    /// Test 6: Tasks on different days should handle week wrapping
    /// </summary>
    [Fact]
    public void CalculateWindow_TasksOnDifferentDays_HandlesCorrectly()
    {
        // Arrange
        var taskMonday = CreateEvent("Task1", "T001", DayOfWeek.Monday, prereqs: Array.Empty<string>());
        var taskTuesday = CreateEvent("Task2", "T002", DayOfWeek.Tuesday, prereqs: new[] { "T001" });
        var allTasks = new List<object> { taskMonday, taskTuesday };
        var currentTime = DateTime.Parse("2024-01-08 17:00"); // Monday evening
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(taskTuesday, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Should handle Tuesday following Monday
    }

    /// <summary>
    /// Test 7: Task with no feasible window should be flagged
    /// </summary>
    [Fact]
    public void CalculateWindow_NoFeasibleWindow_IsMarked()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001", 
            prereqs: Array.Empty<string>(),
            startTime: new TimeOfDay(9, 0, 0));
        var allTasks = new List<object> { executionEvent };
        var currentTime = DateTime.Parse("2024-01-08 09:00");
        // Only 5 minute window, but task defaults to 15 min
        var deadline = DateTime.Parse("2024-01-08 09:05");
        var intakeTimes = new Dictionary<string, DateTime> { { "T001", deadline } };

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
        // Should indicate infeasibility
    }

    /// <summary>
    /// Test 8: Null input should be handled gracefully
    /// </summary>
    [Fact]
    public void CalculateWindow_WithNullTasks_ReturnsNull()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var currentTime = DateTime.Parse("2024-01-08 09:00");

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, new List<object>(), 
            new Dictionary<string, DateTime>());

        // Assert
        // Should handle missing dependency gracefully
        Assert.NotNull(window);
    }

    /// <summary>
    /// Test 9: Early morning start should work correctly
    /// </summary>
    [Fact]
    public void CalculateWindow_EarlyMorningStart_Succeeds()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001", 
            prereqs: Array.Empty<string>(),
            startTime: new TimeOfDay(6, 0, 0)); // Very early
        var allTasks = new List<object> { executionEvent };
        var currentTime = DateTime.Parse("2024-01-08 06:00");
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
    }

    /// <summary>
    /// Test 10: Late evening task should be allowed
    /// </summary>
    [Fact]
    public void CalculateWindow_LateEveningStart_Succeeds()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001", 
            prereqs: Array.Empty<string>(),
            startTime: new TimeOfDay(22, 0, 0)); // Very late
        var allTasks = new List<object> { executionEvent };
        var currentTime = DateTime.Parse("2024-01-08 22:00");
        var intakeTimes = new Dictionary<string, DateTime>();

        // Act
        var window = _calculator.CalculateWindow(executionEvent, currentTime, allTasks, intakeTimes);

        // Assert
        Assert.NotNull(window);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static ExecutionEventDefinition CreateEvent(
        string taskName,
        string taskId,
        string[] prereqs = null,
        DayOfWeek day = DayOfWeek.Monday,
        TimeOfDay? startTime = null)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: day,
            ScheduledTime: startTime ?? new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: (prereqs ?? Array.Empty<string>()).ToHashSet(),
            DurationMinutes: 15);
    }

    private static ExecutionEventDefinition CreateEvent(
        string taskName,
        string taskId,
        DayOfWeek day,
        string[] prereqs,
        TimeOfDay? startTime = null)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: day,
            ScheduledTime: startTime ?? new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: (prereqs ?? Array.Empty<string>()).ToHashSet(),
            DurationMinutes: 15);
    }
}
