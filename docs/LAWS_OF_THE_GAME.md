# Laws of the Game (as simulated)

The simplified laws the simulation implements; only implemented laws are listed. Details and numbers: `MATCH_SIMULATION.md`.

## Match flow (implemented, M2.1)

- Two halves of 45 minutes. The referee's clock runs through stoppages; ball-in-play time is measured separately.
- Coin toss for the opening kickoff; the other team kicks off the second half. A goal is restarted by a kickoff for
  the team that conceded.
- Added time for what happens in regular time — 45 s per goal, 45 s per penalty, 20 s per card, 30 s per dismissal,
  plus time wasted by a leading side slowing its restarts down from minute 60 (ADR-037) — rounded up to whole minutes
  and announced when regular time ends. Time wasted during added time is not added again. Not modelled: substitutions,
  injuries and cautions for time-wasting.
- **A half ends only when play stops** after regular plus added time — a ball in flight is never cut off. If play
  never stops, the half ends at a safety cap 3 minutes past added time (about one half in ten until clearances, D6).
- Simplification: teams do not change ends at half-time (D1).

## Ball out of play (implemented, M2.4)

The ball is out when its path **wholly crosses** a touchline or goal line (a goal is decided by the shot model).
Paths are straight segments — flights, short knocks after a tackle, a parry, and the roll of a misplaced pass — and
the crossing point is where the segment meets the line. Whoever **last touched** the ball decides the restart:

| Crossed | Last touched by | Restart | Where |
|---|---|---|---|
| Touchline | either team | Throw-in to the other team | where it crossed |
| Goal line | attacking team | Goal kick to the defending team | corner of the goal area on that side |
| Goal line | defending team | Corner to the attacking team | the corner on that side |

Touches: gaining possession, striking a pass, shot, clearance or cross, a headed clearance, a tackle that knocks the ball loose, a goalkeeper's parry and a
defender's deflection. Ways the ball goes out: misplaced passes and clearances (in flight or rolling on after landing), tackles that
knock it out, missed shots (a goal kick, or a corner if deflected by a defender in the lane), and saves parried
behind (a corner).

## Offside (implemented, M2.5)

- **Offside position**, judged **when a teammate passes**: in the opponents' half (the halfway line is not), and
  nearer the opponents' goal line than both the ball and the **second-last opponent** (the goalkeeper counts).
  Level is onside.
- **Offence**: only when that player becomes involved — the first to receive or collect the ball from that pass,
  including a misplaced pass. Any other touch first (an opponent, an onside teammate, a parry) resets it.
- **No offside** directly from a throw-in, goal kick or corner. Free kicks are judged like any pass.
- **Restart**: indirect free kick to the defending team where the offender was.
- The referee is perfect. Players are not: a passer may misjudge a marginal offside by up to 1.5 m at Awareness 1
  (0 at Awareness 10), and eager runners drift beyond the line while waiting for the pass.
- Simplifications: no interfering with an opponent or passive offside; shots are not judged (a rebound off the
  keeper is a fresh touch).

## Fouls (implemented, M2.6)

- Only **tackles** can be fouls (no handball, off-ball fouls or diving; D9). Whether a challenge is a foul depends
  on the challenge and the players: missing the ball, coming from behind, sprinting in and tiredness make it
  likelier; a better Defending rating makes it less likely; defenders are much more careful in their own area.
- A foul stops play at once (no advantage, D7): a **direct free kick** where it happened, or a **penalty** if it
  happened inside the fouling team's own penalty area.
- Each foul has a severity (Power, from behind, pace, missing the ball), which decides how likely a card is.

## Cards (implemented, M2.7)

The referee judges every foul (Law 12), checking in this order:

| Offence | When | Card |
|---|---|---|
| Denying an obvious goal-scoring opportunity | the fouled player was within 25 m of goal with no outfield defender (other than the offender) goal-side | red; **inside the area** yellow if the challenge was for the ball, red from behind (D10) |
| Serious foul play | a very severe challenge (severity above 0.7), more likely the worse it is | red |
| Stopping a promising attack | the fouled player was in the final third with at most one outfield defender goal-side | yellow |
| Reckless challenge | any other foul, more likely the more severe | yellow |

- A second yellow is a red. Booked players tackle more carefully, and referees think twice before a second caution
  for a reckless foul.
- A sent-off player leaves the pitch for the rest of the match (no substitutes). The team re-forms: ten players play 4-4-1, nine
  4-3-1, eight 4-2-1, seven 4-1-1. If the goalkeeper is sent off, the outfielder with the best Keeping goes in goal.
- A team reduced to **fewer than seven players** abandons the match (D12); the score stands and the result is
  recorded as abandoned.

## Restarts (implemented, M2.1 kickoffs; M2.4–M2.6 the rest)

| Restart | Taker | Law respected before it is taken | Average dead ball |
|---|---|---|---|
| Kickoff | striker | everyone in position: own half, opponents outside the centre circle | 60 s after a goal |
| Throw-in | nearest full-back or winger | opponents ≥ 2 m | 15 s |
| Goal kick | goalkeeper | opponents outside the penalty area | 25 s |
| Corner | best passer (not a centre-back or the striker) | opponents ≥ 9.15 m | 35 s |
| Indirect free kick (offside) | nearest defender | opponents ≥ 9.15 m | 20 s |
| Direct free kick | best finisher within 32 m of goal, otherwise nearest defender or midfielder | opponents ≥ 9.15 m; a four-man wall within 32 m | 30 s |
| Penalty | best finisher | keeper on the line; everyone else outside the area and the arc | 90 s |

- During a stoppage nobody decides, tackles or collects the ball, and nothing random happens. Players walk (jog) to
  restart positions, which still costs stamina.
- Each stoppage's dead-ball time is drawn around its average (× 0.65–1.35): longer for a leading side and shorter for
  a trailing one from minute 60, longer for a free kick (× 1.3) or throw-in (× 1.2) in the attacking third, + 30 s for
  a booking and + 45 s for a sending-off (ADR-037; provisional values). Kickoffs that start a half wait for nothing.
- If the restart is not ready 30 s after the dead-ball time, the taker and any offending opponents are placed.
- Execution reuses passing: the taker passes at once to the best option (a throw within 25 m; a free kick to a teammate judged onside). A corner is crossed into the box, where attackers
  and defenders contest it in the air (a first-time shot, a headed clearance, the keeper's catch); no offside from it. An indirect free kick
  cannot be scored directly. A direct free kick within 32 m is shot over the wall when worth it (xG ≥ 0.04).
  A penalty is a dedicated shot from the spot (≈ 74 % scored between equal players); a period waits for it, even past the safety cap.
  Throw-ins use the ordinary pass model.
- Routine restarts add no added time, except the time a leading side wastes over them.

## Not yet simulated

Advantage (not in M2, D7); handball, off-ball fouls and dissent (D9); substitutions; headers, set-piece routines,
short corners, long throws.
