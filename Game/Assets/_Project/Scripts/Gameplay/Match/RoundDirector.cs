using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Authority-only round loop: intermission, zombie spawning within the active (unlocked) zones,
    /// round completion and respawning of dead players. Endless: rounds continue until escape or defeat.
    /// </summary>
    public sealed class RoundDirector : MonoBehaviour
    {
        private MatchState match;
        private bool running;
        private int currentRound;
        private int remainingToSpawn;
        private float spawnTimer;
        private float intermission;
        private float syncTimer;
        private readonly System.Random rng = new System.Random();

        public bool InIntermission => intermission > 0f;

        /// <summary>Test hook: stops new spawns (used by the automated smoke test).</summary>
        public bool PauseSpawning { get; set; }

        private void Awake()
        {
            match = GetComponent<MatchState>();
        }

        public void ServerBegin()
        {
            running = true;
            currentRound = 0;
            remainingToSpawn = 0;
            intermission = RoundRules.FirstRoundDelaySeconds;
            Sync();
        }

        private int PlayerCount => Mathf.Max(1, PlayerAvatar.All.Count);

        private void Update()
        {
            if (!running || match == null || !match.IsServer) return;
            if (match.Phase != MatchPhase.Playing)
            {
                if (match.IsOver) running = false;
                return;
            }

            float dt = Time.deltaTime;
            if (intermission > 0f)
            {
                intermission -= dt;
                if (intermission <= 0f) StartRound(currentRound + 1);
                SyncThrottled(dt);
                return;
            }

            spawnTimer -= dt;
            int alive = ZombieAgent.AliveCount;
            if (!PauseSpawning && remainingToSpawn > 0 && alive < RoundRules.MaxAlive(PlayerCount) && spawnTimer <= 0f)
            {
                if (TrySpawn()) remainingToSpawn--;
                spawnTimer = RoundRules.SpawnIntervalSeconds(currentRound);
            }

            if (remainingToSpawn == 0 && alive == 0) EndRound();
            SyncThrottled(dt);
        }

        private void StartRound(int number)
        {
            currentRound = number;
            remainingToSpawn = RoundRules.ZombiesForRound(number, PlayerCount);
            spawnTimer = 1f;
            intermission = 0f;
            Sync();
            match.AnnounceRpc("ROUND " + number, true);
        }

        private void EndRound()
        {
            intermission = RoundRules.IntermissionSeconds;
            // Dead teammates come back for the next round with a starter pistol (points are kept).
            for (int i = 0; i < PlayerAvatar.All.Count; i++)
            {
                var p = PlayerAvatar.All[i];
                if (p.Life == LifeState.Dead) p.ServerRespawn(RespawnPointFor(i));
            }
            Sync();
            match.AnnounceRpc($"Round {currentRound} survived", false);
        }

        /// <summary>A zombie that got stuck is returned to the spawn pool.</summary>
        public void ServerRecycle()
        {
            remainingToSpawn++;
        }

        private void SyncThrottled(float dt)
        {
            syncTimer -= dt;
            if (syncTimer > 0f) return;
            syncTimer = 0.25f;
            Sync();
        }

        private void Sync()
        {
            match.ServerSetRound(currentRound, remainingToSpawn + ZombieAgent.AliveCount, Mathf.Max(0f, intermission));
        }

        private Vector3 RespawnPointFor(int index)
        {
            // Prefer appearing next to a living teammate; fall back to the start street.
            foreach (var p in PlayerAvatar.All)
                if (p.Life == LifeState.Alive) return p.transform.position + new Vector3(1.2f, 0.2f, 0f);
            var s = MapCatalog.PlayerSpawns[index % MapCatalog.PlayerSpawns.Length];
            return Geo.V(s) + Vector3.up * 0.2f;
        }

        private bool TrySpawn()
        {
            var content = GameContent.Instance;
            if (content == null || content.ZombiePrefab == null) return false;

            var active = MapCatalog.ActiveZones(match.DoorMask);
            var scored = new List<KeyValuePair<float, Vector3>>();
            for (int z = 0; z < MapCatalog.Zones.Length; z++)
            {
                if (!active[z]) continue;
                foreach (var sp in MapCatalog.Zones[z].ZombieSpawns)
                {
                    var pos = Geo.V(sp);
                    float nearest = NearestPlayerDistance(pos);
                    if (nearest < 6f) continue; // never pop in right next to someone
                    scored.Add(new KeyValuePair<float, Vector3>(nearest, pos));
                }
            }
            if (scored.Count == 0) return false;

            // Favour the closest few spawn points so pressure stays on the players wherever they are.
            scored.Sort((a, b) => a.Key.CompareTo(b.Key));
            int pickFrom = Mathf.Min(4, scored.Count);
            var point = scored[rng.Next(pickFrom)].Value;
            point += new Vector3((float)(rng.NextDouble() - 0.5) * 1.5f, 0f, (float)(rng.NextDouble() - 0.5) * 1.5f);

            var go = Instantiate(content.ZombiePrefab, point, Quaternion.Euler(0f, rng.Next(360), 0f));
            var zombie = go.GetComponent<ZombieAgent>();
            zombie.ServerConfigure(this, RoundRules.ZombieHealth(currentRound), RoundRules.ZombieDamage(currentRound),
                RoundRules.RollSpeed(currentRound, rng.NextDouble()), (byte)rng.Next(256));
            go.GetComponent<NetworkObject>().Spawn(true);
            return true;
        }

        private static float NearestPlayerDistance(Vector3 pos)
        {
            float best = float.MaxValue;
            foreach (var p in PlayerAvatar.All)
            {
                if (p.Life != LifeState.Alive && p.Life != LifeState.Downed) continue;
                best = Mathf.Min(best, Vector3.Distance(pos, p.transform.position));
            }
            return best;
        }
    }
}
