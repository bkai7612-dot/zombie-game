using System;

namespace UZS.Core
{
    /// <summary>Points awarded by the authority. Clients never send point values.</summary>
    public static class PointsRules
    {
        public const int HitReward = 10;
        public const int KillReward = 60;
        public const int HeadshotKillBonus = 40;
        public const int MeleeKillReward = 130;

        public static int KillRewardFor(bool headshot, bool melee)
        {
            if (melee) return MeleeKillReward;
            return KillReward + (headshot ? HeadshotKillBonus : 0);
        }
    }

    public static class HealthRules
    {
        public const float BaseMaxHealth = 100f;
        public const float IronHideMaxHealth = 250f;
        public const float RegenDelaySeconds = 4f;
        public const float RegenPerSecond = 25f;

        public static float MaxHealth(PerkId perks) => (perks & PerkId.IronHide) != 0 ? IronHideMaxHealth : BaseMaxHealth;
    }

    public static class ReviveRules
    {
        public const float ReviveSeconds = 3f;
        public const float ReviveRange = 2.4f;
        public const float BleedoutSeconds = 30f;
        /// <summary>If the reviver stops sending hold messages for longer than this, progress resets.</summary>
        public const float HoldTimeoutSeconds = 0.5f;
        public const float HealthAfterRevive = 50f;
    }

    public static class InteractionRules
    {
        /// <summary>Distance at which the client shows a prompt.</summary>
        public const float PromptRange = 2.6f;
        /// <summary>Extra slack the authority allows for latency on top of <see cref="PromptRange"/>.</summary>
        public const float ServerTolerance = 1.2f;
        public const float ServerMaxRange = PromptRange + ServerTolerance;
        public const float RequestCooldownSeconds = 0.25f;

        public static bool InRange(float distance) => distance <= ServerMaxRange;
    }

    public static class MovementRules
    {
        public const float WalkSpeed = 4.6f;
        public const float SprintSpeed = 6.8f;
        public const float FleetfootMultiplier = 1.15f;
        public const float CrouchSpeed = 2.4f;
        public const float JumpHeight = 1.0f;
        public const float Gravity = -20f;
        public const float EyeHeight = 1.62f;

        public static float MaxSpeed(PerkId perks) =>
            SprintSpeed * ((perks & PerkId.Fleetfoot) != 0 ? FleetfootMultiplier : 1f);
    }

    public static class ShotValidation
    {
        /// <summary>How far (metres) a reported muzzle origin may be from the authority's view of the player's eye.</summary>
        public const float MaxOriginError = 2.5f;
        /// <summary>A small fire-rate tolerance so jitter in packet arrival doesn't reject honest shots.</summary>
        public const float FireIntervalTolerance = 0.8f;

        public static bool OriginPlausible(float distanceFromEye) => distanceFromEye <= MaxOriginError;

        public static bool FireIntervalOk(double now, double lastShot, float secondsBetweenShots) =>
            now - lastShot >= secondsBetweenShots * FireIntervalTolerance;
    }

    /// <summary>
    /// Token-bucket fire-rate limiter used by the authority. Network batching can deliver two honest shots in
    /// the same frame, so a small burst is allowed while the sustained rate is capped at the weapon's rate.
    /// </summary>
    public sealed class FireRateLimiter
    {
        public const float BurstShots = 3f;

        private double lastTime = double.NegativeInfinity;
        private float tokens = BurstShots;

        public bool TryFire(double now, float secondsBetweenShots)
        {
            if (secondsBetweenShots <= 0f) secondsBetweenShots = 0.01f;
            if (!double.IsNegativeInfinity(lastTime))
            {
                double elapsed = Math.Max(0.0, now - lastTime);
                tokens = (float)Math.Min(BurstShots, tokens + elapsed / (secondsBetweenShots * ShotValidation.FireIntervalTolerance));
            }
            lastTime = now;
            if (tokens < 1f) return false;
            tokens -= 1f;
            return true;
        }

