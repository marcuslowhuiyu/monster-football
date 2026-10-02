using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;

namespace MonsterFootball.Simulation.Actions.Passing;

/// <summary>
/// Chooses the teammate to pass to: expected success (from <see cref="PassResolver.Estimate"/>)
/// plus a bonus for progressing the ball toward goal. Deterministic; ties go to the earlier player.
/// </summary>
internal sealed class PassTargetSelector(PassResolver resolver)
{
    public const double SuccessWeight = 100;
    public const double ProgressionWeight = 0.8;
    public const double GoalkeeperPenalty = 15;
    public const double MinPassDistance = 4;

    public PassIntent? Choose(MatchState state, PlayerMatchState passer)
    {
        var pitch = state.Pitch;
        var passerX = TeamFrame.Convert(pitch, passer.Team, passer.Position).X;

        var best = state.TeamPlayers(passer.Team)
            .Where(r => r != passer && r.Position.DistanceTo(passer.Position) >= MinPassDistance)
            .Select(r => (Receiver: r, Score: Score(state, passer, r, passerX)))
            .MaxBy(c => c.Score);

        return best.Receiver is null ? null : new PassIntent(passer.PlayerId, best.Receiver.PlayerId);
    }

    private double Score(MatchState state, PlayerMatchState passer, PlayerMatchState receiver, double passerX)
    {
        var estimate = resolver.Estimate(state, new PassIntent(passer.PlayerId, receiver.PlayerId));
        var progression = TeamFrame.Convert(state.Pitch, receiver.Team, receiver.Position).X - passerX;
        var keeperPenalty = receiver.Definition.Position.IsGoalkeeper() ? GoalkeeperPenalty : 0;
        return estimate.SuccessProbability * SuccessWeight + progression * ProgressionWeight - keeperPenalty;
    }
}
