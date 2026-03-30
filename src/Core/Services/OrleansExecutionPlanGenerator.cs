using App.TaskSequencer.Domain.Foundation;
using App.TaskSequencer.Domain.Models;
using App.TaskSequencer.Orchestration.Orleans.Grains;
using App.TaskSequencer.BusinessLogic.Services;
using App.TaskSequencer.Infrastructure.Persistence;
using Orleans;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace App.TaskSequencer.Orchestration.Generators;

/// <summary>
/// Orleans-based execution plan generator using iterative grain-based calculation.
/// Uses grains to coordinate time slot refinement until convergence.
/// </summary>
public class OrleansExecutionPlanGenerator
{
    private readonly ManifestCsvParser csvParser;
    private readonly ManifestTransformer transformer;
    private readonly ExecutionEventMatrixBuilder matrixBuilder;
    private readonly DependencyResolver dependencyResolver;
    private readonly DeadlineValidator deadlineValidator;
    private IGrainFactory? grainFactory;
    private object? host;

    public OrleansExecutionPlanGenerator(
        ManifestCsvParser? csvParser = null,
        ManifestTransformer? transformer = null,
        ExecutionEventMatrixBuilder? matrixBuilder = null,
        DependencyResolver? dependencyResolver = null,
        DeadlineValidator? deadlineValidator = null)
    {
        this.csvParser = csvParser ?? new ManifestCsvParser();
        this.transformer = transformer ?? new ManifestTransformer();
        this.matrixBuilder = matrixBuilder ?? new ExecutionEventMatrixBuilder();
        this.dependencyResolver = dependencyResolver ?? new DependencyResolver();
        this.deadlineValidator = deadlineValidator ?? new DeadlineValidator();
    }

    /// <summary>
    /// Generates execution plan using Orleans grains for iterative calculation.
    /// </summary>
    public async Task<ExecutionPlan> GenerateExecutionPlanAsync(
        string taskDefinitionPath,
        string intakeEventPath,
        string? durationHistoryPath = null,
        DateTime? periodStartDate = null)
    {
        try
        {
            // Start Orleans silo in-process
            await this.StartOrleansAsync();

            // Default to today
            periodStartDate ??= DateTime.Now.Date;

            // Phase 0: Load and parse CSV files
            var (taskManifests, intakeEventManifests, durationManifests) =
                this.csvParser.ParseAll(taskDefinitionPath, intakeEventPath, durationHistoryPath);

            // Phase 1: Transform to domain models
            var intakeRequirementsLookup = this.TransformIntakeEvents(intakeEventManifests);
            var durationLookup = this.BuildDurationLookup(durationManifests, taskManifests);
            var taskDefinitions = taskManifests.Select(m =>
                this.transformer.TransformTaskDefinition(m, intakeRequirementsLookup)).ToList();

            // Phase 2: Build execution event matrix
            var executionEvents = this.matrixBuilder.BuildCompleteExecutionEventMatrix(taskDefinitions);

            // Phase 3: Initial execution instances from sequential resolution (baseline)
            var initialInstances = this.ResolveAndValidate(
                executionEvents,
                durationLookup,
                periodStartDate.Value);

            // Phase 4: Use Orleans grains for iterative refinement
            var coordinator = this.grainFactory!.GetGrain<IExecutionPlanCoordinatorGrain>("coordinator");
            var finalPlan = await coordinator.CalculateExecutionPlanAsync(
                executionEvents.AsReadOnly(),
                initialInstances.AsReadOnly(),
                periodStartDate.Value,
                CancellationToken.None);

            return finalPlan;
        }
        finally
        {
            await this.StopOrleansAsync();
        }
    }

    private async Task StartOrleansAsync()
    {
        if (this.host != null)
            return;

        try
        {
            var builderType = Type.GetType("Orleans.Hosting.SiloHostBuilder");
            if (builderType == null)
                throw new NotSupportedException("Orleans.Hosting.SiloHostBuilder not available in current Orleans version");

            // Simplified Orleans setup - full implementation pending
            throw new NotImplementedException("Orleans integration requires API version compatibility");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize Orleans", ex);
        }
    }

    private async Task StopOrleansAsync()
    {
        if (this.host != null)
        {
            try
            {
                var stopAsyncMethod = this.host.GetType().GetMethod("StopAsync");
                if (stopAsyncMethod != null)
                {
                    await (Task)stopAsyncMethod.Invoke(this.host, null)!;
                }

                var disposeMethod = this.host.GetType().GetMethod("Dispose");
                disposeMethod?.Invoke(this.host, null);

                this.host = null;
                this.grainFactory = null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to stop Orleans", ex);
            }
        }
    }

