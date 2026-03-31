using Core.Models;

namespace Core.Services;

/// <summary>
/// Orchestrates the complete execution planning workflow (Phases 2-5).
/// Coordinates dependency analysis, task grouping, criticality analysis, and iterative refinement.
/// </summary>
public class ExecutionPlanOrchestrator
{
    private readonly DependencyGraphBuilder graphBuilder;
    private readonly TaskStratifier stratifier;
    private readonly TaskGrouper grouper;
    private readonly CriticalityAnalyzer criticalityAnalyzer;

    public ExecutionPlanOrchestrator(
        DependencyGraphBuilder graphBuilder,
        TaskStratifier stratifier,
        TaskGrouper grouper,
        CriticalityAnalyzer criticalityAnalyzer)
    {
        this.graphBuilder = graphBuilder ?? throw new ArgumentNullException(nameof(graphBuilder));
        this.stratifier = stratifier ?? throw new ArgumentNullException(nameof(stratifier));
        this.grouper = grouper ?? throw new ArgumentNullException(nameof(grouper));
        this.criticalityAnalyzer = criticalityAnalyzer ?? throw new ArgumentNullException(nameof(criticalityAnalyzer));
    }

    /// <summary>
    /// Executes the complete analysis workflow: dependency analysis → grouping → criticality → validation.
    /// </summary>
    /// <param name="events">All execution events from manifest</param>
    /// <param name="intakeRequirements">Intake deadline requirements</param>
    /// <param name="planningPeriodStart">Planning period start date</param>
    /// <param name="planningPeriodEnd">Planning period end date (deadline)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Complete execution plan analysis</returns>
    /// <exception cref="InvalidOperationException">If circular dependencies or validation errors occur</exception>
    public async Task<ExecutionPlanAnalysis> AnalyzeAndPlanAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        IReadOnlyList<IntakeEventRequirement> intakeRequirements,
        DateTime planningPeriodStart,
        DateTime planningPeriodEnd,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(intakeRequirements);

        ct.ThrowIfCancellationRequested();

