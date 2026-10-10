# Game design (v0.1)

All numbers live in `Game/Assets/_Project/Scripts/Core/` and are covered by unit tests. Change them there.

## Core loop

You start on Main Street with 500 points and a pistol. Zombies come in rounds. Hitting one earns 10 points (once per zombie per shot, so shotgun pellets can't multiply it), a kill earns 60, a headshot kill earns 100 and a knife kill earns 130. Spend points to open doors, buy weapons and ammo, perks, upgrades and Supply Drop rolls, and finally to fund the escape saucer. If nobody funds it, the rounds never end.

## Rounds (`RoundRules`)

| | Formula |
|---|---|
| Zombies per round | `ceil((4 + 2.2r + 0.09r²) × (1 + 0.5 × (players − 1)))`, capped at 300 |
| Zombie health | 120 + 90 per round up to round 9, then ×1.1 per round to round 25, then ×1.04 per round, frozen after round 55 (about 12,500 HP, so endless mode stays killable) |
| Max alive | 18 + 4 per extra player (cap 30) |
| Spawn interval | 2.2 s × 0.95^(r−1), minimum 0.35 s |
| Speed mix | walkers only for the first rounds; runners from round 3; sprinters from round 7 (up to 70%) |
| Zombie hit | 40 damage (50 from round 15); 0.55 s wind-up, 1.3 s cooldown. Needs a clear line to the player (no hits through walls or fences) and the player within 1.9 m above or 0.8 m below the zombie's feet (car roofs aren't safe). |
| Intermission | 10 s (5 s before round 1) |

Zombies only spawn in zones the team has opened. Spawn points are ranked by the door graph, not straight-line distance: first the zone a player is in, then zones one door away, closest first. So the hangar under Main Street never feeds street rounds, and the escape hold-out is fed from the hangar and lab. Points closer than 10 m to anyone are skipped, and so are points within 25 m that a player can see. New zombies climb out of the ground for 1.2 s before they move. A zombie that's stuck for 14 s while far from every player, or that can't path to its target for 10 s, is recycled.

### Power-ups

Each kill has a 3% chance to drop one (at most 4 per round, one on the ground at a time, gone after 25 s). Walk over it to collect it for the whole team:

| Drop | Effect |
|---|---|
| Max Ammo | Refills every weapon of every standing or downed player |
| Insta-Kill | Every hit kills, for 20 s |
| Double Points | Hit and kill points ×2, for 20 s |
| Nuke | Kills every zombie on the map (no kill credit, no drops) and gives each player 400 points |

## Weapons (`WeaponCatalog`)

| Weapon | Type | Damage | Rate | Mag/Reserve | Wall price | Upgraded name |
|---|---|---|---|---|---|---|
| M9 Service Pistol | semi | 34 | 6/s | 8/64 | starter | Moonlight Judge |
| K-9 Compact SMG | auto | 26 | 13/s | 30/180 | 1000 (Main Street) | Hive Swarm |
| Ranch Pump Shotgun | semi, 8 pellets (3.5° spread) | 26 ×8 | 1.4/s | 6/48 | 1200 (Back Alley) | Barn Burner |
| AR-7 Patrol Rifle | auto | 45 | 9.5/s | 30/210 | 1600 (Checkpoint) | Checkpoint Zero |
| DMR-40 Marksman | semi | 150 | 2.5/s | 10/60 | 2000 (Research Lab) | Long Night |
| XR-1 Plasma Rifle | auto | 120 | 6/s | 24/144 | Supply Drop only | Starbreaker |

- Damage falls off with distance, and headshots get a per-weapon multiplier (1.5× to 3×).
- Buying a weapon you already own refills its ammo for half the wall price (minimum 250). Upgraded weapons cost 2× their normal refill (minimum 1000). The pistol refills for 250 and the plasma rifle for 1500.
- **Ammo crates** refill every weapon at once and charge the same per-slot prices as the walls (minimum 600). They're mainly for the pistol and the plasma rifle, which have no wall.
- Aiming down sights halves weapon spread.
- **Xeno-Forge** (upgrade bench, 5000): doubles damage, gives 1.5× magazine and reserve, and adds a purple tint.
- **Knife**: 150 damage, 0.65 s cooldown, always available.
- You carry two weapons. Buying a third replaces the one in your hands.

## Perks (`PerkCatalog`)

| Perk machine | Price | Effect | Location |
|---|---|---|---|
| Iron Hide Tonic | 2500 | Max health goes from 100 to 250 | Checkpoint |
| Quickhands Soda | 3000 | Reloads are twice as fast | Back Alley |
| Trigger Happy Cola | 2000 | Fire rate +33% | Research Lab |
| Fleetfoot Fizz | 2000 | Move speed +15% | Hangar |

You lose all perks when you go down.

