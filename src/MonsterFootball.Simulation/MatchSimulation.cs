using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation;

/// <summary>
/// Runs one match headlessly on a fixed timestep. Each <see cref="Step"/> advances exactly one tick.
/// Same config, teams and seed always produce the same event history.
/// </summary>
public sealed class MatchSimulation
{
    private readonly MatchConfig _config;
    private readonly IMatchRandom _random;
    private readonly MatchEventLog _log = new();
    private readonly TeamShapeSystem _shape = new();
    private readonly MovementSystem _movement = new();
    private bool _started;

    public MatchSimulation(MatchConfig config, TeamDefinition homeTeam, TeamDefinition awayTeam, int seed)
        : this(config, homeTeam, awayTeam, new MatchRng(seed), seed)
    {
    }

    internal MatchSimulation(MatchConfig config, TeamDefinition homeTeam, TeamDefinition awayTeam, IMatchRandom random, int seed)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _random = random;
        Seed = seed;
        State = MatchState.Create(config, homeTeam, awayTeam,
            (side, player) => Formation433.KickoffPosition(config.Pitch, side, player.Position));
    }

    public int Seed { get; }
    public MatchState State { get; }
    public IReadOnlyList<MatchEvent> Events => _log.Events;
    public bool IsFinished => State.Phase == MatchPhase.Finished;

    /// <summary>Advances the match by one tick. Throws once the match has finished.</summary>
    public void Step()
    {
        if (IsFinished)
            throw new InvalidOperationException("The match has finished; it cannot be stepped further.");

        if (!_started)
            Start();

        switch (State.Phase)
        {
            case MatchPhase.Kickoff:
                TakeKickoff();
                break;
            case MatchPhase.OpenPlay:
                RunOpenPlay();
                break;
            case MatchPhase.GoalRestart:
                // Goals arrive in M1.7.
                break;
        }

        State.Clock.Advance();

        if (State.Clock.Tick >= _config.TotalTicks)
            Finish();
    }

    public MatchResult RunToCompletion()
    {
        while (!IsFinished)
            Step();

        return new MatchResult(State.Score.Home, State.Score.Away, Events, State, Seed);
    }

    private void Start()
    {
        _started = true;
        // Coin toss for the opening kickoff.
        State.KickoffTeam = _random.Roll(0.5) ? TeamSide.Home : TeamSide.Away;
        Emit(MatchEventType.MatchStarted);
    }

    private void TakeKickoff()
    {
        var side = State.KickoffTeam;
        var taker = State.TeamPlayers(side).Single(p => p.Definition.Position == PlayingPosition.ST);
        taker.Position = State.Pitch.Centre;
        taker.MovementTarget = taker.Position;
        State.GiveBallTo(taker.PlayerId);
        Emit(MatchEventType.Kickoff, actor: taker.PlayerId, team: side, position: taker.Position);
        State.Phase = MatchPhase.OpenPlay;
    }

    private void RunOpenPlay()
    {
        // On-ball decisions (pass/carry/shoot) arrive in M1.5–M1.9.
        _shape.Apply(State);
        _movement.Apply(State, _config.TickSeconds);
        State.SyncBallToPossessor();
    }

    private void Finish()
    {
        State.Phase = MatchPhase.Finished;
        Emit(MatchEventType.MatchFinished);
    }

    private void Emit(MatchEventType type, PlayerId? actor = null, PlayerId? target = null, TeamSide? team = null, Vec2? position = null) =>
        _log.Add(new MatchEvent(State.Clock.Now, type, actor, target, team, position));
}
