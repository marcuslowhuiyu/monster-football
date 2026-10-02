using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MonsterFootball.Simulation.Tests.Architecture;

/// <summary>
/// Enforces ADR-005: simulation code must not touch non-deterministic sources.
/// Scans the compiled simulation assembly's type references.
/// </summary>
public class DeterminismGuardTests
{
    private static readonly string[] ForbiddenTypes =
    [
        "System.Random",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Guid",
        "System.Diagnostics.Stopwatch",
        "System.Threading.Tasks.Parallel",
    ];

    [Fact]
    public void Simulation_does_not_reference_nondeterministic_types()
    {
        var path = Assembly.Load("MonsterFootball.Simulation").Location;
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var referenced = reader.TypeReferences
            .Select(handle => reader.GetTypeReference(handle))
            .Select(t => $"{reader.GetString(t.Namespace)}.{reader.GetString(t.Name)}")
            .ToHashSet();

        var offending = ForbiddenTypes.Where(referenced.Contains).ToList();

        Assert.True(offending.Count == 0,
            $"Simulation references non-deterministic types: {string.Join(", ", offending)}. Use IMatchRandom and MatchClock instead.");
    }
}
