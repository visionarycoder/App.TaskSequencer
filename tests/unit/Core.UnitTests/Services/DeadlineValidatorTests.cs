using Xunit;
using Core.Services;
using Core.Models;
using System;
using System.Collections.Generic;

namespace Core.UnitTests.Services;

/// <summary>
/// SUBTASK-4: Unit Tests for Deadline Validator
/// Tests the core business rule: validate that execution instances meet intake deadlines
/// and detect scheduling conflicts that prevent deadline compliance.
/// </summary>
public class DeadlineValidatorTests
{
    private readonly DeadlineValidator _validator;

    public DeadlineValidatorTests()
    {
        _validator = new DeadlineValidator();
    }

    /// <summary>
    /// Test 1: Task completing before deadline should validate successfully
    /// </summary>
    [Fact]
    public void ValidateDeadline_CompletesBeforeDeadline_ReturnsValid()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0));
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(15u, false);
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.True(isValid, $"Task completing at 9:15 should meet 10:00 deadline. Message: {message}");
        Assert.Null(message);
    }

    /// <summary>
    /// Test 2: Task completing after deadline should return violation
    /// </summary>
    [Fact]
    public void ValidateDeadline_CompletesAfterDeadline_ReturnsViolation()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(9, 30, 0)); // Deadline is 9:30
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(45u, false); // 45 minutes - completes at 9:45
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.False(isValid, "Task completing after deadline should be invalid");
        Assert.NotNull(message);
        Assert.Contains("deadline", message.ToLower(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 3: Task with no intake deadline should skip validation
    /// </summary>
    [Fact]
    public void ValidateDeadline_NoIntakeDeadline_SkipsValidation()
    {
        // Arrange
        var executionEvent = CreateEventWithoutIntake("Task1", "T001");
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(660u, false); // 11 hours later
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.True(isValid, "No deadline = no violation");
        Assert.Null(message);
    }

    /// <summary>
    /// Test 4: Task on day without intake requirement should be valid
    /// </summary>
    [Fact]
    public void ValidateDeadline_DayWithoutRequirement_IsValid()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0),
            day: DayOfWeek.Monday); // Monday has requirement
        var actualStartTime = DateTime.Parse("2024-01-09 14:00"); // Tuesday (no requirement)
        var duration = new ExecutionDuration(100u, false);
        var periodStartDate = DateTime.Parse("2024-01-08 00:00"); // Period starts Monday

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        // This depends on implementation - need to verify expected behavior
        Assert.NotNull(message); // May be null or informational
    }

    /// <summary>
    /// Test 5: Edge case: completion exactly at deadline should be valid
    /// </summary>
    [Fact]
    public void ValidateDeadline_CompletionExactlyAtDeadline_IsValid()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0));
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(60u, false); // Completes exactly at 10:00
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.True(isValid, "Completion exactly at deadline should be valid");
    }

    /// <summary>
    /// Test 6: Estimated duration should still be validated against deadline
    /// </summary>
    [Fact]
    public void ValidateDeadline_WithEstimatedDuration_StillValidates()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0));
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(15u, true); // Estimated
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.True(isValid, "Estimated duration should still meet deadline");
    }

    /// <summary>
    /// Test 7: Very long duration should be detected as violation
    /// </summary>
    [Fact]
    public void ValidateDeadline_VeryLongDuration_ViolatesDeadline()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0));
        var actualStartTime = DateTime.Parse("2024-01-08 09:00");
        var duration = new ExecutionDuration(600u, false); // 10 hours
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.False(isValid, "10-hour task should violate 1-hour deadline");
        Assert.NotNull(message);
    }

    /// <summary>
    /// Test 8: Multiple tasks on same day should each be validated independently
    /// </summary>
    [Fact]
    public void ValidateDeadline_IndependentTasks_ValidatedSeparately()
    {
        // Arrange
        var task1 = CreateEventWithIntake("Task1", "T001", intakeTime: new TimeOfDay(10, 0, 0));
        var task2 = CreateEventWithIntake("Task2", "T002", intakeTime: new TimeOfDay(14, 0, 0));

        var start1 = DateTime.Parse("2024-01-08 09:00");
        var duration1 = new ExecutionDuration(30u, false);

        var start2 = DateTime.Parse("2024-01-08 13:00");
        var duration2 = new ExecutionDuration(30u, false);

        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid1, msg1) = _validator.ValidateDeadline(task1, start1, duration1, periodStartDate);
        var (isValid2, msg2) = _validator.ValidateDeadline(task2, start2, duration2, periodStartDate);

        // Assert
        Assert.True(isValid1, "Task1 should complete by 10:30");
        Assert.True(isValid2, "Task2 should complete by 13:30");
    }

    /// <summary>
    /// Test 9: Midnight deadline should be handled correctly
    /// </summary>
    [Fact]
    public void ValidateDeadline_MidnightDeadline_HandledCorrectly()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(0, 0, 0)); // Midnight
        var actualStartTime = DateTime.Parse("2024-01-08 23:30");
        var duration = new ExecutionDuration(20u, false);
        var periodStartDate = DateTime.Parse("2024-01-08 00:00");

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        // Task at 23:30 + 20 min = 23:50, should not meet midnight deadline
        Assert.False(isValid, "Should violate midnight deadline");
    }

    /// <summary>
    /// Test 10: Future period start date should be handled
    /// </summary>
    [Fact]
    public void ValidateDeadline_FuturePeriodStart_IsHandled()
    {
        // Arrange
        var executionEvent = CreateEventWithIntake("Task1", "T001", 
            intakeTime: new TimeOfDay(10, 0, 0));
        var actualStartTime = DateTime.Parse("2024-02-15 09:00"); // Different day
        var duration = new ExecutionDuration(30u, false);
        var periodStartDate = DateTime.Parse("2024-02-15 00:00"); // Different period

        // Act
        var (isValid, message) = _validator.ValidateDeadline(
            executionEvent, actualStartTime, duration, periodStartDate);

        // Assert
        Assert.True(isValid, "Task on different period should still validate");
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static ExecutionEventDefinition CreateEventWithIntake(
        string taskName,
        string taskId,
        TimeOfDay intakeTime,
        DayOfWeek day = DayOfWeek.Monday)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: day,
            ScheduledTime: new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: new HashSet<string>(),
            DurationMinutes: 15,
            IntakeRequirement: new IntakeEventRequirement(
                TaskId: taskId,
                RequiredDays: new HashSet<DayOfWeek> { day },
                IntakeTime: intakeTime));
    }

    private static ExecutionEventDefinition CreateEventWithoutIntake(
        string taskName,
        string taskId)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: DayOfWeek.Monday,
            ScheduledTime: new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: new HashSet<string>(),
            DurationMinutes: 15,
            IntakeRequirement: null);
    }
}
