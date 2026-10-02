# Milestone 1 — Deterministic Match Simulation Foundation

## 1. Purpose

Build the first technical foundation for the fantasy football management/deckbuilding game.

This milestone must produce a deterministic **11v11 football simulation** that can run entirely without graphics.

The purpose is not to create a fun or complete football match yet.

The purpose is to prove that we can represent:

- two full football teams,
- 22 active players,
- players positioned on a pitch,
- possession,
- movement,
- passing,
- carrying,
- tackling,
- shooting,
- goalkeeping,
- goals,
- stamina,
- a match clock,
- deterministic randomness,
- structured match events.

At the end of this milestone, the simulation must be executable through automated tests or a simple debug runner without requiring a Godot scene.

---

# 2. Long-Term Game Context

The finished game will be a stylised 2.5D fantasy football management/deckbuilding game.

The core gameplay concept is:

> The squad is the deck.

Football matches run automatically.

At tactically important moments, time slows and the manager can play cards representing tactical interventions.

Examples eventually include:

- through balls,
- overlaps,
- counterattacks,
- pressing instructions,
- defensive responses,
- player-specific signature actions.

However, **none of the card or Opportunity systems belong in this milestone**.

Milestone 1 establishes the underlying football simulation upon which those systems will later operate.

---

# 3. Technical Direction

Use:

- Godot 4.x .NET
- C#
- .NET-compatible unit testing
- Git
- Claude Code or equivalent coding agent

The football simulation must run independently of Godot presentation Nodes.

The core architectural rule is:

> The simulation owns game truth. The presentation layer only visualises simulation state.

Do not use Godot physics to determine football outcomes.

Instead:

1. the simulation decides the outcome;
2. the simulation updates authoritative state;
3. the simulation emits events;
4. the presentation layer later visualises those results.

---

# 4. AI Agent Memory and Project Knowledge

The repository itself is the authoritative long-term memory for the AI agent.

Do **not** rely on conversational memory to preserve important project decisions.

Do not introduce an external memory database such as Mem0, Zep or Letta during Milestone 1.

The initial memory architecture is:

```text
Git Repository
│
├── CLAUDE.md
│
├── docs/
│   ├── GAME_DESIGN.md
│   ├── ARCHITECTURE.md
│   ├── MATCH_SIMULATION.md
│   ├── DECISIONS.md
│   ├── CURRENT_STATE.md
│   ├── GLOSSARY.md
│   └── milestones/
│       └── MILESTONE_1.md
│
└── Git history
```

These files serve different purposes and should not duplicate each other unnecessarily.

---

## 4.1 `CLAUDE.md`

`CLAUDE.md` contains **short, permanent instructions for coding agents**.

Keep it concise.

It should include rules such as:

```text
# Core Project Rules

- Simulation is authoritative.
- Presentation never determines football outcomes.
- Core simulation must run headlessly.
- All simulation randomness must use MatchRng.
- The simulation must remain deterministic by seed.
- Use the approved player stat model only.
- Do not implement systems belonging to future milestones.
- Prefer composition over deep inheritance.
- Add automated tests for football resolution systems.
- Read docs/CURRENT_STATE.md before beginning substantial work.
- Read relevant architecture/design documentation before modifying a system.
- Update docs/CURRENT_STATE.md after substantial completed work.
- Record significant architectural or design decisions in docs/DECISIONS.md.
- Do not silently override an accepted decision.
```

Do not fill `CLAUDE.md` with detailed game design documentation.

It is an instruction layer, not a design document.

---

## 4.2 `GAME_DESIGN.md`

Contains current high-level game-design truth.

For now it only needs enough context to explain:

- autonomous football matches,
- eventual Opportunity system,
- squad-as-deck concept,
- intended stat model,
- long-term distinction between simulation and presentation.

Do not document every speculative future idea during Milestone 1.

---

## 4.3 `ARCHITECTURE.md`

Contains accepted technical architecture.

Document:

- simulation/presentation separation,
- project/module boundaries,
- deterministic simulation requirements,
- core dependency direction,
- event architecture,
- testing philosophy,
- important extension points.

