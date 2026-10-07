using MonsterFootball.Simulation.Core;
using MonsterFootball.Simulation.Events;
using MonsterFootball.Simulation.Players;
using MonsterFootball.Simulation.Random;

namespace MonsterFootball.Simulation.Tests.TestSupport;

/// <summary>
/// A hand-arranged match for action tests. Every player starts parked out of the way
/// (Home along its own goal line, Away along the far goal line) so tests place only who matters.
/// </summary>
internal sealed class Scenario
{
    public Scenario(PlayerStats? homeStats = null, PlayerStats? awayStats = null, IMatchRandom? random = null)
    {
        State = TestTeams.Match(homeStats, awayStats);
        Log = new MatchEventLog();
        Context = new MatchContext(MatchConfig.Default, State, random ?? new MatchRng(1), Log);
        State.ResumePlay(); // action scenarios take place in open play

        var parked = 0;
        foreach (var player in State.Players)
        {
            var x = player.Team == TeamSide.Home ? 1.0 : State.Pitch.Length - 1;
            player.Position = new Vec2(x, 2 + 3 * (parked++ % 11));
            player.MovementTarget = player.Position;
        }
    }

    public MatchState State { get; }
    public MatchEventLog Log { get; }
    public MatchContext Context { get; }

    public IReadOnlyList<MatchEventType> EventTypes => Log.Events.Select(e => e.Type).ToList();

    public PlayerMatchState Place(TeamSide side, PlayingPosition slot, double x, double y)
    {
        var player = State.Slot(side, slot);
        player.Position = new Vec2(x, y);
        player.MovementTarget = player.Position;
        return player;
    }

    public PlayerMatchState GiveBall(TeamSide side, PlayingPosition slot, PossessionCause cause = PossessionCause.Reception)
    {
        var player = State.Slot(side, slot);
        Context.GiveBall(player.PlayerId, cause);
        return player;
    }
}
