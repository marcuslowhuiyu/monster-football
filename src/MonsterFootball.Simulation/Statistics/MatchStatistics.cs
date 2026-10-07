using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Statistics;

/// <summary>Per-team match statistics.</summary>
public sealed record TeamStatistics(
    int Goals,
    int Shots,
    int ShotsOnTarget,
    int PassesAttempted,
    int PassesCompleted,
    int PossessionsWon,
    double PossessionSeconds)
{
    public double PassCompletion => PassesAttempted == 0 ? 0 : (double)PassesCompleted / PassesAttempted;
}

/// <summary>
/// Match statistics derived entirely from the event log (ADR-019). Ball-in-play time runs from each kickoff to the
/// next stoppage (<see cref="MatchEventType.PlayStopped"/>, <see cref="MatchEventType.PeriodEnded"/> or
/// <see cref="MatchEventType.MatchFinished"/>). Possession time is ball-in-play time credited to the team that last had
/// the ball under control — including while a pass is in flight or the ball is loose — switching at each kickoff and
/// <see cref="MatchEventType.PossessionChanged"/>.
/// Possessions won count only changes whose cause is open play (<see cref="PossessionCauseExtensions.IsOpenPlay"/>);
/// kickoffs and keeper restarts change possession without anyone winning the ball.
/// </summary>
public sealed record MatchStatistics(TeamStatistics Home, TeamStatistics Away, int PossessionChanges, double BallInPlaySeconds)
{
    public TeamStatistics For(TeamSide side) => side == TeamSide.Home ? Home : Away;

    /// <summary>Share of possession time, 0–1.</summary>
    public double PossessionShare(TeamSide side)
    {
        var total = Home.PossessionSeconds + Away.PossessionSeconds;
        return total == 0 ? 0 : For(side).PossessionSeconds / total;
    }

    public static MatchStatistics FromEvents(IReadOnlyList<MatchEvent> events)
    {
        var home = new Tally();
        var away = new Tally();
        Tally Of(TeamSide side) => side == TeamSide.Home ? home : away;

        TeamSide? inPossession = null;
        var since = 0.0;
        var inPlay = false;
        var playSince = 0.0;
        var ballInPlay = 0.0;
        var possessionChanges = 0;

        void Accrue(double at)
        {
            if (inPlay && inPossession is { } current)
                Of(current).PossessionSeconds += at - since;
            since = at;
        }

        void SwitchPossession(TeamSide side, double at)
        {
            Accrue(at);
            inPossession = side;
        }

        void StopPlay(double at)
        {
            if (!inPlay)
                return;
            Accrue(at);
            ballInPlay += at - playSince;
            inPlay = false;
        }

        foreach (var e in events)
        {
            var at = e.Timestamp.Seconds;
            switch (e.Type)
            {
                case MatchEventType.Kickoff:
                    SwitchPossession(e.Team!.Value, at);
                    if (!inPlay)
                    {
                        inPlay = true;
                        playSince = at;
                    }

                    break;
                case MatchEventType.PlayStopped or MatchEventType.PeriodEnded:
                    StopPlay(at);
                    break;
                case MatchEventType.PossessionChanged:
                    possessionChanges++;
                    if (e.GetPossessionCause()?.IsOpenPlay() == true)
                        Of(e.Team!.Value).PossessionsWon++;
                    SwitchPossession(e.Team!.Value, at);
                    break;
                case MatchEventType.PassAttempted:
                    Of(e.Team!.Value).PassesAttempted++;
                    break;
                case MatchEventType.PassCompleted:
                    Of(e.Team!.Value).PassesCompleted++;
                    break;
                case MatchEventType.ShotTaken:
                    Of(e.Team!.Value).Shots++;
                    break;
                case MatchEventType.ShotSaved:
                    // Team on the event is the saving side; the shot belongs to the other team.
                    Of(e.Team!.Value.Opponent()).ShotsOnTarget++;
                    break;
                case MatchEventType.GoalScored:
                    Of(e.Team!.Value).Goals++;
                    Of(e.Team.Value).ShotsOnTarget++;
                    break;
                case MatchEventType.MatchFinished:
                    StopPlay(at);
                    break;
            }
        }

        return new MatchStatistics(home.ToStatistics(), away.ToStatistics(), possessionChanges, ballInPlay);
    }

    private sealed class Tally
    {
        public int Goals;
        public int Shots;
        public int ShotsOnTarget;
        public int PassesAttempted;
        public int PassesCompleted;
        public int PossessionsWon;
        public double PossessionSeconds;

        public TeamStatistics ToStatistics() =>
            new(Goals, Shots, ShotsOnTarget, PassesAttempted, PassesCompleted, PossessionsWon, PossessionSeconds);
    }
}
