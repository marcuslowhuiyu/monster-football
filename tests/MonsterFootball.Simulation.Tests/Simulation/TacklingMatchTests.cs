using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

public class TacklingMatchTests
{
    private static MatchResult Run(int seed = 123456) =>
        CheckedRun.ToCompletion(new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed));

    [Fact]
    public void Carries_and_tackles_happen_in_a_full_match()
    {
        var events = Run().Events;
        Assert.True(events.Count(e => e.Type == MatchEventType.CarryStarted) > 20);
        Assert.True(events.Count(e => e.Type == MatchEventType.TackleAttempted) > 20);
    }

    [Fact]
    public void Every_tackle_attempt_is_followed_by_exactly_one_outcome()
    {
        var events = Run().Events;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Type != MatchEventType.TackleAttempted)
                continue;

            var next = events[i + 1];
            Assert.Contains(next.Type, new[] { MatchEventType.TackleWon, MatchEventType.TackleLost, MatchEventType.TackleLooseBall });
            Assert.Equal(events[i].ActorPlayerId, next.ActorPlayerId);
            Assert.Equal(events[i].TargetPlayerId, next.TargetPlayerId);
        }
    }

    [Fact]
    public void Won_tackles_always_change_possession()
    {
        var events = Run().Events;
        for (var i = 0; i < events.Count; i++)
            if (events[i].Type == MatchEventType.TackleWon)
                Assert.Equal(MatchEventType.PossessionChanged, events[i + 1].Type);
    }

    [Fact]
    public void Tacklers_challenge_opponents()
    {
        var result = Run();
        foreach (var e in result.Events.Where(e => e.Type == MatchEventType.TackleAttempted))
            Assert.NotEqual(result.FinalState.Player(e.ActorPlayerId!.Value).Team, result.FinalState.Player(e.TargetPlayerId!.Value).Team);
    }
}
