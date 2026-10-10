using UnityEditor;
using UnityEditor.SceneManagement;
using UZS.Gameplay;

namespace UZS.EditorTools
{
    /// <summary>Tools > UZS > Run Smoke Test: enters Play Mode in the Main scene and runs <see cref="SmokeTest"/>.</summary>
    [InitializeOnLoad]
    internal static class SmokeTestMenu
    {
        private const string Flag = "UZS.RunSmokeTest";

        static SmokeTestMenu()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !UnityEditor.SessionState.GetBool(Flag, false)) return;
                UnityEditor.SessionState.SetBool(Flag, false);
                if (GameBootstrap.Instance != null) GameBootstrap.Instance.gameObject.AddComponent<SmokeTest>();
            };
        }

        [MenuItem("Tools/UZS/Run Smoke Test (Play Mode)", priority = 10)]
        private static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            UnityEditor.SessionState.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }
    }
}
