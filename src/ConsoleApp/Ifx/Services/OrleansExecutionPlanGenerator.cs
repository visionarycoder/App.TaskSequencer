using ConsoleApp.Ifx.Models;
using ConsoleApp.Ifx.Orleans.Grains;
using Orleans;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleApp.Ifx.Services;

/// <summary>
/// Orleans-based execution plan generator using iterative grain-based calculation.
/// Uses grains to coordinate time slot refinement until convergence.
/// </summary>
public class OrleansExecutionPlanGenerator
{
    private readonly ManifestCsvParser _csvParser;
    private readonly ManifestTransformer _transformer;
    private readonly ExecutionEventMatrixBuilder _matrixBuilder;
    private readonly DependencyResolver _dependencyResolver;
    private readonly DeadlineValidator _deadlineValidator;
    private IGrainFactory? _grainFactory;
    private object? _host; // ISiloHost

    public OrleansExecutionPlanGenerator(
        ManifestCsvParser? csvParser = null,
        ManifestTransformer? transformer = null,
        ExecutionEventMatrixBuilder? matrixBuilder = null,
        DependencyResolver? dependencyResolver = null,
        DeadlineValidator? deadlineValidator = null)
    {
        _csvParser = csvParser ?? new ManifestCsvParser();
        _transformer = transformer ?? new ManifestTransformer();
        _matrixBuilder = matrixBuilder ?? new ExecutionEventMatrixBuilder();
        _dependencyResolver = dependencyResolver ?? new DependencyResolver();
        _deadlineValidator = deadlineValidator ?? new DeadlineValidator();
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
            await StartOrleansAsync();

            // Default to today
            periodStartDate ??= DateTime.Now.Date;

            // Phase 0: Load and parse CSV files
            var (taskManifests, intakeEventManifests, durationManifests) =
                _csvParser.ParseAll(taskDefinitionPath, intakeEventPath, durationHistoryPath);

            // Phase 1: Transform to domain models
            var intakeRequirementsLookup = TransformIntakeEvents(intakeEventManifests);
            var durationLookup = BuildDurationLookup(durationManifests, taskManifests);
            var taskDefinitions = taskManifests.Select(m =>
                _transformer.TransformTaskDefinition(m, intakeRequirementsLookup)).ToList();

            // Phase 2: Build execution event matrix
            var executionEvents = _matrixBuilder.BuildCompleteExecutionEventMatrix(taskDefinitions);

            // Phase 3: Initial execution instances from sequential resolution (baseline)
            var initialInstances = ResolveAndValidate(
                executionEvents,
                durationLookup,
                periodStartDate.Value);

            // Phase 4: Use Orleans grains for iterative refinement
            var coordinator = _grainFactory!.GetGrain<IExecutionPlanCoordinatorGrain>("coordinator");
            var finalPlan = await coordinator.CalculateExecutionPlanAsync(
                executionEvents.AsReadOnly(),
                initialInstances.AsReadOnly(),
                periodStartDate.Value);

            return finalPlan;
        }
        finally
        {
            await StopOrleansAsync();
        }
    }

    private async Task StartOrleansAsync()
    {
        if (_host != null)
            return;

        var builder = new SiloHostBuilder()
            .UseLocalhostClustering()
            .ConfigureApplicationParts(parts =>
            {
                parts.AddApplicationPart(typeof(ExecutionTaskGrain).Assembly)
                    .WithReferences();
            });

        _host = builder.Build();
        await _host.StartAsync();
        _grainFactory = _host.Services.GetRequiredService<IGrainFactory>();
    }

    private async Task StopOrleansAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
            _grainFactory = null;
        }
    }

    private Dictionary<string, IntakeEventRequirement> TransformIntakeEvents(
        List<IntakeEventManifest> intakeEventManifests)
    {
        var lookup = new Dictionary<string, IntakeEventRequirement>();

        foreach (var manifest in intakeEventManifests)
        {
            var requirement = _transformer.TransformIntakeEvent(manifest);
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

            var duration = _transformer.TransformExecutionDuration(manifest);
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
            var resolvedPrerequisites = _dependencyResolver.ResolvePrerequisites(executionEvent, executionEvents);
            var duration = ExecutionDuration.Default();
            var scheduledStart = ApplyTimeToDateForWeek(executionEvent.ScheduledDay, executionEvent.ScheduledTime, periodStartDate);
            var adjustedStart = _dependencyResolver.CalculateAdjustedStartTime(
                executionEvent, resolvedPrerequisites, eventTimingLookup, periodStartDate);
            var plannedCompletion = adjustedStart.Add(duration.ToTimeSpan());

            eventTimingLookup[eventKey] = (scheduledStart, plannedCompletion, duration);

            var (isValidDeadline, deadlineMessage) = _deadlineValidator.ValidateDeadline(
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
