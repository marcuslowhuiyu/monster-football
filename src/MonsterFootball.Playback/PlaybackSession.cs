using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Playback;

/// <summary>
/// A viewer's match: a <see cref="MatchPlayer"/> and a <see cref="PlaybackClock"/>. Each rendered frame calls
/// <see cref="Update"/> with the real time elapsed; the clock moves, the match is simulated just far enough to cover
/// the position (the frame shown and the next one), and the two frames are interpolated. Rendering never advances or
/// resolves football by itself (ADR-026).
/// </summary>
public sealed class PlaybackSession
{
    private readonly MatchConfig _config;
    private readonly TeamDefinition _home;
    private readonly TeamDefinition _away;

    public PlaybackSession(MatchConfig config, TeamDefinition home, TeamDefinition away, int seed)
    {
        _config = config;
        _home = home;
        _away = away;
        Match = new MatchPlayer(config, home, away, seed);
    }

    public MatchPlayer Match { get; private set; }
    public PlaybackClock Clock { get; } = new();

    public ViewState Update(double realSeconds)
    {
        Clock.Advance(realSeconds, _config.TickSeconds);
        var tick = (long)Math.Floor(Clock.Position);
        Match.EnsureFrame(tick + 1);

        if (Match.IsComplete && Clock.Position >= Match.LastTick)
        {
            Clock.Pause();
            Clock.Seek(Match.LastTick);
            tick = Match.LastTick;
        }

        var frames = Match.Frames;
        return FrameInterpolator.Interpolate(frames[(int)tick], frames[(int)Math.Min(tick + 1, Match.LastTick)], Clock.Position - tick);
    }

    /// <summary>Starts the match again from kickoff with the same seed, or with <paramref name="seed"/>; speed and play state are kept.</summary>
    public void Restart(int? seed = null)
    {
        Match = new MatchPlayer(_config, _home, _away, seed ?? Match.Seed);
        Clock.Seek(0);
    }
}
