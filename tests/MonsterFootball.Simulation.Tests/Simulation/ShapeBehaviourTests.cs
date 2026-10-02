using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Simulation;

/// <summary>Whole-match checks that the 22 players keep football shape rather than swarming the ball.</summary>
public class ShapeBehaviourTests
{
    private static MatchSimulation NewSimulation() =>
        new(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed: 123456);

    [Fact]
    public void Players_do_not_all_chase_the_ball()
    {
        var simulation = NewSimulation();
        for (var tick = 0; tick < 4 * 120; tick++)
        {
            simulation.Step();
            if (tick < 4 * 10)
                continue; // allow the press to arrive after kickoff

            var nearBall = simulation.State.Players.Count(p => p.Position.DistanceTo(simulation.State.Ball.Position) < 5);
            Assert.True(nearBall <= 3, $"{nearBall} players within 5 m of the ball at tick {tick}");
        }
    }

    [Fact]
    public void Each_team_keeps_width_and_depth()
    {
        var simulation = NewSimulation();
        for (var tick = 0; tick < 4 * 60; tick++)
            simulation.Step();

        foreach (var side in Enum.GetValues<TeamSide>())
        {
            var outfield = simulation.State.TeamPlayers(side).Where(p => !p.Definition.Position.IsGoalkeeper()).ToList();
            var depth = outfield.Max(p => p.Position.X) - outfield.Min(p => p.Position.X);
            var width = outfield.Max(p => p.Position.Y) - outfield.Min(p => p.Position.Y);

            Assert.True(depth > 20, $"{side} depth {depth:0.0}");
            Assert.True(width > 30, $"{side} width {width:0.0}");
        }
    }

    [Fact]
    public void Goalkeepers_stay_in_their_own_penalty_area_all_match()
    {
        var simulation = NewSimulation();
        while (!simulation.IsFinished)
        {
            simulation.Step();
            foreach (var side in Enum.GetValues<TeamSide>())
            {
                var keeper = simulation.State.Slot(side, PlayingPosition.GK);
                Assert.True(simulation.State.Pitch.IsInPenaltyArea(keeper.Position, side), keeper.ToString());
            }
        }
    }

    [Fact]
    public void Ball_travels_with_its_carrier()
    {
        var simulation = NewSimulation();
        for (var tick = 0; tick < 40; tick++)
        {
            simulation.Step();
            if (simulation.State.PossessionPlayerId is { } id)
                Assert.Equal(simulation.State.Player(id).Position, simulation.State.Ball.Position);
        }
    }
}
