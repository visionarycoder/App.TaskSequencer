using Core.Models;

namespace Core.Orleans.Grains;

/// <summary>
/// Implementation of execution task grain for iterative time slot calculation.
/// </summary>
public class ExecutionTaskGrain : Grain, IExecutionTaskGrain
{
    private ExecutionEventDefinition? eventDef;
    private ExecutionDuration? duration;
    private DateTime currentStartTime;
    private DateTime plannedCompletion;
    private bool isValid = false;
    private string? validationMessage;
    private IReadOnlySet<string> prerequisites = new HashSet<string>().AsReadOnly();

    public Task InitializeAsync(ExecutionEventDefinition eventDef, ExecutionDuration duration, CancellationToken ct)
    {
        this.eventDef = eventDef ?? throw new ArgumentNullException(nameof(eventDef));
        this.duration = duration ?? throw new ArgumentNullException(nameof(duration));
        this.currentStartTime = GetDefaultStartTime();
        this.plannedCompletion = this.currentStartTime.Add(this.duration.ToTimeSpan());
        this.prerequisites = eventDef.PrerequisiteTaskIds;
        return Task.CompletedTask;
    }

    public Task<ExecutionInstanceEnhanced> GetExecutionInstanceAsync(CancellationToken ct)
    {
        if (this.eventDef is null || this.duration is null)
            throw new InvalidOperationException("Grain not initialized");

        var instance = new ExecutionInstanceEnhanced(
            Id: GetHashCode(),
            TaskId: -1,
            TaskIdString: this.eventDef.TaskId,
            TaskName: this.eventDef.TaskName,
            ScheduledStartTime: GetDefaultStartTime(),
            FunctionalStartTime: this.currentStartTime != GetDefaultStartTime() ? this.currentStartTime : null,
            RequiredEndTime: this.eventDef.IntakeRequirement?.GetIntakeDeadline(GetDefaultStartTime()),
            Duration: this.duration,
            PlannedCompletionTime: this.plannedCompletion,
            PrerequisiteTaskIds: this.prerequisites,
            IsValid: this.isValid,
            Status: this.isValid ? ExecutionStatus.ReadyToExecute : ExecutionStatus.Invalid,
            ValidationMessage: this.validationMessage
        );

        return Task.FromResult(instance);
    }

    public Task<DateTime> UpdateStartTimeAsync(IReadOnlyDictionary<string, DateTime> prerequisiteCompletions, CancellationToken ct)
    {
        if (this.eventDef is null || this.duration is null)
            throw new InvalidOperationException("Grain not initialized");

        var defaultStart = GetDefaultStartTime();

        // Find latest prerequisite completion time
        var latestPrereqCompletion = DateTime.MinValue;

        foreach (var prereqTaskId in this.prerequisites)
        {
            // Look for any prerequisite that has been calculated
            var matchingKey = prerequisiteCompletions.Keys
                .FirstOrDefault(k => k.StartsWith(prereqTaskId + "_"));

            if (matchingKey != null && prerequisiteCompletions.TryGetValue(matchingKey, out var completion))
            {
                if (completion > latestPrereqCompletion)
                    latestPrereqCompletion = completion;
            }
        }

        // Adjusted start = MAX(scheduled start, latest prerequisite completion)
        var newStartTime = latestPrereqCompletion > defaultStart
            ? latestPrereqCompletion
            : defaultStart;

        var oldStartTime = this.currentStartTime;
        this.currentStartTime = newStartTime;
        this.plannedCompletion = this.currentStartTime.Add(this.duration.ToTimeSpan());

        // Return the new start time
        return Task.FromResult(newStartTime);
    }

    public Task<DateTime> GetPlannedCompletionAsync(CancellationToken ct)
    {
        return Task.FromResult(this.plannedCompletion);
    }

    public Task<(bool IsValid, ExecutionStatus Status, string? Message)> ValidateDeadlineAsync(CancellationToken ct)
    {
        if (this.eventDef is null || this.duration is null)
            return Task.FromResult<(bool, ExecutionStatus, string?)>((false, ExecutionStatus.Invalid, "Grain not initialized"));

        // Check deadline
        if (this.eventDef.IntakeRequirement is null)
            return Task.FromResult<(bool, ExecutionStatus, string?)>((true, ExecutionStatus.ReadyToExecute, null));

        var deadline = this.eventDef.IntakeRequirement.GetIntakeDeadline(GetDefaultStartTime());

        if (this.plannedCompletion <= deadline)
        {
            this.isValid = true;
            this.validationMessage = null;
            return Task.FromResult<(bool, ExecutionStatus, string?)>((true, ExecutionStatus.ReadyToExecute, null));
        }

        this.isValid = false;
        this.validationMessage = $"Deadline miss: completion {this.plannedCompletion:g}, deadline {deadline:g}";
        return Task.FromResult<(bool, ExecutionStatus, string?)>((false, ExecutionStatus.DeadlineMiss, this.validationMessage));
    }

