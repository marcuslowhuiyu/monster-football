namespace MonsterFootball.Simulation.Core;

using MonsterFootball.Simulation.Players;

/// <summary>
/// The complete authoritative state of an active match.
/// Read access is public; mutation is internal to the simulation.
/// </summary>
public sealed class MatchState
{
    public const int ActivePlayerCount = TeamDefinition.PlayersPerTeam * 2;

    private readonly Dictionary<PlayerId, PlayerMatchState> _playersById;

    private MatchState(
        PitchGeometry pitch,
        double tickSeconds,
        TeamDefinition homeTeam,
        TeamDefinition awayTeam,
        IReadOnlyList<PlayerMatchState> players)
    {
        Pitch = pitch;
        Clock = new MatchClock(tickSeconds);
        HomeTeam = homeTeam;
        AwayTeam = awayTeam;
        Players = players;
        _playersById = players.ToDictionary(p => p.PlayerId);
        Ball = new BallState(pitch.Centre);
        _homeShapeFocus = pitch.Centre;
        _awayShapeFocus = pitch.Centre;
    }

    public PitchGeometry Pitch { get; }
    public MatchClock Clock { get; }
    public TeamDefinition HomeTeam { get; }
    public TeamDefinition AwayTeam { get; }

    /// <summary>All 22 players in a stable order: home team then away team, each in definition order.</summary>
    public IReadOnlyList<PlayerMatchState> Players { get; }

    public BallState Ball { get; }
    public Score Score { get; } = new();
    public MatchPhase Phase { get; internal set; } = MatchPhase.Kickoff;

    /// <summary>The team that most recently had the ball under control; null before the kickoff.</summary>
    public TeamSide? LastPossessionTeam { get; private set; }

    /// <summary>Tick at which the current possessor gained the ball.</summary>
    public long PossessionStartTick { get; private set; }

    /// <summary>
    /// The team currently attacking: the possessor's team, or the team that struck a pass or shot in flight.
    /// Null when the ball is loose.
    /// </summary>
    public TeamSide? AttackingTeam => PossessionTeam ?? Ball.Flight?.Team;

    private Vec2 _homeShapeFocus;
    private Vec2 _awayShapeFocus;

    /// <summary>
    /// Smoothed ball position a team's shape is built around (a moving average of the ball), so blocks shift with
    /// the flow of play rather than with every square pass. Pressing and marking use the real ball.
    /// </summary>
    public Vec2 ShapeFocus(TeamSide side) => side == TeamSide.Home ? _homeShapeFocus : _awayShapeFocus;

    internal void SetShapeFocus(TeamSide side, Vec2 focus)
    {
        if (side == TeamSide.Home)
            _homeShapeFocus = focus;
        else
            _awayShapeFocus = focus;
    }

    /// <summary>The side that takes the next kickoff.</summary>
    public TeamSide KickoffTeam { get; internal set; } = TeamSide.Home;

    public PlayerId? PossessionPlayerId => Ball.PossessorId;

    /// <summary>The team of the player holding the ball, or null when the ball is loose.</summary>
    public TeamSide? PossessionTeam =>
        Ball.PossessorId is { } id ? _playersById[id].Team : null;

    /// <summary>
    /// Creates a match with every player placed by <paramref name="startingPosition"/>.
    /// The ball starts loose at the centre spot.
    /// </summary>
    public static MatchState Create(
        MatchConfig config,
        TeamDefinition homeTeam,
        TeamDefinition awayTeam,
        Func<TeamSide, PlayerDefinition, Vec2> startingPosition)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(homeTeam);
        ArgumentNullException.ThrowIfNull(awayTeam);
        ArgumentNullException.ThrowIfNull(startingPosition);
        var pitch = config.Pitch;

        var sharedIds = homeTeam.Players.Select(p => p.Id)
            .Intersect(awayTeam.Players.Select(p => p.Id))
            .ToList();
        if (sharedIds.Count > 0)
            throw new ArgumentException(
                $"Player ids must be unique across both teams; shared: {string.Join(", ", sharedIds)}.");

        var players = homeTeam.Players.Select(d => (Side: TeamSide.Home, Definition: d))
            .Concat(awayTeam.Players.Select(d => (Side: TeamSide.Away, Definition: d)))
            .Select(x => new PlayerMatchState(x.Definition, x.Side, startingPosition(x.Side, x.Definition)))
            .ToList();

        var outside = players.Where(p => !pitch.Contains(p.Position)).ToList();
        if (outside.Count > 0)
            throw new ArgumentException(
                $"Starting positions must be on the pitch; outside: {string.Join("; ", outside)}.");

        return new MatchState(pitch, config.TickSeconds, homeTeam, awayTeam, players.AsReadOnly());
    }

    public TeamDefinition Team(TeamSide side) => side == TeamSide.Home ? HomeTeam : AwayTeam;

    public IEnumerable<PlayerMatchState> TeamPlayers(TeamSide side) => Players.Where(p => p.Team == side);

    public PlayerMatchState Player(PlayerId id) =>
        _playersById.TryGetValue(id, out var player)
            ? player
            : throw new KeyNotFoundException($"No player {id} in this match.");

    public bool HasBall(PlayerId id) => Ball.PossessorId == id;

    internal void GiveBallTo(PlayerId id)
    {
        var player = Player(id);
        ReleaseHolder(except: id);
        Ball.GiveTo(id, player.Position);
        LastPossessionTeam = player.Team;
        PossessionStartTick = Clock.Tick;
    }

    internal void SetBallLoose(Vec2 position)
    {
        ReleaseHolder();
        Ball.SetLoose(position);
    }

    internal void LaunchBall(Actions.BallFlight flight)
    {
        ReleaseHolder();
        Ball.Launch(flight);
    }

    /// <summary>A player who loses the ball also loses any action that needed it (e.g. a carry).</summary>
    private void ReleaseHolder(PlayerId? except = null)
    {
        if (Ball.PossessorId is { } holder && holder != except)
            Player(holder).ClearIntent();
    }

    /// <summary>A possessed ball travels with its carrier.</summary>
    internal void SyncBallToPossessor()
    {
        if (Ball.PossessorId is { } id)
            Ball.GiveTo(id, Player(id).Position);
    }
}
