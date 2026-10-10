using UnityEngine;
using UnityEngine.Rendering;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Third-person survivor model assembled from primitives using <see cref="CharacterDef"/> colours and
    /// headgear (placeholder art). The local player only renders its shadow.
    /// </summary>
    public sealed class CharacterModel
    {
        private GameObject root;
        private Transform body, armL, armR, legL, legR, hand;
        private bool shadowOnly;
        private Vector3 lastPos;
        private float phase;
        private WeaponId heldWeapon = (WeaponId)(-1);
        private bool heldUpgraded;
        private GameObject heldModel;
        private bool hidden;

        public static CharacterModel Build(Transform parent, CharacterDef def, bool shadowOnly)
        {
            var m = new CharacterModel { shadowOnly = shadowOnly };
            m.root = Geo.Group(parent, "CharacterModel");
            var r = m.root.transform;
            r.localScale = new Vector3(def.BuildScale, def.HeightScale, def.BuildScale);
            var jacket = Palette.Tint(def.Jacket);
            var trousers = Palette.Tint(def.Trousers);
            var skin = Palette.Tint(def.Skin);
            var accent = Palette.Tint(def.Accent);

            m.legL = Limb(r, "LegL", new Vector3(-0.12f, 0.92f, 0f), 0.9f, 0.16f, trousers);
            m.legR = Limb(r, "LegR", new Vector3(0.12f, 0.92f, 0f), 0.9f, 0.16f, trousers);
            m.body = Geo.Group(r, "Body", new Vector3(0, 0.92f, 0)).transform;
            Geo.Box(m.body, "Torso", new Vector3(0, 0.33f, 0), new Vector3(0.46f, 0.62f, 0.26f), jacket, false);
            Geo.Box(m.body, "Belt", new Vector3(0, 0.03f, 0), new Vector3(0.47f, 0.07f, 0.27f), accent, false);
            var head = Geo.Group(m.body, "Head", new Vector3(0, 0.8f, 0)).transform;
            Geo.Sphere(head, "Skull", Vector3.zero, new Vector3(0.26f, 0.3f, 0.27f), skin);
            Geo.Box(head, "Visor", new Vector3(0, 0.02f, 0.125f), new Vector3(0.18f, 0.05f, 0.02f), Palette.DarkMetal, false);
            Headgear(head, def, accent, jacket);
            if (def.Backpack) Geo.Box(m.body, "Pack", new Vector3(0, 0.35f, -0.2f), new Vector3(0.36f, 0.45f, 0.16f), accent, false);
            if (def.Id == 9) Geo.Sphere(head, "Glow", new Vector3(0, 0.05f, 0.13f), new Vector3(0.2f, 0.04f, 0.02f), Palette.LampAlien);

            m.armL = Limb(m.body, "ArmL", new Vector3(-0.3f, 0.6f, 0f), 0.6f, 0.12f, jacket);
            m.armR = Limb(m.body, "ArmR", new Vector3(0.3f, 0.6f, 0f), 0.6f, 0.12f, jacket);
            m.hand = Geo.Group(m.armR, "Hand", new Vector3(0, -0.58f, 0.05f)).transform;

            if (shadowOnly)
                foreach (var renderer in m.root.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            return m;
        }

        private static void Headgear(Transform head, CharacterDef def, Material accent, Material jacket)
        {
            switch (def.Headgear)
            {
                case UZS.Core.Headgear.CombatHelmet:
                    Geo.Sphere(head, "Helmet", new Vector3(0, 0.07f, 0), new Vector3(0.31f, 0.22f, 0.32f), Palette.MilitaryGreen);
                    break;
                case UZS.Core.Headgear.BallCap:
                    Geo.Sphere(head, "Cap", new Vector3(0, 0.09f, 0), new Vector3(0.28f, 0.16f, 0.29f), accent);
                    Geo.Box(head, "Brim", new Vector3(0, 0.07f, 0.17f), new Vector3(0.22f, 0.02f, 0.14f), accent, false);
                    break;
                case UZS.Core.Headgear.Beret:
                    Geo.Sphere(head, "Beret", new Vector3(0.03f, 0.13f, 0), new Vector3(0.3f, 0.08f, 0.3f), accent);
                    break;
                case UZS.Core.Headgear.Hood:
                    Geo.Sphere(head, "Hood", new Vector3(0, 0.03f, -0.03f), new Vector3(0.32f, 0.34f, 0.32f), jacket);
                    break;
                case UZS.Core.Headgear.HardHat:
                    Geo.Sphere(head, "HardHat", new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.17f, 0.31f), Palette.Hazard);
                    Geo.Cylinder(head, "Rim", new Vector3(0, 0.07f, 0), 0.19f, 0.01f, Palette.Hazard, false);
                    break;
                case UZS.Core.Headgear.WideBrimHat:
                    Geo.Cylinder(head, "Crown", new Vector3(0, 0.15f, 0), 0.13f, 0.12f, Palette.DarkWood, false);
                    Geo.Cylinder(head, "Brim", new Vector3(0, 0.09f, 0), 0.27f, 0.01f, Palette.DarkWood, false);
                    Geo.Box(head, "Badge", new Vector3(0, 0.15f, 0.13f), new Vector3(0.04f, 0.04f, 0.01f), accent, false);
                    break;
                case UZS.Core.Headgear.Headset:
                    Geo.Box(head, "Band", new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.03f, 0.05f), Palette.DarkMetal, false);
                    Geo.Box(head, "Mic", new Vector3(-0.1f, -0.06f, 0.12f), new Vector3(0.12f, 0.015f, 0.015f), Palette.DarkMetal, false);
                    break;
                case UZS.Core.Headgear.Goggles:
                    Geo.Box(head, "Goggles", new Vector3(0, 0.05f, 0.13f), new Vector3(0.22f, 0.06f, 0.04f), Palette.LampAlien, false);
                    break;
                case UZS.Core.Headgear.Beanie:
                    Geo.Sphere(head, "Beanie", new Vector3(0, 0.08f, 0), new Vector3(0.28f, 0.2f, 0.29f), accent);
                    break;
            }
        }

        private static Transform Limb(Transform parent, string name, Vector3 pivot, float length, float thickness, Material mat)
        {
            var p = Geo.Group(parent, name, pivot).transform;
            Geo.Box(p, "Seg", new Vector3(0, -length * 0.5f, 0), new Vector3(thickness, length, thickness), mat, false);
            return p;
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        public void Animate(PlayerAvatar avatar)
        {
            if (root == null) return;
            bool hide = avatar.Life == LifeState.Dead || avatar.Life == LifeState.Escaped;
            var match = MatchState.Instance;
            if (match != null && match.Phase == MatchPhase.Lobby) hide = true;
            if (hide != hidden)
            {
                hidden = hide;
                root.SetActive(!hide);
            }
            if (hide) return;

            // Held weapon follows the replicated loadout.
            var active = avatar.Weapons.View.Active;
            if (active.Weapon != heldWeapon || active.Upgraded != heldUpgraded)
            {
                if (heldModel != null) Object.Destroy(heldModel);
                heldWeapon = active.Weapon;
                heldUpgraded = active.Upgraded;
                heldModel = heldWeapon != WeaponId.None ? WeaponModels.Build(hand, heldWeapon, heldUpgraded, 1f) : null;
                if (heldModel != null)
                {
                    heldModel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    if (shadowOnly)
                        foreach (var renderer in heldModel.GetComponentsInChildren<MeshRenderer>())
                            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var pos = avatar.transform.position;
            float speed = new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude / dt;
            lastPos = pos;

            if (avatar.Life == LifeState.Downed)
            {
                root.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
                root.transform.localPosition = new Vector3(0f, 0.25f, -0.6f);
                return;
            }
            root.transform.localRotation = Quaternion.identity;
            root.transform.localPosition = Vector3.zero;

            float moving = Mathf.Clamp01(speed / 4f);
            phase += dt * (6f + speed * 1.2f) * Mathf.Max(0.2f, moving);
            float swing = Mathf.Sin(phase) * 38f * moving;
            legL.localRotation = Quaternion.Euler(swing, 0, 0);
            legR.localRotation = Quaternion.Euler(-swing, 0, 0);
            armL.localRotation = Quaternion.Euler(-55f - swing * 0.2f, 0, 20f);
            armR.localRotation = Quaternion.Euler(-80f, 0, -5f);
        }
    }
}
