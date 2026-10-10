using UnityEngine;

namespace UZS.Gameplay
{
    /// <summary>Shared hitscan tracing used by the authority (for damage) and by clients (for tracer visuals).</summary>
    public static class Ballistics
    {
        private static readonly RaycastHit[] Buffer = new RaycastHit[48];

        public struct Result
        {
            public bool Hit;
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;
            public Hitbox Hitbox;
        }

        /// <summary>First thing a bullet hits, ignoring all players (no friendly fire / self hits) and dead zombies.</summary>
        public static Result Trace(Vector3 origin, Vector3 direction, float range)
        {
            int n = Physics.RaycastNonAlloc(origin, direction, Buffer, range, ~0, QueryTriggerInteraction.Ignore);
            Result best = new Result { Point = origin + direction * range, Distance = range };
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Buffer[i];
                if (h.distance >= bestDist) continue;
                if (h.collider.GetComponentInParent<PlayerAvatar>() != null) continue;
                var hb = h.collider.GetComponent<Hitbox>();
                if (hb != null && (hb.Owner == null || hb.Owner.IsDead)) continue;
                bestDist = h.distance;
                best = new Result { Hit = true, Point = h.point, Normal = h.normal, Distance = h.distance, Hitbox = hb };
            }
            return best;
        }

        /// <summary>True if nothing solid (world geometry) lies between two points. Players and zombies don't block.</summary>
        public static bool ClearLine(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(from, dir / dist, Buffer, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Buffer[i].collider;
                if (c.GetComponentInParent<PlayerAvatar>() != null) continue;
                if (c.GetComponent<Hitbox>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>Deterministic pellet/bullet spread shared by client visuals and authority damage.</summary>
        public static Vector3 Spread(Vector3 forward, float spreadDegrees, int seed, int pellet)
        {
            if (spreadDegrees <= 0f) return forward.normalized;
            var rng = new System.Random(seed * 397 + pellet * 7919 + 17);
            double angle = rng.NextDouble() * System.Math.PI * 2.0;
            double radius = System.Math.Sqrt(rng.NextDouble()) * spreadDegrees;
            var rot = Quaternion.LookRotation(forward.normalized);
            var offset = Quaternion.Euler((float)(System.Math.Sin(angle) * radius), (float)(System.Math.Cos(angle) * radius), 0f);
            return (rot * offset) * Vector3.forward;
        }
    }
}
