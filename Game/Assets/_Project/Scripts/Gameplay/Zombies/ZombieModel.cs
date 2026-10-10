using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Primitive placeholder zombie with procedural shamble animation and hitboxes.</summary>
    public sealed class ZombieModel
    {
        public Transform Root;
        public Transform Hitboxes;
        private Transform armL, armR, legL, legR, torso, head;
        private float phase;
        private float deadT = -1f;

        private static readonly Color[] ShirtColors =
        {
            new Color(0.25f, 0.22f, 0.2f), new Color(0.2f, 0.25f, 0.3f), new Color(0.32f, 0.3f, 0.22f),
            new Color(0.35f, 0.15f, 0.12f), new Color(0.24f, 0.28f, 0.17f), new Color(0.4f, 0.4f, 0.38f),
        };

        public static ZombieModel Build(Transform parent, ZombieAgent owner, byte variant)
        {
            var m = new ZombieModel();
            float height = 0.92f + (variant % 7) * 0.025f;
            m.Root = Geo.Group(parent, "Model").transform;
            m.Root.localScale = new Vector3(1f, height, 1f);
            var skin = Palette.Lit("zSkin" + variant % 3, Color.Lerp(new Color(0.36f, 0.42f, 0.33f), new Color(0.45f, 0.4f, 0.36f), (variant % 3) / 2f), 0.15f);
            var shirt = Palette.Tint(ShirtColors[variant % ShirtColors.Length]);
            var pants = Palette.ZombieCloth;

            m.torso = Geo.Group(m.Root, "Torso", new Vector3(0, 1.0f, 0)).transform;
            Geo.Box(m.torso, "Chest", new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.62f, 0.28f), shirt, false);
            Geo.Box(m.torso, "Wound", new Vector3(0.1f, 0.35f, 0.142f), new Vector3(0.14f, 0.18f, 0.01f), Palette.Blood, false);
            m.head = Geo.Group(m.torso, "Head", new Vector3(0, 0.75f, 0.04f)).transform;
            Geo.Sphere(m.head, "Skull", Vector3.zero, new Vector3(0.3f, 0.34f, 0.3f), skin);
            Geo.Sphere(m.head, "EyeL", new Vector3(-0.07f, 0.03f, 0.135f), Vector3.one * 0.045f, Palette.ZombieEyes);
            Geo.Sphere(m.head, "EyeR", new Vector3(0.07f, 0.03f, 0.135f), Vector3.one * 0.045f, Palette.ZombieEyes);
            Geo.Box(m.head, "Jaw", new Vector3(0, -0.11f, 0.08f), new Vector3(0.16f, 0.06f, 0.1f), Palette.Blood, false);

            m.armL = Limb(m.torso, "ArmL", new Vector3(-0.32f, 0.55f, 0f), 0.62f, 0.12f, skin);
            m.armR = Limb(m.torso, "ArmR", new Vector3(0.32f, 0.55f, 0f), 0.62f, 0.12f, skin);
            m.legL = Limb(m.Root, "LegL", new Vector3(-0.13f, 1.0f, 0f), 0.95f, 0.17f, pants);
            m.legR = Limb(m.Root, "LegR", new Vector3(0.13f, 1.0f, 0f), 0.95f, 0.17f, pants);

            // Hitboxes live under their own transform so lag compensation can rewind them without touching the agent.
            m.Hitboxes = Geo.Group(parent, "Hitboxes").transform;
            var bodyGo = Geo.Group(m.Hitboxes, "Body").transform;
            var capsule = bodyGo.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.75f * height, 0);
            capsule.height = 1.5f * height;
            capsule.radius = 0.32f;
            bodyGo.gameObject.AddComponent<Hitbox>().Owner = owner;

            // Head sphere overlaps the top of the body capsule so there is no gap at the neck.
            var headGo = Geo.Group(m.Hitboxes, "Head", new Vector3(0, 1.72f * height, 0.04f)).transform;
            var sphere = headGo.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.22f;
            var hb = headGo.gameObject.AddComponent<Hitbox>();
            hb.Owner = owner;
            hb.IsHead = true;
            return m;
        }

        private static Transform Limb(Transform parent, string name, Vector3 pivot, float length, float thickness, Material mat)
        {
            var p = Geo.Group(parent, name, pivot).transform;
            Geo.Box(p, "Seg", new Vector3(0, -length * 0.5f, 0), new Vector3(thickness, length, thickness), mat, false);
            return p;
        }

        public void Animate(float velocity, float attack, ZombieSpeed tier, float emerge = 1f)
        {
            if (deadT >= 0f)
            {
                deadT = Mathf.Min(1f, deadT + Time.deltaTime * 2.2f);
                Root.localRotation = Quaternion.Euler(-88f * deadT * deadT, 0f, 0f);
                Root.localPosition = new Vector3(0f, 0.15f * deadT, 0f);
                return;
            }

            // Rising out of the ground when freshly spawned.
            Root.localPosition = new Vector3(0f, -1.7f * (1f - emerge), 0f);

            float rate = tier == ZombieSpeed.Walk ? 4.5f : (tier == ZombieSpeed.Run ? 9f : 12f);
            float moving = Mathf.Clamp01(velocity / 1.5f);
            phase += Time.deltaTime * rate * Mathf.Max(0.25f, moving);
            float swing = Mathf.Sin(phase) * 35f * moving;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);

            float reach = tier == ZombieSpeed.Walk ? -80f : -55f;
            float strike = attack > 0f ? Mathf.Sin(attack * Mathf.PI) * 70f : 0f;
            armL.localRotation = Quaternion.Euler(reach + Mathf.Sin(phase * 0.5f) * 8f + strike, 0, -6f);
            armR.localRotation = Quaternion.Euler(reach - Mathf.Sin(phase * 0.5f) * 8f + strike, 0, 6f);
            torso.localRotation = Quaternion.Euler((tier == ZombieSpeed.Sprint ? 18f : 8f) + Mathf.Sin(phase * 2f) * 2f, 0, Mathf.Sin(phase) * 4f);
            head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * 0.7f) * 10f);
        }

        public void SetDead()
        {
            if (deadT < 0f) deadT = 0f;
        }
    }
}
