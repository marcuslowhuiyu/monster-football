using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Simulation.Positioning;

/// <summary>
/// Reduced-team shapes (D11, ADR-031): which 4-3-3 roles a side fills with a given number of outfield players. The
/// roles keep their 4-3-3 anchors, so dropping slots gives the shape: 10 → 4-3-3, 9 → 4-4-1 (no CM; the wingers'
/// defending anchors sit on the midfield line), 8 → 4-3-1, 7 → 4-2-1, 6 → 4-1-1.
/// </summary>
internal static class ShapeTemplates
{
    public static IReadOnlyList<PlayingPosition> OutfieldRoles(int outfieldPlayers) => outfieldPlayers switch
    {
        >= 10 => [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB, PlayingPosition.LCM, PlayingPosition.CM, PlayingPosition.RCM, PlayingPosition.LW, PlayingPosition.ST, PlayingPosition.RW],
        9 => [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB, PlayingPosition.LW, PlayingPosition.LCM, PlayingPosition.RCM, PlayingPosition.RW, PlayingPosition.ST],
        8 => [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB, PlayingPosition.LCM, PlayingPosition.CM, PlayingPosition.RCM, PlayingPosition.ST],
        7 => [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB, PlayingPosition.LCM, PlayingPosition.RCM, PlayingPosition.ST],
        _ => [PlayingPosition.LB, PlayingPosition.LCB, PlayingPosition.RCB, PlayingPosition.RB, PlayingPosition.CM, PlayingPosition.ST],
    };

    /// <summary>
    /// Re-forms <paramref name="side"/> after a dismissal. If it has no goalkeeper, the outfielder with the best Keeping
    /// goes in goal. Players whose role is still in the template keep it; each vacant template role (in template order)
    /// goes to the player left without one whose 4-3-3 kickoff anchor is nearest (ties → earlier player).
    /// </summary>
    public static void Reform(MatchState state, TeamSide side)
    {
        var players = state.TeamPlayers(side).ToList();
        if (!players.Any(p => p.Role.IsGoalkeeper()) && players.Count > 0)
            players.OrderByDescending(p => p.Definition.Stats.Keeping).First().Role = PlayingPosition.GK;

        var outfield = players.Where(p => !p.Role.IsGoalkeeper()).ToList();
        var roles = OutfieldRoles(outfield.Count);
        var unassigned = outfield.Where(p => !roles.Contains(p.Role) || outfield.First(q => q.Role == p.Role) != p).ToList();
        foreach (var role in roles.Where(r => !outfield.Any(p => p.Role == r && !unassigned.Contains(p))))
        {
            var anchor = Formation433.KickoffPosition(state.Pitch, side, role);
            var chosen = unassigned.MinBy(p => Formation433.KickoffPosition(state.Pitch, side, p.Role).DistanceTo(anchor));
            if (chosen is null)
                break;
            chosen.Role = role;
            unassigned.Remove(chosen);
        }
    }
}
