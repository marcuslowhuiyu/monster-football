# Game Design

Current high-level design truth. Speculative future ideas are deliberately excluded.

## Concept

A stylised 2.5D fantasy football management/deckbuilding game.

> The squad is the deck.

## Autonomous matches

Football matches run automatically, driven by a deterministic simulation. The manager does not control players directly.

## Match flow and laws (Milestone 2)

Matches follow simplified association-football laws: the ball goes out of play and is restarted (throw-ins, goal kicks, corners), offside is called, tackles can be fouls leading to free kicks and penalties, and players can be cautioned and sent off — a dismissed player leaves the match and the team plays on with fewer. The match clock runs through stoppages; each half gets added time and ends at the next stoppage once time is up.

Set pieces are automated and simple. No new player attributes: fouls, cards and set pieces come from the nine core stats plus match context.

Stoppages give the match a natural rhythm of moments; set pieces are a likely future source of manager intervention.

## Opportunities (future)

At tactically important moments, time slows and the manager can play cards representing tactical interventions — e.g. through balls, overlaps, counterattacks, pressing instructions, defensive responses, player-specific signature actions.

Cards are not part of Milestones 1–2. Milestone 2 builds the tactical understanding that recognises Opportunities in open play (six initial types: Transition, Wide Isolation, Through Ball Lane, Overload, Final Third Entry, Defensive Emergency); Opportunities only exist while the ball is in play and never affect the football.

## Squad as deck (future)

The cards available to the manager (the *Playbook*) derive from the players in the squad.

## Player stat model

Compact model, all stats 1–10 (ADR-002):

| Stat | Represents |
|---|---|
| Technique | First touch, close control, dribbling, difficult technical actions |
| Passing | Short/long passing, crosses, delivery, through balls |
| Finishing | Shooting accuracy, conversion, attacking execution near goal |
| Defending | Tackling, interceptions, marking |
| Pace | Acceleration, sprint and movement speed |
| Power | Strength, physical contests, jumping, shot power |
| Stamina | Endurance, fatigue resistance, work-rate capacity |
| Awareness | Positioning, anticipation, vision, decision quality |
| Keeping | Goalkeeper shot stopping, handling, technique |

## Simulation vs presentation

The simulation decides what happens; the presentation shows it. The visual layer may stylise and animate but never changes an outcome (ADR-001).