This should describe **why the system is structured this way**, not merely list classes.

---

## 4.4 `MATCH_SIMULATION.md`

Contains detailed documentation specific to the football simulation.

Document:

- fixed timestep,
- match state,
- player state,
- ball state,
- action intent/resolution pattern,
- utility AI,
- event generation,
- deterministic RNG,
- current limitations.

Update this whenever the simulation architecture materially changes.

---

## 4.5 `DECISIONS.md`

This is the permanent decision log.

Important design and architectural decisions should be recorded here.

Use entries similar to:

```text
## ADR-001 — Simulation Is Authoritative

Status: Accepted

Decision:
Football outcomes are determined entirely by the headless simulation.
Godot presentation code only visualises results.

Reason:
This preserves determinism, testability and separation of concerns.

Rejected alternatives:
Using Godot physics and collision outcomes to determine football actions.
```

Another example:

```text
## ADR-002 — Core Player Attributes

Status: Accepted

Decision:
Players use the following attributes:

Technique
Passing
Finishing
Defending
Pace
Power
Stamina
Awareness
Keeping

Scale:
1–10

Reason:
The game intentionally uses a compact stat model rather than
Football Manager-style attribute granularity.
```

When an old decision changes:

- do not erase the history;
- mark the old decision superseded;
- create a new decision explaining the replacement.

This prevents the AI from treating obsolete ideas as current truth.

---

## 4.6 `CURRENT_STATE.md`

This is short-term project memory.

It should be concise and regularly updated.

Suggested format:

```text
# Current Development State

Current milestone:
M1 — Deterministic Match Simulation

Current subtask:
M1.4 — 11v11 Movement and Positioning

Completed:
- MatchState
- PlayerDefinition
- PlayerStats
- MatchRng
- deterministic simulation clock

In progress:
- positional anchors
- defensive shape

Known issues:
- fullbacks collapse too centrally
- striker closes ball too aggressively

Next recommended work:
- implement attacking support positions
- add formation-shape tests

Last verified:
All automated tests passing.
```

At the end of a substantial coding task, the AI agent should update this file.

Do not turn it into a historical diary.

Delete obsolete progress details as the project moves forward.

Git already preserves history.

---

## 4.7 `GLOSSARY.md`

Maintain definitions for important domain terminology.

Examples:

```text
MatchState
The complete authoritative state of an active football match.

Intent
An action a player or system wishes to attempt.

Resolver
A system responsible for evaluating an intent and producing an outcome.

MatchEvent
An immutable structured record describing something that occurred.

Opportunity
Future system representing a tactically significant moment where
manager intervention may become available.

Playbook
Future term for the cards/actions available to the manager during a match.
```

This reduces terminology drift between coding sessions.

---

## 4.8 Git History

Git is also part of project memory.

Commits should:

- be reasonably small,
- describe meaningful completed changes,
- avoid mixing unrelated systems.

Preferred commit style:

```text
feat(sim): add deterministic match clock

feat(pass): implement basic pass resolution

test(pass): add possession-transfer invariants

docs(sim): document intent/resolver architecture
```

The AI should not create enormous commits covering an entire milestone where avoidable.

---

## 4.9 Future Memory Expansion

Do not implement these now.

Possible later phases:

### Phase 2 — Semantic retrieval

If documentation and source code become large enough that agents repeatedly waste context discovering information, add semantic/RAG retrieval over:

- `/docs`
- source code
- tests
- ADRs

### Phase 3 — Persistent agent memory

Only if needed for long-running multi-agent workflows, investigate systems such as:

- Zep,
- Letta,
- Mem0.

Repository documentation remains authoritative even if external memory is later added.

The rule is:

> Authoritative retrieval beats unverified remembered information.

---

# 5. Hard Architectural Rules

## 5.1 Simulation must be deterministic

Given:

- identical starting state,
- identical configuration,
- identical random seed,

the simulation must produce identical results.

All randomness must come through one deterministic random-number service.

No simulation system may independently create random generators.

---

## 5.2 Simulation must run headlessly

It must be possible to run a complete simulated match without:

- loading a Godot scene,
- rendering graphics,
- creating character nodes,
- requiring input.

