# Glossary

**MatchState** — The complete authoritative state of an active football match.

**TeamSide** — `Home` or `Away`; identifies a team within a match. Home attacks toward increasing X.

**PlayingPosition** — One of the 11 4-3-3 slots (GK, LB, LCB, RCB, RB, LCM, CM, RCM, LW, ST, RW). Grouped into Goalkeeper / Defender / Midfielder / Forward.

**TeamDefinition** — A validated starting eleven with a name.

**PlayerDefinition** — Persistent player data: identity, name, position, stats. Does not change during a match.

**PlayerMatchState** — Mutable per-match player data: position, movement target, stamina (and later current intent). Possession is not stored here.

**Team frame** — Coordinates relative to a team: own goal at X = 0, attacking +X, left flank +Y. Shape rules are written in team frame.

**Anchor** — A slot's reference position in a layout (kickoff, in possession, out of possession) before shifting with the ball.

**Defensive line** — The single X shared by the back four out of possession; always goal-side of the ball.

**Presser** — The nearest outfield player of a team without the ball, sent to close it down at sprint intensity.

**MovementIntensity** — `Jog` (60 % of top speed) or `Sprint` (100 %).

**BallState** — Ball position and possessor. The only stored record of possession (ADR-007).

**Possessor** — The player currently holding the ball; none when the ball is loose.

**Intent** — An action a player or system wishes to attempt (e.g. `PassIntent`).

**Resolver** — A system responsible for evaluating an intent and producing an outcome (e.g. `PassResolver`).

**Result** — The outcome produced by a resolver, including data the presentation layer needs to animate it.

**MatchEvent** — An immutable structured record describing something that occurred.

**Tick** — One fixed simulation step (default 0.25 simulated seconds).

**MatchConfig** — Match-wide settings: pitch, tick length, match duration.

**MatchClock** — Counts ticks; the authoritative measure of elapsed match time.

**MatchTime** — A simulated timestamp in seconds, displayed as MM:SS.

**MatchSimulation** — Runs one match: owns the `MatchState`, the random source and the event log; advanced with `Step()` or `RunToCompletion()`.

**MatchResult** — The outcome of a completed match: goals, events, final state, seed.

**KickoffTeam** — The side that takes the next kickoff; decided by coin toss at the start, later the conceding side after a goal.

**MatchRng / IMatchRandom** — The single seeded random source for a match. All simulation randomness goes through it.

**Invariant** — A condition that must always hold for a valid `MatchState` (e.g. at most one player possesses the ball).

**Utility score** — A number rating how desirable a candidate action is for a player; the AI picks among high-scoring candidates.

**PassEstimate** — RNG-free assessment of a pass: ability, accuracy probability, interception threats, overall success probability.

**Interception threat** — An opponent close enough to the pass lane to cut it out, with a probability and position along the lane.

**BallFlight** — A pass in the air: its resolved result plus launch and arrival ticks.

**Loose ball** — Nobody possesses the ball and it is not in flight; the nearest player within 1.5 m collects it.

**AttackingTeam** — The possessor's team, or the passing team while a pass is in flight.

**MatchContext** — What a system needs to act: state, config, random source, event output; owns possession transfer.

**Opportunity** — Future system representing a tactically significant moment where manager intervention may become available.

**Playbook** — Future term for the cards/actions available to the manager during a match.
