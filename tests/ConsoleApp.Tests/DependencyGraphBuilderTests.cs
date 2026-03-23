namespace ProductionDependencyLib.Tests;

using Models;
using Services;
using Xunit;

public class DependencyGraphBuilderTests
{
    [Fact]
    public void BuildGraph_ShouldCreateAssemblyDictionary()
    {
        // Arrange
        var assemblies = new List<Assembly>
        {
            new("A001", "Main Assembly"),
            new("A002", "Motor Assembly"),
            new("A003", "Control Board")
        };
        var builder = new DependencyGraphBuilder();

        // Act
        var graph = builder.BuildGraph(assemblies);

        // Assert
        Assert.Equal(3, graph.Assemblies.Count);
        Assert.Contains("A001", graph.Assemblies.Keys);
        Assert.Contains("A002", graph.Assemblies.Keys);
        Assert.Contains("A003", graph.Assemblies.Keys);
    }

    [Fact]
    public void BuildGraph_ShouldBuildDependencyRelationships()
    {
        // Arrange
        var mainAssembly = new Assembly("A001", "Main");
        mainAssembly.AddInput(new Subassembly("S001", "A002", "Motor", 1, 100));

        var motorAssembly = new Assembly("A002", "Motor");
        var assemblies = new List<Assembly> { mainAssembly, motorAssembly };

        var builder = new DependencyGraphBuilder();

        // Act
        var graph = builder.BuildGraph(assemblies);

        // Assert
        Assert.True(graph.Dependencies.ContainsKey("A001"));
        Assert.Contains("A002", graph.Dependencies["A001"]);
    }

    [Fact]
    public void ValidateGraph_ShouldDetectMissingReference()
    {
        // Arrange
        var assembly = new Assembly("A001", "Main");
        assembly.AddInput(new Subassembly("S001", "A999", "Missing", 1, 100));

        var graph = new DependencyGraphBuilder().BuildGraph(new List<Assembly> { assembly });
        var validator = new DependencyGraphBuilder();

        // Act
        var result = validator.ValidateGraph(graph);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("A999"));
    }

    [Fact]
    public void ValidateGraph_ShouldDetectCircularDependency()
    {
        // Arrange
        var a1 = new Assembly("A001", "Assembly 1");
        a1.AddInput(new Subassembly("S001", "A002", "Ref to A2", 1, 100));

        var a2 = new Assembly("A002", "Assembly 2");
        a2.AddInput(new Subassembly("S002", "A001", "Ref to A1", 1, 100));

        var graph = new DependencyGraphBuilder().BuildGraph(new List<Assembly> { a1, a2 });
        var validator = new DependencyGraphBuilder();

        // Act
        var result = validator.ValidateGraph(graph);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