        try
        {
            // Phase 2: Build and validate dependency graph
            var graph = await this.BuildDependencyGraphAsync(events, ct);
            ct.ThrowIfCancellationRequested();

            // Phase 3: Stratify tasks by level
            var stratification = await this.stratifier.AssignStratificationLevelsAsync(graph, ct);
            ct.ThrowIfCancellationRequested();

            // Phase 3: Classify and group tasks
            var patterns = await this.grouper.ClassifyTasksAsync(graph, ct);
            ct.ThrowIfCancellationRequested();

            var executionGroups = await this.grouper.CreateExecutionGroupsAsync(graph, patterns, stratification, ct);
            ct.ThrowIfCancellationRequested();

            // Phase 3: Calculate criticality metrics
            var durations = this.BuildDurationLookup(events);
            var criticalityMetrics = await this.criticalityAnalyzer.ComputeCriticalityMetricsAsync(
                graph,
                durations,
                planningPeriodStart,
                planningPeriodEnd,
                ct);
            ct.ThrowIfCancellationRequested();

            // Validate execution requirements
            var validationResult = await ValidateExecutionRequirementsAsync(
                events,
                graph,
                criticalityMetrics,
                planningPeriodStart,
                planningPeriodEnd,
                ct);
            ct.ThrowIfCancellationRequested();

            return new ExecutionPlanAnalysis
            {
                DependencyGraph = graph,
                Stratification = stratification,
                ExecutionGroups = executionGroups,
                CriticalityInfo = criticalityMetrics,
                ValidationResult = validationResult,
                AnalysisCompletedAt = DateTime.UtcNow
            };
        }
        catch (InvalidOperationException ex)
        {
            // Re-throw validation errors as-is
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Execution plan analysis failed: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets a summary of the execution plan analysis.
    /// </summary>
    /// <param name="analysis">The execution plan analysis</param>
    /// <returns>Summary information</returns>
    public ExecutionPlanSummary GetExecutionPlanSummary(ExecutionPlanAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var (stratLevelCount, stratTotalTasks, stratAvgTasks) = this.stratifier.GetStratificationStats(analysis.Stratification);
        var (groupCount, groupTotalTasks, parallelGroups) = this.grouper.GetGroupingStats(analysis.ExecutionGroups);
        var (critPercentage, avgSlack, minSlack, maxSlack) = this.criticalityAnalyzer.GetCriticalityStats(analysis.CriticalityInfo.TaskSlack);

        return new ExecutionPlanSummary
        {
            TotalTasks = analysis.DependencyGraph.AllTaskIds.Count,
            StratificationLevelCount = stratLevelCount,
            MaxStratificationLevel = analysis.Stratification.MaxLevel,
            ExecutionGroupCount = groupCount,
            ParallelizableGroups = parallelGroups,
            CriticalTaskCount = analysis.CriticalityInfo.CriticalTasks.Count,
            CriticalTaskPercentage = critPercentage,
            CriticalPathCompletion = analysis.CriticalityInfo.CriticalPathCompletion,
            AverageSlackTime = avgSlack,
            MinSlackTime = minSlack,
            MaxSlackTime = maxSlack,
            ValidationErrors = analysis.ValidationResult.Errors.Count,
            ValidationWarnings = analysis.ValidationResult.Warnings.Count,
            IsValid = analysis.ValidationResult.IsValid
        };
    }

    /// <summary>
    /// Suggests optimization strategies based on analysis results.
    /// </summary>
    /// <param name="analysis">The execution plan analysis</param>
    /// <returns>List of optimization suggestions</returns>
    public IReadOnlyList<string> SuggestOptimizations(ExecutionPlanAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var suggestions = new List<string>();

        var (critPercentage, avgSlack, _, _) = this.criticalityAnalyzer.GetCriticalityStats(analysis.CriticalityInfo.TaskSlack);

        // High criticality
        if (critPercentage > 80)
            suggestions.Add("CRITICAL: Over 80% of tasks are on critical path. High risk - very tight schedule.");

        // Long critical path
        if (analysis.Stratification.MaxLevel > 10)
            suggestions.Add("WARNING: Dependency chain is very long (>10 levels). Consider parallelizing where possible.");

        // Low average slack
        if (avgSlack < TimeSpan.FromMinutes(30))
            suggestions.Add("WARNING: Average slack is less than 30 minutes. Limited buffer for delays.");

        // Many groups
        var (groupCount, _, _) = this.grouper.GetGroupingStats(analysis.ExecutionGroups);
        if (groupCount > 50)
            suggestions.Add("INFO: Large number of execution groups. Consider consolidating related tasks.");

        // Unbalanced stratification
        var (levelCount, _, avgTasksPerLevel) = this.stratifier.GetStratificationStats(analysis.Stratification);
        if (avgTasksPerLevel < 2)
            suggestions.Add("INFO: Unbalanced stratification (few tasks per level). Limited parallelization opportunity.");

        if (suggestions.Count == 0)
            suggestions.Add("INFO: Execution plan appears well-optimized.");

        return suggestions.AsReadOnly();
    }

    /// <summary>
    /// Builds the dependency graph with validation.
    /// </summary>
    private async Task<IDependencyGraph> BuildDependencyGraphAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        CancellationToken ct)
    {
        var graph = await this.graphBuilder.BuildDependencyGraphAsync(events, ct);
        return graph;
    }

    /// <summary>
    /// Builds duration lookup from execution events.
    /// </summary>
    private IReadOnlyDictionary<string, ExecutionDuration> BuildDurationLookup(
        IReadOnlyList<ExecutionEventDefinition> events)
    {
        var lookup = new Dictionary<string, ExecutionDuration>();

        foreach (var evt in events)
        {
            if (!lookup.ContainsKey(evt.TaskId))
            {
                lookup[evt.TaskId] = new ExecutionDuration(
                    DurationMinutes: evt.DurationMinutes,
                    IsEstimated: true);
            }
        }

        return lookup.AsReadOnly();
    }

