using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Laws;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Laws;

public class DisciplineTests
{
    /// <summary>Home ST on the ball at (x, y) attacking +X; Away LCB tackling from (tx, 34); every other Away outfielder parked deep in their own goal-side positions unless <paramref name="clearDefence"/>.</summary>
    private static (Scenario S, PlayerMatchState Carrier, PlayerMatchState Tackler) Foul(double x, double tx, bool clearDefence, params double[] draws)
    {
        var s = new Scenario(random: new ScriptedRandom(draws));
        foreach (var away in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper()))
            away.Position = clearDefence ? new Vec2(30, away.Position.Y) : new Vec2(103, away.Position.Y); // behind the ball, or goal-side
        var carrier = s.Place(TeamSide.Home, PlayingPosition.ST, x, 34);
        var tackler = s.Place(TeamSide.Away, PlayingPosition.LCB, tx, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.ST);
        return (s, carrier, tackler);
    }

    private static TackleResult Result(PlayerMatchState tackler, PlayerMatchState carrier, double severity) =>
        new(TackleOutcome.Foul, tackler.PlayerId, carrier.PlayerId, carrier.Position, carrier.Position, 0.5, 0.5) { Severity = severity };

    [Fact]
    public void Denying_an_obvious_chance_outside_the_area_is_a_red()
    {
        var (s, carrier, tackler) = Foul(83, 84, clearDefence: true);

        Assert.Equal((DisciplinaryOutcome.Red, CardReason.Dogso), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random));
    }

    [Fact]
    public void Denying_an_obvious_chance_in_the_area_while_playing_the_ball_is_a_yellow()
    {
        var (s, carrier, tackler) = Foul(95, 96, clearDefence: true);

        Assert.Equal((DisciplinaryOutcome.Yellow, CardReason.Dogso), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random));
    }

    [Fact]
    public void Denying_an_obvious_chance_in_the_area_from_behind_is_a_red()
    {
        var (s, carrier, tackler) = Foul(95, 94, clearDefence: true);

        Assert.Equal((DisciplinaryOutcome.Red, CardReason.Dogso), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random));
    }

    [Fact]
    public void Serious_foul_play_is_a_red()
    {
        var (s, carrier, tackler) = Foul(40, 41, clearDefence: false, 0.0);

        Assert.Equal((DisciplinaryOutcome.Red, CardReason.SeriousFoulPlay), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 1.0), s.Context.Random));
    }

    [Fact]
    public void Stopping_a_promising_attack_is_a_yellow()
    {
        var (s, carrier, tackler) = Foul(75, 76, clearDefence: false, 0.9999);
        foreach (var away in s.State.TeamPlayers(TeamSide.Away).Where(p => !p.Role.IsGoalkeeper() && p != tackler).Skip(1))
            away.Position = new Vec2(30, away.Position.Y); // just one defender left goal-side

        Assert.Equal((DisciplinaryOutcome.Yellow, CardReason.PromisingAttack), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random));
    }

    [Fact]
    public void A_harmless_foul_is_usually_not_carded_and_a_reckless_one_is_a_yellow()
    {
        var (s, carrier, tackler) = Foul(40, 41, clearDefence: false, 0.9999, 0.9999, 0.9999, 0.0);

        Assert.Equal((DisciplinaryOutcome.None, null), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random));
        Assert.Equal((DisciplinaryOutcome.Yellow, CardReason.Reckless), Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.8), s.Context.Random));
    }

    [Fact]
    public void A_second_caution_is_a_second_yellow()
    {
        var (s, carrier, tackler) = Foul(83, 84, clearDefence: true);
        tackler.YellowCards = 1;
        var inArea = Foul(95, 96, clearDefence: true);
        inArea.Tackler.YellowCards = 1;

        Assert.Equal(DisciplinaryOutcome.Red, Discipline.Decide(s.State, tackler, carrier, Result(tackler, carrier, 0.3), s.Context.Random).Outcome);
        Assert.Equal((DisciplinaryOutcome.SecondYellow, CardReason.SecondYellow),
            Discipline.Decide(inArea.S.State, inArea.Tackler, inArea.Carrier, Result(inArea.Tackler, inArea.Carrier, 0.3), inArea.S.Context.Random));
    }

    [Fact]
    public void A_yellow_is_recorded_and_adds_time()
    {
        var s = new Scenario();
        var player = s.State.Slot(TeamSide.Away, PlayingPosition.RB);

        s.Context.Caution(player, DisciplinaryOutcome.Yellow, "Reckless");

        Assert.Equal(1, player.YellowCards);
        Assert.True(player.IsActive);
        Assert.Equal(MatchEventType.YellowCard, s.EventTypes.Single());
        Assert.Equal(AddedTimeTuning.Default.PerCardSeconds, s.State.AddedTimeAllowanceSeconds);
    }

    [Fact]
    public void A_second_yellow_shows_both_cards_and_sends_the_player_off()
    {
        var s = new Scenario();
        var player = s.State.Slot(TeamSide.Away, PlayingPosition.RB);
        player.YellowCards = 1;

        s.Context.Caution(player, DisciplinaryOutcome.SecondYellow, "Reckless");

        Assert.Equal([MatchEventType.YellowCard, MatchEventType.RedCard], s.EventTypes);
        Assert.False(player.IsActive);
        Assert.Equal(10, s.State.TeamPlayers(TeamSide.Away).Count());
        MatchStateInvariants.Verify(s.State);
    }

    [Fact]
    public void A_fifth_dismissal_abandons_the_match()
    {
        var s = new Scenario();
        var slots = new[] { PlayingPosition.LB, PlayingPosition.RB, PlayingPosition.LW, PlayingPosition.RW, PlayingPosition.ST };
        foreach (var slot in slots)
            s.Context.Caution(s.State.TeamPlayers(TeamSide.Away).First(p => p.Definition.Position == slot), DisciplinaryOutcome.Red, "SeriousFoulPlay");

        Assert.True(s.State.Abandoned);
        Assert.Equal(MatchPhase.Finished, s.State.Phase);
        Assert.Equal([MatchEventType.MatchAbandoned, MatchEventType.MatchFinished], s.EventTypes.Skip(5).ToList());
        Assert.Equal(TeamSide.Away, s.Log.Events.Single(e => e.Type == MatchEventType.MatchAbandoned).Team);
    }

    [Fact]
    public void A_carded_foul_records_the_card_on_the_stoppage()
    {
        // attempt, contact, foul, then serious foul play.
        var s = new Scenario(awayStats: new PlayerStats(5, 5, 5, 5, 5, 10, 5, 5, 5), random: new ScriptedRandom(0.0, 0.0, 0.0, 0.0));
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        var tackler = s.Place(TeamSide.Away, PlayingPosition.CM, 39, 34); // from behind
        tackler.MovementIntensity = MovementIntensity.Sprint;
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();

        new TackleSystem(new TackleResolver(), AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Equal([MatchEventType.TackleAttempted, MatchEventType.FoulCommitted, MatchEventType.PlayStopped, MatchEventType.RedCard], s.EventTypes);
        Assert.Equal(DisciplinaryOutcome.Red, s.State.CurrentStoppage!.Discipline);
        Assert.False(tackler.IsActive);
    }
}
