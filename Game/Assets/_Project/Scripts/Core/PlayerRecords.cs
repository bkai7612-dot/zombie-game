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

    public enum ReleaseResult
    {
        /// <summary>The key was released; a snapshot (if any) was stored for a later reconnect.</summary>
        Stored,
        /// <summary>The same player already reconnected on a new connection; apply the snapshot to that player now.</summary>
        TakenOver,
    }

    /// <summary>
    /// Keeps snapshots of players who left mid-match so a reconnect restores (not re-grants) their progress.
    /// A snapshot is consumed exactly once, so reconnecting repeatedly cannot duplicate points or purchases.
    /// If a player reconnects before the host noticed the old connection dropped, the new connection takes the
    /// key over (the caller disconnects the stale one) and the old progress is handed to the new connection.
    /// </summary>
    public sealed class ReconnectLedger
    {
        private readonly Dictionary<string, PlayerSnapshot> snapshots = new Dictionary<string, PlayerSnapshot>();
        private readonly Dictionary<string, ulong> holders = new Dictionary<string, ulong>();

        public int StoredCount => snapshots.Count;

        public bool IsHeld(string key) => holders.ContainsKey(key);

        /// <summary>Registers <paramref name="clientId"/> as the holder of <paramref name="key"/>. Returns the previous holder, if any, which the caller should disconnect.</summary>
        public ulong? Claim(string key, ulong clientId)
        {
            ulong? previous = null;
            if (holders.TryGetValue(key, out var old) && old != clientId) previous = old;
            holders[key] = clientId;
            return previous;
        }

        public ReleaseResult Release(string key, ulong clientId, PlayerSnapshot snapshot)
        {
            if (holders.TryGetValue(key, out var holder) && holder != clientId) return ReleaseResult.TakenOver;
            holders.Remove(key);
            if (snapshot != null) snapshots[key] = snapshot;
            return ReleaseResult.Stored;
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

        public bool TryGetHolder(string key, out ulong clientId) => holders.TryGetValue(key, out clientId);

        public void Clear()
        {
            snapshots.Clear();
            holders.Clear();
        }
    }
}