---

## 5.3 Presentation and simulation remain separate

Core simulation classes should not depend on:

- `Node3D`
- `CharacterBody3D`
- `AnimationPlayer`
- cameras
- UI controls
- shaders
- audio

Godot-specific presentation consumes simulation output.

---

## 5.4 Football actions use intents and resolutions

Conceptual pattern:

```text
Player AI
    ↓
Intent
    ↓
Resolver
    ↓
Result
    ↓
MatchState update
    ↓
MatchEvent
```

Example:

```text
PassIntent
    ↓
PassResolver
    ↓
PassResult
```

Future actions should follow the same general architecture where appropriate.

---

## 5.5 Avoid premature systems

Do not implement:

- cards,
- decks,
- Opportunity detection,
- fantasy species,
- injuries,
- contracts,
- transfers,
- campaign logic,
- narrative,
- detailed tactical instructions,
- 3D graphics,
- animations,
- advanced fouls,
- substitutions,
- offsides,
- set-piece systems.

These belong to later milestones.

---

# 6. Milestone Scope

Implement a simplified **11v11 association-football simulation**.

Each team contains:

- 1 goalkeeper
- 4 defenders
- 3 midfielders
- 3 forwards

Use a default **4-3-3 formation**.

Suggested positions:

```text
GK

LB
LCB
RCB
RB

LCM
CM
RCM

LW
ST
RW
```

The precise midfield interpretation may evolve later.

Use continuous logical coordinates.

Suggested pitch dimensions:

```text
Length: 105
Width: 68
```

One team attacks toward increasing X.

The other attacks toward decreasing X.

---

# 7. Match State

Create a central authoritative `MatchState`.

Conceptually:

```text
MatchState

MatchClock
Score

HomeTeam
AwayTeam

Players
Ball

PossessionTeamId
PossessionPlayerId

Phase
```

Random-number state should be owned by the simulation rather than hidden globally.

---

# 8. Player Definition vs Match State

Separate persistent player information from mutable match information.

## PlayerDefinition

Contains:

```text
Id
Name
Position
Stats
```

---

## PlayerMatchState

Contains mutable match state such as:

```text
PlayerId
TeamId

Position
MovementTarget

CurrentStamina
HasBall
CurrentIntent
```

Future systems should be addable without replacing this distinction.

---

# 9. Final Player Stat Model

Use the intended core player attributes from Milestone 1.

All stats use:

```text
1–10
```

## Technique

Represents:

- first touch,
- close control,
- dribbling execution,
- difficult technical actions.

## Passing

Represents:

- short passing,
- long passing,
- crosses,
- delivery,
- through balls.

## Finishing

Represents:

- shooting accuracy,
- goal conversion,
- attacking execution near goal.

## Defending

Represents:

- tackling,
- interceptions,
- marking execution.

## Pace

Represents:

- acceleration,
- sprint speed,
- movement speed.

## Power

Represents:

- strength,
- physical contests,
- jumping effectiveness,
- shot power.

## Stamina

Represents:

- endurance,
- fatigue resistance,
- sustained work rate capacity.

## Awareness

Represents:

- positioning,
- anticipation,
- vision,
- decision quality,
- reading space.

## Keeping

Goalkeeper-specific attribute representing:

- shot stopping,
- handling,
- goalkeeper technique.

Goalkeepers still use universal stats where relevant.

Do not introduce separate attributes such as:

```text
Vision
Crossing
Strength
Composure
Aggression
Acceleration
Tackling
```

without an explicit accepted design change.

Those concepts should emerge from combinations of the core attributes, context, traits or future cards.

---

# 10. Pitch Representation

Use continuous logical coordinates.

The simulation should know:

- boundaries,
- centre line,
- penalty areas,
- goal positions.

Detailed tactical zones are not required yet.

The architecture should make future zone classification straightforward.

Do not use a grid as the authoritative movement representation.

---

# 11. Simulation Tick

Use a fixed simulation timestep.

Suggested:

```text
0.25 simulated seconds
```

Therefore:

```text
4 ticks = 1 simulated second
```