    public Task MarkAsReadyAsync(CancellationToken ct)
    {
        this.isValid = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> GetPrerequisitesAsync(CancellationToken ct)
    {
        return Task.FromResult(this.prerequisites);
    }

    public Task<string> GetExecutionEventKeyAsync(CancellationToken ct)
    {
        if (this.eventDef is null)
            throw new InvalidOperationException("Grain not initialized");

        return Task.FromResult(this.eventDef.GetExecutionEventKey());
    }

    public Task<TimeSpan?> GetDeadlineSlackAsync(CancellationToken ct)
    {
        if (this.eventDef?.IntakeRequirement is null)
            return Task.FromResult<TimeSpan?>(null);

        var deadline = this.eventDef.IntakeRequirement.GetIntakeDeadline(GetDefaultStartTime());
        var slack = deadline - this.plannedCompletion;
        return Task.FromResult<TimeSpan?>(slack);
    }

    public Task SetPrerequisitesAsync(IReadOnlySet<string> prerequisiteTaskIds, CancellationToken ct)
    {
        this.prerequisites = prerequisiteTaskIds ?? throw new ArgumentNullException(nameof(prerequisiteTaskIds));
        return Task.CompletedTask;
    }

    public Task<ExecutionDuration> GetDurationAsync(CancellationToken ct)
    {
        if (this.duration is null)
            throw new InvalidOperationException("Grain not initialized");

        return Task.FromResult(this.duration);
    }

    private DateTime GetDefaultStartTime()
    {
        if (this.eventDef is null)
            return DateTime.Now;

        // Calculate start time based on day and time in current week
        var today = DateTime.Now.Date;
        var daysToAdd = ((int)this.eventDef.ScheduledDay - (int)today.DayOfWeek + 7) % 7;
        var targetDate = today.AddDays(daysToAdd);
        return this.eventDef.ScheduledTime.ApplyToDate(targetDate);
    }
}

/// <summary>
/// Coordinator grain that manages iterative time slot refinement.
/// </summary>
public class ExecutionPlanCoordinatorGrain : Grain, IExecutionPlanCoordinatorGrain
{
    private Dictionary<string, IExecutionTaskGrain> taskGrains = [];
    private DateTime periodStartDate;
    private IReadOnlyList<ExecutionInstanceEnhanced>? currentPlan;
    private int iterationCount;
    private DateTime refinementStartTime;
    private List<string> conflictingTasks = [];
    private const int MaxIterations = 100; // Prevent infinite loops

    public async Task<ExecutionPlan> CalculateExecutionPlanAsync(
        IReadOnlyList<ExecutionEventDefinition> executionEvents,
        IReadOnlyList<ExecutionInstanceEnhanced> initialInstances,
        DateTime periodStartDate,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(executionEvents);
        ArgumentNullException.ThrowIfNull(initialInstances);

        this.periodStartDate = periodStartDate;
        this.iterationCount = 0;
        this.refinementStartTime = DateTime.UtcNow;
        this.conflictingTasks.Clear();

        // Create grain for each execution event
        var grainFactory = this.GrainFactory;

        foreach (var eventDef in executionEvents)
        {
            ct.ThrowIfCancellationRequested();

            var grainKey = eventDef.GetExecutionEventKey();
            var grain = grainFactory.GetGrain<IExecutionTaskGrain>(grainKey);

            // Get duration (from initial plan or default)
            var instance = initialInstances.FirstOrDefault(i => i.TaskIdString == eventDef.TaskId);
            var duration = instance?.Duration ?? ExecutionDuration.Default();

            await grain.InitializeAsync(eventDef, duration, ct);
            taskGrains[grainKey] = grain;
        }

        // Iteratively refine time slots
        bool converged = false;
        while (iterationCount < MaxIterations && !converged)
        {
            ct.ThrowIfCancellationRequested();
            (converged, _) = await RefineTimeSlotIterationAsync(ct);
            iterationCount++;
        }

        // Build final execution plan
        return await BuildExecutionPlanAsync(ct);
    }

