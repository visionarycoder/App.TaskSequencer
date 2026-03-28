using ConsoleApp.Ifx.Models;

namespace ConsoleApp.Ifx.Services;

/// <summary>
/// Generates the complete execution plan from raw manifests.
/// Orchestrates the entire pipeline: parsing → transformation → matrix building → dependency resolution → validation → plan generation.
/// This is the main entry point for batch execution sequencing.
/// </summary>
public class ExecutionPlanGenerator
{
    private readonly ManifestCsvParser csvParser;
    private readonly ManifestTransformer transformer;
    private readonly ExecutionEventMatrixBuilder matrixBuilder;
    private readonly DependencyResolver dependencyResolver;
    private readonly DeadlineValidator deadlineValidator;

    public ExecutionPlanGenerator(
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
    /// Generates complete execution plan from CSV files.
    /// </summary>
    public ExecutionPlan GenerateExecutionPlan(
        string taskDefinitionPath,
        string intakeEventPath,
        string? durationHistoryPath = null,
        DateTime? periodStartDate = null)
    {
        // Default to today as period start
        periodStartDate ??= DateTime.Now.Date;

        // Phase 0: Load and parse all CSV files
        var (taskManifests, intakeEventManifests, durationManifests) =
            this.csvParser.ParseAll(taskDefinitionPath, intakeEventPath, durationHistoryPath);

        // Phase 0.5: Transform intake events to requirements lookup
        var intakeRequirementsLookup = this.TransformIntakeEvents(intakeEventManifests);

        // Phase 1: Build duration lookup from history (or use defaults)
        var durationLookup = this.BuildDurationLookup(durationManifests, taskManifests);

        // Phase 2: Transform task manifests to enhanced definitions
        var taskDefinitions = taskManifests.Select(m =>
            this.transformer.TransformTaskDefinition(m, intakeRequirementsLookup)).ToList();

        // Phase 3: Build execution event matrix
        var executionEvents = this.matrixBuilder.BuildCompleteExecutionEventMatrix(taskDefinitions);

        // Phase 4: Resolve dependencies and validate
        var executionInstances = this.ResolveAndValidate(
            executionEvents,
            durationLookup,
            periodStartDate.Value);

        // Phase 5: Generate execution plan
        var executionPlan = this.BuildExecutionPlan(
            executionInstances,
            periodStartDate.Value);

        return executionPlan;
    }

    /// <summary>
    /// Transforms intake event manifests to a lookup dictionary.
    /// </summary>
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

    /// <summary>
    /// Builds duration lookup from execution history.
    /// Returns default 15-minute duration for tasks without history.
    /// </summary>
    private Dictionary<(string TaskId, DateTime Date, TimeOfDay Time), ExecutionDuration> BuildDurationLookup(
        List<ExecutionDurationManifest> durationManifests,
        List<TaskDefinitionManifest> taskDefinitions)
    {
        var lookup = new Dictionary<(string, DateTime, TimeOfDay), ExecutionDuration>();

        // Add actual durations from history
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
    /// Resolves dependencies and validates all execution events.
    /// Returns enhanced execution instances ready for plan generation.
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

        // First pass: resolve dependencies and calculate timing
        foreach (var executionEvent in executionEvents)
        {
            var eventKey = executionEvent.GetExecutionEventKey();

            // Resolve prerequisites
            var resolvedPrerequisites = this.dependencyResolver.ResolvePrerequisites(
                executionEvent,
                executionEvents);

            // Get duration (actual from history or default 15 min)
            var duration = this.GetDuration(executionEvent, durationLookup);

            // Calculate scheduled start time
            var scheduledStart = this.ApplyTimeToDateForWeek(
                executionEvent.ScheduledDay,
                executionEvent.ScheduledTime,
                periodStartDate);

            // Calculate adjusted start (accounting for prerequisites)
            var adjustedStart = this.dependencyResolver.CalculateAdjustedStartTime(
                executionEvent,
                resolvedPrerequisites,
                eventTimingLookup,
                periodStartDate);

            // Calculate planned completion
            var plannedCompletion = adjustedStart.Add(duration.ToTimeSpan());

            // Store timing for subsequent prerequisite calculations
            eventTimingLookup[eventKey] = (scheduledStart, plannedCompletion, duration);

            // Validate deadline compliance
            var (isValidDeadline, deadlineMessage) = this.deadlineValidator.ValidateDeadline(
                executionEvent,
                adjustedStart,
                duration,
                periodStartDate);

            // Check for cascading failures from invalid prerequisites
            var invalidDueToPrereq = this.dependencyResolver.ResolvePrerequisites(
                executionEvent,
                executionEvents).Count > 0 &&
                this.dependencyResolver.ResolvePrerequisites(
                    executionEvent,
                    executionEvents).Any(p =>
                    {
                        var pKey = p.GetExecutionEventKey();
                        return validationResults.TryGetValue(pKey, out var r) && !r.IsValid;
                    });

            var isValid = isValidDeadline && !invalidDueToPrereq;
            var validationMessage = !isValid
                ? (deadlineMessage ?? "Invalid due to prerequisite failure")
                : null;

            validationResults[eventKey] = (isValid, validationMessage);

            // Get intake deadline (if applicable)
            var deadline = executionEvent.IntakeRequirement?.MustCompleteByIntake(executionEvent.ScheduledDay) ?? false
                ? executionEvent.IntakeRequirement?.GetIntakeDeadline(scheduledStart)
                : null;

            // Determine status
            var status = isValid
                ? ExecutionStatus.ReadyToExecute
                : (invalidDueToPrereq ? ExecutionStatus.Invalid : ExecutionStatus.DeadlineMiss);

            // Create enhanced execution instance
            var instance = new ExecutionInstanceEnhanced(
                Id: instanceId++,
                TaskId: -1, // TODO: Link to actual task ID
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
                ValidationMessage: validationMessage
            );

            instances.Add(instance);
        }

        return instances;
    }

    /// <summary>
    /// Gets duration for execution event (from history or default).
    /// </summary>
    private ExecutionDuration GetDuration(
        ExecutionEventDefinition executionEvent,
        Dictionary<(string TaskId, DateTime Date, TimeOfDay Time), ExecutionDuration> durationLookup)
    {
        // For now, use default 15-minute duration
        // In production, would match from history based on task pattern
        return ExecutionDuration.Default();
    }

    /// <summary>
    /// Applies a day-of-week + time-of-day to a period start date.
    /// </summary>
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

    /// <summary>
    /// Builds final execution plan from resolved instances.
    /// </summary>
    private ExecutionPlan BuildExecutionPlan(
        List<ExecutionInstanceEnhanced> executionInstances,
        DateTime periodStartDate)
    {
        var validInstances = executionInstances.Where(i => i.IsValid).ToList();
        var invalidInstances = executionInstances.Where(i => !i.IsValid).ToList();
        var deadlineMisses = invalidInstances
            .Where(i => i.Status == ExecutionStatus.DeadlineMiss)
            .Select(i => i.TaskIdString)
            .Distinct()
            .ToList();
        var dstWarnings = new List<string>(); // TODO: Implement DST detection

        // Build task chain via depth-first traversal
        var taskChain = BuildTaskChain(validInstances);

        // Calculate critical path completion
        var criticalPathCompletion = validInstances.Count > 0
            ? validInstances.Max(i => i.PlannedCompletionTime)
            : (DateTime?)null;

        var incrementId = periodStartDate.ToString("yyyy-MM-dd");
        var incrementEnd = periodStartDate.AddDays(7); // Assume weekly increment

        return new ExecutionPlan(
            IncrementId: incrementId,
            IncrementStart: periodStartDate,
            IncrementEnd: incrementEnd,
            Tasks: validInstances.AsReadOnly(),
            TaskChain: taskChain.AsReadOnly(),
            TotalValidTasks: validInstances.Count,
            TotalInvalidTasks: invalidInstances.Count,
            CriticalPathCompletion: criticalPathCompletion,
            DeadlineMisses: deadlineMisses.AsReadOnly(),
            DSTWarnings: dstWarnings.AsReadOnly()
        );
    }

    /// <summary>
    /// Builds task chain via depth-first traversal of dependency graph.
    /// </summary>
    private List<string> BuildTaskChain(List<ExecutionInstanceEnhanced> validInstances)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();

        // Find root tasks (no prerequisites)
        var rootTasks = validInstances
            .Where(t => t.PrerequisiteTaskIds.Count == 0)
            .ToList();

        foreach (var root in rootTasks)
        {
            TraverseDepthFirst(root.TaskIdString, validInstances, visited, chain);
        }

        return chain;
    }

    private void TraverseDepthFirst(
        string taskId,
        List<ExecutionInstanceEnhanced> validInstances,
        HashSet<string> visited,
        List<string> chain)
    {
        if (visited.Contains(taskId))
            return;

        visited.Add(taskId);
        chain.Add(taskId);

        // Find children: tasks that depend on this task
        var children = validInstances
            .Where(t => t.PrerequisiteTaskIds.Contains(taskId))
            .Select(t => t.TaskIdString)
            .Distinct();

        foreach (var child in children)
        {
            TraverseDepthFirst(child, validInstances, visited, chain);
        }
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
