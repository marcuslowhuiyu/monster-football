# Glossary

**MatchState** — The complete authoritative state of an active football match.

**TeamSide** — `Home` or `Away`; identifies a team within a match. Home attacks toward increasing X.

**PlayingPosition** — One of the 11 4-3-3 slots (GK, LB, LCB, RCB, RB, LCM, CM, RCM, LW, ST, RW). Grouped into Goalkeeper / Defender / Midfielder / Forward.

**TeamDefinition** — A validated starting eleven with a name.

**PlayerDefinition** — Persistent player data: identity, name, position, stats. Does not change during a match.

**PlayerMatchState** — Mutable per-match player data: position, movement target and intensity, stamina, distance covered, current intent, active carry, off-ball plan. Possession is not stored here.

**Team frame** — Coordinates relative to a team: own goal at X = 0, attacking +X, left flank +Y. Shape rules are written in team frame.

**Anchor** — A slot's reference position in a layout (kickoff, in possession, out of possession) before shifting with the ball.

**Defensive line** — The single X shared by the back four out of possession; always goal-side of the ball.

**Presser** — The outfield player of a team without the ball with the lowest fatigue-weighted distance to it, sent at sprint intensity to jockey goal-side of the carrier (or to a loose ball). The CloseDownOpponent behaviour.

**MovementIntensity** — `Easy` (jog that slows over the last 12 m), `Jog` (60 % of top speed, slows over the last 4 m), `Sprint` (100 %), `Carry` (0.55 + 0.03 × Technique).

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

**ActionIntent** — Base type for intents (`PassIntent`, `CarryIntent`, `TackleIntent`); `PlayerMatchState.CurrentIntent` holds an ongoing one.

**Carry** — Running with the ball along a planned segment (≤ 10 m); the carrier keeps possession.

**Challenge range** — Distance (1.8 m) within which a defender may attempt a tackle.

**Contact / clean** — The two tackle rolls: did the defender get to the ball, and did they keep it.

**xG (GoalProbability)** — A shot's estimated chance of scoring: on-target probability × (1 − save probability).

**Out of position** — Distance between the goalkeeper and their ideal spot on the goal–ball line; lowers save chance.

**GoalRestart** — Phase after a goal: players reset to the kickoff layout before the conceding side kicks off.

**Tiredness** — 1 − stamina/100: 0 when fresh, 1 when empty. All fatigue penalties are linear in it.

**Drain multiplier** — How fast a player's stamina falls, from their Stamina attribute (1.24 at 1, 0.7 at 10).

**Effectiveness factor** — Fatigue multiplier on action ratings (down to 0.85 at empty).

**Match fingerprint** — Canonical text of every event plus full-precision final state; equal fingerprints mean bit-identical matches. Hashed by `DebugRunner fingerprint` and compared across OSes in CI.

**Utility** — A score for how good a candidate action is right now; higher is better.

**ActionCandidate** — One option for a player (kind, utility, and the intent or movement target to carry it out).

**Plausible options** — Candidates within 8 utility of the best; the only ones the selector may choose.

**OffBallPlan** — An off-ball behaviour a player has committed to for a short time (support, run, mark, recover, hold position).

**Jockeying** — The presser holding a spot 1.5 m goal-side of the carrier instead of standing on the ball.

**AiTuning** — All AI utility weights as data, on `MatchConfig.Ai`.

**MatchStatistics** — Per-team goals, shots, shots on target, passes, possessions won and possession time, derived from the event log.

**Possession cause** — Why a player has the ball, recorded on `PossessionChanged`: won in open play (interception, tackle, loose ball, save, reception) or awarded by a restart (kickoff, keeper restart). Only open-play causes count as possessions won.

**Possession time** — Seconds credited to the team last in control of the ball, including while its pass is in flight or the ball is loose.

**Shape focus** — The smoothed ball position a team's shape is built around (3 s average when attacking, 0.75 s when defending).

**Easy** — Movement intensity for the team in possession holding position: a jog that slows over the last 12 m to the target.

**Arrival** — Slowing in proportion to the remaining distance when close to a movement target.

**Batch** — Many headless matches aggregated into a `BatchSummary`.

**Health band** — A wide min–max range a batch figure must fall within; failing one flags pathological behaviour.

**Opportunity** — A tactically significant open-play situation recognised by tactical analysis (M2: six types); an episode with a start and an end. Future: where manager intervention may become available.

*Milestone 2 terms (specified, not yet implemented):*

**Ball in play / play stopped** — The ball is either live (open play) or dead (a stoppage) until a restart.

**Stoppage** — Play stopped, with its reason, restart type, awarded team, restart location and any offending/fouled player and disciplinary outcome.

**Restart** — How play resumes: kickoff, throw-in, goal kick, corner, direct free kick, indirect free kick, penalty.

**Ball-in-play time** — Time the ball was live; the match clock keeps running through stoppages.

**Added time (stoppage time)** — Minutes added to a period for time lost to goals, cards, penalties and dismissals; the period then ends at the next stoppage. Not to be confused with *extra time* (30-minute knockout periods, out of scope).

**Last touch** — The player who last played the ball; decides throw-in, goal kick or corner when it goes out.

**Offside position** — In the opponents' half, ahead of the ball and of the second-last opponent when a teammate plays the ball; an offence only on becoming involved.

**Second-last opponent** — The offside line: normally the last outfield defender, the keeper being last.

**DOGSO** — Denying an obvious goal-scoring opportunity; a foul that usually means a red card.

**Dismissal** — A player sent off (straight red or second yellow); removed from active play for the rest of the match.

**MatchFrame** — Immutable copy of observable match state at the end of a tick; what analysis and the viewer read.

**Tactical zone** — One of 20 team-frame regions: a longitudinal band (Defensive, OwnMidfield, OppositionMidfield, Attacking) × a vertical channel (LeftWide, LeftHalfSpace, Centre, RightHalfSpace, RightWide).

**Tactical snapshot** — Immutable derived interpretation of one frame: zones, lines, pressure, numbers, lanes, play phase.

**Playbook** — Future term for the cards/actions available to the manager during a match.
