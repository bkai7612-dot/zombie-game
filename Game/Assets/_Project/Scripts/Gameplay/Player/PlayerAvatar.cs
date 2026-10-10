using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Replicated player identity, economy, health and life state. All values are written by the authority
    /// only; the owning client sends requests (RPCs) that the authority validates.
    /// </summary>
    [RequireComponent(typeof(PlayerWeapons), typeof(PlayerMotor), typeof(PlayerInteractor))]
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        public static readonly List<PlayerAvatar> All = new List<PlayerAvatar>();
        public static PlayerAvatar Local { get; private set; }

        public static event Action<PlayerAvatar> LocalSpawned;
        public static event Action<float, Vector3> LocalHurt;

        private readonly NetworkVariable<FixedString64Bytes> displayName = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<int> characterId = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> points = new NetworkVariable<int>(0);
        private readonly NetworkVariable<float> health = new NetworkVariable<float>(HealthRules.BaseMaxHealth);
        private readonly NetworkVariable<float> maxHealth = new NetworkVariable<float>(HealthRules.BaseMaxHealth);
        private readonly NetworkVariable<LifeState> life = new NetworkVariable<LifeState>(LifeState.Alive);
        private readonly NetworkVariable<PerkId> perks = new NetworkVariable<PerkId>(PerkId.None);
        private readonly NetworkVariable<int> kills = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> headshots = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> downs = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> revives = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> pointsEarned = new NetworkVariable<int>(0);
        private readonly NetworkVariable<float> bleedoutLeft = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<float> reviveProgress = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false);

        public string DisplayName => displayName.Value.ToString();
        public int CharacterId => characterId.Value;
        public int Points => points.Value;
        public float Health => health.Value;
        public float MaxHealth => maxHealth.Value;
        public LifeState Life => life.Value;
        public PerkId Perks => perks.Value;
        public int Kills => kills.Value;
        public int Headshots => headshots.Value;
        public int Downs => downs.Value;
        public int Revives => revives.Value;
        public int PointsEarned => pointsEarned.Value;
        public float BleedoutLeft => bleedoutLeft.Value;
        public float ReviveProgress => reviveProgress.Value;
        public bool Ready => ready.Value;

        public PlayerWeapons Weapons { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerInteractor Interactor { get; private set; }

        public Vector3 Center => transform.position + Vector3.up * 1.0f;
        public Vector3 EyePosition => transform.position + Vector3.up * MovementRules.EyeHeight;

        // Authority-only.
        private string playerKey = string.Empty;
        private float lastDamageTime = -100f;
        private PlayerAvatar reviver;
        private float lastReviveHold;
        private float bleedTimer;
        private float syncTimer;

        private CharacterModel model;

        private void Awake()
        {
            Weapons = GetComponent<PlayerWeapons>();
            Motor = GetComponent<PlayerMotor>();
            Interactor = GetComponent<PlayerInteractor>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            characterId.OnValueChanged += OnCharacterChanged;
            life.OnValueChanged += OnLifeChanged;

            if (IsServer) ServerInitialise();

            RebuildModel();
            if (IsOwner)
            {
                Local = this;
                LocalSpawned?.Invoke(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            characterId.OnValueChanged -= OnCharacterChanged;
            life.OnValueChanged -= OnLifeChanged;
            All.Remove(this);
            if (Local == this) Local = null;

            if (IsServer)
            {
                var match = MatchState.Instance;
                var cc = ConnectionController.Instance;
                bool keep = match != null && match.Phase == MatchPhase.Playing;
                var snapshot = keep ? Snapshot() : null;
                if (cc != null && cc.Ledger.Release(playerKey, OwnerClientId, snapshot) == ReleaseResult.TakenOver && snapshot != null)
                {
                    // The same player already reconnected on a new connection: hand them their progress.
                    if (cc.Ledger.TryGetHolder(playerKey, out var holder))
                        foreach (var p in All)
                            if (p.OwnerClientId == holder) p.ServerRestore(snapshot);
                }
                if (match != null) match.ServerOnPlayerLeft();
            }
        }

        private void ServerInitialise()
        {
            var cc = ConnectionController.Instance;
            ConnectionPayload payload = null;
            if (cc != null) cc.ServerTakePayload(OwnerClientId, out payload);
            payload = payload ?? new ConnectionPayload { PlayerKey = "client" + OwnerClientId };

            playerKey = payload.PlayerKey;
            displayName.Value = new FixedString64Bytes(payload.DisplayName);
            characterId.Value = CharacterCatalog.Clamp(payload.CharacterId);

            var match = MatchState.Instance;
            if (match != null && match.Phase == MatchPhase.Playing)
            {
                // Late join or reconnect: restore a snapshot exactly once, otherwise start fresh.
                if (cc != null && cc.Ledger.TryConsume(playerKey, out var snap)) ServerRestore(snap);
                else ServerResetForMatch(LateJoinRules.StartingPoints(match.Round));
                match.ServerOnPlayerJoined(this);
            }

            // Claim the identity last: if an older connection with the same key is still around (the player
            // reconnected before the host noticed the drop), disconnect it. Its despawn hands its progress to us.
            if (cc != null)
            {
                var stale = cc.Ledger.Claim(playerKey, OwnerClientId);
                if (stale.HasValue && NetworkManager != null)
                    NetworkManager.DisconnectClient(stale.Value, "You connected again from another game window.");
            }
        }

        // ------------------------------------------------------------------ authority API

        public void ServerBeginMatch(int spawnIndex)
        {
            ServerResetForMatch(GameInfo.StartingPoints);
            var s = MapCatalog.PlayerSpawns[spawnIndex % MapCatalog.PlayerSpawns.Length];
            ServerTeleport(Geo.V(s) + Vector3.up * 0.2f, 90f);
        }

        private void ServerResetForMatch(int startingPoints)
        {
            points.Value = startingPoints;
            perks.Value = PerkId.None;
            maxHealth.Value = HealthRules.BaseMaxHealth;
            health.Value = HealthRules.BaseMaxHealth;
            life.Value = LifeState.Alive;
            Weapons.ServerResetLoadout();
        }

        public void ServerRestore(PlayerSnapshot s)
        {
            points.Value = Mathf.Max(0, s.Points);
            perks.Value = s.Perks;
            maxHealth.Value = HealthRules.MaxHealth(s.Perks);
            health.Value = maxHealth.Value;
            life.Value = s.LifeState == LifeState.Downed ? LifeState.Dead : s.LifeState;
            kills.Value = s.Kills;
            headshots.Value = s.Headshots;
            revives.Value = s.Revives;
            downs.Value = s.Downs;
            Weapons.ServerRestore(s.Slot0, s.Slot1, s.ActiveSlot);
        }

        private PlayerSnapshot Snapshot()
        {
            var l = Weapons.View;
            return new PlayerSnapshot
            {
                Points = Points, Perks = Perks, Kills = Kills, Headshots = Headshots, Revives = Revives, Downs = Downs,
                LifeState = Life, Slot0 = l[0], Slot1 = l[1], ActiveSlot = l.ActiveSlot,
            };
        }

        public void ServerAddPoints(int amount)
        {
            if (!IsServer || amount <= 0) return;
            points.Value += amount;
            pointsEarned.Value += amount;
        }

        public bool ServerSpend(int amount)
        {
            if (!IsServer || amount < 0 || points.Value < amount) return false;
            points.Value -= amount;
            return true;
        }

        public void ServerAddPerk(PerkId perk)
        {
            perks.Value |= perk;
            maxHealth.Value = HealthRules.MaxHealth(perks.Value);
            if (perk == PerkId.IronHide) health.Value = maxHealth.Value;
            PerkFxRpc();
        }

        private static int Multiplier => MatchState.Instance != null ? MatchState.Instance.PointsMultiplier : 1;

        public void ServerRecordHit()
        {
            ServerAddPoints(PointsRules.HitReward * Multiplier);
        }

        public void ServerRecordKill(bool headshot, bool melee)
        {
            kills.Value++;
            if (headshot) headshots.Value++;
            ServerAddPoints(PointsRules.KillRewardFor(headshot, melee) * Multiplier);
        }

        public void ServerTakeDamage(float amount, Vector3 from)
        {
            if (!IsServer || Life != LifeState.Alive) return;
            lastDamageTime = Time.time;
            health.Value = Mathf.Max(0f, health.Value - amount);
            HurtRpc(amount, from);
            if (health.Value <= 0f) ServerDown();
        }

        private void ServerDown()
        {
            life.Value = LifeState.Downed;
            downs.Value++;
            perks.Value = PerkId.None;
            maxHealth.Value = HealthRules.BaseMaxHealth;
            bleedTimer = ReviveRules.BleedoutSeconds;
            bleedoutLeft.Value = bleedTimer;
            reviveProgress.Value = 0f;
            reviver = null;
            Weapons.ServerCancelReload();
            var match = MatchState.Instance;
            if (match != null) match.AnnounceRpc($"{DisplayName} is down!", false);
        }

        /// <summary>Called while a teammate holds the revive key next to this downed player.</summary>
        public void ServerReviveHold(PlayerAvatar by)
        {
            if (!IsServer || Life != LifeState.Downed || by == null || by == this || by.Life != LifeState.Alive) return;
            if (Vector3.Distance(by.transform.position, transform.position) > ReviveRules.ReviveRange + InteractionRules.ServerTolerance) return;

            float now = Time.time;
            if (reviver != by || now - lastReviveHold > ReviveRules.HoldTimeoutSeconds)
            {
                reviver = by;
                reviveProgress.Value = 0f;
            }
            else
            {
                reviveProgress.Value = Mathf.Min(1f, reviveProgress.Value + (now - lastReviveHold) / ReviveRules.ReviveSeconds);
            }
            lastReviveHold = now;

            if (reviveProgress.Value >= 1f)
            {
                life.Value = LifeState.Alive;
                health.Value = ReviveRules.HealthAfterRevive;
                reviveProgress.Value = 0f;
                by.revives.Value++;
                reviver = null;
                var match = MatchState.Instance;
                if (match != null) match.AnnounceRpc($"{by.DisplayName} revived {DisplayName}", false);
            }
        }

        public void ServerRespawn(Vector3 position)
        {
            life.Value = LifeState.Alive;
            perks.Value = PerkId.None;
            maxHealth.Value = HealthRules.BaseMaxHealth;
            health.Value = maxHealth.Value;
            Weapons.ServerResetLoadout();
            ServerTeleport(position, transform.eulerAngles.y);
        }

        public void ServerEscape()
        {
            if (Life == LifeState.Alive) life.Value = LifeState.Escaped;
        }

        public void ServerTeleport(Vector3 position, float yaw) => TeleportRpc(position, yaw);

        private void Update()
        {
            if (IsServer) ServerTick(Time.deltaTime);
            if (model != null) model.Animate(this);
        }

        private void ServerTick(float dt)
        {
            var match = MatchState.Instance;
            if (match == null || match.Phase != MatchPhase.Playing) return;

            if (Life == LifeState.Alive && health.Value < maxHealth.Value && Time.time - lastDamageTime > HealthRules.RegenDelaySeconds)
                health.Value = Mathf.Min(maxHealth.Value, health.Value + HealthRules.RegenPerSecond * dt);

            if (Life == LifeState.Downed)
            {
                if (reviver != null && Time.time - lastReviveHold > ReviveRules.HoldTimeoutSeconds)
                {
                    reviver = null;
                    reviveProgress.Value = 0f;
                }
                if (reviver == null) bleedTimer -= dt;
                syncTimer -= dt;
                if (syncTimer <= 0f)
                {
                    syncTimer = 0.25f;
                    bleedoutLeft.Value = Mathf.Max(0f, bleedTimer);
                }
                if (bleedTimer <= 0f)
                {
                    life.Value = LifeState.Dead;
                    match.AnnounceRpc($"{DisplayName} bled out", false);
                }
            }
        }

        // ------------------------------------------------------------------ RPCs

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SetCharacterRpc(int id)
        {
            var match = MatchState.Instance;
            if (match != null && match.Phase != MatchPhase.Lobby) return;
            characterId.Value = CharacterCatalog.Clamp(id);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void SetReadyRpc(bool value)
        {
            ready.Value = value;
        }

        [Rpc(SendTo.Owner)]
        private void TeleportRpc(Vector3 position, float yaw)
        {
            Motor.Teleport(position, yaw);
        }

        [Rpc(SendTo.Owner)]
        private void HurtRpc(float amount, Vector3 from)
        {
            LocalHurt?.Invoke(amount, from);
            if (AudioManager.Instance != null) AudioManager.Instance.Play2D(Sfx.PlayerHurt, 0.8f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PerkFxRpc()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.PerkDrink, transform.position + Vector3.up, 1f);
        }

        // ------------------------------------------------------------------ presentation

        private void OnCharacterChanged(int previous, int current) => RebuildModel();

        private void OnLifeChanged(LifeState previous, LifeState current)
        {
            if (AudioManager.Instance == null) return;
            if (current == LifeState.Downed) AudioManager.Instance.PlayAt(Sfx.Downed, transform.position + Vector3.up, 1f);
            if (previous == LifeState.Downed && current == LifeState.Alive) AudioManager.Instance.PlayAt(Sfx.Revived, transform.position + Vector3.up, 1f);
        }

        private void RebuildModel()
        {
            if (model != null) model.Destroy();
            model = CharacterModel.Build(transform, CharacterCatalog.Get(CharacterId), IsOwner);
        }
    }
}
