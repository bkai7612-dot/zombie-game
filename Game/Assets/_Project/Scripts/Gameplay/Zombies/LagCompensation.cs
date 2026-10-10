using System.Collections.Generic;
using UnityEngine;

namespace UZS.Gameplay
{
    /// <summary>
    /// Light-weight server-side rewind of zombie hitboxes. Clients see zombies slightly in the past
    /// (latency + interpolation), so the authority briefly moves hitboxes to where the shooter saw them
    /// before tracing a shot, then restores them.
    /// </summary>
    public static class LagCompensation
    {
        private const int Capacity = 48;
        public const double MaxRewindSeconds = 0.35;
        public const double InterpolationAllowance = 0.1;

        private struct Sample
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private sealed class History
        {
            public readonly Sample[] Samples = new Sample[Capacity];
            public int Head;
            public int Count;
        }

        private static readonly Dictionary<ZombieAgent, History> Histories = new Dictionary<ZombieAgent, History>();
        private static readonly List<ZombieAgent> Rewound = new List<ZombieAgent>();

        public static void Record(ZombieAgent z)
        {
            if (!Histories.TryGetValue(z, out var h))
            {
                h = new History();
                Histories[z] = h;
            }
            h.Samples[h.Head] = new Sample { Time = Time.timeAsDouble, Position = z.transform.position, Rotation = z.transform.rotation };
            h.Head = (h.Head + 1) % Capacity;
            h.Count = Mathf.Min(Capacity, h.Count + 1);
        }

        public static void Forget(ZombieAgent z) => Histories.Remove(z);

        /// <summary>Seconds to rewind for a shot from <paramref name="clientId"/>.</summary>
        public static double RewindFor(Unity.Netcode.NetworkManager nm, ulong clientId)
        {
            if (nm == null || clientId == nm.LocalClientId) return 0;
            double rtt = nm.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId) / 1000.0;
            return Mathf.Clamp((float)(rtt * 0.5 + InterpolationAllowance), 0f, (float)MaxRewindSeconds);
        }

        public static void Rewind(double seconds)
        {
            Rewound.Clear();
            if (seconds <= 0) return;
            double t = Time.timeAsDouble - seconds;
            foreach (var kv in Histories)
            {
                var z = kv.Key;
                if (z == null || z.IsDead || !TrySample(kv.Value, t, out var pos, out var rot)) continue;
                var hitboxes = z.transform.Find("Hitboxes");
                if (hitboxes == null) continue;
                hitboxes.SetPositionAndRotation(pos, rot);
                Rewound.Add(z);
            }
            if (Rewound.Count > 0) Physics.SyncTransforms();
        }

        public static void Restore()
        {
            if (Rewound.Count == 0) return;
            foreach (var z in Rewound)
            {
                if (z == null) continue;
                var hitboxes = z.transform.Find("Hitboxes");
                if (hitboxes == null) continue;
                hitboxes.localPosition = Vector3.zero;
                hitboxes.localRotation = Quaternion.identity;
            }
            Rewound.Clear();
            Physics.SyncTransforms();
        }

        private static bool TrySample(History h, double t, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = Quaternion.identity;
            if (h.Count == 0) return false;
            Sample newer = default, older = default;
            bool haveNewer = false;
            for (int i = 1; i <= h.Count; i++)
            {
                var s = h.Samples[(h.Head - i + Capacity) % Capacity];
                if (s.Time >= t)
                {
                    newer = s;
                    haveNewer = true;
                    continue;
                }
                older = s;
                if (!haveNewer)
                {
                    pos = s.Position;
                    rot = s.Rotation;
                    return true;
                }
                float k = (float)((t - older.Time) / System.Math.Max(1e-6, newer.Time - older.Time));
                pos = Vector3.Lerp(older.Position, newer.Position, k);
                rot = Quaternion.Slerp(older.Rotation, newer.Rotation, k);
                return true;
            }
            // Requested time is older than the buffer: use the oldest sample.
            pos = newer.Position;
            rot = newer.Rotation;
            return haveNewer;
        }
    }
}
