namespace Manager.Orchestration.Contract;

/// <summary>
/// Orchestration service interface for coordinating workflow execution.
/// Handles the orchestration and sequencing of business operations.
/// </summary>
public interface IOrchestrationService
{
    /// <summary>
    /// Orchestrates the execution of a workflow with given parameters.
    /// </summary>
    Task<TResult> ExecuteWorkflowAsync<TResult>(string workflowName, object parameters, CancellationToken ct) where TResult : notnull;

    /// <summary>
    /// Orchestrates loading and validation of input data.
    /// </summary>
    Task<object> LoadAndValidateAsync(string dataSourcePath, CancellationToken ct);
}