The timestep must be configurable.

Simulation progression must not depend on rendering framerate.

---

# 12. Match Phases

Minimum required phases:

```text
Kickoff
OpenPlay
GoalRestart
Finished
```

Additional phases can be introduced later.

---

# 13. Player Behaviour

Use simple utility-based AI.

Ball-carrier candidates:

```text
Pass
Carry
Shoot
```

Off-ball candidates:

```text
MoveIntoPosition
SupportBallCarrier
MakeForwardRun
CloseDownOpponent
RecoverPosition
DefendGoalSide
```

Goalkeepers require basic positional behaviour.

Do not implement scripted sequences.

---

# 14. Utility AI

Each valid action receives a utility score.

Example:

```text
Safe pass          65
Progressive pass   78
Carry              43
Shoot               9
```

The AI should normally choose a high-utility action.

Controlled deterministic randomness among plausible options is acceptable.

The architecture must eventually support modifiers from:

- roles,
- traits,
- tactics,
- manager instructions,
- species,
- cards.

Do not implement those modifiers yet.

---

# 15. Movement

Players have:

```text
CurrentPosition
DesiredPosition
MaximumMovementSpeed
```

Move players toward desired positions based primarily on Pace and current fatigue.

Do not use collision-driven locomotion to determine tactical outcomes.

Basic avoidance/separation is optional during this milestone.

---

# 16. Basic 11v11 Positioning

The 4-3-3 should preserve recognisable football shape.

## In possession

Typical behaviour:

- goalkeeper provides deep support;
- centre-backs remain available behind the ball;
- fullbacks advance moderately;
- midfielders provide options at different depths;
- wingers create width;
- striker occupies advanced central areas.

## Out of possession

Typical behaviour:

- team recovers toward its own goal;
- defenders maintain a defensive line;
- midfield protects central areas;
- forwards provide limited pressure;
- nearest appropriate player closes the ball.

The aim is not sophisticated football.

The aim is to avoid 22 players simply chasing the ball.

---

# 17. Ball State

Suggested model:

```text
BallState

Position
PossessingPlayerId
IsLoose
```

Ball travel can initially be abstractly resolved.

Results should nevertheless contain presentation information such as:

```text
StartPosition
TargetPosition
TravelDuration
Quality
```

This will later allow the visual layer to animate the authoritative outcome.

---

# 18. Passing

Players can select teammates and attempt passes.

Resolution considers at least:

- Passing,
- Technique,
- Awareness,
- distance,
- pressure,
- receiver openness,
- interception risk.

Do not overcomplicate the initial formula.

Conceptually:

```text
PassAbility =
    Passing
    + TechniqueModifier
    + AwarenessModifier

PassDifficulty =
    DistancePenalty
    + PressurePenalty
    + InterceptionRisk
```

Resolve probabilistically through deterministic Match RNG.

---

# 19. Pass Outcomes

Support:

```text
Completed
Intercepted
Misplaced
```

Completed passes transfer possession to the receiver.

Intercepted passes transfer possession to an opponent.

Misplaced passes create an appropriate loose-ball or turnover state.

Emit structured MatchEvents.

---

# 20. Carrying

Carrying should:

- move the ball carrier,
- retain possession,
- potentially progress toward goal,
- expose the player to defenders.

Relevant stats:

- Technique
- Pace
- Awareness

Power may influence physical pressure situations.

No advanced skill-move system is required.

---

# 21. Tackling

Relevant defender stats:

- Defending
- Power
- Awareness

Relevant ball-carrier stats:

- Technique
- Power
- Awareness

Pace influences getting into challenge range.

Possible outcomes:

```text
TackleWon
TackleLost
LooseBall
```

No foul system is required yet.

---

# 22. Shooting

Relevant factors:

- Finishing
- Technique
- Power
- distance
- pressure
- goalkeeper Keeping
- goalkeeper Awareness

Possible outcomes:

```text
Goal
Saved
Missed
```

Do not implement yet:

- posts,
- rebounds,
- corners,
- detailed shot placement.

---

# 23. Goalkeepers

Goalkeepers use:

- Keeping,
- Awareness,
- Pace,
- Power,
- Passing

