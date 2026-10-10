# Testing

## 1. Without Unity (any machine with the .NET 8 SDK)

```
./tools/verify/run.sh
```

- `tools/verify/CoreTests`: NUnit tests for every rule in `Scripts/Core` (prices, doors, rounds, ammo, box, escape, approval, reconnect, fire-rate limiter).
- `tools/verify/UnityCompileCheck`: type-checks each script assembly against UnityEngine/UnityEditor reference assemblies from NuGet, plus signature stubs for NGO 2.4.4 and UGS. **This proves the code compiles, not that the game works.**

The same thing runs in CI on every push (`.github/workflows/verify.yml`).

## 2. In the Unity editor

| Test | How |
|---|---|
| EditMode tests | Window > General > Test Runner > EditMode > Run All (the same Core tests) |
| PlayMode smoke test | Test Runner > PlayMode > `SinglePlayerLoopWorks`, or **Tools > UZS > Run Smoke Test (Play Mode)** |
| Manual play | Open `Main.unity`, press Play, choose Single Player |

The smoke test (`SmokeTest.cs`) goes through the real authority code paths. It starts the single-player host, builds the map, starts the match and waits for zombies to spawn. Then it checks the headshot kill reward, makes sure a door is refused out of range and accepted in range, charges once and can't be bought twice, buys a wall weapon, rolls and takes from the Supply Drop, funds the escape, and confirms that going down solo ends in defeat.

## 3. In a Windows build

```
UntitledZombieSurvival.exe -uzssmoketest -batchmode -nographics
echo %ERRORLEVEL%        (0 = pass)
type "%USERPROFILE%\AppData\LocalLow\UZS Dev\Untitled Zombie Survival\smoketest.log"
```

## 4. Multiplayer acceptance (must pass before release)

Run with two separate application instances. Do it once on one PC (`-uzsprofile A/B`) and once on **two PCs on different networks** using Play Online.

| # | Check | Pass? |
|---|---|---|
| 1 | Both players end up in the same lobby (Find Match, and also by private code) | |
| 2 | Both pick survivors, the host starts, and both see the same map | |
| 3 | Each sees the other move, aim, fire (tracers and sound) and reload | |
| 4 | Both damage and kill the same zombies, with no ghost zombies | |
| 5 | Points match on both screens after hits, kills and purchases | |
| 6 | A door opened by one player opens for both, zombies path through it, and a late joiner sees it open | |
| 7 | Both buy wall weapons and use them, and ammo counts stay correct | |
| 8 | One player goes down and the other revives them (hold F for 3 s) | |
| 9 | A round completes and the next starts with more zombies | |
| 10 | The escape is funded together (contributions from both add up), the countdown runs, both stand in the circle and both escape | |
| 11 | A client quits mid-round: the host continues and the client rejoins with the same points (once) | |
| 12 | The host quits: the client gets the "session ended" message and returns to the menu | |
| 13 | A client with a different version is refused with the version message | |
| 14 | A 5th player is refused with "session is full" | |

## 5. Release checks

- Fresh Windows 10 and Windows 11 machines with no Unity installed: extract the ZIP and run the game.
- 1920×1080 and 2560×1440 resolutions, windowed / borderless / exclusive full screen.
- Changes to volume, sensitivity and keys are still there after a restart.
- At least a 30-minute session with no errors in `Player.log`.
