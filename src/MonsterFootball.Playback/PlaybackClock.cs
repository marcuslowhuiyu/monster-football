namespace MonsterFootball.Playback;

/// <summary>
/// Maps real (rendering) time to a position in the match, in ticks, at a chosen speed. It only ever moves a
/// position; it never steps the simulation (ADR-026). <see cref="PlaybackSession"/> asks the simulation for the
/// frames this position needs.
/// </summary>
public sealed class PlaybackClock
{
    public static IReadOnlyList<double> Speeds { get; } = [0.25, 0.5, 1, 2, 4];

    public bool IsPlaying { get; private set; }
    public double Speed { get; private set; } = 1;

    /// <summary>Current position in ticks; the fractional part is how far between two frames.</summary>
    public double Position { get; private set; }

    public void Play() => IsPlaying = true;
    public void Pause() => IsPlaying = false;
    public void Toggle() => IsPlaying = !IsPlaying;

    public void SetSpeed(double speed) =>
        Speed = speed > 0 ? speed : throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be positive.");

    public void Seek(double tick) => Position = Math.Max(0, tick);

    /// <summary>Moves the position on by <paramref name="realSeconds"/> of real time, if playing.</summary>
    public void Advance(double realSeconds, double tickSeconds)
    {
        if (IsPlaying)
            Position += realSeconds * Speed / tickSeconds;
    }
}
