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
using MonsterFootball.Simulation.Statistics;

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
    private readonly TackleSystem _tackle;
    private readonly TeamShapeSystem _shape = new();
    private readonly OffBallDecisionSystem _offBall;
    private readonly MovementSystem _movement = new();
    private readonly LooseBallSystem _looseBall = new();
    private readonly StaminaSystem _stamina = new();
    private readonly RestartSystem _restart = new();
    private bool _started;

    /// <summary>Time constants of the ball average each team's shape follows (see <see cref="MatchState.ShapeFocus"/>):
    /// attackers drift with the flow of play, defenders track the ball closely.</summary>
    public const double AttackingShapeFocusSeconds = 3.0;

    public const double DefendingShapeFocusSeconds = 0.75;

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
        var selector = new UtilitySelector();
        _tackle = new TackleSystem(new TackleResolver(), config.Ai.OffBall);
        _offBall = new OffBallDecisionSystem(new OffBallCandidateGenerator(config.Ai.OffBall), selector);
        var carryResolver = new CarryResolver();
        var shotResolver = new ShotResolver();
        _onBallDecision = new OnBallDecisionSystem(
            new OnBallCandidateGenerator(config.Ai.OnBall, passResolver, carryResolver, shotResolver),
            selector,
            passResolver,
            carryResolver,
            shotResolver);
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

        return new MatchResult(State.Score.Home, State.Score.Away, Events, State, MatchStatistics.FromEvents(Events), Seed);
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
        _onBallDecision.Apply(_context);  // carrier holds, passes, carries or shoots (utility AI)
        if (State.Phase != MatchPhase.OpenPlay)
            return;

        _tackle.Apply(_context);          // defender in range challenges (placeholder until M1.9)
        _shape.Apply(State,                                  // default targets: shape, presser, pass receiver
            _config.TickSeconds / AttackingShapeFocusSeconds,
            _config.TickSeconds / DefendingShapeFocusSeconds);
        _offBall.Apply(_context);         // off-ball utility AI: support, runs, marking, recovery
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