        public void Reset()
        {
            lastTime = double.NegativeInfinity;
            tokens = BurstShots;
        }
    }

    [Flags]
    public enum PerkId
    {
        None = 0,
        IronHide = 1,
        Quickhands = 2,
        RapidFire = 4,
        Fleetfoot = 8,
    }

    public static class PerkCatalog
    {
        public const float QuickhandsReloadMultiplier = 0.5f;
        public const float RapidFireIntervalMultiplier = 0.75f;

        public static readonly PerkId[] All = { PerkId.IronHide, PerkId.Quickhands, PerkId.RapidFire, PerkId.Fleetfoot };

        public static int Price(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.IronHide: return 2500;
                case PerkId.Quickhands: return 3000;
                case PerkId.RapidFire: return 2000;
                case PerkId.Fleetfoot: return 2000;
                default: throw new ArgumentOutOfRangeException(nameof(perk));
            }
        }

        public static string Name(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.IronHide: return "Iron Hide Tonic";
                case PerkId.Quickhands: return "Quickhands Soda";
                case PerkId.RapidFire: return "Trigger Happy Cola";
                case PerkId.Fleetfoot: return "Fleetfoot Fizz";
                default: return perk.ToString();
            }
        }

        public static string Description(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.IronHide: return "Max health 250";
                case PerkId.Quickhands: return "Reload twice as fast";
                case PerkId.RapidFire: return "Fire 33% faster";
                case PerkId.Fleetfoot: return "Move 15% faster";
                default: return string.Empty;
            }
        }
    }

    /// <summary>Round pacing and difficulty curve. Endless: rounds keep going, with health growth capped.</summary>
    public static class RoundRules
    {
        public const float IntermissionSeconds = 10f;
        public const float FirstRoundDelaySeconds = 5f;
        public const int HealthCapRound = 55;
        public const int MaxZombiesPerRound = 300;

        public static int ZombiesForRound(int round, int players)
        {
            if (round < 1) round = 1;
            players = Math.Max(1, Math.Min(GameInfo.MaxPlayers, players));
            double baseCount = 4.0 + 2.2 * round + 0.09 * round * round;
            double factor = 1.0 + 0.5 * (players - 1);
            return Math.Min(MaxZombiesPerRound, (int)Math.Ceiling(baseCount * factor));
        }

        public const int SteepGrowthUntilRound = 25;

        /// <summary>
        /// Linear to round 9, then x1.1 per round to round 25, then x1.04 per round, frozen after round 55.
        /// The gentler late slope keeps endless mode hard but playable (see tools/playtest).
        /// </summary>
        public static float ZombieHealth(int round)
        {
            if (round < 1) round = 1;
            int r = Math.Min(round, HealthCapRound);
            if (r <= 9) return 120f + 90f * (r - 1);
            float hp = 120f + 90f * 8f;
            for (int i = 10; i <= r; i++) hp *= i <= SteepGrowthUntilRound ? 1.1f : 1.04f;
            return hp;
        }

        public static int MaxAlive(int players)
        {
            players = Math.Max(1, Math.Min(GameInfo.MaxPlayers, players));
            return Math.Min(30, 18 + 4 * (players - 1));
        }

        public static float SpawnIntervalSeconds(int round)
        {
            if (round < 1) round = 1;
            return (float)Math.Max(0.35, 2.2 * Math.Pow(0.95, round - 1));
        }

        public static float ZombieDamage(int round) => round < 15 ? 40f : 50f;

        /// <summary>Returns the speed tier for a new zombie given a uniform random roll in [0,1).</summary>
        public static ZombieSpeed RollSpeed(int round, double roll)
        {
            double sprint = Clamp((round - 6) * 0.08, 0, 0.7);
            double run = Clamp((round - 2) * 0.12, 0, 1.0 - sprint);
            if (roll < sprint) return ZombieSpeed.Sprint;
            if (roll < sprint + run) return ZombieSpeed.Run;
            return ZombieSpeed.Walk;
        }

        public static float MoveSpeed(ZombieSpeed speed)
        {
            switch (speed)
            {
                case ZombieSpeed.Run: return 3.4f;
                case ZombieSpeed.Sprint: return 5.2f;
                default: return 1.7f;
            }
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    public enum ZombieSpeed : byte
    {
        Walk = 0,
        Run = 1,
        Sprint = 2,
    }

    public static class ZombieRules
    {
        public const float AttackRange = 1.7f;
        /// <summary>How far above a zombie's feet a target can stand and still be hit (car roofs are 1.5 m).</summary>
        public const float MaxReachUp = 1.9f;
        public const float MaxReachDown = 0.8f;
        /// <summary>Zombies spawn at least this far from every player and out of sight when close.</summary>
        public const float MinSpawnDistance = 10f;
        public const float VisibleSpawnCheckDistance = 25f;
        /// <summary>Freshly spawned zombies climb out of the ground before moving.</summary>
        public const float EmergeSeconds = 1.2f;
        /// <summary>A zombie whose path to its target is incomplete for this long is recycled.</summary>
        public const float UnreachableSeconds = 10f;

        public static bool CanReach(float targetFeetY, float zombieFeetY)
        {
            float dy = targetFeetY - zombieFeetY;
            return dy <= MaxReachUp && dy >= -MaxReachDown;
        }
        public const float AttackWindup = 0.55f;
        public const float AttackCooldown = 1.3f;
        public const float RepathInterval = 0.3f;
        /// <summary>A zombie that makes no progress for this long while far from every player is recycled.</summary>
        public const float StuckSeconds = 14f;
        public const float StuckDistance = 25f;
    }

    /// <summary>Catch-up for players who join a match already in progress.</summary>
    public static class LateJoinRules
    {
        public const int MaxStartingPoints = 10000;

        public static int StartingPoints(int round)
        {
            if (round <= 1) return GameInfo.StartingPoints;
            return Math.Min(MaxStartingPoints, GameInfo.StartingPoints + 750 * (round - 1));
        }
    }

    public enum PowerUpType : byte
    {
        None = 0,
        MaxAmmo = 1,
        InstaKill = 2,
        DoublePoints = 3,
        Nuke = 4,
    }

    /// <summary>Random drops from killed zombies that keep high rounds playable.</summary>
    public static class PowerUpRules
    {
        public const double DropChance = 0.03;
        public const int MaxDropsPerRound = 4;
        public const float DurationSeconds = 20f;
        public const float LifetimeSeconds = 25f;
        public const float PickupRadius = 1.6f;
        public const int NukePoints = 400;

        public static readonly PowerUpType[] All = { PowerUpType.MaxAmmo, PowerUpType.InstaKill, PowerUpType.DoublePoints, PowerUpType.Nuke };

        /// <summary>Returns the drop for a kill, or None. <paramref name="roll"/> and <paramref name="pick"/> are uniform [0,1).</summary>
        public static PowerUpType RollDrop(double roll, double pick, int dropsThisRound, bool dropActive)
        {
            if (dropActive || dropsThisRound >= MaxDropsPerRound || roll >= DropChance) return PowerUpType.None;
            return All[Math.Min(All.Length - 1, (int)(pick * All.Length))];
        }

        public static string Name(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.MaxAmmo: return "MAX AMMO";
                case PowerUpType.InstaKill: return "INSTA-KILL";
                case PowerUpType.DoublePoints: return "DOUBLE POINTS";
                case PowerUpType.Nuke: return "NUKE";
                default: return string.Empty;
            }
        }
    }

    public enum MatchPhase : byte
    {
        Lobby = 0,
        Playing = 1,
        Victory = 2,
        Defeat = 3,
    }

    public enum LifeState : byte
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
        Escaped = 3,
    }
}
