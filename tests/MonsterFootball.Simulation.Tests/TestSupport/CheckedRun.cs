using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Tests.TestSupport;

/// <summary>Steps a simulation to completion, verifying invariants and clock monotonicity after every tick.</summary>
internal static class CheckedRun
{
    public static MatchResult ToCompletion(MatchSimulation simulation)
    {
        MatchStateInvariants.Verify(simulation.State);
        var previousTick = simulation.State.Clock.Tick;

        while (!simulation.IsFinished)
        {
            simulation.Step();
            MatchStateInvariants.Verify(simulation.State);

            var tick = simulation.State.Clock.Tick;
            if (tick <= previousTick)
                throw new InvalidOperationException($"Clock did not advance: tick {previousTick} -> {tick}.");
            previousTick = tick;
        }

        return simulation.RunToCompletion();
    }
}
