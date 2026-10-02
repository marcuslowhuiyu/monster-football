# Game Design

Current high-level design truth. Speculative future ideas are deliberately excluded.

## Concept

A stylised 2.5D fantasy football management/deckbuilding game.

> The squad is the deck.

## Autonomous matches

Football matches run automatically, driven by a deterministic simulation. The manager does not control players directly.

## Opportunities (future)

At tactically important moments, time slows and the manager can play cards representing tactical interventions — e.g. through balls, overlaps, counterattacks, pressing instructions, defensive responses, player-specific signature actions.

Opportunities and cards are **not** part of Milestone 1. They will operate on top of the match simulation.

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
