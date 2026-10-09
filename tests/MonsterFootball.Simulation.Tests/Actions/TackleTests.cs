using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Actions;

public class TackleTests
{
    private readonly TackleResolver _resolver = new();

    /// <summary>Home CM has the ball at (40, 34) and has held it long enough to be challenged; Away CM is 1 m away.</summary>
    private static Scenario Duel(PlayerStats? homeStats = null, PlayerStats? awayStats = null, IMatchRandom? random = null)
    {
        var s = new Scenario(homeStats, awayStats, random);
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        s.Place(TeamSide.Away, PlayingPosition.CM, 41, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();
        return s;
    }

    private static TackleIntent Intent(Scenario s) =>
        new(s.State.Slot(TeamSide.Away, PlayingPosition.CM).PlayerId, s.State.Slot(TeamSide.Home, PlayingPosition.CM).PlayerId);

    private double WinRate(Scenario s, int trials = 5000)
    {
        var random = new MatchRng(77);
        var intent = Intent(s);
        return Enumerable.Range(0, trials).Count(_ => _resolver.Resolve(s.State, intent, random).Outcome == TackleOutcome.Won) / (double)trials;
    }

    [Fact]
    public void Strong_defender_beats_weak_carrier_more_often_than_the_reverse()
    {
        var strongDefender = Duel(homeStats: PlayerStats.Uniform(3), awayStats: PlayerStats.Uniform(8));
        var weakDefender = Duel(homeStats: PlayerStats.Uniform(8), awayStats: PlayerStats.Uniform(3));

        Assert.True(WinRate(strongDefender) - WinRate(weakDefender) > 0.25);
    }

    [Fact]
    public void Defending_power_and_awareness_raise_the_defender_rating()
    {
        var baseline = TackleResolver.DefenderRating(PlayerStats.Uniform(5));
        Assert.True(TackleResolver.DefenderRating(new PlayerStats(5, 5, 5, 8, 5, 5, 5, 5, 5)) > baseline);
        Assert.True(TackleResolver.DefenderRating(new PlayerStats(5, 5, 5, 5, 5, 8, 5, 5, 5)) > baseline);
        Assert.True(TackleResolver.DefenderRating(new PlayerStats(5, 5, 5, 5, 5, 5, 5, 8, 5)) > baseline);
    }

    [Fact]
    public void Technique_power_and_awareness_protect_the_carrier()
    {
        var baseline = TackleResolver.CarrierRating(PlayerStats.Uniform(5));
        Assert.True(TackleResolver.CarrierRating(new PlayerStats(8, 5, 5, 5, 5, 5, 5, 5, 5)) > baseline);
        Assert.True(TackleResolver.CarrierRating(new PlayerStats(5, 5, 5, 5, 5, 8, 5, 5, 5)) > baseline);
        Assert.True(TackleResolver.CarrierRating(new PlayerStats(5, 5, 5, 5, 5, 5, 5, 8, 5)) > baseline);
    }

    [Fact]
    public void Won_tackle_gives_the_defender_the_ball()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.9999, 0.0));
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.True(s.State.HasBall(tackler.PlayerId));
        Assert.Equal(TeamSide.Away, s.State.PossessionTeam);
        Assert.Equal(
            [MatchEventType.TackleAttempted, MatchEventType.TackleWon, MatchEventType.PossessionChanged],
            s.EventTypes);
        Assert.Equal(PossessionCause.Tackle, s.Log.Events[^1].GetPossessionCause());
    }

    [Fact]
    public void Lost_tackle_leaves_the_carrier_with_the_ball_and_the_defender_beaten()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.9999, 0.9999));
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.True(s.State.HasBall(carrier.PlayerId));
        Assert.Equal([MatchEventType.TackleAttempted, MatchEventType.TackleLost], s.EventTypes);
        Assert.True(tackler.TackleCooldownUntilTick > s.State.Clock.Tick);
    }

    [Fact]
    public void Loose_ball_tackle_leaves_nobody_in_possession()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.9999, 0.9999, 0.75, 0.5, 0.5));

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.True(s.State.Ball.IsLoose);
        Assert.Null(s.State.PossessionPlayerId);
        Assert.Equal([MatchEventType.TackleAttempted, MatchEventType.TackleLooseBall], s.EventTypes);
        var distance = s.State.Ball.Position.DistanceTo(new Vec2(40, 34));
        Assert.InRange(distance, TackleResolver.LooseBallMinDistance - 1e-9, TackleResolver.LooseBallMaxDistance + 1e-9);
    }

    [Fact]
    public void A_foul_outside_the_area_is_a_direct_free_kick_where_it_happened()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.0));
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Equal([MatchEventType.TackleAttempted, MatchEventType.FoulCommitted, MatchEventType.PlayStopped], s.EventTypes);
        var foul = s.Log.Events[1];
        Assert.Equal((tackler.PlayerId, carrier.PlayerId), (foul.ActorPlayerId!.Value, foul.TargetPlayerId!.Value));
        Assert.InRange(double.Parse(foul.Metadata["severity"], System.Globalization.CultureInfo.InvariantCulture), 0, 1);
        var stoppage = s.State.CurrentStoppage!;
        Assert.Equal((StoppageReason.Foul, RestartType.DirectFreeKick, TeamSide.Home), (stoppage.Reason, stoppage.Restart, stoppage.AwardedTeam));
        Assert.Equal((tackler.PlayerId, carrier.PlayerId), (stoppage.Offender!.Value, stoppage.Fouled!.Value));
        Assert.Equal(new Vec2(40, 34), stoppage.Location);
    }

    [Fact]
    public void A_foul_in_the_defenders_own_area_is_a_penalty()
    {
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.Place(TeamSide.Home, PlayingPosition.ST, 95, 34);
        s.Place(TeamSide.Away, PlayingPosition.LCB, 96, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.ST);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        var stoppage = s.State.CurrentStoppage!;
        Assert.Equal((RestartType.Penalty, TeamSide.Home, new Vec2(94, 34)), (stoppage.Restart, stoppage.AwardedTeam, stoppage.Location));
    }

    [Fact]
    public void Fouls_are_likelier_when_the_ball_is_missed_from_behind_by_a_tired_poor_defender()
    {
        var s = Duel();
        var carrier = s.State.Slot(TeamSide.Home, PlayingPosition.CM);
        var tackler = s.State.Slot(TeamSide.Away, PlayingPosition.CM);
        double P(bool gotBall = true) => TackleResolver.FoulProbability(s.State, tackler, carrier, gotBall);

        var clean = P();
        Assert.True(P(gotBall: false) > clean);
        tackler.Position = new Vec2(39, 34); // behind the carrier, who attacks +X
        Assert.True(P() > clean);
        Assert.True(TackleResolver.Severity(s.State, tackler, carrier, true) > TackleResolver.SeverityBase);
        tackler.Position = new Vec2(41, 34);
        tackler.CurrentStamina = 10;
        Assert.True(P() > clean);

        var strong = Duel(awayStats: new PlayerStats(5, 5, 5, 9, 5, 5, 5, 5, 5));
        Assert.True(TackleResolver.FoulProbability(strong.State, strong.State.Slot(TeamSide.Away, PlayingPosition.CM),
            strong.State.Slot(TeamSide.Home, PlayingPosition.CM), true) < clean);
    }

    [Fact]
    public void Defenders_foul_less_in_their_own_area()
    {
        var s = new Scenario();
        var carrier = s.Place(TeamSide.Home, PlayingPosition.ST, 60, 34);
        var tackler = s.Place(TeamSide.Away, PlayingPosition.LCB, 61, 34);
        var outside = TackleResolver.FoulProbability(s.State, tackler, carrier, true);
        carrier.Position = new Vec2(95, 34);
        tackler.Position = new Vec2(96, 34);

        Assert.True(TackleResolver.FoulProbability(s.State, tackler, carrier, true) < outside);
    }

    [Fact]
    public void No_tackle_from_outside_challenge_range()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.Place(TeamSide.Away, PlayingPosition.CM, 40 + TackleSystem.ChallengeRange + 0.2, 34);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Empty(s.EventTypes);
    }

    [Fact]
    public void Goalkeepers_do_not_tackle()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.Place(TeamSide.Away, PlayingPosition.CM, 80, 34);
        s.Place(TeamSide.Away, PlayingPosition.GK, 41, 34);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Empty(s.EventTypes);
    }

    [Fact]
    public void No_tackle_during_first_touch_protection()
    {
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.Place(TeamSide.Home, PlayingPosition.CM, 40, 34);
        s.Place(TeamSide.Away, PlayingPosition.CM, 41, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.CM);

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Empty(s.EventTypes);
    }

    [Fact]
    public void Tackler_on_cooldown_does_not_tackle()
    {
        var s = Duel(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.State.Slot(TeamSide.Away, PlayingPosition.CM).TackleCooldownUntilTick = s.State.Clock.Tick + 10;

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Empty(s.EventTypes);
    }

    [Fact]
    public void Keeper_holding_the_ball_in_own_area_cannot_be_tackled()
    {
        var s = new Scenario(random: new ScriptedRandom(0.0, 0.0, 0.0));
        s.Place(TeamSide.Home, PlayingPosition.GK, 5, 34);
        s.Place(TeamSide.Away, PlayingPosition.ST, 6, 34);
        s.GiveBall(TeamSide.Home, PlayingPosition.GK);
        for (var i = 0; i < 8; i++)
            s.State.Clock.Advance();

        new TackleSystem(_resolver, AiTuning.Default.OffBall).Apply(s.Context);

        Assert.Empty(s.EventTypes);
    }

    [Fact]
    public void Cannot_tackle_a_teammate()
    {
        var s = Duel();
        var intent = new TackleIntent(s.State.Slot(TeamSide.Home, PlayingPosition.LCM).PlayerId, s.State.Slot(TeamSide.Home, PlayingPosition.CM).PlayerId);
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(s.State, intent, new MatchRng(1)));
    }
}
