using ConsoleApp.Ifx.Models;
using Orleans;

namespace ConsoleApp.Ifx.Orleans.Grains;

/// <summary>
/// Implementation of execution task grain for iterative time slot calculation.
/// </summary>
public class ExecutionTaskGrain : Grain, IExecutionTaskGrain
{
    private ExecutionEventDefinition? _eventDef;
    private ExecutionDuration? _duration;
    private DateTime _currentStartTime;
    private DateTime _plannedCompletion;
    private bool _isValid = false;
    private string? _validationMessage;

    public Task InitializeAsync(ExecutionEventDefinition eventDef, ExecutionDuration duration)
    {
        _eventDef = eventDef;
        _duration = duration;
        _currentStartTime = GetDefaultStartTime();
        _plannedCompletion = _currentStartTime.Add(_duration.ToTimeSpan());
        return Task.CompletedTask;
    }

    public Task<ExecutionInstanceEnhanced> GetExecutionInstanceAsync()
    {
        if (_eventDef is null || _duration is null)
            throw new InvalidOperationException("Grain not initialized");

        var instance = new ExecutionInstanceEnhanced(
            Id: GetHashCode(),
            TaskId: -1,
            TaskIdString: _eventDef.TaskId,
            TaskName: _eventDef.TaskName,
            ScheduledStartTime: GetDefaultStartTime(),
            FunctionalStartTime: _currentStartTime != GetDefaultStartTime() ? _currentStartTime : null,
            RequiredEndTime: _eventDef.IntakeRequirement?.GetIntakeDeadline(GetDefaultStartTime()),
            Duration: _duration,
            PlannedCompletionTime: _plannedCompletion,
            PrerequisiteTaskIds: _eventDef.PrerequisiteTaskIds,
            IsValid: _isValid,
            Status: _isValid ? ExecutionStatus.ReadyToExecute : ExecutionStatus.Invalid,
            ValidationMessage: _validationMessage
        );

        return Task.FromResult(instance);
    }

    public Task<DateTime> UpdateStartTimeAsync(Dictionary<string, DateTime> prerequisiteCompletions)
    {
        if (_eventDef is null || _duration is null)
            throw new InvalidOperationException("Grain not initialized");

        var defaultStart = GetDefaultStartTime();

        // Find latest prerequisite completion time
        var latestPrereqCompletion = DateTime.MinValue;

        foreach (var prereqTaskId in _eventDef.PrerequisiteTaskIds)
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

        var oldStartTime = _currentStartTime;
        _currentStartTime = newStartTime;
        _plannedCompletion = _currentStartTime.Add(_duration.ToTimeSpan());

        // Return whether this grain's start time changed
        return Task.FromResult(newStartTime);
    }

    public Task<DateTime> GetPlannedCompletionAsync()
    {
        return Task.FromResult(_plannedCompletion);
    }

    public Task<(bool IsValid, string? Message)> ValidateDeadlineAsync()
    {
        if (_eventDef is null || _duration is null)
            return Task.FromResult((false, "Grain not initialized"));

        // Check deadline
        if (_eventDef.IntakeRequirement is null)
            return Task.FromResult((true, null));

        var deadline = _eventDef.IntakeRequirement.GetIntakeDeadline(GetDefaultStartTime());

        if (_plannedCompletion <= deadline)
        {
            _isValid = true;
            _validationMessage = null;
            return Task.FromResult((true, null));
        }

        _isValid = false;
        _validationMessage = $"Deadline miss: completion {_plannedCompletion:g}, deadline {deadline:g}";
        return Task.FromResult((false, _validationMessage));
    }

