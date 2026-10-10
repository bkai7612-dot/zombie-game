using UnityEngine;
using UnityEngine.AI;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Authority-side search for a free, walkable spot near a teammate (respawns and late joins).</summary>
    public static class SpawnSpot
    {
        private static readonly float[] Rings = { 1.6f, 2.6f, 3.6f };

        public static Vector3 Near(Vector3 anchor, Vector3 fallback)
        {
            foreach (float r in Rings)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    var candidate = anchor + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.0f, NavMesh.AllAreas)) continue;
                    var p = hit.position;
                    if (Mathf.Abs(p.y - anchor.y) > 1.5f) continue;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!Ballistics.ClearLine(anchor + Vector3.up, p + Vector3.up)) continue;
                    return p + Vector3.up * 0.1f;
                }
            }
            return fallback;
        }

        /// <summary>Next to the first living teammate, or the start street when nobody is alive.</summary>
        public static Vector3 NearLivingTeammate(int fallbackIndex)
        {
            var start = Geo.V(MapCatalog.PlayerSpawns[fallbackIndex % MapCatalog.PlayerSpawns.Length]) + Vector3.up * 0.2f;
            foreach (var p in PlayerAvatar.All)
                if (p.Life == LifeState.Alive) return Near(p.transform.position, start);
            return start;
        }
    }
}