as appropriate.

`Keeping` is the primary shot-stopping stat.

`Awareness` influences positioning.

Basic possession and distribution after saves are sufficient.

Do not yet implement:

- crosses,
- aerial claims,
- sweeper-keeper behaviour,
- advanced distribution.

---

# 24. Stamina

Every player has match stamina.

Movement and actions reduce stamina.

Higher `Stamina` reduces fatigue rate.

Fatigue may apply small penalties to:

- Pace,
- action effectiveness,
- demanding movement utility.

Keep this simple during Milestone 1.

Architecture should allow later expansion for:

- pressing intensity,
- sprinting,
- species physiology,
- substitutions,
- recovery,
- injuries.

---

# 25. Goals and Restarts

When a goal occurs:

1. increment score;
2. emit `GoalScored`;
3. reset player positions;
4. give kickoff to the conceding team;
5. resume the match.

---

# 26. Match Clock

Simulate:

```text
90 minutes
```

No stoppage time is required.

At 90:00:

```text
MatchPhase = Finished
```

Emit `MatchFinished`.

---

# 27. Match Events

Create a structured event system.

Suggested common data:

```text
MatchEvent

Timestamp
EventType
ActorPlayerId
TargetPlayerId
TeamId
Position
Metadata
```

Required initial event types:

```text
MatchStarted
Kickoff

PassAttempted
PassCompleted
PassIntercepted
PassMisplaced

CarryStarted

TackleAttempted
TackleWon
TackleLost

ShotTaken
ShotSaved
ShotMissed

GoalScored

PossessionChanged

MatchFinished
```

Adding future event types should be straightforward.

---

# 28. Event Log

`MatchSimulation` must expose the ordered event history.

This will eventually support:

- commentary,
- statistics,
- replays,
- Opportunity detection,
- narrative triggers,
- debugging.

Do not implement these consumers yet.

---

# 29. Deterministic Randomness

Create a dedicated abstraction such as:

```text
IMatchRandom
```

Possible methods:

```text
NextDouble()
NextInt(min, max)
Roll(probability)
```

All simulation randomness must pass through this service.

A match receives a seed:

```text
seed = 123456
```

Same:

- configuration,
- teams,
- starting state,
- seed

must produce the same event sequence and result.

---

# 30. Simulation API

Target an interface conceptually similar to:

```text
var simulation =
    new MatchSimulation(config, homeTeam, awayTeam, seed);

MatchResult result = simulation.RunToCompletion();
```

`MatchResult` should contain:

```text
HomeGoals
AwayGoals
Events
FinalState
Statistics
Seed
```

Also support:

```text
simulation.Step();
```

for advancing one simulation tick.

This will eventually support visual playback.

---

# 31. Debug Runner

Provide a simple method for running one 11v11 match and viewing readable events.

Example:

```text
MATCH START

04:31 Blue #6 passes to Blue #9
04:33 Pass completed

09:14 Red #10 shoots
09:14 Saved by Blue GK

27:51 Blue #9 shoots
27:51 GOAL

FULL TIME
Blue 2–1 Red
```

Use the simplest appropriate implementation:

- console application,
- test/debug runner,
- or minimal Godot debug entry point.

---

# 32. Statistics

Track initially:

```text
Goals
Shots
ShotsOnTarget
CompletedPasses
AttemptedPasses
PossessionChanges
```

Also track possession time if straightforward.

Do not create a large analytics framework yet.

---

# 33. Automated Tests

Tests are mandatory.

## Team setup

Verify:

- exactly 11 active players per team;
- exactly one goalkeeper per team;
- 22 unique active player identities;
- valid starting positions.

## Determinism

Same teams and seed must produce equivalent:

- score,
- event order,
- important state transitions.

## Passing

Verify:

- easy passes outperform difficult ones statistically;
- successful passes transfer possession;
- interceptions transfer possession correctly.

## Tackling

Verify:

- possession changes correctly;
- events are emitted;
- two players never simultaneously possess the ball.

## Shooting

Verify:

- goals increment score exactly once;
- saves do not increment score;
- misses do not increment score;
- goal restarts occur correctly.

