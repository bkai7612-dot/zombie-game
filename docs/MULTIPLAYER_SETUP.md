# Multiplayer setup

## Architecture

```
UI (UZS.UI) ──> ISessionService (UZS.Gameplay) ──> UgsSessionService (Assembly-CSharp)
                                                     ├─ Unity Authentication (anonymous)
                                                     └─ Multiplayer Services sessions (Lobby + Relay)
                                                          └─ starts Netcode host/client on NetworkManager
ConnectionController (UZS.Gameplay): approval, payload, spawning the match, disconnect handling
```

- **Topology:** host-authoritative ("client-hosted"). No dedicated server.
- **Transport:** Unity Transport. Relay is used for online games, and direct UDP for LAN/IP play.
- **Single player** runs a host bound to `127.0.0.1` that rejects every remote connection, so it shares 100% of the gameplay code with co-op and works offline.
- Every service call is behind `ISessionService`, so you can replace the backend (Steam, EOS and so on) without touching gameplay or UI.

## One-time Unity Cloud setup (needed for "Play Online")

1. Sign in to Unity Hub and open the project.
2. **Edit > Project Settings > Services**: link the project to a Unity Cloud project (create one if needed).
3. In the Unity Cloud dashboard (cloud.unity.com), open that project and turn on **Multiplayer > Lobby** and **Multiplayer > Relay**. Both have free tiers. Check current pricing and limits before you launch.
4. Nothing else needs to be configured in code. The package reads the project id from the linked project.

If services can't be reached, the Online screen shows the error and single player still works.

## Finding and joining games

| Button | What happens |
|---|---|
| Find Match | Signs in anonymously, quick-joins a public session that has a free slot (searching for about 6 s), or creates a new public session if none is found. Status shows searching / joining / connected / failed, and Cancel is available. |
| Create Private Game | Creates a private session that doesn't appear in quick-join. The lobby screen shows the join code. |
| Join (code) | Joins a session by its code. |
| Leave / End game | Leaves the session. If the host leaves, the session ends for everyone and they're told why. |

Clients running a different `GameInfo.ProtocolVersion` are rejected with a message. Full sessions (4 players) are rejected too, and so are sessions whose match has already ended.

## Testing on one PC

Each running copy needs its own identity, so pass a profile:

```
UntitledZombieSurvival.exe -uzsprofile A -uzshost            (LAN host on port 7777)
UntitledZombieSurvival.exe -uzsprofile B -uzsjoin 127.0.0.1  (joins it)
```

For online tests on one PC, start both copies with different `-uzsprofile` values and use Create Private Game plus the join code. Without `-uzsprofile`, each launch gets a fresh anonymous identity anyway.

In the editor you can use **Window > Multiplayer > Multiplayer Play Mode** (an optional package) or a second build.

## Testing across the internet

Use two PCs on **different networks** (for example, one on a phone hotspot) and go through `docs/TESTING.md` > "Multiplayer acceptance". A localhost test alone doesn't prove internet connectivity.

## Reconnecting

If a player disconnects during a match, the host keeps a snapshot of their points, weapons, perks and stats, keyed by their saved player key. When they reconnect, the snapshot is restored once, then deleted, so nothing can be duplicated. A downed player who disconnects comes back dead (they can't use reconnecting to skip bleeding out).
