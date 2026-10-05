using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Positioning;

public class TeamShapeSystemTests
{
    private static readonly PlayingPosition[] Defenders =
        [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB];

    private static readonly PlayingPosition[] Midfielders =
        [PlayingPosition.LCM, PlayingPosition.CM, PlayingPosition.RCM];

    /// <summary>Places the ball at <paramref name="ball"/>, held by the given side's CM (or loose), then assigns targets.</summary>
    private static MatchState Arrange(Vec2 ball, TeamSide? possession)
    {
        var state = TestTeams.Match();
        if (possession is { } side)
        {
            var carrier = state.Slot(side, PlayingPosition.CM);
            carrier.Position = ball;
            state.GiveBallTo(carrier.PlayerId);
        }
        else
        {
            state.SetBallLoose(ball);
        }

        new TeamShapeSystem().Apply(state);
        return state;
    }

    private static Vec2 Target(MatchState state, TeamSide side, PlayingPosition slot) =>
        state.Slot(side, slot).MovementTarget;

    public static TheoryData<double, double> BallPositions() => new()
    {
        { 10, 34 }, { 30, 10 }, { 52.5, 34 }, { 70, 60 }, { 85, 34 }, { 100, 5 },
    };

    [Theory]
    [MemberData(nameof(BallPositions))]
    public void Defending_back_four_holds_a_flat_line_goal_side_of_the_ball(double x, double y)
    {
        var state = Arrange(new Vec2(x, y), TeamSide.Away);
        var presser = state.TeamPlayers(TeamSide.Home).Single(p => p.MovementIntensity == MovementIntensity.Sprint);

        var lineXs = Defenders
            .Select(slot => state.Slot(TeamSide.Home, slot))
            .Where(p => p != presser)
            .Select(p => p.MovementTarget.X)
            .Distinct()
            .ToList();

        var lineX = Assert.Single(lineXs);
        Assert.True(lineX < x || lineX <= 6, $"line at {lineX} should be goal-side of ball at {x}");
    }

    [Fact]
    public void Defending_team_recovers_toward_own_goal()
    {
        var ball = new Vec2(60, 34);
        var state = Arrange(ball, TeamSide.Away);

        var shape = state.TeamPlayers(TeamSide.Home)
            .Where(p => p.MovementIntensity != MovementIntensity.Sprint)
            .ToList();

        Assert.True(shape.Average(p => p.MovementTarget.X) < ball.X);
        Assert.All(shape.Where(p => p.Definition.Position.Group() == PositionGroup.Defender),
            p => Assert.True(p.MovementTarget.X < ball.X, p.ToString()));
    }

    [Fact]
    public void Defending_team_sends_exactly_one_outfield_presser_to_the_ball()
    {
        var ball = new Vec2(40, 30);
        var state = Arrange(ball, TeamSide.Away);

        var presser = Assert.Single(state.TeamPlayers(TeamSide.Home), p => p.MovementIntensity == MovementIntensity.Sprint);
        Assert.False(presser.Definition.Position.IsGoalkeeper());

        // Closes down to a jockeying spot goal-side of the carrier, inside challenge range.
        Assert.Equal(TeamShapeSystem.JockeyDistance, presser.MovementTarget.DistanceTo(ball), precision: 9);
        Assert.True(presser.MovementTarget.X < ball.X, "presser should be goal-side (Home defends X = 0)");
        Assert.True(TeamShapeSystem.JockeyDistance < MonsterFootball.Simulation.Actions.Tackling.TackleSystem.ChallengeRange);

        var nearest = state.TeamPlayers(TeamSide.Home)
            .Where(p => !p.Definition.Position.IsGoalkeeper())
            .MinBy(p => p.Position.DistanceTo(ball));
        Assert.Same(nearest, presser);
    }

    [Fact]
    public void Loose_ball_presser_chases_the_ball_itself()
    {
        var ball = new Vec2(40, 30);
        var state = Arrange(ball, possession: null);

        var presser = Assert.Single(state.TeamPlayers(TeamSide.Home), p => p.MovementIntensity == MovementIntensity.Sprint);
        Assert.Equal(ball, presser.MovementTarget);
    }

    [Fact]
    public void Shape_focus_snaps_to_the_ball_by_default_and_lags_when_smoothed()
    {
        var state = TestTeams.Match();
        var carrier = state.Slot(TeamSide.Home, PlayingPosition.CM);
        carrier.Position = new Vec2(80, 20);
        state.GiveBallTo(carrier.PlayerId);
        var shape = new TeamShapeSystem();

        shape.Apply(state, attackingSmoothing: 0.1, defendingSmoothing: 0.5);
        var homeFocus = state.ShapeFocus(TeamSide.Home);
        var awayFocus = state.ShapeFocus(TeamSide.Away);

        // From the centre spot (52.5, 34) toward the ball (80, 20).
        Assert.Equal(new Vec2(52.5 + 0.1 * 27.5, 34 - 0.1 * 14), homeFocus);
        Assert.Equal(new Vec2(52.5 + 0.5 * 27.5, 34 - 0.5 * 14), awayFocus);

        shape.Apply(state);
        Assert.Equal(state.Ball.Position, state.ShapeFocus(TeamSide.Home));
    }

    [Fact]
    public void Team_in_possession_holds_position_at_easy_pace()
    {
        var state = Arrange(new Vec2(52.5, 34), TeamSide.Home);
        Assert.Equal(MovementIntensity.Easy, state.Slot(TeamSide.Home, PlayingPosition.LB).MovementIntensity);
        Assert.Equal(MovementIntensity.Jog, state.Slot(TeamSide.Away, PlayingPosition.LB).MovementIntensity);
    }

