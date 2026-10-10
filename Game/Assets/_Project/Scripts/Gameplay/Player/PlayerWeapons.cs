using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Weapon inventory and combat. The authority owns the real <see cref="Loadout"/>, validates every shot,
    /// computes damage and awards points. The owning client predicts ammo for display and plays effects.
    /// </summary>
    public sealed class PlayerWeapons : NetworkBehaviour
    {
        public event Action<WeaponId, bool> LocalFired;
        public event Action LocalMelee;
        public event Action<bool, bool> LocalHitConfirmed;

        private readonly NetworkVariable<WeaponId> w0 = new NetworkVariable<WeaponId>(WeaponId.None);
        private readonly NetworkVariable<WeaponId> w1 = new NetworkVariable<WeaponId>(WeaponId.None);
        private readonly NetworkVariable<bool> up0 = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<bool> up1 = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<int> mag0 = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> mag1 = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> res0 = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> res1 = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> active = new NetworkVariable<int>(0);
        private readonly NetworkVariable<bool> reloading = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<int> shotsProcessed = new NetworkVariable<int>(0);

        private PlayerAvatar avatar;

        // Authority state.
        private Loadout loadout = Loadout.CreateStarter();
        private readonly FireRateLimiter fireLimiter = new FireRateLimiter();
        private double reloadEnd;
        private double lastMelee = -10;
        private readonly Dictionary<ZombieAgent, (float damage, bool head)> pending = new Dictionary<ZombieAgent, (float, bool)>();

        // Client mirror / prediction.
        private readonly Loadout mirror = new Loadout();
        private int shotsSent;
        private float localCooldown;
        private float localMeleeCooldown;
        private float reloadRequestedAt = -10f;
        private float switchRequestedAt = -10f;

        /// <summary>The loadout as this peer knows it (authoritative on the host, mirrored elsewhere).</summary>
        public Loadout View
        {
            get
            {
                if (IsServer) return loadout;
                mirror.Restore(
                    new WeaponSlot { Weapon = w0.Value, Upgraded = up0.Value, Magazine = mag0.Value, Reserve = res0.Value },
                    new WeaponSlot { Weapon = w1.Value, Upgraded = up1.Value, Magazine = mag1.Value, Reserve = res1.Value },
                    active.Value);
                return mirror;
            }
        }

        public bool IsReloading => reloading.Value;
        public int ActiveSlot => active.Value;

        /// <summary>Magazine count shown in the HUD, accounting for shots the authority hasn't processed yet.</summary>
        public int PredictedMagazine
        {
            get
            {
                int unacked = Mathf.Max(0, shotsSent - shotsProcessed.Value);
                return Mathf.Max(0, View.Active.Magazine - (IsServer ? 0 : unacked));
            }
        }

        private void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) Sync();
            shotsSent = shotsProcessed.Value;
        }

        // ------------------------------------------------------------------ authority API

        public void ServerResetLoadout()
        {
            loadout = Loadout.CreateStarter();
            reloading.Value = false;
            fireLimiter.Reset();
            Sync();
        }

        public void ServerRestore(WeaponSlot s0, WeaponSlot s1, int activeSlot)
        {
            loadout = new Loadout();
            loadout.Restore(s0, s1, activeSlot);
            if (loadout[0].IsEmpty && loadout[1].IsEmpty) loadout = Loadout.CreateStarter();
            Sync();
        }

        public void ServerGive(WeaponId weapon, bool upgraded)
        {
            loadout.Give(weapon, upgraded);
            reloading.Value = false;
            Sync();
            EquipFxRpc();
        }

        public void ServerRefill(WeaponId weapon)
        {
            int slot = loadout.SlotOf(weapon);
            if (slot >= 0) loadout.Refill(slot);
            Sync();
            EquipFxRpc();
        }

        public void ServerRefillAll()
        {
            loadout.RefillAll();
            Sync();
            EquipFxRpc();
        }

        public bool ServerUpgradeActive()
        {
            bool ok = loadout.Upgrade(loadout.ActiveSlot);
            reloading.Value = false;
            Sync();
            if (ok) UpgradeFxRpc();
            return ok;
        }

        public void ServerCancelReload() => reloading.Value = false;

        private void Sync()
        {
            w0.Value = loadout[0].Weapon; up0.Value = loadout[0].Upgraded; mag0.Value = loadout[0].Magazine; res0.Value = loadout[0].Reserve;
            w1.Value = loadout[1].Weapon; up1.Value = loadout[1].Upgraded; mag1.Value = loadout[1].Magazine; res1.Value = loadout[1].Reserve;
            active.Value = loadout.ActiveSlot;
        }

        /// <summary>Alive players can do everything; downed players get a "last stand" and can still shoot and reload.</summary>
        private bool CanAct(bool allowDowned = false)
        {
            var match = MatchState.Instance;
            if (match == null || match.Phase != MatchPhase.Playing) return false;
            return avatar.Life == LifeState.Alive || (allowDowned && avatar.Life == LifeState.Downed);
        }

        // ------------------------------------------------------------------ requests (owner -> authority)

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void FireRpc(Vector3 origin, Vector3 direction, int slot, int seed, bool aiming)
        {
            shotsProcessed.Value++;
            if (!CanAct(true) || reloading.Value || slot != loadout.ActiveSlot) return;
            var weapon = loadout.Active;
            if (weapon.IsEmpty) return;

            float interval = WeaponCatalog.SecondsBetweenShots(weapon.Weapon, (avatar.Perks & PerkId.RapidFire) != 0);
            if (!fireLimiter.TryFire(Time.timeAsDouble, interval)) return;
            if (direction.sqrMagnitude < 0.25f) return;
            direction.Normalize();
            // Never trust the muzzle position blindly: fall back to the authority's view of the player's eye.
            if (!ShotValidation.OriginPlausible(Vector3.Distance(origin, avatar.EyePosition))) origin = avatar.EyePosition;
            if (!loadout.TryConsumeRound(slot)) return;
            Sync();

            var stats = WeaponCatalog.Get(weapon.Weapon);
            pending.Clear();
            LagCompensation.Rewind(LagCompensation.RewindFor(NetworkManager, OwnerClientId));
            try
            {
                for (int i = 0; i < stats.Pellets; i++)
                {
                    var dir = Ballistics.Spread(direction, WeaponCatalog.Spread(weapon.Weapon, aiming), seed, i);
                    var hit = Ballistics.Trace(origin, dir, stats.Range);
                    if (hit.Hitbox == null || hit.Hitbox.Owner == null) continue;
                    float dmg = WeaponCatalog.DamageAt(weapon.Weapon, weapon.Upgraded, hit.Distance, hit.Hitbox.IsHead);
                    var z = hit.Hitbox.Owner;
                    pending.TryGetValue(z, out var acc);
                    pending[z] = (acc.damage + dmg, acc.head || hit.Hitbox.IsHead);
                }
            }
            finally
            {
                LagCompensation.Restore();
            }

            bool anyHit = false, anyKill = false, anyHead = false;
            foreach (var kv in pending)
            {
                anyHit = true;
                anyHead |= kv.Value.head;
                // One hit reward per zombie per shot, so pellets can't multiply points.
                if (kv.Key.ServerTakeDamage(kv.Value.damage, avatar, kv.Value.head, false)) anyKill = true;
                else avatar.ServerRecordHit();
            }
            if (anyHit) HitConfirmRpc(anyKill, anyHead);
            ShotFxRpc(origin, direction, seed, weapon.Weapon, weapon.Upgraded, aiming);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ReloadRpc(int slot)
        {
            if (!CanAct(true) || reloading.Value || slot != loadout.ActiveSlot || !loadout.CanReload(slot)) return;
            reloading.Value = true;
            reloadEnd = Time.timeAsDouble + WeaponCatalog.ReloadSeconds(loadout[slot].Weapon, (avatar.Perks & PerkId.Quickhands) != 0);
            ReloadFxRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SwitchRpc(int slot)
        {
            if (avatar.Life == LifeState.Dead || !loadout.SetActiveSlot(slot)) return;
            reloading.Value = false;
            Sync();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void MeleeRpc(Vector3 direction)
        {
            if (!CanAct() || Time.timeAsDouble - lastMelee < MeleeRules.Cooldown * 0.8f) return;
            lastMelee = Time.timeAsDouble;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            var origin = avatar.EyePosition;

            ZombieAgent best = null;
            float bestDist = float.MaxValue;
            LagCompensation.Rewind(LagCompensation.RewindFor(NetworkManager, OwnerClientId));
            try
            {
                foreach (var c in Physics.OverlapSphere(origin + direction * 1.0f, 1.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var hb = c.GetComponent<Hitbox>();
                    if (hb == null || hb.Owner == null || hb.Owner.IsDead) continue;
                    var to = c.bounds.center - origin;
                    float d = to.magnitude;
                    to.y = 0f;
                    if (d > MeleeRules.Range + 0.6f || Vector3.Angle(direction, to) > 70f) continue;
                    if (!Ballistics.ClearLine(origin, c.bounds.center)) continue; // no knifing through walls
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = hb.Owner;
                    }
                }
            }
            finally
            {
                LagCompensation.Restore();
            }

            if (best == null) return;
            if (best.ServerTakeDamage(MeleeRules.Damage, avatar, false, true)) HitConfirmRpc(true, false);
            else
            {
                avatar.ServerRecordHit();
                HitConfirmRpc(false, false);
            }
        }

        private void Update()
        {
            if (IsServer && reloading.Value && Time.timeAsDouble >= reloadEnd)
            {
                reloading.Value = false;
                loadout.CompleteReload(loadout.ActiveSlot);
                Sync();
            }
            if (IsOwner) OwnerUpdate(Time.deltaTime);
        }

        // ------------------------------------------------------------------ owner input

        private void OwnerUpdate(float dt)
        {
            localCooldown -= dt;
            localMeleeCooldown -= dt;
            if (InputGate.Blocked || !CanAct(true)) return;
            bool downed = avatar.Life == LifeState.Downed;
            var input = KeyboardMouseInput.Instance;
            var view = View;

            int slot = input.SlotPressed;
            if (input.SwapPressed) slot = 1 - view.ActiveSlot;
            if (slot >= 0 && slot != view.ActiveSlot && !view[slot].IsEmpty && Time.time - switchRequestedAt > 0.2f)
            {
                switchRequestedAt = Time.time;
                SwitchRpc(slot);
                return;
            }

            if (input.MeleePressed && localMeleeCooldown <= 0f && !downed)
            {
                localMeleeCooldown = MeleeRules.Cooldown;
                MeleeRpc(avatar.Motor.AimDirection);
                LocalMelee?.Invoke();
                if (AudioManager.Instance != null) AudioManager.Instance.Play2D(Sfx.Melee, 0.8f);
                return;
            }

            var active = view.Active;
            if (active.IsEmpty) return;
            bool reloadPending = Time.time - reloadRequestedAt < 0.5f;

            if (input.ReloadPressed && !reloading.Value && !reloadPending && view.CanReload(view.ActiveSlot))
            {
                RequestReload(view.ActiveSlot);
                return;
            }

            var stats = WeaponCatalog.Get(active.Weapon);
            bool trigger = stats.Mode == FireMode.FullAuto ? input.FireHeld : input.FirePressed;
            if (!trigger || localCooldown > 0f || reloading.Value || reloadPending) return;

            if (PredictedMagazine <= 0)
            {
                if (input.FirePressed && AudioManager.Instance != null) AudioManager.Instance.Play2D(Sfx.DryFire);
                if (active.Reserve > 0) RequestReload(view.ActiveSlot);
                return;
            }

            localCooldown = WeaponCatalog.SecondsBetweenShots(active.Weapon, (avatar.Perks & PerkId.RapidFire) != 0);
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var origin = avatar.Motor.EyeWorldPosition;
            var dir = avatar.Motor.AimDirection;
            bool aiming = input.AimHeld;
            shotsSent++;
            FireRpc(origin, dir, view.ActiveSlot, seed, aiming);
            LocalFired?.Invoke(active.Weapon, active.Upgraded);
            Effects.Shot(origin, dir, seed, active.Weapon, active.Upgraded, aiming, true);
        }

        private void RequestReload(int slot)
        {
            reloadRequestedAt = Time.time;
            ReloadRpc(slot);
        }

        // ------------------------------------------------------------------ feedback (authority -> clients)

        [Rpc(SendTo.Owner)]
        private void HitConfirmRpc(bool kill, bool head)
        {
            LocalHitConfirmed?.Invoke(kill, head);
            if (AudioManager.Instance != null) AudioManager.Instance.Play2D(kill ? Sfx.KillMarker : Sfx.HitMarker, 0.6f);
        }

        [Rpc(SendTo.NotOwner)]
        private void ShotFxRpc(Vector3 origin, Vector3 direction, int seed, WeaponId weapon, bool upgraded, bool aiming)
        {
            Effects.Shot(origin, direction, seed, weapon, upgraded, aiming, false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ReloadFxRpc()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.Reload, transform.position + Vector3.up, 0.8f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void EquipFxRpc()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.Purchase, transform.position + Vector3.up, 0.9f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void UpgradeFxRpc()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.Upgrade, transform.position + Vector3.up, 1f);
        }
    }
}