## Stamina

Verify:

- activity reduces stamina;
- high-Stamina players fatigue more slowly under equivalent conditions;
- stamina stays within valid bounds.

## Match completion

Verify:

- game ends correctly;
- `MatchFinished` occurs once;
- simulation cannot accidentally continue after finish.

---

# 34. State Invariants

Provide reusable invariant checking where sensible.

Important invariants:

```text
Exactly 22 active players exist.

At most one player possesses the ball.

Possessing player belongs to possession team.

Score is never negative.

Players remain within pitch boundaries.

Match clock never decreases.

Stamina remains within valid bounds.
```

Invariant failures should provide useful diagnostic information.

---

# 35. Batch Simulation

Run at least:

```text
1,000 matches
```

between roughly equal teams.

Collect:

```text
average goals per match
average shots
average shots on target
average passes
pass completion %
home win %
draw %
away win %
```

Optional:

```text
possession %
tackles
possession changes
```

No exact realism targets are required yet.

The goal is identifying obviously pathological simulation behaviour.

---

# 36. Suggested Repository Structure

```text
/game
    /src
        /Simulation
            /Core
            /Players
            /Actions
            /AI
            /Events
            /Random
            /Statistics

        /Presentation

    /tests
        /Simulation

    /docs
        GAME_DESIGN.md
        ARCHITECTURE.md
        MATCH_SIMULATION.md
        DECISIONS.md
        CURRENT_STATE.md
        GLOSSARY.md

        /milestones
            MILESTONE_1.md

    CLAUDE.md
    README.md
    project.godot
```

Exact structure may change if clearly justified and documented.

---

# 37. Code Quality

Use:

- clear domain names,
- nullable reference handling where appropriate,
- enums/value objects where beneficial,
- focused classes,
- constructor injection where useful,
- composition.

Avoid:

- global mutable state,
- large static utility classes,
- unnecessary singletons,
- giant manager classes,
- speculative abstraction,
- deep inheritance trees.

Do not overengineer hypothetical future requirements.

---

# 38. Explicit Non-Goals

Do not implement:

```text
Opportunity system
Cards
Deckbuilding
Command resource

Species bonuses
Fantasy anatomy
Flight

Player traits
Player personalities

Advanced tactical roles
Custom formations
Detailed instructions

Offside
Fouls
Cards
Injuries
Substitutions

Corners
Throw-ins
Free kicks
Penalty kicks

Weather
Crowd

Career mode
League simulation
Transfers
Narrative

3D models
Animation
Camera
Match UI

External AI memory database
Vector database
RAG system

Save system
Multiplayer
```

Document useful extension points rather than implementing later systems early.

---

# 39. AI Agent Working Procedure

Before beginning a substantial task, the agent must:

1. Read `CLAUDE.md`.
2. Read `docs/CURRENT_STATE.md`.
3. Read the relevant milestone section.
4. Read relevant architecture documentation.
5. Inspect existing code before proposing replacement architecture.

For each subtask:

```text
Inspect
↓
Plan
↓
Implement
↓
Test
↓
Review invariants
↓
Update documentation if required
↓
Update CURRENT_STATE.md
↓
Commit
```

The agent should not rewrite functioning architecture merely because it prefers a different style.

If an architectural change is necessary:

1. explain the reason;
2. update or add an ADR;
3. implement it only after the new decision is accepted.

---

# 40. Acceptance Criteria

Milestone 1 is complete when:

## Architecture

- simulation runs independently of presentation;
- simulation runs headlessly;
- no football result depends on visual physics;
- randomness is centralized and deterministic.

## Football

- two 11-player teams complete a 90-minute match;
- players maintain basic shape;
- passing works;
- carrying works;
- tackling works;
- shooting works;
- goalkeeping works;
- stamina works;
- goals and restarts work.

## Stats

The only core player attributes are:

```text
Technique
Passing
Finishing
Defending
Pace
Power
Stamina
Awareness
Keeping
```

unless a later accepted design decision changes them.

## Events

Important actions emit structured events.

Complete event history is available after the match.

## Determinism

