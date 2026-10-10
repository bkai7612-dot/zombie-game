using System;
using System.Collections.Generic;

namespace UZS.Core
{
    /// <summary>Weapon identifiers. Values are sent over the network, so never renumber existing entries.</summary>
    public enum WeaponId
    {
        None = 0,
        Pistol = 1,
        Smg = 2,
        Shotgun = 3,
        AssaultRifle = 4,
        MarksmanRifle = 5,
        PlasmaRifle = 6,
    }

    public enum FireMode
    {
        SemiAuto,
        FullAuto,
    }

    /// <summary>Immutable weapon tuning. All damage, fire-rate and ammo numbers are evaluated on the authority only.</summary>
    public sealed class WeaponStats
    {
        public WeaponId Id;
        public string DisplayName;
        public string UpgradedName;
        public FireMode Mode;
        public float Damage;
        public float HeadshotMultiplier;
        public int Pellets;
        public float SpreadDegrees;
        public float Range;
        public float RoundsPerSecond;
        public int MagazineSize;
        public int MaxReserve;
        public float ReloadSeconds;
        /// <summary>Wall-buy price. 0 means the weapon is not sold on walls (starter or mystery-box only).</summary>
        public int WallPrice;
        public bool InMysteryBox;
        /// <summary>Ammo price for weapons that are not sold on walls (starter / box-only).</summary>
        public int BoxAmmoPrice = 250;
        /// <summary>Damage falls off linearly to this fraction at max range.</summary>
        public float MinRangeDamageFraction;

        public float SecondsBetweenShots => 1f / RoundsPerSecond;
        public int AmmoPrice => WallPrice > 0 ? Math.Max(250, WallPrice / 2) : BoxAmmoPrice;
    }

    public static class WeaponCatalog
    {
        public const float UpgradeDamageMultiplier = 2f;
        public const float UpgradeMagazineMultiplier = 1.5f;
        public const float UpgradeReserveMultiplier = 1.5f;
        /// <summary>Upgraded weapons cost this many times their normal refill price (at least <see cref="UpgradedAmmoMinimum"/>).</summary>
        public const int UpgradedAmmoMultiplier = 2;
        public const int UpgradedAmmoMinimum = 1000;
        /// <summary>Aiming down sights tightens spread by this factor.</summary>
        public const float AimSpreadMultiplier = 0.5f;

        private static readonly Dictionary<WeaponId, WeaponStats> Stats = new Dictionary<WeaponId, WeaponStats>
        {
            [WeaponId.Pistol] = new WeaponStats
            {
                Id = WeaponId.Pistol, DisplayName = "M9 Service Pistol", UpgradedName = "Moonlight Judge",
                Mode = FireMode.SemiAuto, Damage = 34f, HeadshotMultiplier = 2.5f, Pellets = 1, SpreadDegrees = 0.6f,
                Range = 60f, RoundsPerSecond = 6f, MagazineSize = 8, MaxReserve = 64, ReloadSeconds = 1.4f,
                WallPrice = 0, InMysteryBox = false, MinRangeDamageFraction = 0.6f,
            },
            [WeaponId.Smg] = new WeaponStats
            {
                Id = WeaponId.Smg, DisplayName = "K-9 Compact SMG", UpgradedName = "Hive Swarm",
                Mode = FireMode.FullAuto, Damage = 26f, HeadshotMultiplier = 2f, Pellets = 1, SpreadDegrees = 2.2f,
                Range = 40f, RoundsPerSecond = 13f, MagazineSize = 30, MaxReserve = 180, ReloadSeconds = 2.0f,
                WallPrice = 1000, InMysteryBox = true, MinRangeDamageFraction = 0.5f,
            },
            [WeaponId.Shotgun] = new WeaponStats
            {
                Id = WeaponId.Shotgun, DisplayName = "Ranch Pump Shotgun", UpgradedName = "Barn Burner",
                Mode = FireMode.SemiAuto, Damage = 26f, HeadshotMultiplier = 1.5f, Pellets = 8, SpreadDegrees = 3.5f,
                Range = 22f, RoundsPerSecond = 1.4f, MagazineSize = 6, MaxReserve = 48, ReloadSeconds = 3.0f,
                WallPrice = 1200, InMysteryBox = true, MinRangeDamageFraction = 0.25f,
            },
            [WeaponId.AssaultRifle] = new WeaponStats
            {
                Id = WeaponId.AssaultRifle, DisplayName = "AR-7 Patrol Rifle", UpgradedName = "Checkpoint Zero",
                Mode = FireMode.FullAuto, Damage = 45f, HeadshotMultiplier = 2.5f, Pellets = 1, SpreadDegrees = 1.3f,
                Range = 75f, RoundsPerSecond = 9.5f, MagazineSize = 30, MaxReserve = 210, ReloadSeconds = 2.3f,
                WallPrice = 1600, InMysteryBox = true, MinRangeDamageFraction = 0.7f,
            },
            [WeaponId.MarksmanRifle] = new WeaponStats
            {
                Id = WeaponId.MarksmanRifle, DisplayName = "DMR-40 Marksman", UpgradedName = "Long Night",
                Mode = FireMode.SemiAuto, Damage = 150f, HeadshotMultiplier = 3f, Pellets = 1, SpreadDegrees = 0.15f,
                Range = 120f, RoundsPerSecond = 2.5f, MagazineSize = 10, MaxReserve = 60, ReloadSeconds = 2.6f,
                WallPrice = 2000, InMysteryBox = true, MinRangeDamageFraction = 0.9f,
            },
            [WeaponId.PlasmaRifle] = new WeaponStats
            {
                Id = WeaponId.PlasmaRifle, DisplayName = "XR-1 Plasma Rifle", UpgradedName = "Starbreaker",
                Mode = FireMode.FullAuto, Damage = 120f, HeadshotMultiplier = 1.5f, Pellets = 1, SpreadDegrees = 0.8f,
                Range = 70f, RoundsPerSecond = 6f, MagazineSize = 24, MaxReserve = 144, ReloadSeconds = 2.4f,
                WallPrice = 0, InMysteryBox = true, MinRangeDamageFraction = 1f, BoxAmmoPrice = 1500,
            },
        };

