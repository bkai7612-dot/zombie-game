# Authority model and security limitations

## What the host decides (clients only *request*)

| Decision | Where | Validation |
|---|---|---|
| Damage, headshots, kills | `PlayerWeapons.FireRpc`, `ZombieAgent.ServerTakeDamage` | The host re-traces every shot with its own physics (rewinding zombie hitboxes for latency). Damage comes from the host's weapon table. Clients never send damage or hit results. |
| Fire rate and ammo | `PlayerWeapons` + `FireRateLimiter` | The host's own `Loadout` decides whether a shot is allowed. A token bucket (a burst of 3, then the weapon's rate) rejects rapid-fire hacks. An empty magazine can't fire. The muzzle position must be within 2.5 m of the host's view of the player's eye. |
| Reloads | `PlayerWeapons.ReloadRpc` | Timed on the host, using the host's perk state. |
| Points | `PlayerAvatar.ServerAddPoints/ServerSpend` | Only the host writes points. Hit rewards count once per zombie per shot. |
| Purchases and prices | `PlayerInteractor.ServerTryInteract` → `MatchState.ServerHandleInteract` | The client sends an interactable **id** and nothing else. The host checks the phase, that the player is alive, a request cooldown, the distance (against its own copy of the map and its view of the player) and the price (`PurchaseRules`, re-evaluated on the host), and spends points before granting anything. Doors already opened, perks already owned and full ammo are refused. |
| Doors | `MatchState.doorMask` | Written only by the host and replicated, so late joiners get the current state. |
| Revives | `PlayerAvatar.ServerReviveHold` | Range and timing are checked on the host. Progress resets if the hold stops. |
| Rounds, spawns, zombie AI | `RoundDirector`, `ZombieAgent` | Host only. |
| Escape | `MatchState.TickEscape` | Funding, countdown and boarding are all decided on the host. |
| Reconnects | `ReconnectLedger` | Snapshots are consumed exactly once. Duplicate keys get a unique suffix. |
| Connections | `ConnectionController.Approve` | Checks the version, a 4-player cap, that the match hasn't ended, and that single-player games stay closed. The payload holds only a name, a character and a key, and it's sanitised. |

## Known limitations (not hidden)

- **Player-hosted sessions aren't secure against a cheating host.** The host's machine runs the authoritative simulation and can change anything. That's inherent to client-hosted co-op.
- **Movement is owner-authoritative.** Players move their own character for responsiveness, and the host only logs implausible speed (`PlayerMotor.ServerPlausibility`). It doesn't correct it. Teleport or speed hacks are possible for clients.
- **Aim direction is trusted.** Aimbots can't be detected.
- There's **no commercial anti-cheat** and no account bans. Traffic protection is only whatever the Relay transport provides.
- Anonymous UGS sign-in only. There are no persistent accounts or progression to protect.

For competitive or monetised features you'd need dedicated servers (for example Unity Multiplay or another host) and server-authoritative movement.
