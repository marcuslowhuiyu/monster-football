using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MonsterFootball.Simulation.Tests.Architecture;

/// <summary>
/// Enforces ADR-005: simulation code must not touch non-deterministic sources.
/// Scans the compiled simulation assembly's type references.
/// </summary>
/// <remarks>
/// Reads the assembly from the simulation project's own build output, not the copy next to the tests:
/// code-coverage tools (coverlet) rewrite that copy with tracking code that references DateTime and Guid.
/// </remarks>
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
        var path = OriginalSimulationAssemblyPath();
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

    private static string OriginalSimulationAssemblyPath()
    {
        // Test output is .../tests/<TestProject>/bin/<Configuration>/<TargetFramework>/
        var outputDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetFramework = outputDir.Name;
        var configuration = outputDir.Parent!.Name;

        var root = outputDir;
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "MonsterFootball.slnx")))
            root = root.Parent;

        Assert.True(root is not null, $"Could not find the repository root above {AppContext.BaseDirectory}.");

        var path = Path.Combine(root!.FullName, "src", "MonsterFootball.Simulation", "bin", configuration, targetFramework, "MonsterFootball.Simulation.dll");
        Assert.True(File.Exists(path), $"Simulation assembly not found at {path}.");
        return path;
    }
}