    /// <summary>
    /// Validates execution requirements.
    /// </summary>
    private async Task<ValidationResult> ValidateExecutionRequirementsAsync(
        IReadOnlyList<ExecutionEventDefinition> events,
        IDependencyGraph graph,
        CriticalityMetrics criticalityMetrics,
        DateTime planningPeriodStart,
        DateTime planningPeriodEnd,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var errors = new List<string>();
        var warnings = new List<string>();
        var missedDeadlines = new HashSet<string>();
        var orphanedTasks = new HashSet<string>();

        // Check each task
        foreach (var evt in events)
        {
            ct.ThrowIfCancellationRequested();

            // Check if task has all prerequisites
            var prerequisites = graph.TaskToPrerequisites.TryGetValue(evt.TaskId, out var prereqs)
                ? prereqs
                : new List<string>().AsReadOnly();

            foreach (var prereq in prerequisites)
            {
                if (!graph.AllTaskIds.Contains(prereq))
                {
                    var error = $"Task '{evt.TaskId}' has missing prerequisite '{prereq}'";
                    if (!errors.Contains(error))
                        errors.Add(error);

                    if (!orphanedTasks.Contains(prereq))
                        orphanedTasks.Add(prereq);
                }
            }

            // Check deadline compliance
            if (evt.IntakeRequirement != null)
            {
                var deadline = evt.IntakeRequirement.GetIntakeDeadline(planningPeriodStart);
                var slack = criticalityMetrics.TaskSlack.TryGetValue(evt.TaskId, out var s) ? s : TimeSpan.Zero;

                if (slack < TimeSpan.Zero)
                {
                    var error = $"Task '{evt.TaskId}' misses deadline by {slack.Negate().TotalMinutes:F0} minutes";
                    if (!errors.Contains(error))
                        errors.Add(error);

                    if (!missedDeadlines.Contains(evt.TaskId))
                        missedDeadlines.Add(evt.TaskId);
                }
                else if (slack < TimeSpan.FromMinutes(15))
                {
                    var warning = $"Task '{evt.TaskId}' has only {slack.TotalMinutes:F0} minutes slack to deadline";
                    if (!warnings.Contains(warning))
                        warnings.Add(warning);
                }
            }
        }

        // Check for unreachable tasks
        var reachable = new HashSet<string>();
        foreach (var rootTask in graph.TopologicalOrder)
        {
            if (graph.TaskToPrerequisites.TryGetValue(rootTask, out var prereqs) && prereqs.Count == 0)
            {
                TraverseReachable(rootTask, graph, reachable);
            }
        }

        var unreachable = graph.AllTaskIds.Where(t => !reachable.Contains(t)).ToHashSet();
        if (unreachable.Count > 0)
        {
            var warning = $"{unreachable.Count} tasks are unreachable from root tasks: {string.Join(", ", unreachable.Take(5))}";
            if (!warnings.Contains(warning))
                warnings.Add(warning);
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors.AsReadOnly(),
            Warnings = warnings.AsReadOnly(),
            CircularDependencies = null,
            MissingPrerequisiteTasks = orphanedTasks.Count > 0 ? orphanedTasks.AsReadOnly() : null,
            UnreachableTasks = unreachable.Count > 0 ? unreachable.AsReadOnly() : null
        };
    }

    /// <summary>
    /// Traverses reachable tasks from a given root.
    /// </summary>
    private void TraverseReachable(string taskId, IDependencyGraph graph, HashSet<string> reachable)
    {
        if (reachable.Contains(taskId))
            return;

        reachable.Add(taskId);

        if (graph.TaskToDependents.TryGetValue(taskId, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                TraverseReachable(dependent, graph, reachable);
            }
        }
    }
}

/// <summary>
/// Summary statistics for an execution plan.
/// </summary>
public record ExecutionPlanSummary
{
    /// <summary>Total number of tasks in the plan.</summary>
    public int TotalTasks { get; init; }

    /// <summary>Number of stratification levels.</summary>
    public int StratificationLevelCount { get; init; }

    /// <summary>Maximum stratification level (critical depth).</summary>
    public int MaxStratificationLevel { get; init; }

    /// <summary>Number of execution groups.</summary>
    public int ExecutionGroupCount { get; init; }

    /// <summary>Number of parallelizable execution groups.</summary>
    public int ParallelizableGroups { get; init; }

    /// <summary>Number of critical tasks.</summary>
    public int CriticalTaskCount { get; init; }

    /// <summary>Percentage of tasks that are critical.</summary>
    public double CriticalTaskPercentage { get; init; }

    /// <summary>Planned completion time of the critical path.</summary>
    public DateTime CriticalPathCompletion { get; init; }

    /// <summary>Average slack time across all tasks.</summary>
    public TimeSpan AverageSlackTime { get; init; }

    /// <summary>Minimum slack time (most constrained task).</summary>
    public TimeSpan MinSlackTime { get; init; }

    /// <summary>Maximum slack time (most flexible task).</summary>
    public TimeSpan MaxSlackTime { get; init; }

    /// <summary>Number of validation errors found.</summary>
    public int ValidationErrors { get; init; }

    /// <summary>Number of validation warnings found.</summary>
    public int ValidationWarnings { get; init; }

    /// <summary>Whether the plan passed all validation checks.</summary>
    public bool IsValid { get; init; }
}
