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
}