Identical match setup and seed produce identical outcomes and event histories.

## Testing

Automated tests pass.

Invariant validation passes.

At least 1,000 automated headless matches can run successfully.

## Project memory

The repository contains usable:

```text
CLAUDE.md
GAME_DESIGN.md
ARCHITECTURE.md
MATCH_SIMULATION.md
DECISIONS.md
CURRENT_STATE.md
GLOSSARY.md
```

The documents agree with the implemented architecture.

---

# 41. Deliverables

Deliver:

1. Working Godot .NET project.
2. Headless C# 11v11 simulation.
3. Automated tests.
4. Deterministic seeded RNG.
5. MatchEvent system.
6. Debug match runner.
7. Basic match statistics.
8. 1,000-match batch simulation.
9. `CLAUDE.md`.
10. `GAME_DESIGN.md`.
11. `ARCHITECTURE.md`.
12. `MATCH_SIMULATION.md`.
13. `DECISIONS.md`.
14. `CURRENT_STATE.md`.
15. `GLOSSARY.md`.
16. README covering:
    - prerequisites,
    - building,
    - running tests,
    - running one match,
    - running batch simulations.

---

# 42. Implementation Strategy

Implement this through small, separately reviewable tasks.

## M1.1 — Project bootstrap and project memory

Create:

- Godot .NET project
- test project
- directory structure
- Git setup
- build validation
- `CLAUDE.md`
- initial `/docs`
- `CURRENT_STATE.md`
- initial ADRs
- README skeleton

No football logic beyond what is necessary to validate the project structure.

## M1.2 — Core domain model

Implement:

- MatchState
- Team
- PlayerDefinition
- PlayerStats
- PlayerMatchState
- BallState
- pitch geometry
- 11-player validation

Update architecture documentation.

## M1.3 — Deterministic simulation clock

Implement:

- MatchSimulation
- fixed timestep
- MatchRng
- deterministic test
- match phases

Record RNG architecture in documentation.

## M1.4 — 11v11 movement and positioning

Implement:

- 4-3-3 starting shape
- 22-player positioning
- movement targets
- attacking shape
- defensive shape
- goalkeeper positioning

## M1.5 — Passing

Implement:

- PassIntent
- PassResolver
- teammate selection
- interception logic
- possession transfer
- events
- tests

## M1.6 — Carrying and tackling

Implement:

- CarryIntent
- TackleIntent
- resolutions
- possession changes
- events
- tests

## M1.7 — Shooting and goalkeeper

Implement:

- ShotIntent
- ShotResolver
- goalkeeper contest
- goals
- saves
- misses
- restarts
- tests

## M1.8 — Stamina

Implement:

- stamina consumption
- Stamina attribute effect
- simple fatigue penalties
- tests

## M1.9 — Utility AI

Implement:

- action candidate generation
- utility scoring
- selection
- basic off-ball decisions

## M1.10 — Full match

Implement:

- 90-minute matches
- MatchResult
- statistics
- complete event history

## M1.11 — Batch simulation

Implement:

- 1,000-match runner
- aggregate statistics
- pathological-behaviour checks

## M1.12 — Documentation and architecture review

Before declaring M1 complete:

- review `ARCHITECTURE.md`;
- review `MATCH_SIMULATION.md`;
- clean obsolete `CURRENT_STATE.md` entries;
- verify ADRs;
- update glossary;
- ensure README instructions work;
- run complete test suite;
- run deterministic regression test;
- run 1,000-match simulation.

---

# 43. Definition of Success

Success does **not** mean:

> This already looks like the finished football game.

Success means we have a clean technical foundation where:

> Twenty-two players maintain basic football structure, move, make decisions, pass, carry, tackle, shoot, save, fatigue, score and generate a deterministic structured event history.

The project must also possess enough durable documentation that a fresh AI coding session can understand:

- what the project is;
- what has been built;
- why key architectural decisions were made;
- what is currently being worked on;
- what must not be implemented yet.

The repository—not conversational memory—is the project's source of truth.

Future milestones can then add tactical intelligence, Opportunities, cards, fantasy species and 2.5D presentation without destabilising this foundation.
