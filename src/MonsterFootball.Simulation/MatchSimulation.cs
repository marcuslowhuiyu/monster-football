using MonsterFootball.Simulation.Actions;
using MonsterFootball.Simulation.Actions.Carrying;
using MonsterFootball.Simulation.Actions.Passing;
using MonsterFootball.Simulation.Actions.Shooting;
using MonsterFootball.Simulation.Actions.Tackling;
using MonsterFootball.Simulation.AI;
using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Fitness;
using MonsterFootball.Simulation.Movement;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Positioning;
using MonsterFootball.Simulation.Random;
using MonsterFootball.Simulation.Restarts;

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
    private readonly CarrySystem _carry = new();
    private readonly OnBallDecisionSystem _onBallDecision;
    private readonly TackleSystem _tackle = new(new TackleResolver());
    private readonly TeamShapeSystem _shape = new();
    private readonly MovementSystem _movement = new();
    private readonly LooseBallSystem _looseBall = new();
    private readonly StaminaSystem _stamina = new();
    private readonly RestartSystem _restart = new();
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
        _onBallDecision = new OnBallDecisionSystem(
            new PassTargetSelector(passResolver), passResolver, new CarryResolver(), new ShotResolver());
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
                _restart.TakeKickoff(_context);
                break;
            case MatchPhase.OpenPlay:
                RunOpenPlay();
                break;
            case MatchPhase.GoalRestart:
                _restart.ResetForKickoff(State);
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

    /// <summary>
    /// Open-play tick order. Systems never call each other; this method is the only sequencing.
    /// A goal switches the phase to GoalRestart; the rest of that tick is skipped.
    /// </summary>
    private void RunOpenPlay()
    {
        _ballFlight.Apply(_context);      // advance / land a pass in flight
        _carry.Apply(_context);           // end finished carries
        _onBallDecision.Apply(_context);  // carrier shoots, passes or carries (placeholder until M1.9)
        if (State.Phase != MatchPhase.OpenPlay)
            return;

        _tackle.Apply(_context);          // defender in range challenges (placeholder until M1.9)
        _shape.Apply(State);              // assign movement targets
        _movement.Apply(State, _config.TickSeconds);
        State.SyncBallToPossessor();
        _looseBall.Apply(_context);       // collect a loose ball
        _stamina.Apply(_context);         // drain stamina from movement and this tick's actions
    }

    private void Finish()
    {
        State.Phase = MatchPhase.Finished;
        _context.Emit(MatchEventType.MatchFinished);
    }
}