    private Dictionary<string, IntakeEventRequirement> TransformIntakeEvents(
        List<IntakeEventManifest> intakeEventManifests)
    {
        var lookup = new Dictionary<string, IntakeEventRequirement>();

        foreach (var manifest in intakeEventManifests)
        {
            var requirement = this.transformer.TransformIntakeEvent(manifest);
            lookup[manifest.TaskId] = requirement;
        }

        return lookup;
    }

    private Dictionary<(string TaskId, DateTime Date, TimeOfDay Time), ExecutionDuration> BuildDurationLookup(
        List<ExecutionDurationManifest> durationManifests,
        List<TaskDefinitionManifest> taskDefinitions)
    {
        var lookup = new Dictionary<(string, DateTime, TimeOfDay), ExecutionDuration>();

        foreach (var manifest in durationManifests)
        {
            if (!DateTime.TryParse(manifest.ExecutionDate, out var executionDate))
                continue;

            var executionTime = ParseTimeOfDay(manifest.ExecutionTime);
            if (executionTime == null)
                continue;

            var duration = this.transformer.TransformExecutionDuration(manifest);
            lookup[(manifest.TaskId, executionDate, executionTime)] = duration;
        }

        return lookup;
    }

    /// <summary>
    /// Initial sequential resolution (same as before) - used as baseline for grain iteration.
    /// </summary>
    private List<ExecutionInstanceEnhanced> ResolveAndValidate(
        List<ExecutionEventDefinition> executionEvents,
        Dictionary<(string TaskId, DateTime Date, TimeOfDay Time), ExecutionDuration> durationLookup,
        DateTime periodStartDate)
    {
        var instances = new List<ExecutionInstanceEnhanced>();
        var validationResults = new Dictionary<string, (bool IsValid, string? Message)>();
        var eventTimingLookup = new Dictionary<string, (DateTime ScheduledStart, DateTime PlannedCompletion, ExecutionDuration Duration)>();

        var instanceId = 1;

        foreach (var executionEvent in executionEvents)
        {
            var eventKey = executionEvent.GetExecutionEventKey();
            var resolvedPrerequisites = this.dependencyResolver.ResolvePrerequisites(executionEvent, executionEvents);
            var duration = ExecutionDuration.Default();
            var scheduledStart = ApplyTimeToDateForWeek(executionEvent.ScheduledDay, executionEvent.ScheduledTime, periodStartDate);
            var adjustedStart = this.dependencyResolver.CalculateAdjustedStartTime(
                executionEvent, resolvedPrerequisites, eventTimingLookup, periodStartDate);
            var plannedCompletion = adjustedStart.Add(duration.ToTimeSpan());

            eventTimingLookup[eventKey] = (scheduledStart, plannedCompletion, duration);

            var (isValidDeadline, deadlineMessage) = this.deadlineValidator.ValidateDeadline(
                executionEvent, adjustedStart, duration, periodStartDate);

            var isValid = isValidDeadline;
            var status = isValid ? ExecutionStatus.ReadyToExecute : ExecutionStatus.DeadlineMiss;

            var deadline = executionEvent.IntakeRequirement?.MustCompleteByIntake(executionEvent.ScheduledDay) ?? false
                ? executionEvent.IntakeRequirement?.GetIntakeDeadline(scheduledStart)
                : null;

            var instance = new ExecutionInstanceEnhanced(
                Id: instanceId++,
                TaskId: -1,
                TaskIdString: executionEvent.TaskId,
                TaskName: executionEvent.TaskName,
                ScheduledStartTime: scheduledStart,
                FunctionalStartTime: adjustedStart != scheduledStart ? adjustedStart : null,
                RequiredEndTime: deadline,
                Duration: duration,
                PlannedCompletionTime: plannedCompletion,
                PrerequisiteTaskIds: new HashSet<string>(resolvedPrerequisites.Select(p => p.TaskId)).AsReadOnly(),
                IsValid: isValid,
                Status: status,
                ValidationMessage: deadlineMessage
            );

            instances.Add(instance);
        }

        return instances;
    }

    private DateTime ApplyTimeToDateForWeek(
        DayOfWeek scheduleDay,
        TimeOfDay scheduleTime,
        DateTime periodStart)
    {
        var periodDayOfWeek = periodStart.DayOfWeek;
        var daysToAdd = ((int)scheduleDay - (int)periodDayOfWeek + 7) % 7;
        var targetDate = periodStart.AddDays(daysToAdd);
        return scheduleTime.ApplyToDate(targetDate);
    }

    private TimeOfDay? ParseTimeOfDay(string timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return null;

        try
        {
            return TimeOfDay.Parse(timeString);
        }
        catch
        {
            return null;
        }
    }
}
