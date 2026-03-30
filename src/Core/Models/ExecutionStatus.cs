namespace App.TaskSequencer.Domain.Models;

/// <summary>
/// Represents the validation/execution status of an ExecutionInstance.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Execution instance initializing.</summary>
    Initializing = 0,

    /// <summary>Awaiting prerequisite completion.</summary>
    AwaitingPrerequisites = 1,

    /// <summary>Ready to execute (all constraints satisfied).</summary>
    ReadyToExecute = 2,

    /// <summary>Invalid (cannot meet constraints).</summary>
    Invalid = 3,

    /// <summary>Failed to meet deadline.</summary>
    DeadlineMiss = 4,

    /// <summary>Successfully completed.</summary>
    Completed = 5,

    /// <summary>Awaiting actual duration data from execution history.</summary>
    DurationPending = 6
}
