using App.TaskSequencer.Contracts.Models;
using App.TaskSequencer.Contracts.Orleans.Grains;
using App.TaskSequencer.Core.Models;
using App.TaskSequencer.Core.Services;
using Orleans;

namespace App.TaskSequencer.Client.Desktop.Maui.Services;

/// <summary>
/// Service for communicating with Orleans grains and coordinating execution planning.
/// Bridges the MAUI UI with the backend Orleans execution coordination system.
/// </summary>
public class ExecutionPlanService
{
    private readonly IClusterClient GrainClient;
    private readonly ManifestCsvParser CsvParser;
    private readonly ManifestTransformer Transformer;
    private readonly ExecutionPlanOrchestrator Orchestrator;

    public ExecutionPlanService(
        IClusterClient grainClient,
        ManifestCsvParser csvParser,
        ManifestTransformer transformer,
        ExecutionPlanOrchestrator orchestrator)
    {
        GrainClient = grainClient ?? throw new ArgumentNullException(nameof(grainClient));
        CsvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        Transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));
        Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Load and process CSV manifest files, creating an execution plan.
    /// </summary>
    public async Task<ExecutionPlanResult> LoadAndPlanAsync(
        string taskDefinitionsPath,
        string intakeEventsPath,
        string? durationManifestPath,
        DateTime incrementStart,
        DateTime incrementEnd,
        CancellationToken ct = default)
    {
        try
        {
            // Parse CSV files
            var taskDefinitions = await CsvParser.ParseTaskDefinitionsAsync(taskDefinitionsPath, ct);
            var intakeEvents = await CsvParser.ParseIntakeEventsAsync(intakeEventsPath, ct);
            var durations = durationManifestPath != null
                ? await CsvParser.ParseDurationManifestAsync(durationManifestPath, ct)
                : new List<ExecutionDurationManifest>();

            // Transform to domain models
            var events = Transformer.TransformToExecutionEvents(
                taskDefinitions,
                intakeEvents,
                durations);

            // Analyze and plan using orchestrator
            var analysis = await Orchestrator.AnalyzeAndPlanAsync(
                events,
                intakeEvents,
                incrementStart,
                incrementEnd,
                ct);

            // Get the execution plan summary
            var summary = Orchestrator.GetExecutionPlanSummary(analysis);

            return new ExecutionPlanResult
            {
                Success = true,
                Analysis = analysis,
                Summary = summary,
                TotalTasks = events.Count,
                ValidTasks = analysis.ValidationResult.ValidTasks.Count,
                InvalidTasks = analysis.ValidationResult.InvalidTasks.Count,
                Errors = analysis.ValidationResult.Errors.ToList(),
                Warnings = analysis.ValidationResult.Warnings.ToList()
            };
        }
        catch (Exception ex)
        {
            return new ExecutionPlanResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Get execution tasks formatted for UI display.
    /// </summary>
    public List<ExecutionTaskDisplay> GetExecutionTasks(ExecutionPlanAnalysis analysis)
    {
        if (analysis?.ExecutionPlan?.Tasks == null)
            return new List<ExecutionTaskDisplay>();

        return analysis.ExecutionPlan.Tasks
            .Select((task, index) => new ExecutionTaskDisplay
            {
                Id = index,
                TaskId = task.TaskIdString,
                TaskName = task.TaskName,
                ScheduledStartTime = task.ScheduledStartTime,
                FunctionalStartTime = task.FunctionalStartTime ?? task.ScheduledStartTime,
                PlannedCompletionTime = task.PlannedCompletionTime,
                RequiredEndTime = task.RequiredEndTime,
                Status = task.ExecutionStatus,
                IsValid = !analysis.ValidationResult.InvalidTasks.Contains(task.TaskIdString),
                IsCritical = analysis.CriticalityMetrics?.CriticalTasks.Contains(task.TaskIdString) ?? false,
                SlackMinutes = (analysis.CriticalityMetrics?.GetSlack(task.TaskIdString) ?? TimeSpan.Zero).TotalMinutes,
                DurationMinutes = (task.PlannedCompletionTime - (task.FunctionalStartTime ?? task.ScheduledStartTime)).TotalMinutes
            })
            .ToList();
    }

    /// <summary>
    /// Get deadline violations for display.
    /// </summary>
    public List<DeadlineViolation> GetDeadlineViolations(ExecutionPlanAnalysis analysis)
    {
        if (analysis?.ValidationResult?.InvalidTasks == null)
            return new List<DeadlineViolation>();

        var violations = new List<DeadlineViolation>();

        foreach (var taskId in analysis.ValidationResult.InvalidTasks)
        {
            var task = analysis.ExecutionPlan?.Tasks
                .FirstOrDefault(t => t.TaskIdString == taskId);

            if (task?.RequiredEndTime != null && task.PlannedCompletionTime > task.RequiredEndTime)
            {
                var missMinutes = (task.PlannedCompletionTime - task.RequiredEndTime.Value).TotalMinutes;
                violations.Add(new DeadlineViolation
                {
                    TaskId = taskId,
                    TaskName = task.TaskName,
                    DeadlineTime = task.RequiredEndTime.Value,
                    PlannedCompletionTime = task.PlannedCompletionTime,
                    MissMinutes = missMinutes,
                    Severity = GetSeverity(missMinutes)
                });
            }
        }

        return violations.OrderByDescending(v => v.MissMinutes).ToList();
    }

    private static string GetSeverity(double missMinutes)
    {
        if (missMinutes <= 0) return "On Time";
        if (missMinutes <= 30) return "Minor";
        if (missMinutes <= 120) return "Moderate";
        return "Critical";
    }

    /// <summary>
    /// Get stratification levels for task grouping.
    /// </summary>
    public List<StratificationLevel> GetStratificationLevels(ExecutionPlanAnalysis analysis)
    {
        if (analysis?.StratificationResult == null)
            return new List<StratificationLevel>();

        var levels = new List<StratificationLevel>();

        for (int level = 0; level <= analysis.StratificationResult.MaxLevel; level++)
        {
            if (analysis.StratificationResult.LevelToTasks.TryGetValue(level, out var tasks))
            {
                levels.Add(new StratificationLevel
                {
                    Level = level,
                    TaskCount = tasks.Count,
                    TaskIds = tasks.ToList(),
                    CanParallelize = tasks.Count > 1
                });
            }
        }

        return levels;
    }

    /// <summary>
    /// Get high-level plan statistics for dashboard.
    /// </summary>
    public PlanStatistics GetPlanStatistics(ExecutionPlanAnalysis analysis)
    {
        var violationCount = analysis.ValidationResult?.InvalidTasks.Count ?? 0;
        var totalTasks = analysis.ExecutionPlan?.Tasks.Count ?? 0;

        return new PlanStatistics
        {
            TotalTasks = totalTasks,
            ValidTasks = totalTasks - violationCount,
            InvalidTasks = violationCount,
            CriticalPathEnd = analysis.ExecutionPlan?.CriticalPathCompletion ?? DateTime.MinValue,
            CriticalTaskCount = analysis.CriticalityMetrics?.CriticalTasks.Count ?? 0,
            MaxParallelLevel = analysis.StratificationResult?.MaxLevel ?? 0,
            ExecutionGroups = analysis.ExecutionGroups?.Count ?? 0
        };
    }
}

/// <summary>
/// Result of loading and planning execution.
/// </summary>
public class ExecutionPlanResult
{
    public bool Success { get; set; }
    public ExecutionPlanAnalysis? Analysis { get; set; }
    public ExecutionPlanSummary? Summary { get; set; }
    public int TotalTasks { get; set; }
    public int ValidTasks { get; set; }
    public int InvalidTasks { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Display model for execution tasks in UI.
/// </summary>
public class ExecutionTaskDisplay
{
    public int Id { get; set; }
    public string TaskId { get; set; } = "";
    public string TaskName { get; set; } = "";
    public DateTime ScheduledStartTime { get; set; }
    public DateTime FunctionalStartTime { get; set; }
    public DateTime PlannedCompletionTime { get; set; }
    public DateTime? RequiredEndTime { get; set; }
    public string Status { get; set; } = "";
    public bool IsValid { get; set; }
    public bool IsCritical { get; set; }
    public double SlackMinutes { get; set; }
    public double DurationMinutes { get; set; }
}

/// <summary>
/// Display model for deadline violations.
/// </summary>
public class DeadlineViolation
{
    public string TaskId { get; set; } = "";
    public string TaskName { get; set; } = "";
    public DateTime DeadlineTime { get; set; }
    public DateTime PlannedCompletionTime { get; set; }
    public double MissMinutes { get; set; }
    public string Severity { get; set; } = "";
}

/// <summary>
/// Display model for stratification levels.
/// </summary>
public class StratificationLevel
{
    public int Level { get; set; }
    public int TaskCount { get; set; }
    public List<string> TaskIds { get; set; } = new();
    public bool CanParallelize { get; set; }
}

/// <summary>
/// High-level plan statistics.
/// </summary>
public class PlanStatistics
{
    public int TotalTasks { get; set; }
    public int ValidTasks { get; set; }
    public int InvalidTasks { get; set; }
    public DateTime CriticalPathEnd { get; set; }
    public int CriticalTaskCount { get; set; }
    public int MaxParallelLevel { get; set; }
    public int ExecutionGroups { get; set; }
}
