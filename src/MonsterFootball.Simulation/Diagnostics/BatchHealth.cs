using System.Globalization;

namespace MonsterFootball.Simulation.Diagnostics;

/// <summary>
/// Checks a batch between evenly matched teams for obviously pathological behaviour (milestone §35).
/// Bands are deliberately wide: they catch broken simulations, not unrealistic-but-plausible ones.
/// </summary>
public static class BatchHealth
{
    public sealed record Check(string Name, double Value, double Min, double Max)
    {
        public bool Passed => Value >= Min && Value <= Max;

        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"{(Passed ? "ok  " : "FAIL")} {Name,-34} {Value,8:0.###}   [{Min:0.###} – {Max:0.###}]");
    }

    public static IReadOnlyList<Check> Evaluate(BatchSummary s) =>
    [
        new("Goals per match", s.AverageGoals, 0.5, 6),
        new("Shots per match", s.AverageShots, 6, 60),
        new("Shots on target share", s.AverageShots == 0 ? 0 : s.AverageShotsOnTarget / s.AverageShots, 0.2, 0.6),
        new("Pass completion", s.PassCompletion, 0.6, 0.97),
        new("Home possession (equal teams)", s.AverageHomePossession, 0.45, 0.55),
        new("Home win − away win (equal teams)", Math.Abs(s.HomeWinShare - s.AwayWinShare), 0, 0.1),
        new("Draw share", s.DrawShare, 0.05, 0.6),
        new("Team-matches without a shot", s.TeamMatchesWithoutShotShare, 0, 0.1),
        new("Most goals in one match", s.MaxGoalsInMatch, 0, 14),
        new("Striker share of goals", s.StrikerGoalShare, 0, 0.85),
        new("Tackles per match", s.AverageTackles, 10, 600),
        new("Outfield distance per player (km)", s.AverageOutfieldDistanceKm, 5, 16),
        new("Outfield stamina at full time", s.AverageOutfieldStaminaAtFullTime, 10, 95),
        // Restart volumes (MILESTONE_2 §16.4). Throw-ins and ball-in-play time join at the M2.7 calibration
        // checkpoint, once fouls and offsides add stoppages (ADR-028).
        new("Goal kicks per match", s.AverageGoalKicks, 8, 35),
        new("Corners per match", s.AverageCorners, 3, 20),
    ];

    public static bool IsHealthy(BatchSummary summary) => Evaluate(summary).All(c => c.Passed);
}
