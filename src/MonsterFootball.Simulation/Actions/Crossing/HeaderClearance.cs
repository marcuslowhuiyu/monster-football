using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Actions.Crossing;

/// <summary>
/// A defender who wins a cross heads it clear (ADR-036): with chance 0.25 behind their own goal line beside the goal (a
/// corner), otherwise 15–25 m away from their goal along the line from its centre through the landing point, rolling.
/// </summary>
internal static class HeaderClearance
{
    public const double BehindChance = 0.25;
    public const double MinDistance = 15;
    public const double MaxDistance = 25;

    public static void Apply(MatchContext context, PlayerMatchState defender, Vec2 landing)
    {
        var state = context.State;
        var pitch = state.Pitch;
        state.RecordTouch(defender.PlayerId);

        if (context.Random.Roll(BehindChance))
        {
            var post = PitchGeometry.GoalWidth / 2 + 1;
            var y = landing.Y >= pitch.Width / 2 ? Math.Max(landing.Y, pitch.Width / 2 + post) : Math.Min(landing.Y, pitch.Width / 2 - post);
            Laws.OutOfPlay.Stop(context, new Vec2(pitch.OwnGoalLineX(defender.Team), y), Laws.BoundaryLine.GoalLine);
            return;
        }

        var away = landing - pitch.OwnGoalCentre(defender.Team);
        var distance = MinDistance + context.Random.NextDouble() * (MaxDistance - MinDistance);
        state.SetBallRolling(landing, LooseBallMotion.Covering(landing, away, distance, state.Clock.Tick));
    }
}
