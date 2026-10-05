namespace MonsterFootball.Simulation.AI;

/// <summary>
/// Every utility weight the AI uses, as data (ADR-018). Defaults came from a random search scored against
/// plausibility bands (goals, shots, passes, carries, tackles, scorer spread); tests and batch
/// tuning (M1.11) can override any of them through <see cref="Core.MatchConfig.Ai"/>.
/// </summary>
public sealed record AiTuning
{
    public static AiTuning Default { get; } = new();

    public OnBallWeights OnBall { get; init; } = new();
    public OffBallWeights OffBall { get; init; } = new();
}

/// <summary>Weights for the ball carrier's options (see <see cref="OnBallCandidateGenerator"/>).</summary>
public sealed record OnBallWeights
{
    // Hold: keep the ball for now. High just after receiving, decays quickly, collapses under pressure.
    public double HoldBase { get; init; } = 88;
    public double HoldDecayPerSecond { get; init; } = 12;
    public double ContinueCarryBase { get; init; } = 62;
    public double ContinueCarryDecayPerSecond { get; init; } = 10;
    public double PressureRadius { get; init; } = 3;
    public double PressurePenalty { get; init; } = 30;

    // Shoot: any reasonable chance is worth taking; better chances are worth much more.
    public double ShotBase { get; init; } = 22;
    public double ShotUtilityPerXg { get; init; } = 318;
    public double MinShotXg { get; init; } = 0.03;
    public double MaxShootingDistance { get; init; } = 35;

    // Pass.
    public double PassSuccessWeight { get; init; } = 50;
    public double PassProgressionWeight { get; init; } = 0.75;
    public double PassGoalProximityWeight { get; init; } = 18;
    public double GoalProximityRange { get; init; } = 35;
    public double PassToKeeperPenalty { get; init; } = 15;
    public double MinPassDistance { get; init; } = 4;

    // Carry.
    public double CarryBase { get; init; } = 42;
    public double CarryProgressionWeight { get; init; } = 0.8;
    public double CarrySpaceWeight { get; init; } = 2;
    public double CarrySpaceCap { get; init; } = 12;
    public double CarryTechniquePressureRelief { get; init; } = 0.05;
}

/// <summary>Weights for off-ball behaviours (see <see cref="OffBallCandidateGenerator"/>).</summary>
public sealed record OffBallWeights
{
    public double MoveIntoPositionUtility { get; init; } = 40;
    public double RecoverThreshold { get; init; } = 10;
    public double RecoverPerMetre { get; init; } = 1.5;
    public double RecoverAheadOfBallBonus { get; init; } = 10;
    public double SupportBase { get; init; } = 28;
    public double SupportDistance { get; init; } = 12;
    public double SupportMaxRange { get; init; } = 30;
    public double SupportOpennessWeight { get; init; } = 2.5;
    public double SupportOpennessCap { get; init; } = 10;
    public double SupportLaneWeight { get; init; } = 2;
    public double SupportLaneCap { get; init; } = 5;
    public double SupportTravelPenalty { get; init; } = 0.4;
    public double SupportForwardBonus { get; init; } = 4;
    public double RunBase { get; init; } = 31;
    public double RunPerPace { get; init; } = 0.8;
    public double RunSpaceWeight { get; init; } = 0.6;
    public double RunSpaceCap { get; init; } = 20;
    public double RunMinBallX { get; init; } = 35;
    public double RunBeyondLine { get; init; } = 1;
    public double RunMaxDepthFromGoalLine { get; init; } = 16;
    public double MarkBase { get; init; } = 36;
    public double MarkThreatWeight { get; init; } = 18;
    public double MarkThreatRange { get; init; } = 50;
    public double MarkZoneRadius { get; init; } = 15;
    public double MarkGoalSideDistance { get; init; } = 2;
    public double MarkTravelPenalty { get; init; } = 0.5;

    // Tackling (the challenge part of CloseDownOpponent; see TackleSystem).
    public double TackleBaseRatePerSecond { get; init; } = 0.17;
    public double TackleDangerBonus { get; init; } = 1.0;
}
