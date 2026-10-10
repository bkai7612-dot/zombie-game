using System.Collections.Generic;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Cached, tinted material variants derived from the base materials in <see cref="GameContent"/>.</summary>
    public static class Palette
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        // Environment.
        public static Material Asphalt => Lit("asphalt", new Color(0.13f, 0.13f, 0.14f), 0.25f);
        public static Material Sidewalk => Lit("sidewalk", new Color(0.42f, 0.41f, 0.39f), 0.1f);
        public static Material Grass => Lit("grass", new Color(0.16f, 0.22f, 0.12f), 0.05f);
        public static Material Dirt => Lit("dirt", new Color(0.25f, 0.2f, 0.15f), 0.05f);
        public static Material Concrete => Lit("concrete", new Color(0.46f, 0.46f, 0.45f), 0.15f);
        public static Material Siding => Lit("siding", new Color(0.55f, 0.53f, 0.47f), 0.1f);
        public static Material SidingBlue => Lit("sidingBlue", new Color(0.33f, 0.41f, 0.47f), 0.1f);
        public static Material Brick => Lit("brick", new Color(0.42f, 0.2f, 0.16f), 0.05f);
        public static Material Wood => Lit("wood", new Color(0.36f, 0.25f, 0.16f), 0.15f);
        public static Material DarkWood => Lit("darkWood", new Color(0.2f, 0.14f, 0.09f), 0.15f);
        public static Material Roof => Lit("roof", new Color(0.19f, 0.17f, 0.17f), 0.1f);
        public static Material Metal => Lit("metal", new Color(0.35f, 0.37f, 0.39f), 0.55f, 0.6f);
        public static Material DarkMetal => Lit("darkMetal", new Color(0.12f, 0.12f, 0.13f), 0.45f, 0.7f);
        public static Material Rust => Lit("rust", new Color(0.36f, 0.2f, 0.12f), 0.15f, 0.3f);
        public static Material MilitaryGreen => Lit("milGreen", new Color(0.24f, 0.28f, 0.17f), 0.2f);
        public static Material Canvas => Lit("canvas", new Color(0.4f, 0.38f, 0.28f), 0.05f);
        public static Material Sandbag => Lit("sandbag", new Color(0.48f, 0.42f, 0.3f), 0.05f);
        public static Material Hazard => Lit("hazard", new Color(0.85f, 0.65f, 0.1f), 0.3f);
        public static Material LabWhite => Lit("labWhite", new Color(0.72f, 0.74f, 0.75f), 0.4f);
        public static Material LabFloor => Lit("labFloor", new Color(0.3f, 0.33f, 0.35f), 0.5f);
        public static Material HangarMetal => Lit("hangar", new Color(0.22f, 0.24f, 0.26f), 0.5f, 0.5f);
        public static Material UfoHull => Lit("ufoHull", new Color(0.6f, 0.63f, 0.66f), 0.85f, 0.9f);
        public static Material Glass => Trans("glass", new Color(0.4f, 0.55f, 0.6f, 0.35f));
        public static Material Window => Emit("window", new Color(0.05f, 0.06f, 0.07f), new Color(0.02f, 0.025f, 0.03f));

        // Lights / readability accents.
        public static Material LampWarm => Emit("lampWarm", new Color(1f, 0.85f, 0.6f), new Color(2.2f, 1.7f, 1.0f));
        public static Material LampRed => Emit("lampRed", new Color(0.9f, 0.1f, 0.1f), new Color(2.5f, 0.2f, 0.15f));
        public static Material LampAlien => Emit("lampAlien", new Color(0.3f, 1f, 0.6f), new Color(0.6f, 2.8f, 1.4f));
        public static Material LampBlue => Emit("lampBlue", new Color(0.4f, 0.7f, 1f), new Color(0.6f, 1.4f, 2.6f));
        public static Material LampPurple => Emit("lampPurple", new Color(0.7f, 0.3f, 1f), new Color(1.6f, 0.5f, 2.6f));
        public static Material Chalk => Emit("chalk", new Color(0.95f, 0.9f, 0.75f), new Color(1.3f, 1.2f, 0.95f));
        public static Material LockedStripe => Emit("locked", new Color(0.8f, 0.15f, 0.1f), new Color(1.8f, 0.25f, 0.15f));
        public static Material Screen => Emit("screen", new Color(0.2f, 0.9f, 0.5f), new Color(0.4f, 1.8f, 0.9f));
        public static Material Moon => Emit("moon", new Color(0.9f, 0.92f, 1f), new Color(1.6f, 1.65f, 1.8f));

        // Characters / zombies / FX.
        public static Material ZombieSkin => Lit("zSkin", new Color(0.36f, 0.42f, 0.33f), 0.15f);
        public static Material ZombieCloth => Lit("zCloth", new Color(0.22f, 0.2f, 0.19f), 0.05f);
        public static Material ZombieEyes => Emit("zEyes", new Color(1f, 0.75f, 0.2f), new Color(2.4f, 1.6f, 0.3f));
        public static Material Blood => Lit("blood", new Color(0.35f, 0.02f, 0.02f), 0.6f);
        public static Material WeaponBody => Lit("weapon", new Color(0.14f, 0.14f, 0.15f), 0.55f, 0.6f);
        public static Material WeaponFurniture => Lit("weaponFurn", new Color(0.24f, 0.2f, 0.16f), 0.25f);
        public static Material WeaponUpgraded => Emit("weaponUp", new Color(0.35f, 0.15f, 0.5f), new Color(0.9f, 0.3f, 1.5f));
        public static Material PlasmaGlow => Emit("plasma", new Color(0.3f, 1f, 0.7f), new Color(0.8f, 3f, 1.8f));
        public static Material Tracer => Unlit("tracer", new Color(1f, 0.85f, 0.5f));
        public static Material TracerPlasma => Unlit("tracerPlasma", new Color(0.4f, 1f, 0.7f));
        public static Material Muzzle => Unlit("muzzle", new Color(1f, 0.8f, 0.4f));
        public static Material Spark => Unlit("spark", new Color(1f, 0.7f, 0.3f));

        public static Material Tint(Color c) => Lit("tint_" + ColorUtility.ToHtmlStringRGB(c), c, 0.15f);

        public static Material Tint(Rgb c) => Tint(new Color(c.R, c.G, c.B));

        public static Material Lit(string key, Color color, float smoothness, float metallic = 0f)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseOrFallback(c => c.Lit)) { name = key, color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        public static Material Emit(string key, Color color, Color emission)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseOrFallback(c => c.Emissive)) { name = key, color = color };
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        public static Material Trans(string key, Color color)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseOrFallback(c => c.Transparent)) { name = key, color = color };
            Cache[key] = m;
            return m;
        }

        public static Material Unlit(string key, Color color)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(BaseOrFallback(c => c.Unlit)) { name = key, color = color };
            Cache[key] = m;
            return m;
        }

        private static Material BaseOrFallback(System.Func<GameContent, Material> pick)
        {
            var content = GameContent.Instance;
            var mat = content != null ? pick(content) : null;
            if (mat != null) return mat;
            // Fallback only for editor experiments without the generated content asset.
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }
    }
}
