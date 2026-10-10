using UnityEngine;

namespace UZS.Gameplay
{
    /// <summary>
    /// References to the few assets that must exist on disk (network prefabs and base materials).
    /// Created by the editor bootstrapper (Tools > UZS > Set Up Project); everything else is built in code.
    /// </summary>
    [CreateAssetMenu(menuName = "UZS/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject
    {
        [Header("Network prefabs")]
        public GameObject PlayerPrefab;
        public GameObject ZombiePrefab;
        public GameObject MatchPrefab;

        [Header("Base materials (variants are tinted at runtime)")]
        [Tooltip("Standard shader, no keywords.")]
        public Material Lit;
        [Tooltip("Standard shader with _EMISSION enabled.")]
        public Material Emissive;
        [Tooltip("Standard shader in Fade mode for glass/holograms.")]
        public Material Transparent;
        [Tooltip("Unlit/Color for tracers and UI-like world elements.")]
        public Material Unlit;

        public static GameContent Instance { get; private set; }

        public void MakeCurrent() => Instance = this;
    }
}
