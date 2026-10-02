# Decision Log

Permanent record of accepted design and architecture decisions.

When a decision changes, do not delete it: mark it `Superseded by ADR-XXX` and add a new entry.

---

## ADR-001 — Simulation Is Authoritative

Status: Accepted

Decision:
Football outcomes are determined entirely by the headless simulation.
Godot presentation code only visualises results.

Reason:
This preserves determinism, testability and separation of concerns.

Rejected alternatives:
Using Godot physics and collision outcomes to determine football actions.

---

## ADR-002 — Core Player Attributes

Status: Accepted

Decision:
Players use the following attributes:

Technique, Passing, Finishing, Defending, Pace, Power, Stamina, Awareness, Keeping

Scale: 1–10

Reason:
The game intentionally uses a compact stat model rather than
Football Manager-style attribute granularity. Concepts such as vision,
crossing, strength or composure emerge from combinations of these attributes,
context, and (later) traits or cards.

Rejected alternatives:
Separate attributes such as Vision, Crossing, Strength, Composure, Aggression,
Acceleration, Tackling.

---

## ADR-003 — Simulation Is a Plain .NET Library; Godot Is an Isolated Presentation Project

Status: Accepted

Decision:
- `src/MonsterFootball.Simulation` is a plain `net10.0` class library with no Godot reference.
- The Godot project lives in `godot/` (`project.godot` + `MonsterFootball.Godot.csproj`) and references the simulation, never the reverse.
- Debug runner and tests are ordinary .NET projects that run with `dotnet` alone; the Godot editor is not required for Milestone 1.
- An architecture test fails the build if the simulation assembly references Godot or presentation assemblies.

Reason:
Enforces ADR-001 at compile time rather than by convention, keeps tests fast,
and allows headless batch simulation. The Godot project sits in its own folder
because the Godot SDK compiles every `.cs` file beneath its project directory;
placing it at the repository root would pull simulation and test sources into
the Godot assembly.

Rejected alternatives:
- Godot project at repository root (the structure suggested in the milestone spec): source globbing conflicts described above.
- Simulation code inside the Godot project: makes Godot a dependency of every test and of batch runs.

---

## ADR-004 — Repository Is the Agent's Long-Term Memory

Status: Accepted

Decision:
Project knowledge lives in `CLAUDE.md`, `docs/` and Git history.
No external memory database (Mem0, Zep, Letta) or RAG system is used in Milestone 1.

Reason:
Authoritative retrieval beats unverified remembered information. Repository
documents are versioned, reviewable and travel with the code.

Rejected alternatives:
External agent memory or vector databases — deferred until documentation size demonstrably wastes agent context.

---

## ADR-005 — Single Deterministic Random Source

Status: Accepted (implemented in M1.3 — see ADR-009)

Decision:
All simulation randomness flows through one seeded service (`IMatchRandom`) owned by the match.
No simulation system may create its own random generator or read wall-clock time.

Reason:
Identical setup + identical seed must produce identical event histories (testing, replays, debugging).

Rejected alternatives:
`System.Random` instances per system; `Random.Shared`; Godot's random functions.

---

## ADR-006 — Toolchain

Status: Accepted

Decision:
- .NET 10 (LTS), C# latest, nullable enabled, warnings treated as errors (`Directory.Build.props`).
- xUnit for tests.
- Godot 4.7 via `Godot.NET.Sdk/4.7.2` for the presentation placeholder.
- Solution file: `MonsterFootball.slnx`.

Reason:
.NET 10 is the current LTS release and is supported by Godot 4.7's .NET SDK.
Warnings-as-errors keeps nullability honest from the start.

---

## ADR-007 — The Ball Is the Single Source of Truth for Possession

Status: Accepted

Decision:
`BallState.PossessorId` is the only stored record of possession.
`MatchState.HasBall(id)`, `MatchState.PossessionPlayerId` and `MatchState.PossessionTeam` are derived from it.
`PlayerMatchState` has no stored `HasBall` flag, and `MatchState` has no stored possession team.

