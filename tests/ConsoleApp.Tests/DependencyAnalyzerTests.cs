namespace ProductionDependencyLib.Tests;

using Models;
using Services;
using Xunit;

public class DependencyAnalyzerTests
{
    [Fact]
    public void GetAllDependencies_ShouldReturnAllSubassemblies()
    {
        // Arrange
        var mainAssembly = new Assembly("A001", "Main");
        mainAssembly.AddInput(new Subassembly("S001", "A002", "Motor", 1, 100));
        mainAssembly.AddInput(new Subassembly("S002", "A003", "Board", 1, 80));

        var motorAssembly = new Assembly("A002", "Motor");
        var boardAssembly = new Assembly("A003", "Board");

        var graph = new DependencyGraphBuilder().BuildGraph(
            new List<Assembly> { mainAssembly, motorAssembly, boardAssembly }
        );

        var analyzer = new DependencyAnalyzer();

        // Act
        var dependencies = analyzer.GetAllDependencies(graph, "A001");

        // Assert
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(motorAssembly, dependencies);
        Assert.Contains(boardAssembly, dependencies);
    }

    [Fact]
    public void CalculateTotalCost_ShouldSumDirectAndIndirectCosts()
    {
        // Arrange
        var mainAssembly = new Assembly("A001", "Main");
        mainAssembly.AddInput(new Part("P001", "BOLT", "Bolt", 5, 10));  // 5 * 10 = 50
        mainAssembly.AddInput(new Subassembly("S001", "A002", "Motor", 1, 100));

        var motorAssembly = new Assembly("A002", "Motor");
        motorAssembly.AddInput(new Part("P002", "WIRE", "Wire", 10, 5));  // 10 * 5 = 50

        var graph = new DependencyGraphBuilder().BuildGraph(
            new List<Assembly> { mainAssembly, motorAssembly }
        );

        var analyzer = new DependencyAnalyzer();

        // Act
        var totalCost = analyzer.CalculateTotalCost(graph, "A001");

        // Assert
        // Main: 50 + Subassembly (cost of 100) + Motor (50) = 200
        Assert.Equal(200, totalCost);
    }

    [Fact]
    public void CalculateTotalLaborHours_ShouldSumAllLaborHours()
    {
        // Arrange
        var mainAssembly = new Assembly("A001", "Main");
        mainAssembly.AddInput(new Labor("L001", "Assembly", 5, 50));
        mainAssembly.AddInput(new Subassembly("S001", "A002", "Motor", 1, 100));

        var motorAssembly = new Assembly("A002", "Motor");
        motorAssembly.AddInput(new Labor("L002", "Winding", 8, 45));
        motorAssembly.AddInput(new Labor("L003", "Testing", 2, 50));

        var graph = new DependencyGraphBuilder().BuildGraph(
            new List<Assembly> { mainAssembly, motorAssembly }
        );

        var analyzer = new DependencyAnalyzer();

        // Act
        var totalHours = analyzer.CalculateTotalLaborHours(graph, "A001");

        // Assert
        // Main: 5 + Motor: 8 + 2 = 15
        Assert.Equal(15, totalHours);
    }

    [Fact]
    public void GetBillOfMaterials_ShouldIncludeSummaryInfo()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        assembly.AddInput(new Labor("L001", "Work", 5, 50));  // 250
        assembly.AddInput(new Part("P001", "BOLT", "Bolt", 10, 0.5m));  // 5

        var graph = new DependencyGraphBuilder().BuildGraph(new List<Assembly> { assembly });
        var analyzer = new DependencyAnalyzer();

        // Act
        var bom = analyzer.GetBillOfMaterials(graph, "A001");

        // Assert
        Assert.Equal("A001", bom.AssemblyId);
        Assert.Equal("Test Assembly", bom.AssemblyName);
        Assert.Equal(5, bom.TotalLaborHours);
        Assert.Equal(255, bom.TotalCost);
        Assert.Equal(10, bom.TotalPartCount);
    }
}
