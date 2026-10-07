using MonsterFootball.Simulation;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Observation;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Playback;

/// <summary>
/// Plays one match on demand and records a frame for every tick, so playback can seek backwards without
/// re-simulating. <c>Frames[n]</c> is the match after <c>n</c> steps; frame 0 is the line-up before kickoff.
/// The simulation is only stepped as far as <see cref="EnsureFrame"/> is asked to go.
/// </summary>
public sealed class MatchPlayer
{
    private readonly MatchSimulation _simulation;
    private readonly List<MatchFrame> _frames = [];

    public MatchPlayer(MatchConfig config, TeamDefinition home, TeamDefinition away, int seed)
    {
        Config = config;
        _simulation = new MatchSimulation(config, home, away, seed);
        _frames.Add(MatchFrame.Capture(_simulation.State));
    }

    public MatchConfig Config { get; }
    public int Seed => _simulation.Seed;
    public IReadOnlyList<MatchFrame> Frames => _frames;
    public IReadOnlyList<MatchEvent> Events => _simulation.Events;
    public bool IsComplete => _simulation.IsFinished;
    public long LastTick => _frames.Count - 1;

    /// <summary>How far the simulation has actually been stepped.</summary>
    public long SimulatedTick => _simulation.State.Clock.Tick;

    /// <summary>Steps the match until frame <paramref name="tick"/> exists or the match ends; returns the last frame available up to it.</summary>
    public long EnsureFrame(long tick)
    {
        while (LastTick < tick && !_simulation.IsFinished)
        {
            _simulation.Step();
            _frames.Add(MatchFrame.Capture(_simulation.State));
        }

        return Math.Min(tick, LastTick);
    }
}