    public async Task<(bool HasConverged, int UpdateCount)> RefineTimeSlotIterationAsync(CancellationToken ct)
    {
        if (taskGrains.Count == 0)
            return (true, 0);

        int updateCount = 0;
        var completionTimes = new Dictionary<string, DateTime>();

        // Get all current planned completions
        foreach (var (key, grain) in taskGrains)
        {
            ct.ThrowIfCancellationRequested();
            var completion = await grain.GetPlannedCompletionAsync(ct);
            completionTimes[key] = completion;
        }

        // Update all grains with prerequisite information (in parallel where possible)
        var tasks = new List<Task<DateTime>>();
        foreach (var grain in taskGrains.Values)
        {
            tasks.Add(grain.UpdateStartTimeAsync(completionTimes, ct));
        }

        var results = await Task.WhenAll(tasks);
        updateCount = results.Length;

        // Check convergence: validate all grains against deadlines
        bool allConverged = true;
        conflictingTasks.Clear();

        foreach (var grain in taskGrains.Values)
        {
            ct.ThrowIfCancellationRequested();
            var (isValid, status, message) = await grain.ValidateDeadlineAsync(ct);
            if (!isValid)
            {
                allConverged = false;
                var eventKey = await grain.GetExecutionEventKeyAsync(ct);
                conflictingTasks.Add(eventKey);
            }
        }

        return (allConverged || iterationCount >= MaxIterations - 1, updateCount);
    }

    public async Task<ExecutionPlan> GetCurrentPlanAsync(CancellationToken ct)
    {
        return await BuildExecutionPlanAsync(ct);
    }

    public async Task<ConvergenceInfo> GetConvergenceInfoAsync(CancellationToken ct)
    {
        var validCount = 0;
        var invalidCount = 0;

        foreach (var grain in taskGrains.Values)
        {
            ct.ThrowIfCancellationRequested();
            var instance = await grain.GetExecutionInstanceAsync(ct);
            if (instance.IsValid)
                validCount++;
            else
                invalidCount++;
        }

        var plan = await BuildExecutionPlanAsync(ct);

        return new ConvergenceInfo
        {
            HasConverged = iterationCount < MaxIterations || invalidCount == 0,
            IterationCount = iterationCount,
            Reason = iterationCount >= MaxIterations ? ConvergenceReason.MaxIterations : ConvergenceReason.Organic,
            ValidTaskCount = validCount,
            InvalidTaskCount = invalidCount,
            CriticalPathCompletion = plan.CriticalPathCompletion,
            ElapsedTime = DateTime.UtcNow - refinementStartTime
        };
    }

    public async Task<IReadOnlyList<string>> GetConflictingTasksAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return conflictingTasks.AsReadOnly();
    }

    private async Task<ExecutionPlan> BuildExecutionPlanAsync(CancellationToken ct)
    {
        var tasks = new List<ExecutionInstanceEnhanced>();
        var validCount = 0;
        var invalidCount = 0;
        var deadlineMisses = new List<string>();

        foreach (var grain in taskGrains.Values)
        {
            ct.ThrowIfCancellationRequested();
            var instance = await grain.GetExecutionInstanceAsync(ct);
            tasks.Add(instance);

            if (instance.IsValid)
                validCount++;
            else
                invalidCount++;

            if (instance.Status == ExecutionStatus.DeadlineMiss)
                deadlineMisses.Add(instance.TaskIdString);
        }

        var criticalPath = tasks.Count > 0
            ? tasks.Max(t => t.PlannedCompletionTime)
            : (DateTime?)null;

        var incrementId = periodStartDate.ToString("yyyy-MM-dd");

        return new ExecutionPlan(
            IncrementId: incrementId,
            IncrementStart: periodStartDate,
            IncrementEnd: periodStartDate.AddDays(7),
            Tasks: tasks.AsReadOnly(),
            TaskChain: BuildTaskChain(tasks),
            TotalValidTasks: validCount,
            TotalInvalidTasks: invalidCount,
            CriticalPathCompletion: criticalPath,
            DeadlineMisses: deadlineMisses.AsReadOnly(),
            DSTWarnings: new List<string>().AsReadOnly()
        );
    }

    private IReadOnlyList<string> BuildTaskChain(List<ExecutionInstanceEnhanced> tasks)
    {
        var chain = new List<string>();
        var visited = new HashSet<string>();

        // Find roots
        var roots = tasks.Where(t => t.PrerequisiteTaskIds.Count == 0).ToList();

        foreach (var root in roots)
        {
            TraverseDepthFirst(root.TaskIdString, tasks, visited, chain);
        }

        return chain.AsReadOnly();
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
