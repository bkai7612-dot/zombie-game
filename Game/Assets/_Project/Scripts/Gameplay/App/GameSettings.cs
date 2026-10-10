using System;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Player-facing settings persisted with PlayerPrefs.</summary>
    public sealed class GameSettings
    {
        public float MasterVolume = 0.8f;
        public float EffectsVolume = 1f;
        public float AmbienceVolume = 0.6f;
        public float MouseSensitivity = 2.0f;
        public bool InvertY;
        public float FieldOfView = 80f;
        public int ResolutionWidth;
        public int ResolutionHeight;
        public FullScreenMode ScreenMode = FullScreenMode.FullScreenWindow;
        public bool VSync = true;
        public int FrameCap = 144;
        public string PlayerName = "Survivor";
        public int CharacterId;

        public KeyCode KeyForward = KeyCode.W;
        public KeyCode KeyBack = KeyCode.S;
        public KeyCode KeyLeft = KeyCode.A;
        public KeyCode KeyRight = KeyCode.D;
        public KeyCode KeyJump = KeyCode.Space;
        public KeyCode KeySprint = KeyCode.LeftShift;
        public KeyCode KeyCrouch = KeyCode.LeftControl;
        public KeyCode KeyReload = KeyCode.R;
        public KeyCode KeyInteract = KeyCode.F;
        public KeyCode KeyMelee = KeyCode.V;
        public KeyCode KeySwap = KeyCode.Q;

        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240, 0 };

        private static GameSettings current;
        private static string prefix = "uzs.";

        public static GameSettings Current => current ?? (current = Load());

        public static event Action Changed;

        /// <summary>
        /// "-uzsprofile name" on the command line gives an instance its own saved settings and identity,
        /// which makes it easy to run two copies on one PC for co-op testing.
        /// </summary>
        public static string ProfileName
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-uzsprofile") return ConnectionPayload.SanitizeName(args[i + 1]).Replace(' ', '_');
                return string.Empty;
            }
        }

        private static string Key(string k) => prefix + (ProfileName.Length > 0 ? ProfileName + "." : string.Empty) + k;

        /// <summary>Stable per-install identity used for reconnect bookkeeping (not an account).</summary>
        public static string PlayerKey
        {
            get
            {
                string k = Key("playerKey");
                string v = PlayerPrefs.GetString(k, string.Empty);
                if (string.IsNullOrEmpty(v))
                {
                    v = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString(k, v);
                    PlayerPrefs.Save();
                }
                return v;
            }
        }

        public static GameSettings Load()
        {
            var s = new GameSettings
            {
                MasterVolume = PlayerPrefs.GetFloat(Key("master"), 0.8f),
                EffectsVolume = PlayerPrefs.GetFloat(Key("sfx"), 1f),
                AmbienceVolume = PlayerPrefs.GetFloat(Key("amb"), 0.6f),
                MouseSensitivity = PlayerPrefs.GetFloat(Key("sens"), 2f),
                InvertY = PlayerPrefs.GetInt(Key("invertY"), 0) == 1,
                FieldOfView = PlayerPrefs.GetFloat(Key("fov"), 80f),
                ResolutionWidth = PlayerPrefs.GetInt(Key("resW"), 0),
                ResolutionHeight = PlayerPrefs.GetInt(Key("resH"), 0),
                ScreenMode = (FullScreenMode)PlayerPrefs.GetInt(Key("screenMode"), (int)FullScreenMode.FullScreenWindow),
                VSync = PlayerPrefs.GetInt(Key("vsync"), 1) == 1,
                FrameCap = PlayerPrefs.GetInt(Key("fps"), 144),
                PlayerName = ConnectionPayload.SanitizeName(PlayerPrefs.GetString(Key("name"), "Survivor")),
                CharacterId = CharacterCatalog.Clamp(PlayerPrefs.GetInt(Key("character"), 0)),
            };
            s.KeyForward = LoadKey("kFwd", s.KeyForward);
            s.KeyBack = LoadKey("kBack", s.KeyBack);
            s.KeyLeft = LoadKey("kLeft", s.KeyLeft);
            s.KeyRight = LoadKey("kRight", s.KeyRight);
            s.KeyJump = LoadKey("kJump", s.KeyJump);
            s.KeySprint = LoadKey("kSprint", s.KeySprint);
            s.KeyCrouch = LoadKey("kCrouch", s.KeyCrouch);
            s.KeyReload = LoadKey("kReload", s.KeyReload);
            s.KeyInteract = LoadKey("kInteract", s.KeyInteract);
            s.KeyMelee = LoadKey("kMelee", s.KeyMelee);
            s.KeySwap = LoadKey("kSwap", s.KeySwap);
            return s;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(Key("master"), MasterVolume);
            PlayerPrefs.SetFloat(Key("sfx"), EffectsVolume);
            PlayerPrefs.SetFloat(Key("amb"), AmbienceVolume);
            PlayerPrefs.SetFloat(Key("sens"), MouseSensitivity);
            PlayerPrefs.SetInt(Key("invertY"), InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(Key("fov"), FieldOfView);
            PlayerPrefs.SetInt(Key("resW"), ResolutionWidth);
            PlayerPrefs.SetInt(Key("resH"), ResolutionHeight);
            PlayerPrefs.SetInt(Key("screenMode"), (int)ScreenMode);
            PlayerPrefs.SetInt(Key("vsync"), VSync ? 1 : 0);
            PlayerPrefs.SetInt(Key("fps"), FrameCap);
            PlayerPrefs.SetString(Key("name"), ConnectionPayload.SanitizeName(PlayerName));
            PlayerPrefs.SetInt(Key("character"), CharacterCatalog.Clamp(CharacterId));
            SaveKey("kFwd", KeyForward);
            SaveKey("kBack", KeyBack);
            SaveKey("kLeft", KeyLeft);
            SaveKey("kRight", KeyRight);
            SaveKey("kJump", KeyJump);
            SaveKey("kSprint", KeySprint);
            SaveKey("kCrouch", KeyCrouch);
            SaveKey("kReload", KeyReload);
            SaveKey("kInteract", KeyInteract);
            SaveKey("kMelee", KeyMelee);
            SaveKey("kSwap", KeySwap);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Applies display, frame-rate and audio settings.</summary>
        public void Apply(bool display)
        {
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync || FrameCap <= 0 ? -1 : FrameCap;
            if (display)
            {
                int w = ResolutionWidth > 0 ? ResolutionWidth : Screen.currentResolution.width;
                int h = ResolutionHeight > 0 ? ResolutionHeight : Screen.currentResolution.height;
                Screen.SetResolution(w, h, ScreenMode);
            }
        }

        private static KeyCode LoadKey(string name, KeyCode fallback)
        {
            int v = PlayerPrefs.GetInt(Key(name), (int)fallback);
            return Enum.IsDefined(typeof(KeyCode), v) ? (KeyCode)v : fallback;
        }

        private static void SaveKey(string name, KeyCode key) => PlayerPrefs.SetInt(Key(name), (int)key);
    }
}
