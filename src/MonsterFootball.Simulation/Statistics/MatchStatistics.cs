using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;

namespace MonsterFootball.Simulation.Statistics;

/// <summary>
/// Per-team match statistics. Throw-ins, goal kicks, corners, free kicks and penalties count restarts the team took;
/// offsides, fouls and cards count the team's offences (a second yellow counts as a yellow and a red).
/// </summary>
public sealed record TeamStatistics(
    int Goals,
    int Shots,
    int ShotsOnTarget,
    int PassesAttempted,
    int PassesCompleted,
    int PossessionsWon,
    double PossessionSeconds)
{
    public int ThrowIns { get; init; }
    public int GoalKicks { get; init; }
    public int Corners { get; init; }
    public int FreeKicks { get; init; }
    public int Offsides { get; init; }
    public int Penalties { get; init; }
    public int PenaltiesScored { get; init; }
    public int Fouls { get; init; }
    public int YellowCards { get; init; }
    public int RedCards { get; init; }
    public int Clearances { get; init; }

    public double PassCompletion => PassesAttempted == 0 ? 0 : (double)PassesCompleted / PassesAttempted;
}

/// <summary>
/// Match statistics derived entirely from the event log (ADR-019). Ball-in-play time runs from each restart (kickoff or
/// <see cref="MatchEventType.RestartTaken"/>) to the next stoppage (<see cref="MatchEventType.PlayStopped"/>, <see cref="MatchEventType.PeriodEnded"/> or
/// <see cref="MatchEventType.MatchFinished"/>). Possession time is ball-in-play time credited to the team that last had
/// the ball under control — including while a pass is in flight or the ball is loose — switching at each restart and
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
                case MatchEventType.Kickoff or MatchEventType.RestartTaken:
                    SwitchPossession(e.Team!.Value, at);
                    switch (e.Metadata.GetValueOrDefault("restart"))
                    {
                        case nameof(RestartType.ThrowIn): Of(e.Team.Value).ThrowIns++; break;
                        case nameof(RestartType.GoalKick): Of(e.Team.Value).GoalKicks++; break;
                        case nameof(RestartType.Corner): Of(e.Team.Value).Corners++; break;
                        case nameof(RestartType.IndirectFreeKick) or nameof(RestartType.DirectFreeKick): Of(e.Team.Value).FreeKicks++; break;
                        case nameof(RestartType.Penalty): Of(e.Team.Value).Penalties++; break;
                    }

                    if (!inPlay)
                    {
                        inPlay = true;
                        playSince = at;
                    }

                    break;
                case MatchEventType.PlayStopped or MatchEventType.PeriodEnded:
                    StopPlay(at);
                    if (e.Type == MatchEventType.PlayStopped && e.Metadata.GetValueOrDefault("reason") == nameof(StoppageReason.Offside))
                        Of(e.Team!.Value.Opponent()).Offsides++;
                    break;
                case MatchEventType.PossessionChanged:
                    possessionChanges++;
                    if (e.GetPossessionCause()?.IsOpenPlay() == true)
                        Of(e.Team!.Value).PossessionsWon++;
                    SwitchPossession(e.Team!.Value, at);
                    break;
                case MatchEventType.FoulCommitted:
                    Of(e.Team!.Value).Fouls++;
                    break;
                case MatchEventType.Clearance:
                    Of(e.Team!.Value).Clearances++;
                    break;
                case MatchEventType.YellowCard:
                    Of(e.Team!.Value).YellowCards++;
                    break;
                case MatchEventType.RedCard:
                    Of(e.Team!.Value).RedCards++;
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
                    if (e.Metadata.ContainsKey("penalty"))
                        Of(e.Team.Value).PenaltiesScored++;
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
        public int ThrowIns;
        public int GoalKicks;
        public int Corners;
        public int FreeKicks;
        public int Offsides;
        public int Penalties;
        public int PenaltiesScored;
        public int Fouls;
        public int YellowCards;
        public int RedCards;
        public int Clearances;

        public TeamStatistics ToStatistics() =>
            new(Goals, Shots, ShotsOnTarget, PassesAttempted, PassesCompleted, PossessionsWon, PossessionSeconds)
            {
                ThrowIns = ThrowIns,
                GoalKicks = GoalKicks,
                Corners = Corners,
                FreeKicks = FreeKicks,
                Offsides = Offsides,
                Penalties = Penalties,
                PenaltiesScored = PenaltiesScored,
                Fouls = Fouls,
                YellowCards = YellowCards,
                RedCards = RedCards,
                Clearances = Clearances,
            };
    }
}
