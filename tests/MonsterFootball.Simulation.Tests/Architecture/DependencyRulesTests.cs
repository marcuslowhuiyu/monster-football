using System.Reflection;

namespace MonsterFootball.Simulation.Tests.Architecture;

/// <summary>
/// Enforces ADR-001 / ADR-003: the simulation is a plain .NET library that
/// never depends on Godot or on the presentation layer.
/// </summary>
public class DependencyRulesTests
{
    private static readonly Assembly SimulationAssembly =
        Assembly.Load("MonsterFootball.Simulation");

    [Fact]
    public void Simulation_does_not_reference_Godot_or_presentation()
    {
        string[] forbidden = ["GodotSharp", "GodotSharpEditor", "MonsterFootball.Godot"];

        var offending = SimulationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null &&
                           forbidden.Any(f => name.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(offending.Count == 0,
            $"Simulation must not reference Godot/presentation assemblies, but references: {string.Join(", ", offending)}");
    }

    [Fact]
    public void Simulation_targets_plain_dotnet()
    {
        Assert.Equal("MonsterFootball.Simulation", SimulationAssembly.GetName().Name);
    }
}
