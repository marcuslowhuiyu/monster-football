using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

/// <summary>On-ball actions inside a full match: event-stream consistency and possession bookkeeping.</summary>
public class PossessionFlowMatchTests
{
    private static MatchResult Run(int seed = 123456) =>
        CheckedRun.ToCompletion(new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed));

    [Fact]
    public void Every_pass_attempt_has_exactly_one_outcome()
    {
        var events = Run().Events;
        var attempts = 0;
        var pending = false;
        foreach (var e in events)
        {
            if (e.Type == MatchEventType.PassAttempted)
            {
                Assert.False(pending, $"pass at {e.Timestamp} launched while another had no outcome");
                attempts++;
                pending = true;
            }
            else if (e.Type is MatchEventType.PassCompleted or MatchEventType.PassIntercepted or MatchEventType.PassMisplaced
                     || (e.Type == MatchEventType.PlayStopped && e.Metadata["reason"] == "Offside" && pending))
            {
                Assert.True(pending, $"pass outcome at {e.Timestamp} without a pass");
                pending = false; // a pass received offside ends in the offside call
            }
        }

        Assert.True(attempts > 100, $"only {attempts} passes");
        Assert.False(pending, "a pass was still in the air at full time");
    }

    [Fact]
    public void Passes_are_made_by_the_player_holding_the_ball()
    {
        var events = Run().Events;
        PlayerId? holder = null;

        foreach (var e in events)
        {
            switch (e.Type)
            {
                case MatchEventType.Kickoff:
                case MatchEventType.RestartTaken:
                case MatchEventType.PassIntercepted:
                case MatchEventType.TackleWon:
                case MatchEventType.PossessionChanged:
                    holder = e.ActorPlayerId;
                    break;
                case MatchEventType.PassCompleted:
                    holder = e.TargetPlayerId;
                    break;
                case MatchEventType.PassMisplaced:
                case MatchEventType.TackleLooseBall:
                    holder = null; // a teammate may collect without an event
                    break;
                case MatchEventType.PassAttempted when holder is not null:
                    Assert.Equal(holder, e.ActorPlayerId);
                    break;
            }
        }
    }

    [Fact]
    public void Possession_changes_always_switch_team()
    {
        var events = Run().Events;
        TeamSide? team = null;
        // The opening kickoff establishes possession; every later change must flip the team
        // (including the conceding side's kickoff after a goal).
        foreach (var e in events.Where(e => e.Type is MatchEventType.Kickoff or MatchEventType.PossessionChanged))
        {
            if (e.Type == MatchEventType.Kickoff && team is not null)
                continue;
            if (e.Type == MatchEventType.PossessionChanged)
                Assert.NotEqual(team, e.Team);
            team = e.Team;
        }
    }

    [Fact]
    public void Completion_rate_is_plausible()
    {
        var events = Run().Events;
        double attempted = events.Count(e => e.Type == MatchEventType.PassAttempted);
        double completed = events.Count(e => e.Type == MatchEventType.PassCompleted);

        Assert.InRange(completed / attempted, 0.5, 0.99);
    }

    [Fact]
    public void Both_teams_get_the_ball()
    {
        var events = Run().Events;
        var passers = events.Where(e => e.Type == MatchEventType.PassAttempted).Select(e => e.Team).Distinct();
        Assert.Equal(2, passers.Count());
    }
}
