using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Replicated match state plus the authority's purchase/door/box/escape logic. Spawned by the host
    /// once the server starts. Every client builds the map locally when this object spawns.
    /// </summary>
    public sealed class MatchState : NetworkBehaviour
    {
        public static MatchState Instance { get; private set; }

        public static event Action<string, bool> Announcement;

        private readonly NetworkVariable<MatchPhase> phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        private readonly NetworkVariable<int> round = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> zombiesLeft = new NetworkVariable<int>(0);
        private readonly NetworkVariable<float> intermissionLeft = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<ulong> doorMask = new NetworkVariable<ulong>(0UL);
        private readonly NetworkVariable<int> boxLocation = new NetworkVariable<int>(0);
        private readonly NetworkVariable<BoxState> boxState = new NetworkVariable<BoxState>(BoxState.Idle);
        private readonly NetworkVariable<WeaponId> boxOfferWeapon = new NetworkVariable<WeaponId>(WeaponId.None);
        private readonly NetworkVariable<ulong> boxOfferOwner = new NetworkVariable<ulong>(ulong.MaxValue);
        private readonly NetworkVariable<int> escapeFunded = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> escapeCost = new NetworkVariable<int>(EscapeRules.CostFor(1));
        private readonly NetworkVariable<bool> escapeActive = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<float> escapeSecondsLeft = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<bool> singlePlayer = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<float> matchSeconds = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<float> boardingSecondsLeft = new NetworkVariable<float>(-1f);
        private readonly NetworkVariable<PowerUpType> dropType = new NetworkVariable<PowerUpType>(PowerUpType.None);
        private readonly NetworkVariable<Vector3> dropPosition = new NetworkVariable<Vector3>(Vector3.zero);
        private readonly NetworkVariable<float> instaKillLeft = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<float> doublePointsLeft = new NetworkVariable<float>(0f);

        public MatchPhase Phase => phase.Value;
        public int Round => round.Value;
        public int ZombiesLeft => zombiesLeft.Value;
        public float IntermissionLeft => intermissionLeft.Value;
        public ulong DoorMask => doorMask.Value;
        public int BoxLocation => boxLocation.Value;
        public BoxState BoxState => boxState.Value;
        public WeaponId BoxOfferWeapon => boxOfferWeapon.Value;
        public ulong BoxOfferOwner => boxOfferOwner.Value;
        public int EscapeFunded => escapeFunded.Value;
        public int EscapeCost => escapeCost.Value;
        public bool EscapeActive => escapeActive.Value;
        public float EscapeSecondsLeft => escapeSecondsLeft.Value;
        public bool IsSinglePlayer => singlePlayer.Value;
        public float MatchSeconds => matchSeconds.Value;
        public bool IsOver => Phase == MatchPhase.Victory || Phase == MatchPhase.Defeat;
        /// <summary>Seconds left for teammates to board after the first survivor got in, or -1 if boarding hasn't started.</summary>
        public float BoardingSecondsLeft => boardingSecondsLeft.Value;
        public PowerUpType DropType => dropType.Value;
        public Vector3 DropPosition => dropPosition.Value;
        public float InstaKillLeft => instaKillLeft.Value;
        public float DoublePointsLeft => doublePointsLeft.Value;
        public bool InstaKillActive => instaKillLeft.Value > 0f;
        public int PointsMultiplier => doublePointsLeft.Value > 0f ? 2 : 1;

        public RoundDirector Director { get; private set; }

        // Authority-only state.
        private readonly System.Random rng = new System.Random();
        private float boxTimer;
        private int boxUses;
        private PlayerAvatar boxRoller;
        private float escapeTimer;
        private float statTimer;
        private readonly EscapeState escape = new EscapeState();
        private float boardingElapsed = -1f;
        private int dropsThisRound;
        private float dropLifetime;
        private float instaTimer;
        private float doubleTimer;
        private float powerSyncTimer;

        private void Awake()
        {
            Director = GetComponent<RoundDirector>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            var map = MapBuilder.Build();
            if (IsServer)
            {
                NavMeshBaker.Bake(map);
                singlePlayer.Value = ConnectionController.Instance != null && ConnectionController.Instance.IsSinglePlayer;
                boxLocation.Value = 0;
            }
            doorMask.OnValueChanged += OnDoorsChanged;
            ApplyDoors(doorMask.Value, true);
            if (AudioManager.Instance != null) AudioManager.Instance.SetInMatch(true);
        }

        public override void OnNetworkDespawn()
        {
            doorMask.OnValueChanged -= OnDoorsChanged;
            if (Instance == this) Instance = null;
            MapBuilder.Destroy();
            NavMeshBaker.Clear();
            if (AudioManager.Instance != null) AudioManager.Instance.SetInMatch(false);
        }

        private void OnDoorsChanged(ulong previous, ulong current) => ApplyDoors(current, false);

        private static void ApplyDoors(ulong mask, bool instant)
        {
            var map = MapBuilder.Current;
            if (map == null) return;
            foreach (var d in map.Doors)
                if ((mask & (1UL << d.DoorId)) != 0) d.SetOpen(true, instant);
        }

        private void Update()
        {
            var map = MapBuilder.Current;
            if (map != null)
            {
                if (map.Box != null) map.Box.Sync(this);
                if (map.Ufo != null) map.Ufo.Sync(this);
                if (map.PowerUp != null) map.PowerUp.Sync(this);
            }
            if (IsServer) ServerTick(Time.deltaTime);
        }

        // ------------------------------------------------------------------ lobby / flow

        /// <summary>Host presses Start in the lobby.</summary>
        [Rpc(SendTo.Server)]
        public void StartMatchRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            ServerStartMatch();
        }

        public void ServerStartMatch()
        {
            if (!IsServer || Phase != MatchPhase.Lobby) return;
            phase.Value = MatchPhase.Playing;
            escape.NotePlayers(PlayerAvatar.All.Count);
            SyncEscape();
            int i = 0;
            foreach (var p in PlayerAvatar.All) p.ServerBeginMatch(i++);
            Director.ServerBegin();
            AnnounceRpc("Survive. Find a way out of Hollow Pines.", true);
        }

        /// <summary>Called by a late joiner's avatar so they can play immediately.</summary>
        public void ServerOnPlayerJoined(PlayerAvatar p)
        {
            // Cost follows the peak player count: it can rise for a bigger team but never drops when people leave.
            escape.NotePlayers(PlayerAvatar.All.Count);
            SyncEscape();
        }

        /// <summary>Called after a departing avatar has been removed from <see cref="PlayerAvatar.All"/>.</summary>
        public void ServerOnPlayerLeft()
        {
            if (boxRoller != null && !boxRoller.IsSpawned) boxRoller = null;
        }

        private void SyncEscape()
        {
            escapeFunded.Value = escape.Funded;
            escapeCost.Value = escape.Cost;
        }

        public void ServerSetRound(int value, int remaining, float intermission)
        {
            if (value != round.Value) dropsThisRound = 0;
            round.Value = value;
            zombiesLeft.Value = remaining;
            intermissionLeft.Value = intermission;
        }

        private void ServerTick(float dt)
        {
            if (Phase != MatchPhase.Playing) return;

            statTimer += dt;
            if (statTimer >= 1f)
            {
                matchSeconds.Value += statTimer;
                statTimer = 0f;
            }

            TickBox(dt);
            TickEscape(dt);
            TickPowerUps(dt);
            CheckDefeat();
        }

        private void CheckDefeat()
        {
            if (PlayerAvatar.All.Count == 0) return;
            foreach (var p in PlayerAvatar.All)
                if (p.Life == LifeState.Alive || p.Life == LifeState.Escaped) return;
            ServerEnd(MatchPhase.Defeat);
        }

        public void ServerEnd(MatchPhase result)
        {
            if (Phase != MatchPhase.Playing) return;
            phase.Value = result;
            AnnounceRpc(result == MatchPhase.Victory ? "The saucer breaks through the clouds. You escaped Hollow Pines." : "Everyone is down. Hollow Pines claims another squad.", true);
        }

        // ------------------------------------------------------------------ interactions (authority)

        /// <summary>Validated purchase handler. Returns a message for the requesting player.</summary>
        public bool ServerHandleInteract(PlayerAvatar player, Interactable target, out string message)
        {
            message = string.Empty;
            if (!IsServer || Phase != MatchPhase.Playing || !target.IsActive(this)) return false;

            var quote = target.Quote(this, player);
            if (!quote.Available)
            {
                message = quote.Reason;
                return false;
            }
            if (!quote.Affordable(player.Points))
            {
                message = "Not enough points";
                return false;
            }

            switch (quote.Kind)
            {
                case QuoteKind.OpenDoor:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    doorMask.Value |= 1UL << target.Param;
                    AnnounceRpc($"{player.DisplayName} opened the {target.Title(this)}", false);
                    break;

                case QuoteKind.NewWeapon:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    player.Weapons.ServerGive((WeaponId)target.Param, false);
                    break;

                case QuoteKind.Ammo:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    player.Weapons.ServerRefill((WeaponId)target.Param);
                    break;

                case QuoteKind.Perk:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    player.ServerAddPerk((PerkId)target.Param);
                    break;

                case QuoteKind.AmmoCrate:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    player.Weapons.ServerRefillAll();
                    break;

                case QuoteKind.Upgrade:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    if (!player.Weapons.ServerUpgradeActive())
                    {
                        player.ServerAddPoints(quote.Cost);
                        return false;
                    }
                    break;

                case QuoteKind.BoxRoll:
                    if (!player.ServerSpend(quote.Cost)) return false;
                    boxRoller = player;
                    boxState.Value = BoxState.Rolling;
                    boxTimer = MysteryBoxRules.RollSeconds;
                    break;

                case QuoteKind.BoxTake:
                    player.Weapons.ServerGive(boxOfferWeapon.Value, false);
                    ResetBox();
                    break;

                case QuoteKind.Contribute:
                {
                    int give = escape.Contribute(player.Points);
                    if (give <= 0 || !player.ServerSpend(give)) return false;
                    SyncEscape();
                    if (!escape.FullyFunded)
                        AnnounceRpc($"{player.DisplayName} fed {give} points into the saucer ({escape.Funded}/{escape.Cost})", false);
                    break; // TickEscape starts the countdown as soon as funding covers the cost.
                }

                default:
                    return false;
            }

            message = "OK";
            return true;
        }

        private void TickBox(float dt)
        {
            if (boxState.Value == BoxState.Idle) return;
            boxTimer -= dt;

            if (boxState.Value == BoxState.Rolling && boxTimer <= 0f)
            {
                if (boxRoller == null || !boxRoller.IsSpawned)
                {
                    ResetBox();
                    return;
                }
                bool canMove = MysteryBoxRules.CanRelocate(boxLocation.Value, MapCatalog.BoxLocations, MapCatalog.ActiveZones(doorMask.Value));
                var weapon = MysteryBoxRules.Roll(rng, boxRoller.Weapons.View, boxUses, canMove);
                boxUses++;
                if (weapon == WeaponId.None)
                {
                    boxRoller.ServerAddPoints(MysteryBoxRules.Cost);
                    boxState.Value = BoxState.Moving;
                    boxTimer = MysteryBoxRules.MoveSeconds;
                    AnnounceRpc("The supply drop was pulled away - it will land somewhere else. (Refunded)", false);
                }
                else
                {
                    boxOfferWeapon.Value = weapon;
                    boxOfferOwner.Value = boxRoller.OwnerClientId;
                    boxState.Value = BoxState.Offering;
                    boxTimer = MysteryBoxRules.OfferSeconds;
                }
            }
            else if (boxState.Value == BoxState.Offering && boxTimer <= 0f)
            {
                ResetBox();
            }
            else if (boxState.Value == BoxState.Moving && boxTimer <= 0f)
            {
                boxLocation.Value = MysteryBoxRules.NextLocation(rng, boxLocation.Value, MapCatalog.BoxLocations, MapCatalog.ActiveZones(doorMask.Value));
                boxUses = 0;
                ResetBox();
            }
        }

        private void ResetBox()
        {
            boxState.Value = BoxState.Idle;
            boxOfferWeapon.Value = WeaponId.None;
            boxOfferOwner.Value = ulong.MaxValue;
            boxRoller = null;
        }

        private void TickEscape(float dt)
        {
            if (!escapeActive.Value)
            {
                // Checked every tick (not only on contributions) so funding can never get stuck "engaged".
                if (!escape.TryActivate()) return;
                escapeActive.Value = true;
                escapeTimer = EscapeRules.CountdownSeconds;
                escapeSecondsLeft.Value = escapeTimer;
                AnnounceRpc("LAUNCH SEQUENCE ENGAGED - hold out until the saucer is ready, then get under it!", true);
                return;
            }
            if (escapeTimer > 0f)
            {
                escapeTimer -= dt;
                float shown = Mathf.Max(0f, Mathf.Ceil(escapeTimer * 4f) / 4f);
                if (!Mathf.Approximately(shown, escapeSecondsLeft.Value)) escapeSecondsLeft.Value = shown;
                if (escapeTimer <= 0f) AnnounceRpc("THE SAUCER IS READY - everyone get inside the green circle!", true);
                return;
            }

            // Boarding: lift-off once every living survivor is aboard, or when the window after the first boarder ends.
            int alive = 0, aboard = 0;
            foreach (var p in PlayerAvatar.All)
            {
                if (p.Life != LifeState.Alive) continue;
                alive++;
                if (IsAboard(p)) aboard++;
            }
            if (aboard > 0 && boardingElapsed < 0f)
            {
                boardingElapsed = 0f;
                if (aboard < alive) AnnounceRpc($"Boarding! The saucer leaves in {EscapeRules.BoardingWindowSeconds:0} seconds - get in!", true);
            }
            if (boardingElapsed >= 0f)
            {
                boardingElapsed += dt;
                float left = Mathf.Max(0f, Mathf.Ceil(EscapeRules.BoardingWindowSeconds - boardingElapsed));
                if (!Mathf.Approximately(left, boardingSecondsLeft.Value)) boardingSecondsLeft.Value = left;
            }
            if (!EscapeRules.ShouldLaunch(alive, aboard, Mathf.Max(0f, boardingElapsed))) return;

            foreach (var p in PlayerAvatar.All)
                if (p.Life == LifeState.Alive && IsAboard(p)) p.ServerEscape();
            ServerEnd(MatchPhase.Victory);
        }

        public static bool IsAboard(PlayerAvatar p)
        {
            var ufo = Geo.V(MapCatalog.UfoPosition);
            var pos = p.transform.position;
            return Mathf.Abs(pos.y - ufo.y) < 3f &&
                   Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(ufo.x, ufo.z)) <= MapCatalog.BoardingRadius;
        }

        // ------------------------------------------------------------------ power-ups (authority)

        /// <summary>Called by a zombie when it is killed by a player (not when recycled or nuked).</summary>
        public void ServerOnZombieKilled(Vector3 position)
        {
            if (Phase != MatchPhase.Playing) return;
            var type = PowerUpRules.RollDrop(rng.NextDouble(), rng.NextDouble(), dropsThisRound, dropType.Value != PowerUpType.None);
            if (type == PowerUpType.None) return;
            dropsThisRound++;
            dropType.Value = type;
            dropPosition.Value = position + Vector3.up * 0.9f;
            dropLifetime = PowerUpRules.LifetimeSeconds;
        }

        private void TickPowerUps(float dt)
        {
            if (dropType.Value != PowerUpType.None)
            {
                dropLifetime -= dt;
                PlayerAvatar picker = null;
                foreach (var p in PlayerAvatar.All)
                {
                    if (p.Life != LifeState.Alive) continue;
                    if (Vector3.Distance(p.transform.position + Vector3.up * 0.9f, dropPosition.Value) <= PowerUpRules.PickupRadius)
                    {
                        picker = p;
                        break;
                    }
                }
                if (picker != null) ApplyPowerUp(dropType.Value, picker);
                if (picker != null || dropLifetime <= 0f) dropType.Value = PowerUpType.None;
            }

            instaTimer = Mathf.Max(0f, instaTimer - dt);
            doubleTimer = Mathf.Max(0f, doubleTimer - dt);
            powerSyncTimer -= dt;
            if (powerSyncTimer <= 0f || (instaTimer <= 0f && instaKillLeft.Value > 0f) || (doubleTimer <= 0f && doublePointsLeft.Value > 0f))
            {
                powerSyncTimer = 0.25f;
                instaKillLeft.Value = instaTimer;
                doublePointsLeft.Value = doubleTimer;
            }
        }

        private void ApplyPowerUp(PowerUpType type, PlayerAvatar picker)
        {
            switch (type)
            {
                case PowerUpType.MaxAmmo:
                    foreach (var p in PlayerAvatar.All)
                        if (p.Life == LifeState.Alive || p.Life == LifeState.Downed) p.Weapons.ServerRefillAll();
                    break;
                case PowerUpType.InstaKill:
                    instaTimer = PowerUpRules.DurationSeconds;
                    instaKillLeft.Value = instaTimer;
                    break;
                case PowerUpType.DoublePoints:
                    doubleTimer = PowerUpRules.DurationSeconds;
                    doublePointsLeft.Value = doubleTimer;
                    break;
                case PowerUpType.Nuke:
                    foreach (var z in ZombieAgent.All.ToArray()) z.ServerNuke();
                    foreach (var p in PlayerAvatar.All)
                        if (p.Life == LifeState.Alive || p.Life == LifeState.Downed) p.ServerAddPoints(PowerUpRules.NukePoints);
                    break;
            }
            AnnounceRpc($"{PowerUpRules.Name(type)}!  ({picker.DisplayName})", true);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void AnnounceRpc(string message, bool major)
        {
            Announcement?.Invoke(message, major);
        }
    }
}
