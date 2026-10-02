# Glossary

**MatchState** — The complete authoritative state of an active football match.

**TeamSide** — `Home` or `Away`; identifies a team within a match. Home attacks toward increasing X.

**PlayingPosition** — One of the 11 4-3-3 slots (GK, LB, LCB, RCB, RB, LCM, CM, RCM, LW, ST, RW). Grouped into Goalkeeper / Defender / Midfielder / Forward.

**TeamDefinition** — A validated starting eleven with a name.

**PlayerDefinition** — Persistent player data: identity, name, position, stats. Does not change during a match.

**PlayerMatchState** — Mutable per-match player data: position, movement target, stamina (and later current intent). Possession is not stored here.

**BallState** — Ball position and possessor. The only stored record of possession (ADR-007).

**Possessor** — The player currently holding the ball; none when the ball is loose.

**Intent** — An action a player or system wishes to attempt (e.g. `PassIntent`).

**Resolver** — A system responsible for evaluating an intent and producing an outcome (e.g. `PassResolver`).

**Result** — The outcome produced by a resolver, including data the presentation layer needs to animate it.

**MatchEvent** — An immutable structured record describing something that occurred.

**Tick** — One fixed simulation step (default 0.25 simulated seconds).

**MatchRng / IMatchRandom** — The single seeded random source for a match. All simulation randomness goes through it.

**Invariant** — A condition that must always hold for a valid `MatchState` (e.g. at most one player possesses the ball).

**Utility score** — A number rating how desirable a candidate action is for a player; the AI picks among high-scoring candidates.

**Opportunity** — Future system representing a tactically significant moment where manager intervention may become available.

**Playbook** — Future term for the cards/actions available to the manager during a match.
