# Project status and verification record

_Last updated: 2026-10-10. Protocol/game version 0.1.0._

This file separates what has been **verified** from what is only **implemented in code**. Nothing in this file has been tested in a running game yet.

## Environment the code was written in

| Check | Result |
|---|---|
| Unity Editor installed | **No.** Downloads from `download.unity3d.com` and `packages.unity.com` were blocked by the sandbox network policy (HTTP 403 at the proxy). |
| Unity licence / batch-mode builds | Not possible without the editor. |
| Windows build support / running a `.exe` | Not possible (Linux container, no editor). |
| Unity docs (docs.unity.com / docs.unity3d.com) | Not reachable directly. UGS API shapes were confirmed through web search snippets of Unity's API reference. |
| GitHub source of Netcode for GameObjects | **Reachable.** Tag `ngo/2.4.4` was cloned and used as the reference for every NGO API call. |
| NuGet | Reachable. UnityEngine module reference assemblies (2021.3.33) and UnityEditor.dll (2021.1) were used for type-checking. |

## Verified

| What | How | Result |
|---|---|---|
| Core rules: weapons, loadout/ammo, prices, doors, zone graph, rounds, mystery box, escape funding, payload sanitising, approval rules, reconnect ledger, fire-rate limiter | `dotnet test` (NUnit). The same test file runs in Unity's Test Runner. | **46 / 46 passing** |
| Every script assembly (Gameplay, UI, Assembly-CSharp/UGS, Editor) compiles | `tools/verify/run.sh`: C# 9, warnings treated as errors, built against real UnityEngine and UnityEditor reference assemblies | **Compiles cleanly** |
| NGO API usage matches NGO 2.4.4 | The stubs in `tools/verify/UnityCompileCheck/Stubs/NetcodeStubs.cs` were copied from the 2.4.4 source | Compiles against them |

### Limits of that verification

- The UnityEngine references are from **2021.3**, but the target is **Unity 6**. Every API the code uses exists in both, and obsolete Unity 6 calls like `FindObjectOfType` were avoided. A real Unity 6 compile is still needed.
- The UGS stubs (`UgsStubs.cs`) follow Unity's published API reference for Multiplayer Services 1.x, but they **could not be checked against the package source**. If a signature differs, only `OnlineUgs/UgsSessionService.cs` is affected.
- Netcode's IL post-processor (RPC and NetworkVariable code generation) only runs inside Unity. The code follows its rules: RPC names end in `Rpc`, parameters are serializable types (strings in RPCs are supported, NGO's own test project uses them), and NetworkVariables are initialised at declaration.
- The package versions in `Packages/manifest.json` (NGO 2.4.4, Multiplayer Services 1.1.0, Test Framework 1.4.5) were chosen from known releases but haven't been resolved by Unity's package manager here.

## Implemented but not yet run (needs Unity)

| Requirement | Code | Status |
|---|---|---|
| Single-player (offline, pausable) | `ConnectionController.StartSinglePlayer`, `UIRoot.SetPaused` | Not run |
| Online co-op 2–4 via Relay, Find Match (quick-join or create), private game + join code, cancel/retry states | `UgsSessionService`, `UIRoot.DrawOnline` | Not run; also needs a Unity Cloud project |
| LAN / direct IP host and join (`-uzshost`, `-uzsjoin`) | `ConnectionController` | Not run |
| Host-authoritative damage, points, purchases, doors, ammo, revives, rounds, escape | `PlayerWeapons`, `PlayerInteractor`, `MatchState`, `RoundDirector`, `PlayerAvatar` | Rules unit-tested; networking not run |
| Map with 5 zones, 5 doors, 4 wall buys, 4 perks, 2 ammo crates, upgrade bench, Supply Drop at 3 locations, UFO hangar | `MapCatalog` (tested) + `MapBuilder` (not run) | Layout is data-tested. **Geometry hasn't been seen**, so expect visual tweaks. |
| Zombie AI (NavMesh, runtime bake, door carving), round scaling, endless mode | `ZombieAgent`, `NavMeshBaker`, `RoundRules` | Not run |
| 10 original survivors (placeholder models) | `CharacterCatalog`, `CharacterModel` | Not run |
| UFO escape purchase, countdown and boarding | `MatchState.TickEscape`, `UfoView` | Not run |
| Reconnect without duplicating progress | `ReconnectLedger` (tested) + `PlayerAvatar` | Ledger tested; flow not run |
| Lag compensation for hitscan | `LagCompensation` | Not run |
| Settings (audio, mouse, FOV, resolution, window mode, V-Sync, FPS cap, rebinding) | `GameSettings`, `UIRoot.Menus` | Not run |
| Automated end-to-end smoke test (`-uzssmoketest`, Tools > UZS > Run Smoke Test, PlayMode test `SinglePlayerLoopWorks`) | `SmokeTest.cs`, `Tests/PlayMode` | Written and compile-checked, **never executed** |
| Windows build + ZIP + SHA-256 | `BuildScript` | Not run |

