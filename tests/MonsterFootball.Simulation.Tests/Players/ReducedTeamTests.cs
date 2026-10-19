using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Players;

/// <summary>Dismissed players leave every system; teams re-form deterministically (D11, ADR-031).</summary>
public class ReducedTeamTests
{
    [Fact]
    public void A_sent_off_player_leaves_the_pitch_and_every_active_list()
    {
        var state = TestTeams.Match();
        var lcb = state.Slot(TeamSide.Home, PlayingPosition.LCB);

        state.SendOff(lcb.PlayerId);

        Assert.Equal(PlayerStatus.SentOff, lcb.Status);
        Assert.DoesNotContain(lcb, state.ActivePlayers);
        Assert.DoesNotContain(lcb, state.TeamPlayers(TeamSide.Home));
        Assert.False(state.Pitch.Contains(lcb.Position));
        Assert.Equal(22, state.Players.Count);
        MatchStateInvariants.Verify(state);
    }

    [Fact]
    public void Ten_men_play_4_4_1_with_the_gap_filled_from_the_dropped_role()
    {
        var state = TestTeams.Match();
        var cm = state.Slot(TeamSide.Home, PlayingPosition.CM);

        state.SendOff(state.Slot(TeamSide.Home, PlayingPosition.LCB).PlayerId);

        Assert.Equal(PlayingPosition.LCB, cm.Role); // the CM drops into the back four
        Assert.Equal(ShapeTemplates.OutfieldRoles(9).OrderBy(r => r), state.TeamPlayers(TeamSide.Home).Where(p => !p.Role.IsGoalkeeper()).Select(p => p.Role).OrderBy(r => r));
    }

    [Fact]
    public void Losing_the_dropped_role_changes_nobody()
    {
        var state = TestTeams.Match();
        var before = state.TeamPlayers(TeamSide.Home).ToDictionary(p => p.PlayerId, p => p.Role);

        state.SendOff(state.Slot(TeamSide.Home, PlayingPosition.CM).PlayerId);

        Assert.All(state.TeamPlayers(TeamSide.Home), p => Assert.Equal(before[p.PlayerId], p.Role));
    }

    [Fact]
    public void A_sent_off_keeper_is_replaced_by_the_outfielder_with_the_best_keeping()
    {
        var players = TestTeams.Players("Blue", 1).Select(d => d.Position == PlayingPosition.RB
            ? new PlayerDefinition(d.Id, d.Name, d.ShirtNumber, d.Position, new PlayerStats(5, 5, 5, 5, 5, 5, 5, 5, 7))
            : d).ToList();
        var state = MatchState.Create(MatchConfig.Default, new TeamDefinition("Blue", players), TestTeams.Team("Red", 101), TestTeams.KickoffLayout);
        var rb = state.TeamPlayers(TeamSide.Home).Single(p => p.Definition.Position == PlayingPosition.RB);

        state.SendOff(state.Goalkeeper(TeamSide.Home)!.PlayerId);

        Assert.Equal(PlayingPosition.GK, rb.Role);
        Assert.Same(rb, state.Goalkeeper(TeamSide.Home));
        Assert.Equal(9, state.TeamPlayers(TeamSide.Home).Count(p => !p.Role.IsGoalkeeper()));
        MatchStateInvariants.Verify(state);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(9, 9)]
    [InlineData(8, 8)]
    [InlineData(7, 7)]
    [InlineData(6, 6)]
    public void Templates_have_one_role_per_outfield_player(int outfield, int roles)
    {
        var template = ShapeTemplates.OutfieldRoles(outfield);

        Assert.Equal(roles, template.Count);
        Assert.Equal(roles, template.Distinct().Count());
        Assert.DoesNotContain(PlayingPosition.GK, template);
    }

    [Fact]
    public void Re_forming_is_deterministic()
    {
        IEnumerable<(PlayerId, PlayingPosition)> Roles()
        {
            var state = TestTeams.Match();
            foreach (var slot in new[] { PlayingPosition.ST, PlayingPosition.LB, PlayingPosition.GK })
                state.SendOff(state.Slot(TeamSide.Away, slot).PlayerId);
            return state.TeamPlayers(TeamSide.Away).Select(p => (p.PlayerId, p.Role)).ToList();
        }

        Assert.Equal(Roles(), Roles());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public void Full_matches_with_dismissals_run_cleanly(int seed)
    {
        // Send players off at the first stoppages after 10, 30, 50 and 70 minutes, both sides, keepers included,
        // leaving Home with 7 and Away with 9 before any real red cards.
        var dismissals = new Queue<(double Minute, TeamSide Side, PlayingPosition Slot)>(
        [
            (10, TeamSide.Home, PlayingPosition.GK),
            (30, TeamSide.Away, PlayingPosition.CM),
            (40, TeamSide.Home, PlayingPosition.LCB),
            (50, TeamSide.Home, PlayingPosition.ST),
            (60, TeamSide.Away, PlayingPosition.GK),
            (70, TeamSide.Home, PlayingPosition.LW),
        ]);
        var simulation = new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed);
        var state = simulation.State;
        var sentOffAt = new Dictionary<PlayerId, double>();
        while (!simulation.IsFinished)
        {
            simulation.Step();
            MatchStateInvariants.Verify(state);
            if (dismissals.Count > 0 && state.Clock.Now.Seconds >= dismissals.Peek().Minute * 60 && state.Phase == MatchPhase.Stopped)
            {
                var (_, side, slot) = dismissals.Dequeue();
                var id = state.TeamPlayers(side).First(p => p.Definition.Position == slot).PlayerId;
                state.SendOff(id);
                sentOffAt[id] = state.Clock.Now.Seconds;
                MatchStateInvariants.Verify(state);
            }
        }

        Assert.Empty(dismissals);
        // Real red cards (ADR-032) can come on top of the forced dismissals.
        int RedCards(TeamSide side) => simulation.Events.Count(e => e.Type == MatchEventType.RedCard && e.Team == side);
        Assert.Equal(7 - RedCards(TeamSide.Home), state.TeamPlayers(TeamSide.Home).Count());
        Assert.Equal(9 - RedCards(TeamSide.Away), state.TeamPlayers(TeamSide.Away).Count());
        var result = simulation.RunToCompletion();
        foreach (var e in result.Events.Where(e => e.Type == MatchEventType.RedCard))
            sentOffAt.TryAdd(e.ActorPlayerId!.Value, e.Timestamp.Seconds);
        Assert.DoesNotContain(result.Events, e => e.ActorPlayerId is { } a && sentOffAt.TryGetValue(a, out var at) && e.Timestamp.Seconds > at);
        var lateEvents = result.Events.Where(e => e.Timestamp.Seconds > 75 * 60).ToList();
        Assert.Contains(lateEvents, e => e.Type == MatchEventType.PassAttempted && state.Player(e.ActorPlayerId!.Value).Team == TeamSide.Home);
    }
}
