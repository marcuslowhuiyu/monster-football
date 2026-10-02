using System.Globalization;
using System.Text;

namespace MonsterFootball.Simulation.Diagnostics;

/// <summary>
/// Canonical text form of a finished match: every event plus full-precision final state.
/// Two runs are bit-identical exactly when their fingerprints are equal. Used by determinism
/// tests and by CI to compare the same seeds across operating systems (ADR-013, ADR-017).
/// </summary>
public static class MatchFingerprint
{
    public static string Create(MatchResult result)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"seed={result.Seed} score={result.HomeGoals}-{result.AwayGoals}\n");

        foreach (var e in result.Events)
        {
            sb.Append(e).Append('\n');
            if (e.Position is { } p)
                sb.Append(CultureInfo.InvariantCulture, $"  at={p.X:R},{p.Y:R}\n");
        }

        foreach (var player in result.FinalState.Players)
            sb.Append(CultureInfo.InvariantCulture,
                $"{player.PlayerId} pos={player.Position.X:R},{player.Position.Y:R} stamina={player.CurrentStamina:R}\n");

        var ball = result.FinalState.Ball;
        sb.Append(CultureInfo.InvariantCulture, $"ball={ball.Position.X:R},{ball.Position.Y:R} possessor={ball.PossessorId}\n");
        return sb.ToString();
    }
}
