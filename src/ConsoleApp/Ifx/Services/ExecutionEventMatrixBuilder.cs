using ConsoleApp.Ifx.Models;

namespace ConsoleApp.Ifx.Services;

/// <summary>
/// Generates ExecutionEventDefinition instances from TaskDefinitionEnhanced.
/// Creates all day × time combinations (the execution event matrix).
/// </summary>
public class ExecutionEventMatrixBuilder
{
    /// <summary>
    /// Builds execution event matrix for a single task definition.
    /// </summary>
    public List<ExecutionEventDefinition> BuildExecutionEventMatrix(TaskDefinitionEnhanced taskDef)
    {
        var events = new List<ExecutionEventDefinition>();

        // For Scheduled tasks only
        if (taskDef.ExecutionType != ExecutionType.Scheduled)
            return events;

        // For OnDemand or empty schedule, return empty
        if (taskDef.ScheduledDays.Count == 0 || taskDef.ScheduledTimes.Count == 0)
            return events;

        // Create matrix: every day × every time combination
        foreach (var day in taskDef.ScheduledDays)
        {
            foreach (var time in taskDef.ScheduledTimes)
            {
                var eventDef = new ExecutionEventDefinition(
                    TaskUid: taskDef.Uid,
                    TaskId: taskDef.TaskId,
                    TaskName: taskDef.TaskName,
                    ScheduledDay: day,
                    ScheduledTime: time,
                    PrerequisiteTaskIds: taskDef.PrerequisiteIds,
                    DurationMinutes: taskDef.DurationMinutes,
                    IntakeRequirement: taskDef.IntakeRequirement
                );

                events.Add(eventDef);
            }
        }

        return events;
    }

    /// <summary>
    /// Builds complete execution event matrix for all tasks.
    /// </summary>
    public List<ExecutionEventDefinition> BuildCompleteExecutionEventMatrix(
        IEnumerable<TaskDefinitionEnhanced> taskDefinitions)
    {
        var allEvents = new List<ExecutionEventDefinition>();

        foreach (var taskDef in taskDefinitions)
        {
            var events = BuildExecutionEventMatrix(taskDef);
            allEvents.AddRange(events);
        }

        return allEvents;
    }
}
