using System.Collections.Generic;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Pooled, purely cosmetic shot effects (tracers, impacts). Never affects gameplay.</summary>
    public sealed class Effects : MonoBehaviour
    {
        private const int TracerPool = 40;
        private const int ImpactPool = 40;

        private static Effects instance;

        private readonly List<LineRenderer> tracers = new List<LineRenderer>();
        private readonly List<float> tracerLife = new List<float>();
        private readonly List<Transform> impacts = new List<Transform>();
        private readonly List<float> impactLife = new List<float>();
        private int nextTracer, nextImpact;

        /// <summary>Where local tracers start (the view-model muzzle) - set by the view model.</summary>
        public static Transform LocalMuzzle;

        private static Effects Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Effects");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Effects>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            for (int i = 0; i < TracerPool; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = 0.025f;
                lr.endWidth = 0.01f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                tracers.Add(lr);
                tracerLife.Add(0f);
            }
            for (int i = 0; i < ImpactPool; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(go.GetComponent<Collider>());
                go.name = "Impact";
                go.transform.SetParent(transform);
                go.transform.localScale = Vector3.one * 0.08f;
                go.SetActive(false);
                impacts.Add(go.transform);
                impactLife.Add(0f);
            }
        }

        public static void Shot(Vector3 origin, Vector3 direction, int seed, WeaponId weapon, bool upgraded, bool local)
        {
            var stats = WeaponCatalog.Get(weapon);
            var fx = Instance;
            var start = local && LocalMuzzle != null ? LocalMuzzle.position : origin + direction * 0.6f;
            var mat = weapon == WeaponId.PlasmaRifle || upgraded ? Palette.TracerPlasma : Palette.Tracer;
            for (int i = 0; i < stats.Pellets; i++)
            {
                var dir = Ballistics.Spread(direction, stats.SpreadDegrees, seed, i);
                var hit = Ballistics.Trace(origin, dir, stats.Range);
                fx.Tracer(start, hit.Point, mat, weapon == WeaponId.PlasmaRifle ? 0.08f : 0.05f);
                if (hit.Hit) fx.Impact(hit.Point, hit.Normal, hit.Hitbox != null);
            }
            if (AudioManager.Instance == null) return;
            if (local) AudioManager.Instance.Play2D(AudioManager.ShotSound(weapon), 0.7f, upgraded ? 0.85f : 1f);
            else AudioManager.Instance.PlayAt(AudioManager.ShotSound(weapon), origin, 1f, 0.05f, 70f);
        }

        private void Tracer(Vector3 a, Vector3 b, Material mat, float life)
        {
            var lr = tracers[nextTracer];
            tracerLife[nextTracer] = life;
            nextTracer = (nextTracer + 1) % tracers.Count;
            lr.sharedMaterial = mat;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.enabled = true;
        }

        private void Impact(Vector3 point, Vector3 normal, bool flesh)
        {
            var t = impacts[nextImpact];
            impactLife[nextImpact] = 0.25f;
            nextImpact = (nextImpact + 1) % impacts.Count;
            t.position = point + normal * 0.03f;
            t.rotation = Random.rotation;
            t.GetComponent<MeshRenderer>().sharedMaterial = flesh ? Palette.Blood : Palette.Spark;
            t.localScale = Vector3.one * (flesh ? 0.14f : 0.06f);
            t.gameObject.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < tracers.Count; i++)
            {
                if (!tracers[i].enabled) continue;
                tracerLife[i] -= dt;
                if (tracerLife[i] <= 0f) tracers[i].enabled = false;
            }
            for (int i = 0; i < impacts.Count; i++)
            {
                if (!impacts[i].gameObject.activeSelf) continue;
                impactLife[i] -= dt;
                impacts[i].localScale *= 1f - dt * 3f;
                if (impactLife[i] <= 0f) impacts[i].gameObject.SetActive(false);
            }
        }
    }
}
