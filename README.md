# Untitled Zombie Survival

An original first-person, round-based zombie survival game for Windows. It is built with Unity 6 LTS and Netcode for GameObjects, and you can play it solo or in 2–4 player online co-op.

> **Status: pre-release source code. It has not been opened in Unity or built yet.**
> Unity couldn't be installed in the environment where this code was written, because Unity's download servers were blocked. All code is type-checked against the real UnityEngine/UnityEditor reference assemblies, and the rules engine has 37 passing unit tests. But nobody has run the game, built a Windows `.exe` or tested multiplayer yet.
> [docs/STATUS.md](docs/STATUS.md) lists exactly what has and hasn't been verified, and what to do next.

## What's in it

| Area | Implementation |
|---|---|
| Modes | Offline single-player (local-only host, with pause). Online co-op: **Find Match** quick-joins a random public game or creates one, **Create Private Game** gives you a join code, and **Join by code** uses it. All online play goes through Unity Relay. LAN / direct IP is also available. |
| Authority | The host is authoritative for damage, headshots, points, prices, purchases, ammo, reloads, doors, revives, rounds, zombie spawning and the escape. Clients only send requests. See [docs/SECURITY.md](docs/SECURITY.md). |
| Map | **Hollow Pines** has five zones: Main Street (where you start), Back Alley, Army Checkpoint, Research Lab and the concealed underground Hangar 51-B. Five purchasable doors connect them. |
| Weapons | Six types: M9 pistol (starter), K-9 SMG, Ranch pump shotgun, AR-7 rifle, DMR-40 marksman rifle, and the XR-1 plasma rifle (Supply Drop only). There's also a knife. All six can be upgraded at the Xeno-Forge. |
| Economy | Points come from hits and kills. You spend them on wall buys, ammo, the 4 perk machines, ammo crates, the Supply Drop (a mystery box that relocates), upgrades, doors and the escape. |
| Survivors | 10 original characters with names, roles and bios. Their models are placeholders built from primitives. |
| Ending | Players pool points at the saucer launch console. Once it's funded, a 60-second hold-out starts, then everyone standing in the boarding circle escapes. If nobody funds it, the game is endless. |
| Co-op | Players go down instead of dying, and teammates can revive them by holding Interact. Downed players bleed out after 30 seconds. Dead players respawn when the round ends. Reconnecting restores your progress once, so it can't be used to duplicate anything. |
| Presentation | Night lighting with moonlight, streetlights, emergency lights and alien lighting. All sound effects are generated in code. Menus, lobby, HUD, pause, settings (audio, mouse, FOV, resolution, window mode, V-Sync, FPS cap and key rebinding) and end screens are all included. |

All art and audio are original placeholders created in code. No third-party assets are used.

## Repository layout

```
Game/                         Unity project (open this folder in Unity Hub)
  Assets/_Project/Scripts/
    Core/        UZS.Core      engine-independent rules (weapons, rounds, economy, map, payloads)
    Gameplay/    UZS.Gameplay  networking, map builder, players, zombies, audio, settings
    OnlineUgs/   (Assembly-CSharp) Unity Gaming Services session adapter
    UI/          UZS.UI        IMGUI menus / lobby / HUD
    Editor/      UZS.Editor    one-click project setup + Windows build script
  Assets/_Project/Tests/EditMode   NUnit tests (also run outside Unity)
tools/verify/                 Unity-free verification (dotnet): unit tests + compile checks
docs/                         status, design, multiplayer setup, testing, security, release
```

## Getting started (developer)

1. Install **Unity Hub** and **Unity 6 LTS (6000.0.x)** with the **Windows Build Support (Mono)** module. The project pins `6000.0.23f1`, but any newer 6000.0 patch should work.
2. In Unity Hub choose **Add > Add project from disk**, pick the `Game/` folder, and open it.
3. The first time it opens, **Tools > UZS > Set Up Project** runs automatically. It generates the materials, network prefabs, the `Main` scene and the player settings. If Unity asks whether to enable an input backend, keep the **Input Manager (Old)**.
4. Open `Assets/_Project/Generated/Scenes/Main.unity` and press **Play**, then choose **Single Player**.
5. Commit the generated `Assets/_Project/Generated` folder and the `.meta` files. All builds must share the same prefab GUIDs or they won't be able to play together online.

Online play needs a Unity Cloud project with Relay and Lobby turned on. See [docs/MULTIPLAYER_SETUP.md](docs/MULTIPLAYER_SETUP.md).

## Building the downloadable game

Use **Tools > UZS > Build Windows Release**. It produces:

```
Game/Builds/Windows/UntitledZombieSurvival.exe (+ _Data folder, README.txt)
Game/Builds/UntitledZombieSurvival-Windows-x64-v0.1.0.zip   <- upload this to the website
Game/Builds/UntitledZombieSurvival-Windows-x64-v0.1.0.zip.sha256
```

Players download the ZIP, extract it and run the `.exe`. They don't need to install Unity or anything else. You can also build from the command line or CI; see [docs/RELEASE.md](docs/RELEASE.md).

## Controls

WASD move · Mouse look · LMB fire · RMB aim · R reload · F interact/buy/revive (hold to revive) · V or MMB knife · Q or scroll swap weapon · 1/2 weapon slots · Space jump · Shift sprint · Ctrl/C crouch · Tab scoreboard · Esc menu. All keys can be rebound in Settings.

## Verification without Unity

```
./tools/verify/run.sh      # needs .NET 8 SDK; runs on Linux/macOS/Windows (Git Bash)
```

This runs the rule tests and type-checks every assembly. The same thing runs in CI (`.github/workflows/verify.yml`).

## Renaming the game

The display title, company and executable name are in `Game/Assets/_Project/Scripts/Core/GameInfo.cs`. Re-run **Set Up Project** after changing them so the player settings update.
