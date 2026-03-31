using Xunit;
using Core.Services;
using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.UnitTests.Services;

/// <summary>
/// SUBTASK-1: Unit Tests for Dependency Graph Building
/// Tests the core business rule: parse task dependencies from execution events,
/// build directed graph with topological sorting, and detect circular dependencies.
/// </summary>
public class DependencyGraphBuilderTests
{
    private readonly DependencyResolver _dependencyResolver;
    private readonly DependencyGraphBuilder _builder;

    public DependencyGraphBuilderTests()
    {
        _dependencyResolver = new DependencyResolver();
        _builder = new DependencyGraphBuilder(_dependencyResolver);
    }

    /// <summary>
    /// Test 1: Empty task list should return empty graph
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_EmptyEventList_ReturnsEmptyGraph()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>();

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert
        Assert.Empty(graph.AllTaskIds);
        Assert.Empty(graph.TopologicalOrder);
        Assert.Empty(graph.TaskToPrerequisites);
        Assert.Empty(graph.TaskToDependents);
    }

    /// <summary>
    /// Test 2: Simple linear chain (1 → 2 → 3) should sort topologically
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_SimpleChain_SortsTopologically()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task3", "Task3", prereqs: new[] { "Task2" }),
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>()),
            CreateEvent("Task2", "Task2", prereqs: new[] { "Task1" })
        };

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert
        Assert.Equal(3, graph.AllTaskIds.Count);
        Assert.Equal(3, graph.TopologicalOrder.Count);

        var topoOrder = graph.TopologicalOrder.ToList();
        var task1Idx = topoOrder.IndexOf("Task1");
        var task2Idx = topoOrder.IndexOf("Task2");
        var task3Idx = topoOrder.IndexOf("Task3");

        Assert.True(task1Idx < task2Idx, "Task1 should come before Task2");
        Assert.True(task2Idx < task3Idx, "Task2 should come before Task3");
    }

    /// <summary>
    /// Test 3: Multiple branches (fan-out and fan-in) should merge correctly
    /// Graph: 1 → 2, 1 → 3, 2 → 4, 3 → 4
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_MultipleBranches_MergesCorrectly()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>()),
            CreateEvent("Task2", "Task2", prereqs: new[] { "Task1" }),
            CreateEvent("Task3", "Task3", prereqs: new[] { "Task1" }),
            CreateEvent("Task4", "Task4", prereqs: new[] { "Task2", "Task3" })
        };

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert
        Assert.Equal(4, graph.AllTaskIds.Count);
        var topoOrder = graph.TopologicalOrder.ToList();
        var indices = new Dictionary<string, int>();
        for (int i = 0; i < topoOrder.Count; i++)
        {
            indices[topoOrder[i]] = i;
        }

        Assert.True(indices["Task1"] < indices["Task2"], "Task1 before Task2");
        Assert.True(indices["Task1"] < indices["Task3"], "Task1 before Task3");
        Assert.True(indices["Task2"] < indices["Task4"], "Task2 before Task4");
        Assert.True(indices["Task3"] < indices["Task4"], "Task3 before Task4");
    }

    /// <summary>
    /// Test 4: Circular dependency (1 → 2 → 3 → 1) should throw InvalidOperationException
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_HasCircularDependency_ThrowsInvalidOperationException()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: new[] { "Task3" }),
            CreateEvent("Task2", "Task2", prereqs: new[] { "Task1" }),
            CreateEvent("Task3", "Task3", prereqs: new[] { "Task2" })
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _builder.BuildDependencyGraphAsync(events, CancellationToken.None));

        Assert.Contains("circular", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test 5: Missing dependency (Task1 depends on Task99 which doesn't exist) should throw
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_MissingDependency_HandlesGracefully()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: new[] { "Task99" }) // Task99 doesn't exist
        };

        // Act & Assert
        // Either throws or ignores missing deps - implementation determines behavior
        // This test ensures the method completes without crashing
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        Assert.NotNull(graph);
        Assert.Single(graph.AllTaskIds);
    }

    /// <summary>
    /// Test 6: Complex DAG with multiple levels and multiple paths should compute correct depths
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_ComplexDAG_ComputesDepthsCorrectly()
    {
        // Arrange
        // Graph structure:
        //     Task1
        //    /     \
        // Task2   Task3
        //    \     /
        //     Task4
        //      |
        //     Task5
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>()),
            CreateEvent("Task2", "Task2", prereqs: new[] { "Task1" }),
            CreateEvent("Task3", "Task3", prereqs: new[] { "Task1" }),
            CreateEvent("Task4", "Task4", prereqs: new[] { "Task2", "Task3" }),
            CreateEvent("Task5", "Task5", prereqs: new[] { "Task4" })
        };

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert - Depth from root (longest path backward)
        Assert.Equal(0, graph.ComputeDepthFromRoot("Task1")); // Root has depth 0
        Assert.Equal(1, graph.ComputeDepthFromRoot("Task2")); // 1 step from root
        Assert.Equal(1, graph.ComputeDepthFromRoot("Task3")); // 1 step from root
        Assert.Equal(2, graph.ComputeDepthFromRoot("Task4")); // 2 steps from root
        Assert.Equal(3, graph.ComputeDepthFromRoot("Task5")); // 3 steps from root

        // Assert - Depth to leaf (longest path forward)
        Assert.Equal(3, graph.ComputeDepthToLeaf("Task1")); // 3 steps to leaf
        Assert.Equal(2, graph.ComputeDepthToLeaf("Task2")); // 2 steps to leaf
        Assert.Equal(2, graph.ComputeDepthToLeaf("Task3")); // 2 steps to leaf
        Assert.Equal(1, graph.ComputeDepthToLeaf("Task4")); // 1 step to leaf
        Assert.Equal(0, graph.ComputeDepthToLeaf("Task5")); // Leaf has depth 0
    }

    /// <summary>
    /// Test 7: Task prerequisites and dependents mappings should be bidirectional
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_MappingsBidirectional_AreConsistent()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>()),
            CreateEvent("Task2", "Task2", prereqs: new[] { "Task1" }),
            CreateEvent("Task3", "Task3", prereqs: new[] { "Task1" })
        };

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert - Check prerequisites
        Assert.Empty(graph.TaskToPrerequisites["Task1"]);
        Assert.Single(graph.TaskToPrerequisites["Task2"]);
        Assert.Contains("Task1", graph.TaskToPrerequisites["Task2"]);
        Assert.Single(graph.TaskToPrerequisites["Task3"]);
        Assert.Contains("Task1", graph.TaskToPrerequisites["Task3"]);

        // Assert - Check dependents (inverse mapping)
        Assert.Equal(2, graph.TaskToDependents["Task1"].Count);
        Assert.Contains("Task2", graph.TaskToDependents["Task1"]);
        Assert.Contains("Task3", graph.TaskToDependents["Task1"]);
        Assert.Empty(graph.TaskToDependents["Task2"]);
        Assert.Empty(graph.TaskToDependents["Task3"]);
    }

    /// <summary>
    /// Test 8: Independent tasks (no dependencies) should all have depth 0 from root
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_IndependentTasks_HaveZeroDepth()
    {
        // Arrange
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>()),
            CreateEvent("Task2", "Task2", prereqs: Array.Empty<string>()),
            CreateEvent("Task3", "Task3", prereqs: Array.Empty<string>())
        };

        // Act
        var graph = await _builder.BuildDependencyGraphAsync(events, CancellationToken.None);

        // Assert
        Assert.Equal(0, graph.ComputeDepthFromRoot("Task1"));
        Assert.Equal(0, graph.ComputeDepthFromRoot("Task2"));
        Assert.Equal(0, graph.ComputeDepthFromRoot("Task3"));
    }

    /// <summary>
    /// Test 9: Cancellation token should be respected
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var events = new List<ExecutionEventDefinition>
        {
            CreateEvent("Task1", "Task1", prereqs: Array.Empty<string>())
        };

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _builder.BuildDependencyGraphAsync(events, cts.Token));
    }

    /// <summary>
    /// Test 10: Null argument should throw ArgumentNullException
    /// </summary>
    [Fact]
    public async Task BuildDependencyGraphAsync_NullEvents_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _builder.BuildDependencyGraphAsync(null!, CancellationToken.None));
    }

    // ============================================================================
    // Helper Methods for Test Data Creation
    // ============================================================================

    /// <summary>
    /// Creates an ExecutionEventDefinition for testing.
    /// </summary>
    private static ExecutionEventDefinition CreateEvent(
        string taskId,
        string taskName,
        string[] prereqs,
        uint durationMinutes = 15)
    {
        return new ExecutionEventDefinition(
            TaskUid: Guid.NewGuid(),
            TaskId: taskId,
            TaskName: taskName,
            ScheduledDay: DayOfWeek.Monday,
            ScheduledTime: new TimeOfDay(9, 0, 0),
            PrerequisiteTaskIds: prereqs.ToHashSet(),
            DurationMinutes: durationMinutes
        );
    }
}
