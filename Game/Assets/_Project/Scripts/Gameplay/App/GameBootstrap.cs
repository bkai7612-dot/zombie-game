using System;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Scene entry point. Wires content, audio, camera and Netcode, then lets the UI drive everything.
    /// Command-line switches (for testing):
    ///   -uzsprofile NAME        separate settings/identity per instance (two copies on one PC)
    ///   -uzshost [PORT]         auto-host a direct-IP game
    ///   -uzsjoin ADDRESS [PORT] auto-join a direct-IP game
    ///   -uzssmoketest           run the automated single-player smoke test and quit (exit code 0 = pass)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameContent content;

        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.runInBackground = true;

            if (content == null)
            {
                Debug.LogError("[UZS] GameBootstrap has no GameContent assigned. Run Tools > UZS > Set Up Project.");
                return;
            }
            content.MakeCurrent();

            var settings = GameSettings.Current;
            settings.Apply(!Application.isEditor);
            AudioManager.Ensure();
            CameraDirector.Ensure();

            var nm = GetComponentInChildren<NetworkManager>();
            if (nm == null) nm = FindFirstObjectByType<NetworkManager>();
            if (nm == null)
            {
                Debug.LogError("[UZS] No NetworkManager in the scene.");
                return;
            }
            nm.NetworkConfig.PlayerPrefab = content.PlayerPrefab;
            foreach (var prefab in new[] { content.ZombiePrefab, content.MatchPrefab })
                if (prefab != null && !nm.NetworkConfig.Prefabs.Contains(prefab)) nm.AddNetworkPrefab(prefab);

            var connection = gameObject.GetComponent<ConnectionController>();
            if (connection == null) connection = gameObject.AddComponent<ConnectionController>();
            connection.Init(nm);
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            var cc = ConnectionController.Instance;
            if (cc == null) return;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-uzssmoketest":
                        gameObject.AddComponent<SmokeTest>();
                        return;
                    case "-uzshost":
                        cc.StartDirectHost(ParsePort(args, i + 1));
                        return;
                    case "-uzsjoin":
                        cc.StartDirectClient(i + 1 < args.Length ? args[i + 1] : "127.0.0.1", ParsePort(args, i + 2));
                        return;
                }
            }
        }

        private static ushort ParsePort(string[] args, int index)
        {
            return index < args.Length && ushort.TryParse(args[index], out var p) ? p : GameInfo.DefaultDirectPort;
        }
    }
}
