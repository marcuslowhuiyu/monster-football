namespace MonsterFootball.Simulation.Core;

/// <summary>
/// Response curves for converting ratings into probabilities. Uses only basic arithmetic and
/// Math.Sqrt (IEEE-exact; no Math.Exp) so results are bit-identical on every platform.
/// </summary>
internal static class Curves
{
    /// <summary>Algebraic S-curve mapping any real number to (0, 1); 0 → 0.5, ±1 → 0.85/0.15, ±3 → 0.97/0.03.</summary>
    public static double Sigmoid(double x) => 0.5 + 0.5 * x / Math.Sqrt(1 + x * x);
}