Reason:
The milestone spec lists `HasBall`, `PossessionTeamId` and `PossessionPlayerId` as separate fields.
Storing the same fact three times allows them to disagree. Deriving them makes
"at most one player possesses the ball" and "possessing player belongs to possession team"
true by construction rather than by checking.

Consequences:
When the ball is loose, `PossessionTeam` is null. If later systems need "last team in possession"
(e.g. for restarts), add it as a separate, explicitly named field.

Rejected alternatives:
Separate stored flags kept in sync by convention.

---

## ADR-008 — Milestone 1 Teams Fill Each 4-3-3 Slot Exactly Once

Status: Accepted

Decision:
- `PlayingPosition` enumerates the 11 slots of the default 4-3-3: GK, LB, LCB, RCB, RB, LCM, CM, RCM, LW, ST, RW.
- `TeamDefinition` is valid only with 11 players, exactly one goalkeeper, unique ids, unique shirt numbers, and every slot filled exactly once.
- `PlayerDefinition` includes a `ShirtNumber` (1–99) for readable event output ("Blue #9").

Reason:
Custom formations are out of scope for Milestone 1. A fixed slot set lets positioning (M1.4)
map each player to one anchor without ambiguity. Shirt numbers are presentation-friendly identity, not a stat.

Consequences:
Introducing formations later will replace the "every slot exactly once" rule with formation-specific validation — supersede this ADR when that happens.

---

## ADR-009 — MatchRng Uses an In-House xoshiro256** Implementation

Status: Accepted

Decision:
- `IMatchRandom` exposes `NextDouble()`, `NextInt(min, maxExclusive)` and `Roll(probability)`.
- `MatchRng` implements it with xoshiro256**, state seeded from the `int` match seed via SplitMix64.
- `NextInt` uses rejection sampling (no modulo bias). `Roll` always consumes exactly one draw, even for 0 or 1.
- A test pins the first outputs for seed 123456; the values were cross-checked against an independent reference implementation.
- An architecture test scans the compiled simulation for references to `System.Random`, `DateTime`, `DateTimeOffset`, `Guid`, `Stopwatch` and `Parallel`, and fails if any appear.

Reason:
`System.Random`'s algorithm is an implementation detail of .NET and may change between runtime versions,
which would silently change every seeded match. Owning a small, well-known algorithm makes seeds permanent.
Constant draw consumption in `Roll` keeps the random stream aligned regardless of probability values.

Consequences:
Changing the algorithm, seeding, or the order in which systems draw numbers changes the outcome for every seed.
Treat such changes as deliberate and update pinned tests consciously.

Rejected alternatives:
`new System.Random(seed)`; `Random.Shared`; per-system generators.

---

## ADR-010 — Stepping a Finished Match Throws

Status: Accepted

Decision:
`MatchSimulation.Step()` throws `InvalidOperationException` once `Phase == Finished`.
`RunToCompletion()` is idempotent: calling it again returns the same result without new events.

Reason:
The milestone requires that the simulation "cannot accidentally continue after finish". A silent no-op would
hide caller bugs (e.g. a presentation loop that keeps stepping); an exception surfaces them immediately.

---

## ADR-011 — Team Shape Is Computed in Team Frame from Ball-Shifted Anchors

Status: Accepted

Decision:
- Shape logic is written once in a team-relative frame (own goal at X = 0, attacking +X, left flank +Y) and converted to pitch coordinates by a 180° rotation for Away (`TeamFrame`).
- Each 4-3-3 slot has an in-possession and an out-of-possession anchor (authored on 105 × 68, scaled to the pitch). Each tick the block is shifted toward the ball by a fixed fraction of the ball's offset from the centre spot.
- Out of possession the back four share one line X, kept goal-side of the ball; midfield sits ahead of the line and is compressed centrally; forwards sit ahead of midfield. The nearest outfield player sprints to the ball.
- When the ball is loose, both teams use the out-of-possession shape and each sends one presser.
- Goalkeepers stand on the goal–ball line at a depth that grows with ball distance and is scaled by Awareness; they never leave the penalty area.

Reason:
Gives a recognisable, mirror-symmetric shape with very little code and no randomness, and
avoids "22 players chasing the ball". Writing the rules in team frame removes a whole class of
Home/Away sign bugs; a symmetry test enforces it.

