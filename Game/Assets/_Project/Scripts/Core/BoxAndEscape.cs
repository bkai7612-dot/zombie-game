using System;
using System.Collections.Generic;

namespace UZS.Core
{
    public enum BoxState : byte
    {
        Idle = 0,
        Rolling = 1,
        Offering = 2,
        Moving = 3,
    }

    /// <summary>Mystery box ("Supply Drop Crate") rules. Rolled by the authority only.</summary>
    public static class MysteryBoxRules
    {
        public const int Cost = 950;
        public const float RollSeconds = 3f;
        public const float OfferSeconds = 10f;
        public const float MoveSeconds = 4f;
        public const int MinUsesBeforeMove = 4;
        public const double MoveChance = 0.2;

        public static int Weight(WeaponId id) => id == WeaponId.PlasmaRifle ? 1 : 4;

        /// <summary>
        /// Picks a weapon the player does not already own. Returns None when the box decides to relocate
        /// (the caller refunds the player in that case).
        /// </summary>
        public static WeaponId Roll(Random rng, Loadout loadout, int usesSinceMove)
        {
            if (usesSinceMove >= MinUsesBeforeMove && rng.NextDouble() < MoveChance) return WeaponId.None;

            var pool = new List<WeaponId>();
            int total = 0;
            foreach (var s in WeaponCatalog.All)
            {
                if (!s.InMysteryBox || loadout.Owns(s.Id)) continue;
                pool.Add(s.Id);
                total += Weight(s.Id);
            }
            if (pool.Count == 0) return WeaponId.Pistol;

            int pick = rng.Next(total);
            foreach (var id in pool)
            {
                pick -= Weight(id);
                if (pick < 0) return id;
            }
            return pool[pool.Count - 1];
        }

        public static int NextLocation(Random rng, int current, int locationCount)
        {
            if (locationCount <= 1) return current;
            int next = rng.Next(locationCount - 1);
            return next >= current ? next + 1 : next;
        }
    }

    /// <summary>
    /// The UFO escape is funded collectively: every player can contribute points at the console until the
    /// cost is met. Then a hold-out countdown starts; whoever is inside the boarding area when it ends escapes.
    /// </summary>
    public static class EscapeRules
    {
        public const float CountdownSeconds = 60f;
        public const int ContributionStep = 500;

        public static int CostFor(int players)
        {
            players = Math.Max(1, Math.Min(GameInfo.MaxPlayers, players));
            return 12000 + 6000 * (players - 1);
        }

        /// <summary>How many points a player with <paramref name="points"/> contributes right now.</summary>
        public static int Contribution(int points, int funded, int cost)
        {
            int remaining = cost - funded;
            if (remaining <= 0 || points <= 0) return 0;
            return Math.Min(points, remaining);
        }
    }
}
