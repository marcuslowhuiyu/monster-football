using System;
using System.Linq;
using Godot;
using MonsterFootball.Playback;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Players;

namespace MonsterFootball.Presentation.MatchViewer;

/// <summary>
/// Developer match viewer (Milestone 2, ADR-026). Each rendered frame passes the real time elapsed to a
/// <see cref="PlaybackSession"/> and draws the interpolated result. All football comes from the simulation; this node
/// only draws simple shapes and forwards playback controls. Space toggles play/pause.
/// Launch options after <c>++</c>: <c>--seed=N --start=TICK --speed=X --autoplay</c>.
/// </summary>
public partial class MatchViewer : Node2D
{
    private static readonly Color HomeColour = new(0.25f, 0.5f, 1f);
    private static readonly Color AwayColour = new(0.95f, 0.3f, 0.3f);
    private const string HomeName = "Blue";
    private const string AwayName = "Red";
    private const float HudHeight = 64;

    [Export] public int Seed { get; set; } = 1;

    private PlaybackSession _session = null!;
    private ViewState? _view;
    private Label _score = null!;
    private Label _clock = null!;
    private Label _status = null!;
    private Button _playButton = null!;
    private SpinBox _seedBox = null!;

    public override void _Ready()
    {
        // Command-line options after "++": --seed=N --start=TICK --speed=X --autoplay
        var options = OS.GetCmdlineUserArgs();
        string? Option(string name) =>
            options.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (Option("seed") is { } seed)
            Seed = int.Parse(seed, invariant);

        _session = new PlaybackSession(MatchConfig.Default, SampleTeams.Create(HomeName, 1), SampleTeams.Create(AwayName, 101), Seed);
        if (Option("start") is { } start)
            _session.Clock.Seek(double.Parse(start, invariant));
        if (Option("speed") is { } speed)
            _session.Clock.SetSpeed(double.Parse(speed, invariant));
        if (options.Contains("--autoplay"))
            _session.Clock.Play();
        BuildHud();
    }

    public override void _Process(double delta)
    {
        _view = _session.Update(delta);
        UpdateHud(_view);
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
            TogglePlay();
    }

    public override void _Draw()
    {
        if (_view is null)
            return;

        var area = GetViewportRect();
        if (area.Size.X < 100 || area.Size.Y < 100)
            return; // no room to draw (e.g. a headless run)

        var transform = PitchTransform.Fit(MatchConfig.Default.Pitch,
            new Rect2(area.Position + new Vector2(16, HudHeight + 8), area.Size - new Vector2(32, HudHeight + 24)));
        PitchDrawing.Draw(this, transform);

        var frame = _view.Frame;
        var font = ThemeDB.FallbackFont;
        var radius = transform.Metres(1.1);
        for (var i = 0; i < frame.Players.Length; i++)
        {
            var player = frame.Players[i];
            if (!player.IsActive)
                continue; // sent off
            var centre = transform.ToScreen(_view.PlayerPositions[i]);
            var colour = player.Team == TeamSide.Home ? HomeColour : AwayColour;
            if (player.Slot == PlayingPosition.GK)
                colour = colour.Lightened(0.45f);

            DrawCircle(centre, radius, colour);
            DrawArc(centre, radius, 0, Mathf.Tau, 24, Colors.Black, 1.5f);
            if (frame.Ball.PossessorId == player.Id)
                DrawArc(centre, radius + 3, 0, Mathf.Tau, 24, Colors.White, 2);

            var size = Math.Max(8, Mathf.RoundToInt(radius * 1.1f));
            DrawString(font, centre + new Vector2(-radius, size * 0.35f), player.ShirtNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment.Center, radius * 2, size, Colors.White);
        }

        var ball = transform.ToScreen(_view.BallPosition);
        DrawCircle(ball, transform.Metres(0.55), Colors.White);
        DrawArc(ball, transform.Metres(0.55), 0, Mathf.Tau, 16, Colors.Black, 1.5f);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var bar = new HBoxContainer { Position = new Vector2(16, 12) };
        bar.AddThemeConstantOverride("separation", 14);
        layer.AddChild(bar);

        _score = new Label();
        _score.AddThemeFontSizeOverride("font_size", 22);
        _clock = new Label();
        _clock.AddThemeFontSizeOverride("font_size", 22);
        _status = new Label();
        _status.AddThemeFontSizeOverride("font_size", 18);
        bar.AddChild(_score);
        bar.AddChild(_clock);
        bar.AddChild(_status);

        _playButton = new Button { Text = "Play" };
        _playButton.Pressed += TogglePlay;
        bar.AddChild(_playButton);

        var speed = new OptionButton();
        foreach (var s in PlaybackClock.Speeds)
            speed.AddItem($"{s}×");
        speed.Selected = PlaybackClock.Speeds.ToList().IndexOf(_session.Clock.Speed) is var i and >= 0 ? i : 2;
        speed.ItemSelected += index => _session.Clock.SetSpeed(PlaybackClock.Speeds[(int)index]);
        bar.AddChild(speed);

        var restart = new Button { Text = "Restart" };
        restart.Pressed += () => _session.Restart((int)_seedBox.Value);
        bar.AddChild(restart);

        bar.AddChild(new Label { Text = "Seed" });
        _seedBox = new SpinBox { MinValue = 1, MaxValue = 1_000_000, Step = 1, Value = Seed };
        bar.AddChild(_seedBox);

        var next = new Button { Text = "Next seed" };
        next.Pressed += () =>
        {
            _seedBox.Value += 1;
            _session.Restart((int)_seedBox.Value);
        };
        bar.AddChild(next);
    }

    private void TogglePlay() => _session.Clock.Toggle();

    private void UpdateHud(ViewState view)
    {
        var frame = view.Frame;
        _score.Text = $"{HomeName} {frame.HomeGoals}–{frame.AwayGoals} {AwayName}";
        var half = frame.Time.Period == 1 ? "1st half" : frame.Time.Period == 2 ? "2nd half" : $"period {frame.Time.Period}";
        _clock.Text = $"{frame.Time}  ({half})";
        _status.Text = frame.Phase switch
        {
            MatchPhase.OpenPlay => "IN PLAY",
            MatchPhase.Finished => "FULL TIME",
            _ when frame.Stoppage is { } s => $"STOPPED — {Words(s.Reason.ToString())}: {Words(s.Restart.ToString())} to {(s.AwardedTeam == TeamSide.Home ? HomeName : AwayName)}",
            _ => "STOPPED",
        };
        _status.Modulate = frame.Phase == MatchPhase.OpenPlay ? Colors.LightGreen : Colors.Orange;
        _playButton.Text = _session.Clock.IsPlaying ? "Pause" : "Play";
    }

    /// <summary>An enum name in words: <c>BallOutOfPlay</c> → "ball out of play".</summary>
    private static string Words(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : i == 0 ? char.ToLowerInvariant(c).ToString() : c.ToString()));
}
