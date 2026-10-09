# Laws of the Game (as simulated)

The simplified laws the simulation implements. Grows through M2.4–M2.7 (offside, fouls, free kicks, penalties,
discipline, reduced teams); only implemented laws are listed. Details and numbers: `MATCH_SIMULATION.md`.

## Match flow (implemented, M2.1)

- Two halves of 45 minutes. The referee's clock runs through stoppages; ball-in-play time is measured separately.
- Coin toss for the opening kickoff; the other team kicks off the second half. A goal is restarted by a kickoff for
  the team that conceded.
- Added time: 45 s per goal in regular time, rounded up to whole minutes and announced when regular time ends.
- **A half ends only when play stops** after regular plus added time — a ball in flight is never cut off. If play
  never stops, the half ends at an interim safety cap (60 s past added time).
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

Touches: gaining possession, striking a pass or shot, a tackle that knocks the ball loose, a goalkeeper's parry and a
defender's deflection. Ways the ball goes out: misplaced passes (in flight or rolling on after landing), tackles that
knock it out, missed shots (a goal kick, or a corner if deflected by a defender in the lane), and saves parried
behind (a corner).

## Restarts (implemented, M2.1 kickoffs; M2.4 the rest)

| Restart | Taker | Law respected before it is taken | Minimum dead ball |
|---|---|---|---|
| Kickoff | striker | everyone in position: own half, opponents outside the centre circle | 45 s after a goal |
| Throw-in | nearest full-back or winger | opponents ≥ 2 m | 8 s |
| Goal kick | goalkeeper | opponents outside the penalty area | 15 s |
| Corner | best passer (not a centre-back or the striker) | opponents ≥ 9.15 m | 20 s |

- During a stoppage nobody decides, tackles or collects the ball, and nothing random happens. Players walk (jog) to
  restart positions, which still costs stamina.
- If the restart is not ready 30 s after the dead-ball time, the taker and any offending opponents are placed.
- Execution reuses passing: the taker passes at once to the best option (a throw within 25 m; a corner to an attacker
  in the box, who may then shoot as in open play). Throw-ins use the ordinary pass model.
- Routine restarts add no added time.

## Not yet simulated

Offside (M2.5); fouls, free kicks and penalties (M2.6); cards, dismissals and reduced teams (M2.7); advantage (not in
M2, D7); headers, set-piece routines, short corners, long throws.
