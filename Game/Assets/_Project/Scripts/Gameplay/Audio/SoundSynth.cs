using System;
using UnityEngine;

namespace UZS.Gameplay
{
    public enum Sfx
    {
        ShotPistol, ShotSmg, ShotShotgun, ShotRifle, ShotMarksman, ShotPlasma,
        Reload, DryFire, Melee, HitMarker, KillMarker,
        ZombieGroan, ZombieAttack, ZombieDeath,
        Purchase, Denied, DoorOpen, BoxRoll, BoxOffer, PerkDrink, Upgrade,
        RoundStart, RoundEnd, PlayerHurt, Downed, Revived, UfoCharge, UfoLaunch, Footstep,
        AmbienceWind, AmbienceDrone,
    }

    /// <summary>
    /// Generates every sound procedurally at startup (original, licence-free placeholder audio).
    /// Replace with recorded assets later by swapping <see cref="AudioManager"/>'s clip table.
    /// </summary>
    public static class SoundSynth
    {
        private const int Rate = 44100;

        public static AudioClip Create(Sfx id, int seed)
        {
            var rng = new System.Random(seed + (int)id * 7919);
            switch (id)
            {
                case Sfx.ShotPistol: return Gunshot("pistol", rng, 0.28f, 900f, 0.55f, 160f);
                case Sfx.ShotSmg: return Gunshot("smg", rng, 0.18f, 1400f, 0.45f, 200f);
                case Sfx.ShotShotgun: return Gunshot("shotgun", rng, 0.55f, 500f, 0.9f, 90f);
                case Sfx.ShotRifle: return Gunshot("rifle", rng, 0.32f, 1100f, 0.65f, 130f);
                case Sfx.ShotMarksman: return Gunshot("marksman", rng, 0.7f, 700f, 0.85f, 70f);
                case Sfx.ShotPlasma: return Sweep("plasma", 0.35f, 1800f, 300f, 0.35f, rng, 0.15f);
                case Sfx.Reload: return Clicks("reload", rng, new[] { 0.02f, 0.35f, 0.62f }, 0.8f);
                case Sfx.DryFire: return Clicks("dry", rng, new[] { 0.01f }, 0.15f);
                case Sfx.Melee: return Whoosh("melee", rng, 0.25f);
                case Sfx.HitMarker: return Tone("hit", 0.06f, 2400f, 0.35f);
                case Sfx.KillMarker: return Tone("kill", 0.12f, 1600f, 0.4f, 2200f);
                case Sfx.ZombieGroan: return Groan("groan", rng, 1.3f, 85f);
                case Sfx.ZombieAttack: return Groan("attack", rng, 0.6f, 140f);
                case Sfx.ZombieDeath: return Groan("zdeath", rng, 0.9f, 60f);
                case Sfx.Purchase: return Chime("purchase", new[] { 880f, 1320f }, 0.35f);
                case Sfx.Denied: return Tone("denied", 0.25f, 180f, 0.4f, 140f);
                case Sfx.DoorOpen: return Rumble("door", rng, 1.4f);
                case Sfx.BoxRoll: return Chime("boxroll", new[] { 523f, 659f, 784f, 1046f, 784f, 659f, 523f, 659f, 784f, 1046f }, 3f);
                case Sfx.BoxOffer: return Chime("boxoffer", new[] { 1046f, 1568f }, 0.5f);
                case Sfx.PerkDrink: return Chime("perk", new[] { 392f, 523f, 659f, 784f }, 0.9f);
                case Sfx.Upgrade: return Sweep("upgrade", 1.4f, 200f, 1600f, 0.35f, rng, 0.05f);
                case Sfx.RoundStart: return Chime("roundstart", new[] { 196f, 233f, 196f, 147f }, 2.2f, 0.9f);
                case Sfx.RoundEnd: return Chime("roundend", new[] { 147f, 196f, 233f, 294f }, 2.2f, 0.9f);
                case Sfx.PlayerHurt: return Groan("hurt", rng, 0.3f, 220f);
                case Sfx.Downed: return Sweep("downed", 1.2f, 600f, 90f, 0.4f, rng, 0.1f);
                case Sfx.Revived: return Chime("revived", new[] { 523f, 784f, 1046f }, 0.7f);
                case Sfx.UfoCharge: return Sweep("ufocharge", 4f, 60f, 420f, 0.4f, rng, 0.04f);
                case Sfx.UfoLaunch: return Sweep("ufolaunch", 3f, 900f, 40f, 0.5f, rng, 0.2f);
                case Sfx.Footstep: return Thump("step", rng, 0.12f);
                case Sfx.AmbienceWind: return Wind("wind", rng, 8f);
                case Sfx.AmbienceDrone: return Drone("drone", 8f);
                default: return Tone("blip", 0.1f, 440f, 0.3f);
            }
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        private static AudioClip Gunshot(string name, System.Random r, float len, float cutoff, float gain, float body)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            float lp = 0f;
            float a = Mathf.Clamp01(cutoff / Rate * 6.28f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * (14f / len));
                lp += (Noise(r) - lp) * a;
                float crack = i < Rate * 0.004f ? Noise(r) : 0f;
                float boom = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-t * 30f);
                d[i] = Mathf.Clamp((lp * 1.6f + crack * 0.8f + boom * 0.9f) * env * gain, -1f, 1f);
            }
            return Make(name, d);
        }

        private static AudioClip Sweep(string name, float len, float f0, float f1, float gain, System.Random r, float noise)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += 2.0 * Math.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.2f)) * (1f - t * 0.3f);
                d[i] = ((float)Math.Sin(phase) * 0.7f + (float)Math.Sin(phase * 1.5) * 0.3f + Noise(r) * noise) * env * gain;
            }
            return Make(name, d);
        }

        private static AudioClip Tone(string name, float len, float f0, float gain, float f1 = -1f)
        {
            if (f1 < 0) f1 = f0;
            int n = (int)(len * Rate);
            var d = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2.0 * Math.PI * Mathf.Lerp(f0, f1, t) / Rate;
                d[i] = (float)Math.Sin(phase) * (1f - t) * gain;
            }
            return Make(name, d);
        }

        private static AudioClip Chime(string name, float[] notes, float len, float gain = 0.35f)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            int per = n / notes.Length;
            for (int k = 0; k < notes.Length; k++)
            {
                for (int i = 0; i < per * 2 && k * per + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 5f);
                    float s = Mathf.Sin(2f * Mathf.PI * notes[k] * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * notes[k] * t) * 0.2f;
                    d[k * per + i] += s * env * gain;
                }
            }
            return Make(name, d);
        }

        private static AudioClip Clicks(string name, System.Random r, float[] times, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            foreach (var time in times)
            {
                int start = (int)(time * Rate);
                for (int i = 0; i < Rate * 0.03f && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    d[start + i] += (Noise(r) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.4f) * Mathf.Exp(-t * 180f) * 0.6f;
                }
            }
            return Make(name, d);
        }

        private static AudioClip Whoosh(string name, System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += (Noise(r) - lp) * Mathf.Lerp(0.02f, 0.2f, Mathf.Sin(Mathf.PI * t));
                d[i] = lp * Mathf.Sin(Mathf.PI * t) * 1.2f;
            }
            return Make(name, d);
        }

        private static AudioClip Groan(string name, System.Random r, float len, float pitch)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            double phase = 0;
            float lp = 0;
            float wobble = (float)r.NextDouble() * 3f + 2f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = pitch * (1f + 0.15f * Mathf.Sin(t * wobble * 6.28f)) * (1f - 0.2f * t);
                phase += 2.0 * Math.PI * f / Rate;
                float saw = (float)((phase / Math.PI) % 2.0 - 1.0);
                lp += (saw + Noise(r) * 0.4f - lp) * 0.08f;
                float env = Mathf.Sin(Mathf.PI * t);
                d[i] = lp * env * 0.7f;
            }
            return Make(name, d);
        }

        private static AudioClip Rumble(string name, System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += (Noise(r) - lp) * 0.01f;
                float clank = (i % (Rate / 6)) < 200 ? Noise(r) * 0.3f : 0f;
                d[i] = (lp * 4f + clank) * Mathf.Sin(Mathf.PI * t) * 0.6f;
            }
            return Make(name, d);
        }

        private static AudioClip Thump(string name, System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                d[i] = (Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.6f + Noise(r) * 0.2f) * Mathf.Exp(-t * 40f) * 0.5f;
            }
            return Make(name, d);
        }

        private static AudioClip Wind(string name, System.Random r, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float gust = 0.5f + 0.5f * Mathf.Sin(t * 6.28f * 2f) * Mathf.Sin(t * 6.28f * 0.5f + 1f);
                lp += (Noise(r) - lp) * (0.01f + 0.02f * gust);
                lp2 += (lp - lp2) * 0.05f;
                d[i] = lp2 * 3f * (0.4f + 0.6f * gust);
            }
            // Cross-fade the ends so the loop is seamless.
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                d[i] = d[i] * w + d[n - fade + i] * (1f - w);
            }
            Array.Resize(ref d, n - fade);
            return Make(name, d);
        }

        private static AudioClip Drone(string name, float len)
        {
            int n = (int)(len * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                // Whole-number cycles over the clip length keep the loop click-free.
                d[i] = (float)(Math.Sin(2 * Math.PI * 55 * t) * 0.3 + Math.Sin(2 * Math.PI * 82.5 * t) * 0.15 +
                               Math.Sin(2 * Math.PI * 110.25 * t) * 0.08) * (float)(0.7 + 0.3 * Math.Sin(2 * Math.PI * t / len * 2));
            }
            return Make(name, d);
        }
    }
}