## Map: Hollow Pines (`MapCatalog`, `MapBuilder`)

```
                 N (z+)
   +-------------------------+--------------------+
   |  Back Alley (zone 1)    |                    |
   |  House B [Shotgun]      |  Army Checkpoint   |
   |  Garage [Quickhands]    |  (zone 2)          |
   |  Ammo crate         D2 1250 [AR-7] [Iron Hide]|
   +---- D0 750 -------------+  Supply Drop #2    |
   |  Main Street (zone 0)  D1 1000  Ammo crate   |
   |  House A [Supply Drop #1]                    |
   |  Corner Store [SMG]     +------ D3 1500 -----+
   |  start: road            |  Research Lab (3)  |
   +-------------------------+  [DMR] [Trigger]   |
          (underground)      D4 2500 [Xeno-Forge] |
   Hangar 51-B (zone 4) <-ramp-  Supply Drop #3   |
   [Saucer + Launch Console] [Fleetfoot]         |
```

- The start zone already has a weapon to buy (the SMG) and a Supply Drop, so nobody has to open a door to get going.
- The two routes into the checkpoint (D1 from the street, D2 from the alley) make loops for kiting zombies.
- The hangar is underground under the back yards and is reached through the lab by a ramp. Its roof hatch slides open when the escape starts.
- **Doors**: each has a unique id, a price sign on both sides and a red lock light that turns green when bought. The host validates the purchase and range. The door slides into the floor, the open state is synced and stays open for the run, and the zombie navmesh is updated.
- **Lighting**: moonlight with soft shadows, warm streetlights, cool floodlights at the checkpoint, red emergency lights at the lab and ramp, green alien lights in the hangar, and a dedicated light on every purchasable thing.

## Supply Drop (mystery box)

It costs 950. It spins for 3 s, then offers a weapon you don't already own to the player who paid, for 10 s. The plasma rifle has half the weight of the other weapons. After at least 4 uses since its last move, each roll has a 20% chance of relocation instead: the player is refunded and the crate moves to another location **in a zone the team has already opened**. If no other location is open, it never relocates.

## Escape ending

At the **Saucer Launch Console** in the hangar, players contribute points (all they have, up to what's still needed). It costs 10,000 for one player, plus 5,000 for each extra player. The cost is based on the **most players who have been in the match**, so it never drops when someone leaves. That means funding can't get stuck, and nobody can quit, fund cheaply and rejoin.

Once fully funded, a 60 s hold-out countdown starts while the hatch opens and the saucer powers up. Then boarding opens. The saucer lifts off as soon as every living player is inside the green circle, or 15 s after the first player boards. Everyone inside escapes; anyone outside is shown as "Left behind", and their end screen says so.

## Co-op rules

- At 0 health you go **down**: you crawl and you bleed out after 30 s, but you can still shoot and reload (last stand). A teammate revives you by holding F for 3 s within 2.4 m, and you come back with 50 health.
- If you bleed out you're **dead** and spectate (click to switch who you watch). You respawn at the end of the round with a pistol and keep your points.
- If nobody is left standing, the game is over. In single player, going down ends the game.
- Health regenerates 25 per second after 4 s without taking damage.

## Survivors

Dana Reyes (deputy), Marcus Hale (corporal), Dr. Ingrid Vos (xenobiologist), Tommy Burke (mechanic), Priya Nair (radio operator), Walt Hendricks (retired hunter), Sgt. Ama Owusu (combat medic), Eli Park (delivery driver), Rosa Delgado (construction foreman) and Specimen K-7 (escaped test subject). Each one has their own colours, headgear and build. The models are placeholders built from primitives.

## Assumptions made because the brief was cut off

The brief stopped at "Opening" in the door section. These were chosen as reasonable defaults:

- Door opening animation and timing.
- The exact weapon stats and the 4 perks.
- Escape cost scales with player count, uses a 60 s hold-out, and requires standing in the boarding circle.
- No host migration.

## Late joiners and reconnects

- A player who joins a match in progress appears next to a living teammate (on a free, walkable spot). They start with catch-up points: 500 + 750 per round already played, capped at 10,000.
- Respawns at the end of a round use the same safe-spot search, so nobody lands inside a car or wall.
- If you reconnect before the host notices your old connection dropped, the old connection is closed and its progress is handed to the new one.

## Balance (from `tools/playtest/Sim`, 60% hits, 30% headshots)

| Milestone | Solo | 4 players |
|---|---|---|
| SMG | round 2 | round 2 |
| Upgraded AR | round 8 | round 10 |
| All doors and perks | round 10 | round 12 |
| Escape funded | round 13 (~16 min) | round 14 (~28 min) |
| Horde outgrows best gear (no power-ups) | round 15 | round 25 |

Power-ups aren't included in those numbers. They make late rounds noticeably easier.
