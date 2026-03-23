namespace ProductionDependencyLib.Tests;

using ProductionDependencyLib.Models;
using ProductionDependencyLib.Services;
using Xunit;

public class AssemblyTests
{
    [Fact]
    public void Assembly_AddInput_ShouldIncludeInput()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        var labor = new Labor("L001", "Assembly Work", 5, 50);

        // Act
        assembly.AddInput(labor);

        // Assert
        Assert.Single(assembly.Inputs);
        Assert.Contains(labor, assembly.Inputs);
    }

    [Fact]
    public void Assembly_GetTotalCost_ShouldSumAllInputCosts()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        assembly.AddInput(new Labor("L001", "Work", 5, 50));  // 5 * 50 = 250
        assembly.AddInput(new Part("P001", "BOLT-M8", "M8 Bolt", 10, 0.5m));  // 10 * 0.5 = 5

        // Act
        var totalCost = assembly.GetTotalCost();

        // Assert
        Assert.Equal(255, totalCost);
    }

    [Fact]
    public void Assembly_GetTotalLaborHours_ShouldSumLaborHours()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        assembly.AddInput(new Labor("L001", "Work 1", 5, 50));
        assembly.AddInput(new Labor("L002", "Work 2", 3, 50));
        assembly.AddInput(new Part("P001", "BOLT", "Bolt", 10, 0.5m));

        // Act
        var totalHours = assembly.GetTotalLaborHours();

        // Assert
        Assert.Equal(8, totalHours);
    }

    [Fact]
    public void Assembly_GetSubassemblies_ShouldReturnOnlySubassemblyInputs()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        assembly.AddInput(new Labor("L001", "Work", 5, 50));
        assembly.AddInput(new Subassembly("S001", "A002", "Motor", 1, 150));
        assembly.AddInput(new Part("P001", "BOLT", "Bolt", 10, 0.5m));

        // Act
        var subassemblies = assembly.GetSubassemblies().ToList();

        // Assert
        Assert.Single(subassemblies);
        Assert.IsType<Subassembly>(subassemblies[0]);
    }

    [Fact]
    public void Assembly_RemoveInput_ShouldRemoveInputById()
    {
        // Arrange
        var assembly = new Assembly("A001", "Test Assembly");
        var labor = new Labor("L001", "Work", 5, 50);
        assembly.AddInput(labor);

        // Act
        assembly.RemoveInput("L001");

        // Assert
        Assert.Empty(assembly.Inputs);
    }
}
