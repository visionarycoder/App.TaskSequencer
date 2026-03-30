using App.TaskSequencer.Domain.Foundation;
using App.TaskSequencer.Domain.Models;
using UtilsHelper = App.TaskSequencer.Infrastructure.Utils.Utils;

namespace App.TaskSequencer.BusinessLogic.Services;

/// <summary>
/// Transforms raw CSV manifest records into strongly-typed domain models.
/// Handles validation, parsing, and enrichment of data from all three CSV sources.
/// </summary>
public class ManifestTransformer
{
    /// <summary>
    /// Transforms TaskDefinitionManifest → TaskDefinitionEnhanced with linked intake requirements.
    /// </summary>
    public TaskDefinitionEnhanced TransformTaskDefinition(
        TaskDefinitionManifest manifest,
        Dictionary<string, IntakeEventRequirement> intakeRequirementsLookup)
    {
        ValidateTaskDefinitionManifest(manifest);

        var uid = Guid.NewGuid();
        var prerequisites = UtilsHelper.ParsePrerequisites(manifest.Prerequisites);
        var executionType = ParseExecutionType(manifest.ExecutionType);
        var scheduleType = ParseScheduleType(manifest.ScheduleType);
        var durationMinutes = UtilsHelper.ParseDurationMinutes(manifest.DurationMinutes);

        // Parse execution days
        var scheduledDays = ParseExecutionDays(manifest.ExecutionDays);

        // Parse execution times
        var scheduledTimes = ParseExecutionTimes(manifest.ExecutionTimes);

        // Link intake requirement if available
        var intakeRequirement = intakeRequirementsLookup.TryGetValue(manifest.TaskId, out var intake)
            ? intake
            : null;

        return new TaskDefinitionEnhanced(
            Uid: uid,
            TaskId: manifest.TaskId,
            TaskName: manifest.TaskName,
            DurationMinutes: durationMinutes,
            PrerequisiteIds: prerequisites,
            ExecutionType: executionType,
            ScheduleType: scheduleType,
            ScheduledDays: scheduledDays,
            ScheduledTimes: scheduledTimes,
            IntakeRequirement: intakeRequirement
        );
    }

    /// <summary>
    /// Transforms IntakeEventManifest → IntakeEventRequirement.
    /// </summary>
    public IntakeEventRequirement TransformIntakeEvent(IntakeEventManifest manifest)
    {
        ValidateIntakeEventManifest(manifest);

        var requiredDays = new HashSet<DayOfWeek>();

        if (!string.IsNullOrWhiteSpace(manifest.Monday))
            requiredDays.Add(DayOfWeek.Monday);
        if (!string.IsNullOrWhiteSpace(manifest.Tuesday))
            requiredDays.Add(DayOfWeek.Tuesday);
        if (!string.IsNullOrWhiteSpace(manifest.Wednesday))
            requiredDays.Add(DayOfWeek.Wednesday);
        if (!string.IsNullOrWhiteSpace(manifest.Thursday))
            requiredDays.Add(DayOfWeek.Thursday);
        if (!string.IsNullOrWhiteSpace(manifest.Friday))
            requiredDays.Add(DayOfWeek.Friday);
        if (!string.IsNullOrWhiteSpace(manifest.Saturday))
            requiredDays.Add(DayOfWeek.Saturday);
        if (!string.IsNullOrWhiteSpace(manifest.Sunday))
            requiredDays.Add(DayOfWeek.Sunday);

        var intakeTime = TimeOfDay.Parse(manifest.IntakeTime);

        return new IntakeEventRequirement(
            TaskId: manifest.TaskId,
            RequiredDays: requiredDays.AsReadOnly(),
            IntakeTime: intakeTime
        );
    }

    /// <summary>
    /// Transforms ExecutionDurationManifest → ExecutionDuration with status checking.
    /// </summary>
    public ExecutionDuration TransformExecutionDuration(ExecutionDurationManifest manifest)
    {
        ValidateExecutionDurationManifest(manifest);

        if (!uint.TryParse(manifest.ActualDurationMinutes, out var minutes))
            return ExecutionDuration.Default();

        // If status is "Completed", use actual duration; otherwise mark as pending replacement
        if (manifest.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            return ExecutionDuration.Actual(minutes);
        else
            return ExecutionDuration.PendingReplacement(minutes);
    }

    /// <summary>
    /// Parses execution type string.
    /// </summary>
    private ExecutionType ParseExecutionType(string executionTypeString)
    {
        return executionTypeString.Trim().ToUpperInvariant() switch
        {
            "SCHEDULED" => ExecutionType.Scheduled,
            "ONDEMAND" => ExecutionType.OnDemand,
            _ => ExecutionType.Scheduled
        };
    }

    /// <summary>
    /// Parses schedule type string.
    /// </summary>
    private ScheduleType ParseScheduleType(string scheduleTypeString)
    {
        return scheduleTypeString.Trim().ToUpperInvariant() switch
        {
            "RECURRING" => ScheduleType.Recurring,
            "ONEOFF" => ScheduleType.OneOff,
            _ => ScheduleType.Recurring
        };
    }

    /// <summary>
    /// Parses pipe-separated execution days.
    /// </summary>
    private IReadOnlySet<DayOfWeek> ParseExecutionDays(string executionDaysString)
    {
        if (string.IsNullOrWhiteSpace(executionDaysString))
            return new HashSet<DayOfWeek>().AsReadOnly();

        var days = new HashSet<DayOfWeek>();
        var parts = executionDaysString.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (Enum.TryParse<DayOfWeek>(part, ignoreCase: true, out var day))
                days.Add(day);
        }

        return days.AsReadOnly();
    }

    /// <summary>
    /// Parses pipe-separated execution times.
    /// </summary>
    private IReadOnlyList<TimeOfDay> ParseExecutionTimes(string executionTimesString)
    {
        if (string.IsNullOrWhiteSpace(executionTimesString))
            return new List<TimeOfDay>().AsReadOnly();

        var times = new List<TimeOfDay>();
        var parts = executionTimesString.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            try
            {
                var time = TimeOfDay.Parse(part);
                times.Add(time);
            }
            catch (ArgumentException)
            {
                // Skip invalid times
            }
        }

        return times.AsReadOnly();
    }

    /// <summary>
    /// Validates TaskDefinitionManifest.
    /// </summary>
    private void ValidateTaskDefinitionManifest(TaskDefinitionManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.TaskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(manifest.TaskId));

        if (string.IsNullOrWhiteSpace(manifest.TaskName))
            throw new ArgumentException("Task name cannot be empty", nameof(manifest.TaskName));
    }

    /// <summary>
    /// Validates IntakeEventManifest.
    /// </summary>
    private void ValidateIntakeEventManifest(IntakeEventManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.TaskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(manifest.TaskId));

        if (string.IsNullOrWhiteSpace(manifest.IntakeTime))
            throw new ArgumentException("Intake time cannot be empty", nameof(manifest.IntakeTime));
    }

    /// <summary>
    /// Validates ExecutionDurationManifest.
    /// </summary>
    private void ValidateExecutionDurationManifest(ExecutionDurationManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.TaskId))
            throw new ArgumentException("Task ID cannot be empty", nameof(manifest.TaskId));

        if (string.IsNullOrWhiteSpace(manifest.ExecutionDate))
            throw new ArgumentException("Execution date cannot be empty", nameof(manifest.ExecutionDate));

        if (string.IsNullOrWhiteSpace(manifest.ExecutionTime))
            throw new ArgumentException("Execution time cannot be empty", nameof(manifest.ExecutionTime));
    }
}
