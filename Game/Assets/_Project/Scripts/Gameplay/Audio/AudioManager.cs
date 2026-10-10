using System.Collections.Generic;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Pooled one-shot playback plus ambience loops, respecting volume settings.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 32;

        public static AudioManager Instance { get; private set; }

        private readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private int next;
        private AudioSource wind;
        private AudioSource drone;
        private AudioSource ui;

        public static void Ensure()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            Instance = this;
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) clips[s] = SoundSynth.Create(s, 1337);

            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("sfx" + i);
                child.transform.SetParent(transform);
                var src = child.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2f;
                src.maxDistance = 45f;
                src.dopplerLevel = 0f;
                pool.Add(src);
            }

            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false;
            ui.spatialBlend = 0f;

            wind = CreateLoop(Sfx.AmbienceWind);
            drone = CreateLoop(Sfx.AmbienceDrone);
            GameSettings.Changed += ApplyVolumes;
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateLoop(Sfx id)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clips[id];
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.Play();
            return src;
        }

        private void ApplyVolumes()
        {
            var s = GameSettings.Current;
            AudioListener.volume = Mathf.Clamp01(s.MasterVolume);
            if (wind != null) wind.volume = 0.5f * s.AmbienceVolume;
            if (drone != null) drone.volume = (InMatch ? 0.35f : 0.15f) * s.AmbienceVolume;
        }

        public bool InMatch { get; private set; }

        public void SetInMatch(bool inMatch)
        {
            InMatch = inMatch;
            ApplyVolumes();
        }

        public void Play2D(Sfx id, float volume = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(id, out var clip)) return;
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume * GameSettings.Current.EffectsVolume);
        }

        public void PlayAt(Sfx id, Vector3 position, float volume = 1f, float pitchJitter = 0.06f, float maxDistance = 45f)
        {
            if (!clips.TryGetValue(id, out var clip)) return;
            var src = pool[next];
            next = (next + 1) % pool.Count;
            src.transform.position = position;
            src.clip = clip;
            src.maxDistance = maxDistance;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.volume = volume * GameSettings.Current.EffectsVolume;
            src.Play();
        }

        public static Sfx ShotSound(WeaponId weapon)
        {
            switch (weapon)
            {
                case WeaponId.Smg: return Sfx.ShotSmg;
                case WeaponId.Shotgun: return Sfx.ShotShotgun;
                case WeaponId.AssaultRifle: return Sfx.ShotRifle;
                case WeaponId.MarksmanRifle: return Sfx.ShotMarksman;
                case WeaponId.PlasmaRifle: return Sfx.ShotPlasma;
                default: return Sfx.ShotPistol;
            }
        }
    }
}
