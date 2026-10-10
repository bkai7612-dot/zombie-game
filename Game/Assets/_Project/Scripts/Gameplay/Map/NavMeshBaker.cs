using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UZS.Gameplay
{
    /// <summary>
    /// Bakes the zombie navigation mesh at runtime on the authority only (clients never path-find).
    /// Doors are excluded from the bake and block paths through carving NavMeshObstacles until opened.
    /// Uses only the built-in UnityEngine.AI API, so no extra package is required.
    /// </summary>
    public static class NavMeshBaker
    {
        private static NavMeshDataInstance instance;

        public static bool Bake(MapInstance map)
        {
            if (instance.valid) instance.Remove();

            // Default humanoid agent type (radius 0.5, height 2, slope 45, step 0.75) so agents match the data.
            var settings = NavMesh.GetSettingsByID(0);

            var markups = new List<NavMeshBuildMarkup>();
            foreach (var door in map.Doors)
                markups.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            if (map.Box != null) markups.Add(new NavMeshBuildMarkup { root = map.Box.transform, ignoreFromBuild = true });
            // Players already standing on the map must not punch holes into the navmesh.
            foreach (var p in PlayerAvatar.All)
                markups.Add(new NavMeshBuildMarkup { root = p.transform, ignoreFromBuild = true });

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(map.NavBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);

            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, map.NavBounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("[UZS] NavMesh bake failed - zombies will not be able to move.");
                return false;
            }
            instance = NavMesh.AddNavMeshData(data);
            Debug.Log($"[UZS] NavMesh baked from {sources.Count} sources.");
            return instance.valid;
        }

        public static void Clear()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