    [Fact]
    public void Team_in_possession_does_not_press()
    {
        var state = Arrange(new Vec2(40, 30), TeamSide.Away);
        Assert.DoesNotContain(state.TeamPlayers(TeamSide.Away), p => p.MovementIntensity == MovementIntensity.Sprint);
    }

    [Fact]
    public void Loose_ball_draws_one_presser_from_each_team()
    {
        var state = Arrange(new Vec2(52.5, 34), possession: null);

        foreach (var side in Enum.GetValues<TeamSide>())
            Assert.Single(state.TeamPlayers(side), p => p.MovementIntensity == MovementIntensity.Sprint);
    }

    [Fact]
    public void Ball_carrier_is_not_moved_by_shape()
    {
        var ball = new Vec2(52.5, 34);
        var state = Arrange(ball, TeamSide.Home);
        Assert.Equal(ball, state.Slot(TeamSide.Home, PlayingPosition.CM).MovementTarget);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(52.5)]
    [InlineData(80)]
    public void In_possession_centre_backs_stay_behind_the_ball(double ballX)
    {
        var state = Arrange(new Vec2(ballX, 34), TeamSide.Home);

        Assert.True(Target(state, TeamSide.Home, PlayingPosition.LCB).X < ballX);
        Assert.True(Target(state, TeamSide.Home, PlayingPosition.RCB).X < ballX);
    }

    [Fact]
    public void In_possession_fullbacks_advance_beyond_centre_backs()
    {
        var state = Arrange(new Vec2(52.5, 34), TeamSide.Home);

        Assert.True(Target(state, TeamSide.Home, PlayingPosition.LB).X > Target(state, TeamSide.Home, PlayingPosition.LCB).X);
        Assert.True(Target(state, TeamSide.Home, PlayingPosition.RB).X > Target(state, TeamSide.Home, PlayingPosition.RCB).X);
    }

    [Fact]
    public void In_possession_midfielders_offer_options_at_different_depths()
    {
        var state = Arrange(new Vec2(40, 34), TeamSide.Home);
        // The CM holds the ball here, so compare the two others against the CM's own anchor depth.
        var holdingDepth = Formation433.InPossession(state.Pitch, PlayingPosition.CM).X;

        Assert.True(Target(state, TeamSide.Home, PlayingPosition.LCM).X > holdingDepth);
        Assert.True(Target(state, TeamSide.Home, PlayingPosition.RCM).X > holdingDepth);
    }

    [Fact]
    public void In_possession_wingers_create_width_and_striker_is_advanced_and_central()
    {
        var state = Arrange(new Vec2(52.5, 34), TeamSide.Home);
        var centreY = state.Pitch.Width / 2;

        Assert.True(Math.Abs(Target(state, TeamSide.Home, PlayingPosition.LW).Y - centreY) > 20);
        Assert.True(Math.Abs(Target(state, TeamSide.Home, PlayingPosition.RW).Y - centreY) > 20);

        var striker = Target(state, TeamSide.Home, PlayingPosition.ST);
        Assert.True(Math.Abs(striker.Y - centreY) < 8);
        Assert.Equal(
            state.TeamPlayers(TeamSide.Home).Max(p => p.MovementTarget.X),
            striker.X);
    }

    [Fact]
    public void Out_of_possession_midfield_protects_central_areas()
    {
        var state = Arrange(new Vec2(70, 34), TeamSide.Away);
        var centreY = state.Pitch.Width / 2;

        foreach (var slot in Midfielders)
            Assert.True(Math.Abs(Target(state, TeamSide.Home, slot).Y - centreY) < 15, slot.ToString());
    }

    [Fact]
    public void Out_of_possession_layers_are_ordered_defence_midfield_attack()
    {
        var state = Arrange(new Vec2(52.5, 34), TeamSide.Away);
        var shape = state.TeamPlayers(TeamSide.Home).Where(p => p.MovementIntensity != MovementIntensity.Sprint).ToList();

        double MaxX(PositionGroup g) => shape.Where(p => p.Definition.Position.Group() == g).Max(p => p.MovementTarget.X);
        double MinX(PositionGroup g) => shape.Where(p => p.Definition.Position.Group() == g).Min(p => p.MovementTarget.X);

        Assert.True(MaxX(PositionGroup.Defender) < MinX(PositionGroup.Midfielder));
        Assert.True(MaxX(PositionGroup.Midfielder) < MinX(PositionGroup.Forward));
    }

    [Theory]
    [MemberData(nameof(BallPositions))]
    public void Shape_is_mirror_symmetric_between_home_and_away(double x, double y)
    {
        var pitch = PitchGeometry.Standard;
        var home = Arrange(new Vec2(x, y), TeamSide.Home);
        var away = Arrange(new Vec2(pitch.Length - x, pitch.Width - y), TeamSide.Away);

        foreach (var slot in Enum.GetValues<PlayingPosition>())
        {
            var homeTarget = Target(home, TeamSide.Home, slot);
            var awayTarget = Target(away, TeamSide.Away, slot);
            Assert.Equal(homeTarget.X, pitch.Length - awayTarget.X, precision: 9);
            Assert.Equal(homeTarget.Y, pitch.Width - awayTarget.Y, precision: 9);
        }
    }

    [Fact]
    public void All_targets_stay_on_the_pitch_for_any_ball_position()
    {
        foreach (var possession in new TeamSide?[] { TeamSide.Home, TeamSide.Away, null })
            for (var x = 0.0; x <= 105; x += 7.5)
                for (var y = 0.0; y <= 68; y += 8.5)
                {
                    var state = Arrange(new Vec2(x, y), possession);
                    Assert.All(state.Players, p => Assert.True(state.Pitch.Contains(p.MovementTarget), $"{p} -> {p.MovementTarget}"));
                }
    }
}
