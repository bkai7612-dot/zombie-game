using System;
using System.Collections.Generic;

namespace UZS.Core
{
    /// <summary>
    /// Chooses zombie spawn points: only in opened zones, in the zone a player is in or one door away from it
    /// (by the door graph, not straight-line distance), never too close to anyone, closest first.
    /// </summary>
    public static class SpawnPlanner
    {
        public const int MaxHops = 1;

        public struct Candidate
        {
            public V3 Position;
            public int Zone;
            public int Hops;
            public float Distance;
        }

        /// <param name="players">Positions of players that zombies should pressure (alive or downed).</param>
        public static List<Candidate> Rank(IList<V3> players, ulong unlockedDoorMask, float minDistance)
        {
            var active = MapCatalog.ActiveZones(unlockedDoorMask);
            var bestHops = new int[MapCatalog.Zones.Length];
            for (int i = 0; i < bestHops.Length; i++) bestHops[i] = int.MaxValue;
            foreach (var p in players)
            {
                var hops = MapCatalog.ZoneHops(MapCatalog.ZoneAt(p.X, p.Y, p.Z), unlockedDoorMask);
                for (int i = 0; i < hops.Length; i++) bestHops[i] = Math.Min(bestHops[i], hops[i]);
            }

            var result = new List<Candidate>();
            var fallback = new List<Candidate>();
            foreach (var zone in MapCatalog.Zones)
            {
                if (!active[zone.Id]) continue;
                foreach (var sp in zone.ZombieSpawns)
                {
                    float nearest = float.MaxValue;
                    foreach (var p in players) nearest = Math.Min(nearest, MapCatalog.Distance(sp, p.X, p.Y, p.Z));
                    if (players.Count > 0 && nearest < minDistance) continue;
                    var c = new Candidate { Position = sp, Zone = zone.Id, Hops = bestHops[zone.Id], Distance = nearest };
                    (c.Hops <= MaxHops ? result : fallback).Add(c);
                }
            }
            // If every nearby point is blocked (e.g. players standing on them), fall back to any opened zone.
            if (result.Count == 0) result = fallback;
            result.Sort((a, b) => a.Hops != b.Hops ? a.Hops.CompareTo(b.Hops) : a.Distance.CompareTo(b.Distance));
            return result;
        }
    }
}
