using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Builds placeholder weapon meshes from primitives. The model points along +Z; a child "Muzzle" marks the barrel tip.</summary>
    public static class WeaponModels
    {
        public static GameObject Build(Transform parent, WeaponId id, bool upgraded, float scale = 1f, Material overrideMat = null)
        {
            var root = Geo.Group(parent, "Weapon_" + id);
            root.transform.localScale = Vector3.one * scale;
            var t = root.transform;
            var body = overrideMat != null ? overrideMat : (upgraded ? Palette.WeaponUpgraded : Palette.WeaponBody);
            var furn = overrideMat != null ? overrideMat : Palette.WeaponFurniture;
            var glow = overrideMat != null ? overrideMat : Palette.PlasmaGlow;
            float muzzleZ;

            switch (id)
            {
                case WeaponId.Smg:
                    Geo.Box(t, "receiver", new Vector3(0, 0, 0.05f), new Vector3(0.06f, 0.09f, 0.34f), body, false);
                    Geo.Box(t, "barrel", new Vector3(0, 0.015f, 0.28f), new Vector3(0.03f, 0.03f, 0.14f), body, false);
                    Geo.Box(t, "mag", new Vector3(0, -0.11f, 0.08f), new Vector3(0.035f, 0.16f, 0.05f), body, false);
                    Geo.Box(t, "grip", new Vector3(0, -0.09f, -0.06f), new Vector3(0.04f, 0.12f, 0.05f), furn, false);
                    Geo.Box(t, "stock", new Vector3(0, -0.01f, -0.2f), new Vector3(0.03f, 0.05f, 0.16f), body, false);
                    muzzleZ = 0.36f;
                    break;
                case WeaponId.Shotgun:
                    Geo.Cylinder(t, "barrel", new Vector3(0, 0.03f, 0.25f), 0.018f, 0.7f, body, false, new Vector3(90, 0, 0));
                    Geo.Cylinder(t, "tube", new Vector3(0, -0.01f, 0.2f), 0.016f, 0.55f, body, false, new Vector3(90, 0, 0));
                    Geo.Box(t, "pump", new Vector3(0, -0.01f, 0.22f), new Vector3(0.05f, 0.05f, 0.16f), furn, false);
                    Geo.Box(t, "receiver", new Vector3(0, 0.01f, -0.08f), new Vector3(0.06f, 0.08f, 0.2f), body, false);
                    Geo.Box(t, "stock", new Vector3(0, -0.03f, -0.32f), new Vector3(0.05f, 0.09f, 0.3f), furn, false);
                    muzzleZ = 0.6f;
                    break;
                case WeaponId.AssaultRifle:
                    Geo.Box(t, "receiver", new Vector3(0, 0, 0f), new Vector3(0.06f, 0.09f, 0.4f), body, false);
                    Geo.Box(t, "handguard", new Vector3(0, 0.005f, 0.3f), new Vector3(0.055f, 0.07f, 0.22f), furn, false);
                    Geo.Box(t, "barrel", new Vector3(0, 0.02f, 0.5f), new Vector3(0.025f, 0.025f, 0.2f), body, false);
                    Geo.Rotated(t, "mag", new Vector3(0, -0.12f, 0.08f), new Vector3(0.035f, 0.17f, 0.06f), new Vector3(-12, 0, 0), body, false);
                    Geo.Box(t, "grip", new Vector3(0, -0.09f, -0.08f), new Vector3(0.04f, 0.11f, 0.05f), furn, false);
                    Geo.Box(t, "stock", new Vector3(0, -0.02f, -0.32f), new Vector3(0.05f, 0.1f, 0.24f), body, false);
                    Geo.Box(t, "sight", new Vector3(0, 0.07f, 0.02f), new Vector3(0.03f, 0.04f, 0.08f), body, false);
                    muzzleZ = 0.6f;
                    break;
                case WeaponId.MarksmanRifle:
                    Geo.Box(t, "receiver", new Vector3(0, 0, 0f), new Vector3(0.06f, 0.09f, 0.45f), body, false);
                    Geo.Box(t, "barrel", new Vector3(0, 0.02f, 0.45f), new Vector3(0.025f, 0.025f, 0.45f), body, false);
                    Geo.Cylinder(t, "scope", new Vector3(0, 0.09f, 0.03f), 0.028f, 0.3f, body, false, new Vector3(90, 0, 0));
                    Geo.Box(t, "mag", new Vector3(0, -0.09f, 0.05f), new Vector3(0.035f, 0.1f, 0.07f), body, false);
                    Geo.Box(t, "stock", new Vector3(0, -0.03f, -0.36f), new Vector3(0.05f, 0.11f, 0.3f), furn, false);
                    muzzleZ = 0.68f;
                    break;
                case WeaponId.PlasmaRifle:
                    Geo.Box(t, "body", new Vector3(0, 0, 0.05f), new Vector3(0.09f, 0.11f, 0.48f), Palette.UfoHull, false);
                    Geo.Cylinder(t, "coil1", new Vector3(0, 0.0f, 0.18f), 0.06f, 0.04f, glow, false, new Vector3(90, 0, 0));
                    Geo.Cylinder(t, "coil2", new Vector3(0, 0.0f, 0.26f), 0.055f, 0.04f, glow, false, new Vector3(90, 0, 0));
                    Geo.Cylinder(t, "coil3", new Vector3(0, 0.0f, 0.34f), 0.05f, 0.04f, glow, false, new Vector3(90, 0, 0));
                    Geo.Sphere(t, "core", new Vector3(0, 0.07f, -0.05f), new Vector3(0.07f, 0.05f, 0.12f), glow);
                    Geo.Box(t, "grip", new Vector3(0, -0.1f, -0.06f), new Vector3(0.04f, 0.12f, 0.05f), body, false);
                    muzzleZ = 0.4f;
                    break;
                default: // Pistol
                    Geo.Box(t, "slide", new Vector3(0, 0.02f, 0.04f), new Vector3(0.04f, 0.05f, 0.22f), body, false);
                    Geo.Rotated(t, "grip", new Vector3(0, -0.06f, -0.04f), new Vector3(0.038f, 0.12f, 0.055f), new Vector3(-15, 0, 0), furn, false);
                    muzzleZ = 0.16f;
                    break;
            }

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(t, false);
            muzzle.transform.localPosition = new Vector3(0, 0.02f, muzzleZ);
            return root;
        }

        public static GameObject Knife(Transform parent)
        {
            var root = Geo.Group(parent, "Knife");
            Geo.Box(root.transform, "blade", new Vector3(0, 0, 0.12f), new Vector3(0.008f, 0.03f, 0.18f), Palette.Metal, false);
            Geo.Box(root.transform, "handle", new Vector3(0, 0, -0.02f), new Vector3(0.025f, 0.03f, 0.1f), Palette.DarkWood, false);
            return root;
        }
    }
}
