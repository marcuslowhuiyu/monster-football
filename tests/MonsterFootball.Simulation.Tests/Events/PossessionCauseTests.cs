using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Events;

public class PossessionCauseTests
{
    [Theory]
    [InlineData(PossessionCause.Interception, true)]
    [InlineData(PossessionCause.Tackle, true)]
    [InlineData(PossessionCause.LooseBall, true)]
    [InlineData(PossessionCause.Save, true)]
    [InlineData(PossessionCause.Reception, true)]
    [InlineData(PossessionCause.Kickoff, false)]
    [InlineData(PossessionCause.KeeperRestart, false)]
    public void Only_open_play_causes_count_as_winning_the_ball(PossessionCause cause, bool openPlay) =>
        Assert.Equal(openPlay, cause.IsOpenPlay());

    [Fact]
    public void A_change_of_team_records_its_cause_on_the_event()
    {
        var s = new Scenario();
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        var defender = s.State.Slot(TeamSide.Away, PlayingPosition.CM);

        s.Context.GiveBall(defender.PlayerId, PossessionCause.Tackle);

        var changed = Assert.Single(s.Log.Events, e => e.Type == MatchEventType.PossessionChanged);
        Assert.Equal("Tackle", changed.Metadata["cause"]);
        Assert.Equal(PossessionCause.Tackle, changed.GetPossessionCause());
    }

    [Fact]
    public void Events_without_a_cause_report_none()
    {
        var e = new MatchEvent(new MatchTime(0), MatchEventType.PassAttempted);
        Assert.Null(e.GetPossessionCause());
    }
}
