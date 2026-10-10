# Building and releasing the Windows game

## Output

You end up with one portable ZIP. Players extract it and run the `.exe`. They don't need Unity, .NET or an installer. Unity players need their `_Data` folder next to the `.exe`, so the archive has to be **extracted**, not run directly from inside the ZIP.

```
UntitledZombieSurvival-Windows-x64-v0.1.0.zip
  UntitledZombieSurvival.exe
  UntitledZombieSurvival_Data/
  MonoBleedingEdge/
  UnityPlayer.dll, UnityCrashHandler64.exe
  README.txt
```

## Option A: in the editor (Windows PC)

1. Install Unity 6 LTS with **Windows Build Support (Mono)**.
2. Open `Game/`, wait for the import and for **Set Up Project** to finish.
3. Go to **Tools > UZS > Build Windows Release**.
4. Collect `Game/Builds/UntitledZombieSurvival-Windows-x64-v0.1.0.zip` and its `.sha256` file.

## Option B: command line

```
"C:\Program Files\Unity\Hub\Editor\6000.0.xx\Editor\Unity.exe" -batchmode -quit -projectPath Game ^
  -executeMethod UZS.EditorTools.BuildScript.BuildWindowsFromCommandLine -logFile build.log
```

The exit code is non-zero on failure.

## Option C: GitHub Actions (`.github/workflows/unity-build.yml`)

1. Add the repository secrets `UNITY_EMAIL` and `UNITY_PASSWORD`, plus either `UNITY_LICENSE` (Personal: the contents of your `.ulf` file, see game.ci's "activation" docs) or `UNITY_SERIAL` (Pro/Plus).
2. Run the workflow manually (Actions > Unity tests + Windows build > Run workflow), or push a tag like `v0.1.0`.
3. It runs the EditMode and PlayMode tests (including the end-to-end smoke test), builds Windows x64, uploads the ZIP as an artifact, and on tags attaches it to a GitHub Release.

> Network prefab hashes come from asset GUIDs. Commit `Game/Assets/_Project/Generated/` after the first setup so editor, local and CI builds can all play together. Players always share one release ZIP, so this only matters for testing.

## Publishing on a website

- Upload the ZIP and its `.sha256`, and show the size, version, minimum OS (Windows 10/11 x64) and the SmartScreen note.
- **Code signing:** unsigned games trigger Windows SmartScreen ("More info > Run anyway"). An OV/EV code-signing certificate and `signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256 UntitledZombieSurvival.exe` before zipping removes that friction.
- Bump `GameInfo.ProtocolVersion` for every release that changes networking or rules. Old and new clients will then refuse to connect to each other instead of desyncing.
- Keep the Unity Cloud project (Lobby/Relay) active for the online mode.

## Pre-release checklist

- [ ] `./tools/verify/run.sh` passes
- [ ] Unity EditMode and PlayMode tests pass
- [ ] `-uzssmoketest` passes on the built `.exe`
- [ ] The multiplayer acceptance table in `docs/TESTING.md` is fully ticked, including the two-network test
- [ ] Clean-machine launch on Windows 10 and Windows 11
- [ ] `docs/STATUS.md` is updated with real results
