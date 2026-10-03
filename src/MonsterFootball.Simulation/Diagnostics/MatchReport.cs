using System.Globalization;
using System.Text;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Statistics;

namespace MonsterFootball.Simulation.Diagnostics;

/// <summary>
/// Plain-text match log for the debug runner and debugging. Not commentary: one terse line per event,
/// e.g. <c>04:31 Blue #6 passes to Blue #9</c>. Highlights mode keeps kickoffs, shots, saves, misses and goals.
/// </summary>
public static class MatchReport
{
    private static readonly HashSet<MatchEventType> Highlights =
    [
        MatchEventType.MatchStarted,
        MatchEventType.Kickoff,
        MatchEventType.ShotTaken,
        MatchEventType.ShotSaved,
        MatchEventType.ShotMissed,
        MatchEventType.GoalScored,
        MatchEventType.MatchFinished,
    ];

    public static string Create(MatchResult result, bool allEvents = false)
    {
        var state = result.FinalState;
        var sb = new StringBuilder();

        foreach (var e in result.Events)
        {
            if (!allEvents && !Highlights.Contains(e.Type))
                continue;

            var line = Describe(state, e);
            if (line is null)
                continue;

            if (e.Type is MatchEventType.MatchStarted or MatchEventType.MatchFinished)
                sb.Append(line).Append('\n');
            else
                sb.Append(e.Timestamp).Append(' ').Append(line).Append('\n');
        }

        sb.Append(state.HomeTeam.Name).Append(' ').Append(result.HomeGoals).Append('–')
          .Append(result.AwayGoals).Append(' ').Append(state.AwayTeam.Name).Append('\n');
        sb.Append('\n').Append(StatisticsTable(state, result.Statistics));
        return sb.ToString();
    }

    /// <summary>One line for an event, or null for events not worth printing on their own.</summary>
    public static string? Describe(MatchState state, MatchEvent e)
    {
        string P(PlayerId? id) => id is { } x ? Name(state, x) : "?";

        return e.Type switch
        {
            MatchEventType.MatchStarted => "MATCH START",
            MatchEventType.MatchFinished => "FULL TIME",
            MatchEventType.Kickoff => $"Kick-off: {P(e.ActorPlayerId)}",
            MatchEventType.PassAttempted => $"{P(e.ActorPlayerId)} passes to {P(e.TargetPlayerId)}",
            MatchEventType.PassCompleted => "Pass completed",
            MatchEventType.PassIntercepted => $"Intercepted by {P(e.ActorPlayerId)}",
            MatchEventType.PassMisplaced => "Pass misplaced",
            MatchEventType.CarryStarted => $"{P(e.ActorPlayerId)} runs with the ball",
            MatchEventType.TackleAttempted => $"{P(e.ActorPlayerId)} challenges {P(e.TargetPlayerId)}",
            MatchEventType.TackleWon => "Tackle won",
            MatchEventType.TackleLost => "Tackle beaten",
            MatchEventType.TackleLooseBall => "Ball knocked loose",
            MatchEventType.PossessionChanged => $"{TeamName(state, e.Team)} win the ball",
            MatchEventType.ShotTaken => $"{P(e.ActorPlayerId)} shoots",
            MatchEventType.ShotSaved => $"Saved by {P(e.ActorPlayerId)}",
            MatchEventType.ShotMissed => "Wide",
            MatchEventType.GoalScored => $"GOAL — {P(e.ActorPlayerId)} ({e.Metadata.GetValueOrDefault("score", "")})",
            _ => null,
        };
    }

    public static string StatisticsTable(MatchState state, MatchStatistics stats)
    {
        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        static string Pct(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        var rows = new List<(string Label, string Home, string Away)>
        {
            ("", state.HomeTeam.Name, state.AwayTeam.Name),
            ("Goals", N(stats.Home.Goals), N(stats.Away.Goals)),
            ("Shots", N(stats.Home.Shots), N(stats.Away.Shots)),
            ("Shots on target", N(stats.Home.ShotsOnTarget), N(stats.Away.ShotsOnTarget)),
            ("Passes", $"{N(stats.Home.PassesCompleted)}/{N(stats.Home.PassesAttempted)}", $"{N(stats.Away.PassesCompleted)}/{N(stats.Away.PassesAttempted)}"),
            ("Pass completion", Pct(stats.Home.PassCompletion), Pct(stats.Away.PassCompletion)),
            ("Possession", Pct(stats.PossessionShare(TeamSide.Home)), Pct(stats.PossessionShare(TeamSide.Away))),
            ("Possessions won", N(stats.Home.PossessionsWon), N(stats.Away.PossessionsWon)),
        };

        var labelWidth = rows.Max(r => r.Label.Length);
        var valueWidth = rows.Max(r => Math.Max(r.Home.Length, r.Away.Length));
        var sb = new StringBuilder();
        foreach (var (label, home, away) in rows)
            sb.Append(label.PadRight(labelWidth)).Append("  ").Append(home.PadLeft(valueWidth))
              .Append("  ").Append(away.PadLeft(valueWidth)).Append('\n');
        return sb.ToString();
    }

    private static string Name(MatchState state, PlayerId id)
    {
        var player = state.Player(id);
        var team = state.Team(player.Team).Name;
        return player.Definition.Position.IsGoalkeeper()
            ? $"{team} GK"
            : $"{team} #{player.Definition.ShirtNumber}";
    }

    private static string TeamName(MatchState state, TeamSide? side) => side is { } s ? state.Team(s).Name : "?";
}
