using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Diagnostics;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Tests.TestSupport;

namespace MonsterFootball.Simulation.Tests.Diagnostics;

public class MatchReportTests
{
    private static MatchResult Play(int seed = 7) =>
        new MatchSimulation(MatchConfig.Default, TestTeams.Team("Blue", 1), TestTeams.Team("Red", 101), seed).RunToCompletion();

    [Fact]
    public void Describes_events_with_team_and_shirt_number()
    {
        var state = TestTeams.Match();
        var cm = state.Slot(TeamSide.Home, PlayingPosition.CM);
        var st = state.Slot(TeamSide.Home, PlayingPosition.ST);
        var keeper = state.Slot(TeamSide.Away, PlayingPosition.GK);

        Assert.Equal($"Blue #{cm.Definition.ShirtNumber} passes to Blue #{st.Definition.ShirtNumber}",
            MatchReport.Describe(state, new MatchEvent(new MatchTime(271), MatchEventType.PassAttempted, cm.PlayerId, st.PlayerId, TeamSide.Home)));
        Assert.Equal("Saved by Red GK",
            MatchReport.Describe(state, new MatchEvent(new MatchTime(554), MatchEventType.ShotSaved, keeper.PlayerId, st.PlayerId, TeamSide.Away)));
    }

    [Theory]
    [InlineData(PossessionCause.Tackle, "Red win the ball")]
    [InlineData(PossessionCause.Kickoff, "Red have the ball (kickoff)")]
    [InlineData(PossessionCause.KeeperRestart, "Red have the ball (keeper restart)")]
    [InlineData(PossessionCause.ThrowIn, "Red have the ball (throw-in)")]
    [InlineData(PossessionCause.GoalKick, "Red have the ball (goal kick)")]
    [InlineData(PossessionCause.Corner, "Red have the ball (corner)")]
    public void Possession_changes_say_whether_the_ball_was_won_or_awarded(PossessionCause cause, string expected)
    {
        var state = TestTeams.Match();
        var e = new MatchEvent(new MatchTime(10), MatchEventType.PossessionChanged, Team: TeamSide.Away)
        {
            Metadata = System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(
                [new KeyValuePair<string, string>("cause", cause.ToString())]),
        };

        Assert.Equal(expected, MatchReport.Describe(state, e));
    }

    [Fact]
    public void A_stoppage_says_why_play_stopped_and_how_it_restarts()
    {
        var state = TestTeams.Match();
        var e = new MatchEvent(new MatchTime(10), MatchEventType.PlayStopped, Team: TeamSide.Away)
        {
            Metadata = System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(
                [new KeyValuePair<string, string>("reason", "Goal"), new KeyValuePair<string, string>("restart", "Kickoff")]),
        };

        Assert.Equal("Play stopped (Goal) — Red to restart: Kickoff", MatchReport.Describe(state, e));
    }