    public Task MarkAsReadyAsync()
    {
        _isValid = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> GetPrerequisitesAsync()
    {
        if (_eventDef is null)
            return Task.FromResult((IReadOnlySet<string>)new HashSet<string>().AsReadOnly());

        return Task.FromResult(_eventDef.PrerequisiteTaskIds);
    }

    public Task<string> GetExecutionEventKeyAsync()
    {
        if (_eventDef is null)
            throw new InvalidOperationException("Grain not initialized");

        return Task.FromResult(_eventDef.GetExecutionEventKey());
    }

    private DateTime GetDefaultStartTime()
    {
        if (_eventDef is null)
            return DateTime.Now;

        // Calculate start time based on day and time in current week
        var today = DateTime.Now.Date;
        var daysToAdd = ((int)_eventDef.ScheduledDay - (int)today.DayOfWeek + 7) % 7;
        var targetDate = today.AddDays(daysToAdd);
        return _eventDef.ScheduledTime.ApplyToDate(targetDate);
    }
}

/// <summary>
/// Coordinator grain that manages iterative time slot refinement.
/// </summary>
public class ExecutionPlanCoordinatorGrain : Grain, IExecutionPlanCoordinatorGrain
{
    private Dictionary<string, IExecutionTaskGrain> _taskGrains = new();
    private DateTime _periodStartDate;
    private IReadOnlyList<ExecutionInstanceEnhanced>? _currentPlan;
    private int _iterationCount;
    private const int MAX_ITERATIONS = 100; // Prevent infinite loops

    public async Task<ExecutionPlan> CalculateExecutionPlanAsync(
        IReadOnlyList<ExecutionEventDefinition> executionEvents,
        IReadOnlyList<ExecutionInstanceEnhanced> initialInstances,
        DateTime periodStartDate)
    {
        _periodStartDate = periodStartDate;
        _iterationCount = 0;

        // Create grain for each execution event
        var grainFactory = this.GrainFactory;

        foreach (var eventDef in executionEvents)
        {
            var grainKey = eventDef.GetExecutionEventKey();
            var grain = grainFactory.GetGrain<IExecutionTaskGrain>(grainKey);

            // Get duration (from initial plan or default)
            var instance = initialInstances.FirstOrDefault(i => i.TaskIdString == eventDef.TaskId);
            var duration = instance?.Duration ?? ExecutionDuration.Default();

            await grain.InitializeAsync(eventDef, duration);
            _taskGrains[grainKey] = grain;
        }

        // Iteratively refine time slots
        bool converged = false;
        while (_iterationCount < MAX_ITERATIONS && !converged)
        {
            (converged, _) = await RefineTimeSlotIterationAsync();
            _iterationCount++;
        }

        // Build final execution plan
        return await BuildExecutionPlanAsync();
    }

    public async Task<(bool HasConverged, int UpdateCount)> RefineTimeSlotIterationAsync()
    {
        if (_taskGrains.Count == 0)
            return (true, 0);

        int updateCount = 0;
        var completionTimes = new Dictionary<string, DateTime>();

        // Get all current planned completions
        foreach (var (key, grain) in _taskGrains)
        {
            var completion = await grain.GetPlannedCompletionAsync();
            completionTimes[key] = completion;
        }

        // Update all grains with prerequisite information
        var tasks = new List<Task<DateTime>>();
        foreach (var grain in _taskGrains.Values)
        {
            tasks.Add(grain.UpdateStartTimeAsync(completionTimes));
        }

        var results = await Task.WhenAll(tasks);
        updateCount = results.Length;

        // Check convergence: if no grain start times changed significantly
        bool allConverged = true;
        foreach (var grain in _taskGrains.Values)
        {
            var result = await grain.ValidateDeadlineAsync();
            if (!result.IsValid)
            {
                allConverged = false;
                break;
            }
        }

        return (allConverged || _iterationCount >= MAX_ITERATIONS - 1, updateCount);
    }

    public async Task<ExecutionPlan> GetCurrentPlanAsync()
    {
        return await BuildExecutionPlanAsync();
    }

    private async Task<ExecutionPlan> BuildExecutionPlanAsync()
    {
        var tasks = new List<ExecutionInstanceEnhanced>();
        var validCount = 0;
        var invalidCount = 0;
        var deadlineMisses = new List<string>();

        foreach (var grain in _taskGrains.Values)
        {
            var instance = await grain.GetExecutionInstanceAsync();
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

        var incrementId = _periodStartDate.ToString("yyyy-MM-dd");

        return new ExecutionPlan(
            IncrementId: incrementId,
            IncrementStart: _periodStartDate,
            IncrementEnd: _periodStartDate.AddDays(7),
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
