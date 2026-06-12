# Fixed Core Roguelike Battle Design

## Positioning

The battle is a fixed-core defense roguelike with automatic combat and many units on screen.

- The player core is fixed near the bottom of a vertical screen and has HP. The run fails when core HP reaches zero.
- The player directly controls one hero unit. The hero can move and automatically attacks while idle or when a target is valid.
- Other player units are gained during the run and placed manually.
- Placeable units do not move after placement. Each unit type defines a valid distance band from the core, creating outer frontline units and inner damage/support units.
- Monsters spawn from the upper edge/fan-shaped battlefield and advance toward the core.

This is not intended to be a pure tower defense. The player-controlled hero is the active rescue and pressure-management tool, while placed units form the defensive line.

## Unit Rings

Placed player units form conceptual rings around the core:

- Outer ring: tanks and melee blockers.
- Middle ring: melee damage, short-ranged damage, utility units.
- Inner ring: ranged damage, supports, core protection.

The core-distance band controls where a unit can be placed. A unit may be dragged within its allowed band at placement time, but it does not move automatically afterward.

## Targeting And Interception

Melee monster breakthrough is controlled by interception rules, not by physical pushing.

- Every player unit, including the controlled hero, can have a melee interception capacity.
- Melee monsters can only select a player unit as a melee target while that unit has free interception capacity.
- When a unit reaches its interception capacity, additional melee monsters ignore it and continue searching for a valid target closer to the core.
- If no valid player unit can intercept a melee monster, the monster advances to and attacks the core.
- Ranged attacks do not consume interception capacity and are not limited by interception capacity.

This keeps frontline pressure readable: tanks hold a finite number of melee enemies, then the excess pressure breaks through to inner units or the core.

## Pushing

Pushing is a lightweight spatial presentation rule. It is not the source of interception or breakthrough logic.

- Units have a push radius that is separate from combat collision radius.
- Pushing keeps large crowds from perfectly overlapping.
- Pushing is same-camp spatial separation. It does not depend on whether a unit moved during the current logic tick.
- Units from different camps never push each other and receive no displacement from this system.
- Same-camp units can push each other when the pushing unit is allowed to push others and the receiving unit can be pushed.
- Static units can be displaced by same-camp pushing. Enemy movement does not displace them.
- Overlaps that cannot legally push, such as different-camp overlaps or Endure-protected units, should be prevented by placement, spawning, or AI movement rules.
- Pushing includes a small deterministic sideways slide, so head-on overlaps can gradually separate instead of only pushing straight back.
- A unit can be configured to push others while not being pushed itself. This is the default future behavior for fixed placed units.
- Units with Endure are temporarily not pushable and do not actively avoid, so protected actions do not drift or get visually interrupted. They still block other moving units.
- Skill casting grants Endure for `castPreMs + castBackMs`, or until the cast ends or is interrupted. `castBackMs` only controls this protection window and does not change the full cast duration, which remains driven by the action animation timing.
- Hit lock does not directly affect pushing. If a hit reaction should resist pushing, it must grant Endure.
- Pushing should use simple position offsets, not Rigidbody2D or physics simulation.

## Death And Revival

Player-controlled and placed player units are expected to revive after death.

- Player hero death: respawn near the core after a short delay.
- Placed unit death: leave a marker at its placement slot, then revive in place after a cooldown.
- Revival timing, invulnerability windows, and slot behavior are future implementation details.

## Out Of Scope For First Push Implementation

The first implementation only adds soft pushing and records this design.

Not included yet:

- Core HP and failure.
- Unit placement and core-distance bands.
- Interception capacity and melee target capacity accounting.
- Monster core-progress targeting.
- Player hero input.
- Unit death/revival flow.
- Fan-shaped battlefield constraints.
