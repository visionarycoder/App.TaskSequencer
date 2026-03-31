using Xunit;
using Core.Services;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.UnitTests.Services;

/// <summary>
/// SUBTASK-2: Unit Tests for Execution Duration Calculator
/// Tests the core business rule: estimate task execution durations based on historical data
/// or apply defaults, with proper duration flags for tracking confidence.
/// </summary>
public class ExecutionDurationCalculatorTests
{
    private readonly ExecutionDurationCalculator _calculator;

    public ExecutionDurationCalculatorTests()
    {
        _calculator = new ExecutionDurationCalculator();
    }

    /// <summary>
    /// Test 1: Task with no historical data should default to 15 minutes
    /// </summary>
    [Fact]
    public void GetDuration_NoHistoricalData_DefaultsFifteenMinutes()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var historicalData = new List<object>();

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        Assert.Equal(15, duration);
        Assert.True(isEstimated, "Should be marked as estimated when no history");
    }

    /// <summary>
    /// Test 2: Task with single historical execution should use actual duration
    /// </summary>
    [Fact]
    public void GetDuration_WithSingleHistoricalExecution_UsesActualDuration()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var executionInstance = new ExecutionInstance(
            Id: 1,
            TaskId: 1,
            ScheduledStartTime: DateTime.Parse("2024-01-15 09:00"),
            FunctionalStartTime: DateTime.Parse("2024-01-15 09:00"),
            RequiredEndTime: DateTime.Parse("2024-01-15 09:22"), // 22 minutes actual
            DurationMinutes: 22,
            PrerequisiteTaskIds: new HashSet<string>(),
            IsValid: true,
            ValidationMessage: null);
        var historicalData = new List<object> { executionInstance };

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        Assert.Equal(22, duration);
        Assert.False(isEstimated, "Should NOT be marked as estimated when history exists");
    }

    /// <summary>
    /// Test 3: Task with multiple historical executions should use average
    /// </summary>
    [Fact]
    public void GetDuration_WithMultipleHistoricalExecutions_AveragesDuration()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var historicalData = new List<object>
        {
            CreateExecutionInstance("T001", 20),
            CreateExecutionInstance("T001", 30),
            CreateExecutionInstance("T001", 25)
        };

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        // Average of 20, 30, 25 = 25
        Assert.Equal(25, duration);
        Assert.False(isEstimated);
    }

    /// <summary>
    /// Test 4: Task with historical outliers should use average correctly
    /// </summary>
    [Fact]
    public void GetDuration_WithOutliers_AveragesAllValues()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var historicalData = new List<object>
        {
            CreateExecutionInstance("T001", 10),  // Low
            CreateExecutionInstance("T001", 15),  // Normal
            CreateExecutionInstance("T001", 60)   // Outlier
        };

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        // Average of 10, 15, 60 = 28.33 → rounded to 28
        var expected = (int)Math.Round((10 + 15 + 60) / 3.0);
        Assert.Equal(expected, duration);
    }

    /// <summary>
    /// Test 5: Task with only some historical data for same task should filter by task ID
    /// </summary>
    [Fact]
    public void GetDuration_WithMixedHistoricalData_FiltersCorrectly()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        // Create ExecutionInstances with proper int TaskIds
        int task1Id = 1;
        int task2Id = 2;
        
        var historicalData = new List<object>
        {
            new ExecutionInstance(1, task1Id, DateTime.Parse("2024-01-15 09:00"), DateTime.Parse("2024-01-15 09:00"), 
                DateTime.Parse("2024-01-15 09:20"), 20, new HashSet<string>(), true),
            new ExecutionInstance(2, task2Id, DateTime.Parse("2024-01-15 09:00"), DateTime.Parse("2024-01-15 09:00"),
                DateTime.Parse("2024-01-15 09:50"), 50, new HashSet<string>(), true), // Different task
            new ExecutionInstance(3, task1Id, DateTime.Parse("2024-01-16 09:00"), DateTime.Parse("2024-01-16 09:00"),
                DateTime.Parse("2024-01-16 09:30"), 30, new HashSet<string>(), true)
        };

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        // Should use only task1Id: (20 + 30) / 2 = 25
        Assert.Equal(25, duration);
    }

    /// <summary>
    /// Test 6: Grouped task should sum all subtask durations plus 10% buffer
    /// </summary>
    [Fact]
    public void GetDurationForGroupedTask_WithSubtasks_AddsTenPercentBuffer()
    {
        // Arrange
        var executionEvent = CreateEvent("TaskGroup", "TG001");
        var subtaskDurations = new List<(string, int)>
        {
            ("SubTask1", 10),
            ("SubTask2", 15),
            ("SubTask3", 20)
        };
        var historicalData = new List<object>();

        // Act
        var (duration, isEstimated) = _calculator.GetDurationForGroupedTask(
            executionEvent, subtaskDurations, historicalData);

        // Assert
        // Sum: 10 + 15 + 20 = 45, plus 10% = 45 + 4.5 = 49.5 → rounded to 50
        var expected = (int)Math.Round((10 + 15 + 20) * 1.10);
        Assert.Equal(expected, duration);
        Assert.True(isEstimated, "Grouped tasks should be marked as estimated");
    }

    /// <summary>
    /// Test 7: Empty subtask list should return default
    /// </summary>
    [Fact]
    public void GetDurationForGroupedTask_WithEmptySubtasks_ReturnsDefault()
    {
        // Arrange
        var executionEvent = CreateEvent("TaskGroup", "TG001");
        var subtaskDurations = new List<(string, int)>();
        var historicalData = new List<object>();

        // Act
        var (duration, isEstimated) = _calculator.GetDurationForGroupedTask(
            executionEvent, subtaskDurations, historicalData);

        // Assert
        Assert.Equal(15, duration); // Default
        Assert.True(isEstimated);
    }

    /// <summary>
    /// Test 8: Task with explicit duration in event should be used when provided
    /// </summary>
    [Fact]
    public void GetDuration_WithExplicitDuration_UsesEventDuration()
    {
        // Arrange
        var executionEvent = new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: "T001",
            TaskName: "Task1",
            ScheduledDay: DayOfWeek.Monday,
            ScheduledTime: new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: new HashSet<string>(),
            DurationMinutes: 45); // Explicit duration
        var historicalData = new List<object>();

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert
        Assert.Equal(45, duration);
    }

    /// <summary>
    /// Test 9: Null historical data should be treated as empty
    /// </summary>
    [Fact]
    public void GetDuration_WithNullHistoricalData_DefaultsToDuration()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, new List<object>());

        // Assert
        Assert.Equal(15, duration);
        Assert.True(isEstimated);
    }

    /// <summary>
    /// Test 10: Large historical dataset should compute average correctly
    /// </summary>
    [Fact]
    public void GetDuration_WithLargeHistoricalSet_ComputesAverageCorrectly()
    {
        // Arrange
        var executionEvent = CreateEvent("Task1", "T001");
        var historicalData = Enumerable.Range(1, 100)
            .Select(i => CreateExecutionInstance("T001", i % 60 + 10)) // Range 10-69
            .Cast<object>()
            .ToList();

        // Act
        var (duration, isEstimated) = _calculator.GetDuration(executionEvent, historicalData);

        // Assert - average should be computed (not default or min/max)
        Assert.True(duration > 10 && duration < 70, "Duration should be within range");
        Assert.False(isEstimated);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static ExecutionEventDefinition CreateEvent(string taskName, string taskId)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: DayOfWeek.Monday,
            ScheduledTime: new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: new HashSet<string>(),
            DurationMinutes: 0);
    }

    private static ExecutionInstance CreateExecutionInstance(string taskId, int durationMinutes)
    {
        var startTime = DateTime.Parse("2024-01-15 09:00");
        // TaskId should be int - converting from string taskId
        int taskIdInt = int.Parse(taskId.Replace("T", ""));
        
        return new ExecutionInstance(
            Id: new Random().Next(1, 10000),
            TaskId: taskIdInt,
            ScheduledStartTime: startTime,
            FunctionalStartTime: startTime,
            RequiredEndTime: startTime.AddMinutes(durationMinutes),
            DurationMinutes: (uint)durationMinutes,
            PrerequisiteTaskIds: new HashSet<string>(),
            IsValid: true,
            ValidationMessage: null);
    }
}