        public static IEnumerable<WeaponStats> All => Stats.Values;

        public static bool TryGet(WeaponId id, out WeaponStats stats) => Stats.TryGetValue(id, out stats);

        public static WeaponStats Get(WeaponId id)
        {
            if (!Stats.TryGetValue(id, out var s))
                throw new ArgumentException("Unknown weapon " + id, nameof(id));
            return s;
        }

        public static int MagazineSize(WeaponId id, bool upgraded)
        {
            var s = Get(id);
            return upgraded ? (int)Math.Ceiling(s.MagazineSize * UpgradeMagazineMultiplier) : s.MagazineSize;
        }

        public static int MaxReserve(WeaponId id, bool upgraded)
        {
            var s = Get(id);
            return upgraded ? (int)Math.Ceiling(s.MaxReserve * UpgradeReserveMultiplier) : s.MaxReserve;
        }

        /// <summary>Price to refill one slot (wall or crate); upgraded weapons always cost the upgraded price.</summary>
        public static int AmmoPriceFor(WeaponSlot slot)
        {
            if (slot.IsEmpty) return 0;
            int normal = Get(slot.Weapon).AmmoPrice;
            return slot.Upgraded ? Math.Max(UpgradedAmmoMinimum, normal * UpgradedAmmoMultiplier) : normal;
        }

        public static float Spread(WeaponId id, bool aiming)
        {
            float s = Get(id).SpreadDegrees;
            return aiming ? s * AimSpreadMultiplier : s;
        }

        public static string NameOf(WeaponId id, bool upgraded)
        {
            if (id == WeaponId.None) return "Knife";
            var s = Get(id);
            return upgraded ? s.UpgradedName : s.DisplayName;
        }

        /// <summary>Damage dealt by one pellet/bullet at <paramref name="distance"/>.</summary>
        public static float DamageAt(WeaponId id, bool upgraded, float distance, bool headshot)
        {
            var s = Get(id);
            if (distance > s.Range) return 0f;
            float t = s.Range <= 0f ? 0f : Clamp01(distance / s.Range);
            float falloff = 1f - (1f - s.MinRangeDamageFraction) * t;
            float dmg = s.Damage * falloff;
            if (upgraded) dmg *= UpgradeDamageMultiplier;
            if (headshot) dmg *= s.HeadshotMultiplier;
            return dmg;
        }

        public static float ReloadSeconds(WeaponId id, bool fastReloadPerk)
        {
            float t = Get(id).ReloadSeconds;
            return fastReloadPerk ? t * PerkCatalog.QuickhandsReloadMultiplier : t;
        }

        public static float SecondsBetweenShots(WeaponId id, bool rapidFirePerk)
        {
            float t = Get(id).SecondsBetweenShots;
            return rapidFirePerk ? t * PerkCatalog.RapidFireIntervalMultiplier : t;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Melee constants (the knife is always available and is not a weapon slot).</summary>
    public static class MeleeRules
    {
        public const float Damage = 150f;
        public const float Range = 2.0f;
        public const float Cooldown = 0.65f;
    }
}