Consequences:
This is positional scaffolding, not tactical intelligence. Off-ball choices such as support runs,
forward runs and recovery (M1.9 utility AI) will adjust or override these targets.

---

## ADR-012 — Passes Are Resolved at the Kick and Applied on Arrival

Status: Accepted

Decision:
- `PassResolver.Resolve` decides the outcome (Completed / Intercepted / Misplaced) and the landing point when the ball is struck, using `IMatchRandom`.
- The ball then travels as a `BallFlight` for the resolved duration (ball speed 12 + 0.25 × distance m/s, capped at 25). During flight nobody possesses it; `MatchState.AttackingTeam` stays with the passing team.
- On the arrival tick the outcome is applied: receiver or interceptor gains possession, or the ball is left loose at the misplaced landing point.
- A loose ball is collected by the nearest player within 1.5 m.
- Every possession transfer goes through `MatchContext.GiveBall`, which emits `PossessionChanged` whenever control passes to the other team. `MatchState.LastPossessionTeam` records who last had control (the field anticipated by ADR-007).

Reason:
Deciding at the kick keeps resolution a single, testable function and gives the presentation layer the
authoritative result (start, target, duration, quality) before the animation starts. Holding the ball in
flight stops possession teleporting and means nobody can act on a ball that hasn't arrived.

Consequences:
Players can move during the flight, so a receiver may be a little off the landing point; the ball snaps to
the player who gains it. Misplaced passes are clamped to the pitch — there are no throw-ins or goal kicks yet.

Rejected alternatives:
Instant resolution with possession transferred immediately (simpler, but possession changes before the ball
visibly arrives); continuous ball physics with interception checks per tick (more complex, harder to test).

---

## ADR-013 — No Transcendental Maths Functions in Outcome Calculations

Status: Accepted

Decision:
Outcome-affecting code uses only +, −, ×, ÷, `Math.Sqrt`, `Math.Abs`, `Math.Min/Max/Clamp`. Probabilities come
from `Curves.Sigmoid(x) = 0.5 + 0.5·x/√(1+x²)` rather than a logistic `1/(1+e^−x)`. Random directions are
produced by rejection sampling the unit disc, not with `sin`/`cos`.

Reason:
IEEE 754 requires basic arithmetic and square root to be correctly rounded, so they give identical results on
every platform. `Math.Exp`, `Math.Sin` etc. delegate to platform maths libraries that can differ in the last bit,
which would let the same seed play a different match on another OS or CPU.

---

## ADR-014 — Carries Keep the Ball; Tackles Resolve in Two Rolls

Status: Accepted

Decision:
- A carry is a planned run of up to 10 m (`CarryResolver`, no randomness). The carrier keeps possession throughout; only an opponent's tackle (or the carrier's own pass) ends it early. Speed = top speed × (0.55 + 0.03 × Technique); Awareness steers away from the nearest opponent.
- A tackle (`TackleResolver`) rolls **contact** — `Sigmoid(0.3 × (defender rating − carrier rating) + 0.15)` — and, if made, **clean** — `0.6 + 0.03 × (Awareness − 5)`. Outcomes: no contact → `Lost`; clean → `Won`; otherwise → `LooseBall` (ball 2–5 m away in a random direction).
- The spec's "LooseBall" outcome is emitted as a new event type `TackleLooseBall`.
- Any time the ball leaves a player (`GiveBallTo` someone else, `SetBallLoose`, `LaunchBall`), `MatchState` clears that player's intent and carry. An invariant checks a carry only exists on the ball carrier.

Reason:
The spec says carrying should "retain possession" and "expose the player to defenders": the exposure is the
tackle, so a separate heavy-touch roll would double-count risk. Two-roll tackles give the three required
outcomes with separate, tunable knobs (who wins the duel vs. who ends up with the ball). Clearing intents
in `MatchState` rather than in each caller makes stale carries impossible (an invariant caught exactly this bug).

Consequences:
Tackle and carry *decisions* (when to tackle, when to carry) are placeholders until utility AI (M1.9).
