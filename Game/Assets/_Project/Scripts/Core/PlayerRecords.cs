using System.Collections.Generic;

namespace UZS.Core
{
    /// <summary>Authority-side copy of a disconnected player's progress.</summary>
    public sealed class PlayerSnapshot
    {
        public int Points;
        public WeaponSlot Slot0;
        public WeaponSlot Slot1;
        public int ActiveSlot;
        public PerkId Perks;
        public int Kills;
        public int Headshots;
        public int Revives;
        public int Downs;
        public LifeState LifeState;
    }

    /// <summary>
    /// Keeps snapshots of players who left mid-match so a reconnect restores (not re-grants) their progress.
    /// A snapshot is consumed exactly once, so reconnecting repeatedly cannot duplicate points or purchases.
    /// </summary>
    public sealed class ReconnectLedger
    {
        private readonly Dictionary<string, PlayerSnapshot> snapshots = new Dictionary<string, PlayerSnapshot>();
        private readonly HashSet<string> connectedKeys = new HashSet<string>();

        public int StoredCount => snapshots.Count;

        /// <summary>
        /// Registers a joining key. If the key is already in use by a connected player (e.g. two game
        /// instances on one PC share saved settings) a unique suffix is appended and returned.
        /// </summary>
        public string Claim(string key)
        {
            string unique = key;
            int n = 2;
            while (connectedKeys.Contains(unique)) unique = key + "#" + n++;
            connectedKeys.Add(unique);
            return unique;
        }

        public void Release(string key, PlayerSnapshot snapshot)
        {
            connectedKeys.Remove(key);
            if (snapshot != null) snapshots[key] = snapshot;
        }

        public bool TryConsume(string key, out PlayerSnapshot snapshot)
        {
            if (snapshots.TryGetValue(key, out snapshot))
            {
                snapshots.Remove(key);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            snapshots.Clear();
            connectedKeys.Clear();
        }
    }
}
