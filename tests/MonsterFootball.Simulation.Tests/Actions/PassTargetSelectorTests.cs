using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class PassTargetSelectorTests
{
    private readonly PassTargetSelector _selector = new(new PassResolver());

    /// <summary>Home CM at the given spot; everyone else parked on the goal lines (see <see cref="Scenario"/>).</summary>
    private static Scenario WithCarrierAt(double x, double y)
    {
        var s = new Scenario();
        s.Place(TeamSide.Home, PlayingPosition.CM, x, y);
        return s;
    }

    [Fact]
    public void Prefers_an_open_receiver_over_a_marked_one()
    {
        var s = WithCarrierAt(40, 34);
        var open = s.Place(TeamSide.Home, PlayingPosition.LCM, 52, 44);
        s.Place(TeamSide.Home, PlayingPosition.RCM, 52, 24);
        s.Place(TeamSide.Away, PlayingPosition.CM, 52.5, 23.5);   // marking RCM

        var intent = _selector.Choose(s.State, s.State.Slot(TeamSide.Home, PlayingPosition.CM));

        Assert.Equal(open.PlayerId, intent!.Receiver);
    }

    [Fact]
    public void Prefers_progressing_the_ball_when_both_options_are_open()
    {
        var s = WithCarrierAt(40, 34);
        var forward = s.Place(TeamSide.Home, PlayingPosition.ST, 55, 34);
        s.Place(TeamSide.Home, PlayingPosition.LCB, 25, 34);

        var intent = _selector.Choose(s.State, s.State.Slot(TeamSide.Home, PlayingPosition.CM));

        Assert.Equal(forward.PlayerId, intent!.Receiver);
    }

    [Fact]
    public void Never_selects_the_passer_or_a_teammate_too_close()
    {
        var s = WithCarrierAt(40, 34);
        var tooClose = s.Place(TeamSide.Home, PlayingPosition.LCM, 42, 35);
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);

        var intent = _selector.Choose(s.State, carrier);

        Assert.NotNull(intent);
        Assert.NotEqual(carrier.PlayerId, intent.Receiver);
        Assert.NotEqual(tooClose.PlayerId, intent.Receiver);
    }

    [Fact]
    public void Away_team_progression_is_toward_decreasing_x()
    {
        var s = new Scenario();
        s.Place(TeamSide.Away, PlayingPosition.CM, 60, 34);
        var forward = s.Place(TeamSide.Away, PlayingPosition.ST, 45, 34);
        s.Place(TeamSide.Away, PlayingPosition.LCB, 75, 34);

        var intent = _selector.Choose(s.State, s.State.Slot(TeamSide.Away, PlayingPosition.CM));

        Assert.Equal(forward.PlayerId, intent!.Receiver);
    }
}
