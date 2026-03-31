namespace App.TaskSequencer.Contracts.Models;

/// <summary>
/// DTOs and models for Orleans grain communication (serializable for RPC)
/// </summary>
/// 
public record ExecutionInstanceEnhanced(
    string TaskIdString,
    string TaskName,
    DateTime ScheduledStartTime,
    DateTime? FunctionalStartTime,
    DateTime PlannedCompletionTime,
    DateTime? RequiredEndTime,
    string ExecutionStatus);

public record ExecutionEventDefinition(
    string TaskId,
    string TaskName,
    TimeSpan DefaultDuration,
    DateTime DeadlineTime,
    IReadOnlySet<string> PrerequisiteTasks);

public record ExecutionDuration(
    string TaskId,
    uint DurationSeconds,
    DateTime? LastActualDuration);

public record ExecutionPlan(
    string IncrementId,
    int TotalValidTasks,
    int TotalInvalidTasks,
    DateTime CriticalPathCompletion,
    IReadOnlyList<string> TaskChain,
    IReadOnlyList<ExecutionInstanceEnhanced> Tasks,
    IReadOnlySet<string> DeadlineMisses);