## Known limitations (by design for this version)

- **Host migration isn't supported.** If the host leaves, the session ends cleanly for everyone and they see a message saying so.
- Players move on their own machines (owner-authoritative movement), so the host can only log implausible speed. See `docs/SECURITY.md`.
- Character choices can be duplicated (two players can pick the same survivor).
- Matchmaking doesn't filter by game version. A client running a different version gets rejected when it tries to connect, with a clear message.
- All art and audio are placeholders made in code. Lighting is real-time, not baked.
- The UI uses Unity's IMGUI, which is functional but not final-quality.

## Playtest (veteran-gamer agent, code trace + simulation)

A playtester agent (`.claude/agents/veteran-gamer-playtester.md`) reviewed the game by tracing play sessions through the code and simulating balance with `tools/playtest/Sim` against the real rules. It found 1 blocker, 7 major, 4 minor and 4 polish issues. **All of them have been fixed** in code, and the rule-level fixes have unit tests. None of the fixes have been seen in a running game yet.

| Finding | Fix |
|---|---|
| Escape could lock forever if a player left while it was part-funded | Cost uses the peak player count (never drops); the countdown starts on any tick where funding covers the cost (`EscapeState`) |
| Quit, fund cheaply, rejoin exploit | Same peak-player cost |
| First player into the circle ended the match for everyone | 15 s boarding window, or lift-off when all living players are aboard; per-player "Escaped" / "Left behind" |
| Hold-out zombies spawned on the street above the hangar | `SpawnPlanner` ranks spawns by door-graph hops, then distance |
| 600-point crate refilled upgraded weapons | Crate charges per-slot wall prices; upgraded refill is 2× normal (min 1000) |
| Late joiners were dead weight | Catch-up points (500 + 750 per round, max 10,000), spawn beside a living teammate |
| Hard difficulty wall / endless unplayable | Gentler health growth after round 25 (cap about 12.5k HP), power-up drops |
| Zombies hit through walls; elevated "god spots"; knife through walls | Line-of-sight and vertical-reach checks for zombie attacks; line-of-sight check for the knife; unreachable-target recycling |
| Shotgun was a trap pick | 3.5° spread (was 6.5°), 26 damage per pellet |
| Supply Drop moved behind locked doors | Relocates only into opened zones; plasma weight doubled |
| Fast reconnect started fresh | New connection takes over the key, the stale one is disconnected and its progress handed over |
| Respawn inside props | NavMesh and free-space search (`SpawnSpot`) |
| Dirt apron cut through the ramp and launch shaft | Apron split around the hangar footprint |
| UI cut off below 16:10, overlapping text, wrong swap key label, rounds-survived off by one | Widths clamp to the screen, feed moved, swap key read from settings, defeat counts completed rounds |
| Aiming didn't help accuracy; zombies popped into view; downed players helpless | ADS halves spread; spawns skip visible points within 25 m and zombies climb out of the ground; last stand (shoot and reload while downed) |

## Your original prompt was cut off

The prompt ended mid-sentence at the start of the "Opening..." part of the door section. Everything after that, including the details for weapons, characters, the UFO ending, the release website, and so on, was filled in with reasonable choices recorded in `docs/DESIGN.md`. Please send the rest of the prompt so the design can be checked against it.

## Next steps (in order)

1. Open `Game/` in Unity 6 LTS. Fix any compile or package-resolution errors (expected to be small and limited to package versions or UGS signatures).
2. Press Play and choose Single Player. Walk the map and fix geometry and lighting problems.
3. Run **Tools > UZS > Run Smoke Test** (or the PlayMode test), then run it in a build with `UntitledZombieSurvival.exe -uzssmoketest -batchmode -nographics`.
4. Run two instances on one PC: `-uzsprofile A -uzshost` and `-uzsprofile B -uzsjoin 127.0.0.1`. Then go through the acceptance checklist in `docs/TESTING.md`.
5. Link a Unity Cloud project, then test Find Match and private codes across two PCs on **different networks**.
6. Build the Windows release ZIP and test it on a clean Windows 10 and Windows 11 machine.