    [Fact]
    public void Highlights_show_added_time_and_the_end_of_each_half()
    {
        var lines = MatchReport.Create(Play(), details: false).Split('\n');

        Assert.Equal(2, lines.Count(l => l.Contains("Added time: +", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.Contains("END OF PERIOD 1", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("END OF PERIOD 2", StringComparison.Ordinal));
    }

    [Fact]
    public void Highlights_start_with_match_start_and_end_with_the_score_and_statistics()
    {
        var result = Play();
        var lines = MatchReport.Create(result).Split('\n');

        Assert.Equal("MATCH START", lines[0]);
        Assert.Contains("FULL TIME", lines);
        Assert.Contains($"Blue {result.HomeGoals}–{result.AwayGoals} Red", lines);
        Assert.Contains(lines, l => l.StartsWith("Shots on target", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("passes to", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_goal_appears_in_the_highlights()
    {
        var result = Play();
        var report = MatchReport.Create(result);
        Assert.Equal(result.HomeGoals + result.AwayGoals, report.Split('\n').Count(l => l.Contains("GOAL", StringComparison.Ordinal)));
    }

    [Fact]
    public void Full_log_includes_passes_with_timestamps()
    {
        var lines = MatchReport.Create(Play(), allEvents: true).Split('\n');
        Assert.Contains(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\d\d:\d\d Blue #\d+ passes to Blue #\d+$"));
    }

    [Fact]
    public void Actions_carry_the_odds_they_were_resolved_with()
    {
        var events = Play().Events;

        Assert.All(events.Where(e => e.Type == MatchEventType.PassAttempted),
            e => Assert.True(new[] { "quality", "success", "distance", "threats", "stamina" }.All(e.Metadata.ContainsKey)));
        Assert.All(events.Where(e => e.Type == MatchEventType.ShotTaken),
            e => Assert.True(new[] { "onTarget", "save", "xg", "distance", "stamina", "keeperStamina" }.All(e.Metadata.ContainsKey)));
        Assert.All(events.Where(e => e.Type == MatchEventType.TackleAttempted),
            e => Assert.True(new[] { "contact", "clean", "stamina", "carrierStamina" }.All(e.Metadata.ContainsKey)));
        Assert.All(events.Where(e => e.Type == MatchEventType.PassIntercepted),
            e => Assert.True(e.Metadata.ContainsKey("chance")));
    }

    [Fact]
    public void Shot_odds_are_consistent_with_expected_goals()
    {
        foreach (var e in Play().Events.Where(e => e.Type == MatchEventType.ShotTaken))
        {
            double M(string key) => double.Parse(e.Metadata[key], System.Globalization.CultureInfo.InvariantCulture);
            // Metadata is rounded to 3 decimals, so allow a little slack.
            Assert.InRange(Math.Abs(M("onTarget") * (1 - M("save")) - M("xg")), 0, 0.002);
        }
    }

    [Fact]
    public void Details_show_ratings_and_odds_under_each_action()
    {
        var lines = MatchReport.Create(Play(), allEvents: true).Split('\n');

        Assert.Contains(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l,
            @"^ {6}Passing \d+ · Technique \d+ · Awareness \d+ · stamina \d+ · \d+ m · accuracy \d+% · success \d+% · \d+ in lane · [A-Za-z]+ / [A-Za-z]+$"));
        Assert.Contains(lines, l => System.Text.RegularExpressions.Regex.IsMatch(l,
            @"^ {6}Finishing \d+ · Technique \d+ · Power \d+ · stamina \d+ · \d+ m · on target \d+% · save \d+% · xG \d\.\d\d · [A-Za-z]+ / [A-Za-z]+$"));
        Assert.Contains(lines, l => l.StartsWith("      Defending", StringComparison.Ordinal) && l.Contains(" vs Technique", StringComparison.Ordinal));
    }

    [Fact]
    public void Describes_ball_out_of_play_and_the_restart()
    {
        var state = TestTeams.Match();
        var lb = state.Slot(TeamSide.Away, PlayingPosition.LB);
        static System.Collections.Immutable.ImmutableSortedDictionary<string, string> Meta(params (string, string)[] pairs) =>
            System.Collections.Immutable.ImmutableSortedDictionary.CreateRange(pairs.Select(p => new KeyValuePair<string, string>(p.Item1, p.Item2)));

        Assert.Equal("Out of play — throw-in to Red", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(10), MatchEventType.PlayStopped, Team: TeamSide.Away) { Metadata = Meta(("reason", "BallOutOfPlay"), ("restart", "ThrowIn")) }));
        Assert.Equal($"Throw-in: Red #{lb.Definition.ShirtNumber}", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(18), MatchEventType.RestartTaken, lb.PlayerId, Team: TeamSide.Away) { Metadata = Meta(("restart", "ThrowIn")) }));
        var striker = state.Slot(TeamSide.Home, PlayingPosition.ST);
        Assert.Equal($"Offside — Blue #{striker.Definition.ShirtNumber}; indirect free kick to Red", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(20), MatchEventType.PlayStopped, striker.PlayerId, Team: TeamSide.Away) { Metadata = Meta(("reason", "Offside"), ("restart", "IndirectFreeKick")) }));
        Assert.Equal($"Foul by Red #{lb.Definition.ShirtNumber} on Blue #{striker.Definition.ShirtNumber}", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(25), MatchEventType.FoulCommitted, lb.PlayerId, striker.PlayerId, TeamSide.Away)));
        Assert.Equal("Penalty to Blue", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(25), MatchEventType.PlayStopped, lb.PlayerId, striker.PlayerId, TeamSide.Home) { Metadata = Meta(("reason", "Foul"), ("restart", "Penalty")) }));
        Assert.Equal($"RED CARD: Red #{lb.Definition.ShirtNumber} (denying an obvious goal-scoring opportunity)", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(26), MatchEventType.RedCard, lb.PlayerId, Team: TeamSide.Away) { Metadata = Meta(("reason", "Dogso")) }));
        Assert.Equal("MATCH ABANDONED — Red down to fewer than 7 players", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(27), MatchEventType.MatchAbandoned, Team: TeamSide.Away)));
        var keeper = state.Slot(TeamSide.Away, PlayingPosition.GK);
        Assert.Equal("Saved by Red GK — pushed behind", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(30), MatchEventType.ShotSaved, keeper.PlayerId, Team: TeamSide.Away) { Metadata = Meta(("save", "ParriedBehind")) }));
        Assert.Equal($"Deflected behind by Red #{lb.Definition.ShirtNumber}", MatchReport.Describe(state,
            new MatchEvent(new MatchTime(40), MatchEventType.ShotMissed, TargetPlayerId: lb.PlayerId, Team: TeamSide.Home) { Metadata = Meta(("deflected", "true")) }));
    }

    [Fact]
    public void Brief_mode_omits_details()
    {
        var report = MatchReport.Create(Play(), allEvents: true, details: false);

        Assert.DoesNotContain("accuracy", report, StringComparison.Ordinal);
        Assert.DoesNotContain("· on target", report, StringComparison.Ordinal);
        Assert.DoesNotContain("· contact", report, StringComparison.Ordinal);
    }
}
