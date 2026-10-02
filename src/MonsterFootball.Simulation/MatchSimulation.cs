using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.AI;
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
    private readonly MatchEventLog _log = new();
    private readonly MatchContext _context;

    private readonly BallFlightSystem _ballFlight = new();
    private readonly PassDecisionSystem _passDecision;
    private readonly TeamShapeSystem _shape = new();
    private readonly MovementSystem _movement = new();
    private readonly LooseBallSystem _looseBall = new();
    private bool _started;

    public MatchSimulation(MatchConfig config, TeamDefinition homeTeam, TeamDefinition awayTeam, int seed)
        : this(config, homeTeam, awayTeam, new MatchRng(seed), seed)
    {
    }

    internal MatchSimulation(MatchConfig config, TeamDefinition homeTeam, TeamDefinition awayTeam, IMatchRandom random, int seed)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        Seed = seed;
        State = MatchState.Create(config, homeTeam, awayTeam,
            (side, player) => Formation433.KickoffPosition(config.Pitch, side, player.Position));
        _context = new MatchContext(config, State, random, _log);

        var passResolver = new PassResolver();
        _passDecision = new PassDecisionSystem(new PassTargetSelector(passResolver), passResolver);
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
        State.KickoffTeam = _context.Random.Roll(0.5) ? TeamSide.Home : TeamSide.Away;
        _context.Emit(MatchEventType.MatchStarted);
    }

    private void TakeKickoff()
    {
        var side = State.KickoffTeam;
        var taker = State.TeamPlayers(side).Single(p => p.Definition.Position == PlayingPosition.ST);
        taker.Position = State.Pitch.Centre;
        taker.MovementTarget = taker.Position;
        _context.GiveBall(taker.PlayerId);
        _context.Emit(MatchEventType.Kickoff, actor: taker.PlayerId, team: side, position: taker.Position);
        State.Phase = MatchPhase.OpenPlay;
    }

    /// <summary>Open-play tick order. Systems never call each other; this method is the only sequencing.</summary>
    private void RunOpenPlay()
    {
        _ballFlight.Apply(_context);      // advance / land a pass in flight
        _passDecision.Apply(_context);    // carrier decides (placeholder until M1.9)
        _shape.Apply(State);              // assign movement targets
        _movement.Apply(State, _config.TickSeconds);
        State.SyncBallToPossessor();
        _looseBall.Apply(_context);       // collect a loose ball
    }

    private void Finish()
    {
        State.Phase = MatchPhase.Finished;
        _context.Emit(MatchEventType.MatchFinished);
    }
}
