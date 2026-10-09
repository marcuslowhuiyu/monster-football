using System.Collections.Immutable;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Laws;

/// <summary>
/// Who was in an offside position when <see cref="Team"/> played the ball (MILESTONE_2 §10). Kept until the next touch:
/// the first of these players to receive or collect the ball commits the offence; anyone else touching it ends the
/// judgement. Being in an offside position and becoming involved are separate steps, so later refinements (interfering
/// with an opponent, passive offside) can change the second without touching the first.
/// </summary>
public sealed record OffsideJudgement(TeamSide Team, ImmutableHashSet<PlayerId> OffsidePlayers, long AtTick);

/// <summary>
/// Offside position (Law 11, ADR-029), judged in the attacking team's frame: a player is offside when in the
/// opponents' half (the halfway line is not), and nearer the opponents' goal line than both the ball and the
/// second-last opponent (the goalkeeper usually being last). Level is onside.
/// </summary>
internal static class Offside
{
    /// <summary>
    /// The X (in <paramref name="attackers"/>' frame) beyond which an attacker is offside with the ball at
    /// <paramref name="ball"/>: the furthest of halfway, the ball and the second-last opponent.
    /// </summary>
    public static double LineX(MatchState state, TeamSide attackers, Vec2 ball)
    {
        var pitch = state.Pitch;
        var ballX = TeamFrame.Convert(pitch, attackers, ball).X;
        return Math.Max(Math.Max(pitch.HalfwayX, ballX), SecondLastOpponentX(state, attackers));
    }

    /// <summary>The second-highest X among <paramref name="attackers"/>' opponents, in the attackers' frame.</summary>
    public static double SecondLastOpponentX(MatchState state, TeamSide attackers)
    {
        var pitch = state.Pitch;
        var xs = state.TeamPlayers(attackers.Opponent())
            .Select(p => TeamFrame.Convert(pitch, attackers, p.Position).X)
            .OrderByDescending(x => x)
            .ToList();
        return xs.Count >= 2 ? xs[1] : 0;
    }

    public static bool IsOffsidePosition(MatchState state, PlayerMatchState player, Vec2 ball, double margin = 0) =>
        TeamFrame.Convert(state.Pitch, player.Team, player.Position).X > LineX(state, player.Team, ball) + margin;

    /// <summary>Every teammate of <paramref name="passer"/> in an offside position as they play the ball.</summary>
    public static OffsideJudgement Judge(MatchState state, PlayerMatchState passer)
    {
        var offside = state.TeamPlayers(passer.Team)
            .Where(p => p != passer && IsOffsidePosition(state, p, passer.Position))
            .Select(p => p.PlayerId)
            .ToImmutableHashSet();
        return new OffsideJudgement(passer.Team, offside, state.Clock.Tick);
    }
}
