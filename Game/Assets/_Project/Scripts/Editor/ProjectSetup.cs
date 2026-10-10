using System.IO;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UZS.Core;
using UZS.Gameplay;
using UZS.UI;

namespace UZS.EditorTools
{
    /// <summary>
    /// One-click, idempotent project setup. Everything the game needs on disk (base materials, the three network
    /// prefabs, the GameContent asset, the Main scene, build settings and player settings) is generated here from
    /// code, so the repository contains no hand-edited binary/YAML assets.
    /// Menu: Tools > UZS > Set Up Project. Command line: -executeMethod UZS.EditorTools.ProjectSetup.RunFromCommandLine
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/_Project/Generated";
        public const string ContentPath = Root + "/GameContent.asset";
        public const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Tools/UZS/Set Up Project", priority = 0)]
        public static void Run()
        {
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();

            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContent>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            content.Lit = MakeMaterial("Lit", "Standard", m => m.enableInstancing = true);
            content.Emissive = MakeMaterial("Emissive", "Standard", m =>
            {
                m.enableInstancing = true;
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });
            content.Transparent = MakeMaterial("Transparent", "Standard", m =>
            {
                // Standard shader "Fade" mode.
                m.SetFloat("_Mode", 2f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                m.color = new Color(1f, 1f, 1f, 0.35f);
            });
            content.Unlit = MakeMaterial("Unlit", "Unlit/Color", null);

            content.PlayerPrefab = MakePlayerPrefab();
            content.ZombiePrefab = MakeZombiePrefab();
            content.MatchPrefab = MakeMatchPrefab();
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            MakeScene(content);
            ConfigureProject();
            AssetDatabase.SaveAssets();
            Debug.Log("[UZS] Project setup complete. Open " + ScenePath + " and press Play.");
        }

        /// <summary>Batch-mode entry point (use with -quit); exceptions make Unity exit with a non-zero code.</summary>
        public static void RunFromCommandLine() => Run();

        private static Material MakeMaterial(string name, string shaderName, System.Action<Material> configure)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Shader not found: " + shaderName + " (the project must use the built-in render pipeline).");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject SavePrefab(GameObject temp, string name)
        {
            string path = $"{Root}/Prefabs/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            // NGO assigns GlobalObjectIdHash in NetworkObject.OnValidate; make sure it ran for script-created prefabs.
            var no = prefab.GetComponent<NetworkObject>();
            var onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (onValidate != null) onValidate.Invoke(no, null);
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject MakePlayerPrefab()
        {
            var go = new GameObject("Player");
            go.AddComponent<CharacterController>();
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            go.AddComponent<PlayerAvatar>(); // RequireComponent adds weapons, motor and interactor.
            return SavePrefab(go, "Player");
        }

        private static GameObject MakeZombiePrefab()
        {
            var go = new GameObject("Zombie");
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            go.AddComponent<ZombieAgent>();
            return SavePrefab(go, "Zombie");
        }

        private static GameObject MakeMatchPrefab()
        {
            var go = new GameObject("Match");
            var no = go.AddComponent<NetworkObject>();
            no.SynchronizeTransform = false;
            go.AddComponent<MatchState>();
            go.AddComponent<RoundDirector>();
            return SavePrefab(go, "Match");
        }

        private static void MakeScene(GameContent content)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            cam.AddComponent<CameraDirector>();

            var app = new GameObject("App");
            var transport = app.AddComponent<UnityTransport>();
            var nm = app.AddComponent<NetworkManager>();
            nm.NetworkConfig = nm.NetworkConfig ?? new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = content.PlayerPrefab;
            nm.NetworkConfig.ConnectionApproval = true;
            var boot = app.AddComponent<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            app.AddComponent<UIRoot>();

            // Keep fog shader variants in builds (the map enables fog at runtime).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.skybox = null;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigureProject()
        {
            PlayerSettings.productName = GameInfo.DisplayTitle;
            PlayerSettings.companyName = GameInfo.CompanyName;
            PlayerSettings.bundleVersion = GameInfo.ProtocolVersion;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);

            // The game reads keyboard/mouse through the built-in Input Manager (behind IPlayerInput).
            SetSerialized("ProjectSettings/ProjectSettings.asset", "activeInputHandler", 0);
            // Keep fog + instancing variants that are only used by runtime-created materials.
            SetSerialized("ProjectSettings/GraphicsSettings.asset", "m_FogStripping", 1);
            SetSerialized("ProjectSettings/GraphicsSettings.asset", "m_FogKeepLinear", 1);
            SetSerialized("ProjectSettings/GraphicsSettings.asset", "m_FogKeepExp", 1);
            SetSerialized("ProjectSettings/GraphicsSettings.asset", "m_FogKeepExp2", 1);
            SetSerialized("ProjectSettings/GraphicsSettings.asset", "m_InstancingStripping", 2);
        }

        private static void SetSerialized(string assetPath, string property, int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogWarning($"[UZS] {assetPath}: property {property} not found (Unity version difference) - skipped.");
                return;
            }
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Runs setup automatically the first time the project is opened.</summary>
    [InitializeOnLoad]
    internal static class AutoSetup
    {
        static AutoSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<GameContent>(ProjectSetup.ContentPath) != null) return;
                Debug.Log("[UZS] First open detected - generating project assets.");
                ProjectSetup.Run();
                EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            };
        }
    }
}
