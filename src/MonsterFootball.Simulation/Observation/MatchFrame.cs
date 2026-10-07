using System.Collections.Immutable;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Observation;

/// <summary>
/// An immutable copy of everything observable about a match at the end of a tick (ADR-025). Presentation, tactical
/// analysis and debug tools read frames, never the live <see cref="MatchState"/>, so observing a match cannot change
/// it, and consecutive frames give the viewer the states it interpolates between. Fields exist only when something
/// reads them.
/// </summary>
public sealed record MatchFrame(
    long Tick,
    MatchTime Time,
    MatchPhase Phase,
    Stoppage? Stoppage,
    int HomeGoals,
    int AwayGoals,
    TeamSide? AttackingTeam,
    BallFrame Ball,
    ImmutableArray<PlayerFrame> Players)
{
    public static MatchFrame Capture(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var flight = state.Ball.Flight is { } f
            ? new BallFlightFrame(f.Shot is null ? BallFlightKind.Pass : BallFlightKind.Shot, f.Team, f.StartPosition, f.TargetPosition,
                f.LaunchTick, f.ArrivalTick, f.Pass?.IntendedReceiver)
            : null;

        return new MatchFrame(
            state.Clock.Tick,
            state.Clock.Now,
            state.Phase,
            state.CurrentStoppage,
            state.Score.Home,
            state.Score.Away,
            state.AttackingTeam,
            new BallFrame(state.Ball.Position, state.Ball.PossessorId, flight),
            [.. state.Players.Select(p => new PlayerFrame(
                p.PlayerId,
                p.Team,
                p.Definition.Position,
                p.Definition.ShirtNumber,
                p.Position,
                p.MovementTarget,
                p.MovementIntensity,
                p.CurrentStamina,
                p.OffBallPlan?.Kind,
                p.ActiveCarry is not null))]);
    }
}

/// <summary>One player at the end of a tick. <see cref="Players"/> order is the match's stable player order.</summary>
public sealed record PlayerFrame(
    PlayerId Id,
    TeamSide Team,
    PlayingPosition Slot,
    int ShirtNumber,
    Vec2 Position,
    Vec2 MovementTarget,
    MovementIntensity Intensity,
    double Stamina,
    ActionKind? OffBallPlan,
    bool IsCarrying);

public sealed record BallFrame(Vec2 Position, PlayerId? PossessorId, BallFlightFrame? Flight);

public enum BallFlightKind
{
    Pass,
    Shot,
}

/// <summary>
/// A pass or shot in the air: where it was struck, where it lands, and when. <see cref="Target"/> is the actual
/// landing point, which the viewer needs to animate the ball; tactical analysis must use only the intended receiver
/// and the lane, never the predetermined outcome.
/// </summary>
public sealed record BallFlightFrame(
    BallFlightKind Kind,
    TeamSide Team,
    Vec2 Start,
    Vec2 Target,
    long LaunchTick,
    long ArrivalTick,
    PlayerId? IntendedReceiver);
