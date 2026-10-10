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

        public static int Weight(WeaponId id) => id == WeaponId.PlasmaRifle ? 2 : 4;

        /// <summary>
        /// Picks a weapon the player does not already own. Returns None when the box decides to relocate
        /// (the caller refunds the player in that case).
        /// </summary>
        public static WeaponId Roll(Random rng, Loadout loadout, int usesSinceMove, bool canRelocate = true)
        {
            if (canRelocate && usesSinceMove >= MinUsesBeforeMove && rng.NextDouble() < MoveChance) return WeaponId.None;

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

        /// <summary>
        /// Picks a new location among those whose zone the team has opened (never behind a locked door).
        /// Returns <paramref name="current"/> when there is nowhere else to go.
        /// </summary>
        public static int NextLocation(Random rng, int current, StationDef[] locations, bool[] activeZones)
        {
            var options = new List<int>();
            for (int i = 0; i < locations.Length; i++)
                if (i != current && activeZones[locations[i].Zone]) options.Add(i);
            return options.Count == 0 ? current : options[rng.Next(options.Count)];
        }

        /// <summary>True if relocating is possible at all (otherwise the box never "moves" and never refunds).</summary>
        public static bool CanRelocate(int current, StationDef[] locations, bool[] activeZones)
        {
            for (int i = 0; i < locations.Length; i++)
                if (i != current && activeZones[locations[i].Zone]) return true;
            return false;
        }
    }

    /// <summary>
    /// The UFO escape is funded collectively: every player can contribute points at the console until the
    /// cost is met. Then a hold-out countdown starts, followed by boarding.
    /// </summary>
    public static class EscapeRules
    {
        public const float CountdownSeconds = 60f;
        /// <summary>After the first survivor boards, the others get this long to join before lift-off.</summary>
        public const float BoardingWindowSeconds = 15f;

        public static int CostFor(int players)
        {
            players = Math.Max(1, Math.Min(GameInfo.MaxPlayers, players));
            return 10000 + 5000 * (players - 1);
        }

        /// <summary>How many points a player with <paramref name="points"/> contributes right now.</summary>
        public static int Contribution(int points, int funded, int cost)
        {
            int remaining = cost - funded;
            if (remaining <= 0 || points <= 0) return 0;
            return Math.Min(points, remaining);
        }

        /// <summary>
        /// Lift-off happens when every living survivor is aboard, or when the boarding window after the first
        /// survivor boarded runs out. Nobody aboard means the saucer keeps waiting.
        /// </summary>
        public static bool ShouldLaunch(int alivePlayers, int aliveAboard, float secondsSinceFirstAboard)
        {
            if (aliveAboard <= 0) return false;
            return aliveAboard >= alivePlayers || secondsSinceFirstAboard >= BoardingWindowSeconds;
        }
    }

    /// <summary>
    /// Authority-side escape funding. The cost is based on the most players that have been in the match, so
    /// it never drops when someone leaves (no "quit, fund cheaply, rejoin" exploit) and funding can never get
    /// stuck above a lowered cost.
    /// </summary>
    public sealed class EscapeState
    {
        public int PeakPlayers { get; private set; } = 1;
        public int Funded { get; private set; }
        public bool Active { get; private set; }

        public int Cost => EscapeRules.CostFor(PeakPlayers);
        public bool FullyFunded => Funded >= Cost;

        public void NotePlayers(int count)
        {
            if (!Active) PeakPlayers = Math.Max(PeakPlayers, Math.Min(GameInfo.MaxPlayers, count));
        }

        public int Contribute(int points)
        {
            if (Active) return 0;
            int give = EscapeRules.Contribution(points, Funded, Cost);
            Funded += give;
            return give;
        }

        /// <summary>Starts the launch sequence once funding covers the cost. Safe to call every tick.</summary>
        public bool TryActivate()
        {
            if (Active || !FullyFunded) return false;
            Active = true;
            return true;
        }
    }
}
