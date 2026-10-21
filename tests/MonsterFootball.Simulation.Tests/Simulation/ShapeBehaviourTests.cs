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
        // Marking and support legitimately bring a few players close to the ball at times,
        // so judge the distribution over a whole match rather than every single tick.
        var simulation = NewSimulation();
        var nearBall = new List<int>();
        while (!simulation.IsFinished)
        {
            simulation.Step();
            nearBall.Add(simulation.State.Players.Count(p => p.Position.DistanceTo(simulation.State.Ball.Position) < 5));
        }

        nearBall.Sort();
        var mean = nearBall.Average();
        var p95 = nearBall[(int)(nearBall.Count * 0.95)];
        Assert.True(mean <= 2.5, $"mean {mean:0.00} players within 5 m of the ball");
        Assert.True(p95 <= 4, $"95th percentile {p95} players within 5 m of the ball");
        Assert.True(nearBall[^1] <= 9, $"peak {nearBall[^1]} players within 5 m of the ball");
    }

    [Fact]
    public void Each_team_keeps_width_and_depth()
    {
        // Judge the typical open-play shape over the first ten minutes, not one moment: just after a set piece a
        // team can briefly be packed into its own box, and when that happens depends on stoppage lengths (ADR-037).
        var simulation = NewSimulation();
        var depths = Enum.GetValues<TeamSide>().ToDictionary(side => side, _ => new List<double>());
        var widths = Enum.GetValues<TeamSide>().ToDictionary(side => side, _ => new List<double>());
        for (var tick = 0; tick < 4 * 600; tick++)
        {
            simulation.Step();
            if (simulation.State.Phase != MatchPhase.OpenPlay)
                continue;

            foreach (var side in Enum.GetValues<TeamSide>())
            {
                var outfield = simulation.State.TeamPlayers(side).Where(p => !p.Definition.Position.IsGoalkeeper()).ToList();
                depths[side].Add(outfield.Max(p => p.Position.X) - outfield.Min(p => p.Position.X));
                widths[side].Add(outfield.Max(p => p.Position.Y) - outfield.Min(p => p.Position.Y));
            }
        }

        foreach (var side in Enum.GetValues<TeamSide>())
        {
            var depth = Median(depths[side]);
            var width = Median(widths[side]);
            Assert.True(depth > 20, $"{side} median depth {depth:0.0}");
            Assert.True(width > 30, $"{side} median width {width:0.0}");
        }
    }

    private static double Median(List<double> values) => values.Order().ElementAt(values.Count / 2);

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
