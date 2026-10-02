using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class DeterminismTests
{
    private static MatchResult Run(int seed) =>
        new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed)
            .RunToCompletion();

    private static string Fingerprint(MatchResult result) => MatchFingerprint.Create(result);

    [Theory]
    [InlineData(123456)]
    [InlineData(1)]
    [InlineData(-42)]
    public void Same_setup_and_seed_produce_identical_matches(int seed)
    {
        Assert.Equal(Fingerprint(Run(seed)), Fingerprint(Run(seed)));
    }

    [Fact]
    public void Different_seeds_can_produce_different_matches()
    {
        var fingerprints = Enumerable.Range(0, 20).Select(seed => Fingerprint(Run(seed)));
        Assert.True(fingerprints.Distinct().Count() > 1);
    }
}
