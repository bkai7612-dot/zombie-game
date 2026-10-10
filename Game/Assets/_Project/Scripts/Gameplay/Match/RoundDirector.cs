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

        private static Vector3 RespawnPointFor(int index) => SpawnSpot.NearLivingTeammate(index);

        private bool TrySpawn()
        {
            var content = GameContent.Instance;
            if (content == null || content.ZombiePrefab == null) return false;

            var players = new List<V3>();
            var eyes = new List<Vector3>();
            foreach (var p in PlayerAvatar.All)
            {
                if (p.Life != LifeState.Alive && p.Life != LifeState.Downed) continue;
                var pos = p.transform.position;
                players.Add(new V3(pos.x, pos.y, pos.z));
                eyes.Add(p.EyePosition);
            }

            // Zone-aware ranking (door graph, not straight lines), then skip close points a player can see,
            // so zombies don't pop into existence in plain view.
            var ranked = SpawnPlanner.Rank(players, match.DoorMask, ZombieRules.MinSpawnDistance);
            var usable = new List<Vector3>();
            foreach (var c in ranked)
            {
                var pos = Geo.V(c.Position);
                if (c.Distance < ZombieRules.VisibleSpawnCheckDistance && SeenByAnyone(pos, eyes)) continue;
                usable.Add(pos);
                if (usable.Count >= 4) break;
            }
            if (usable.Count == 0 && ranked.Count > 0) usable.Add(Geo.V(ranked[0].Position));
            if (usable.Count == 0) return false;

            var point = usable[rng.Next(usable.Count)];
            point += new Vector3((float)(rng.NextDouble() - 0.5) * 1.5f, 0f, (float)(rng.NextDouble() - 0.5) * 1.5f);

            var go = Instantiate(content.ZombiePrefab, point, Quaternion.Euler(0f, rng.Next(360), 0f));
            var zombie = go.GetComponent<ZombieAgent>();
            zombie.ServerConfigure(this, RoundRules.ZombieHealth(currentRound), RoundRules.ZombieDamage(currentRound),
                RoundRules.RollSpeed(currentRound, rng.NextDouble()), (byte)rng.Next(256));
            go.GetComponent<NetworkObject>().Spawn(true);
            return true;
        }

        private static bool SeenByAnyone(Vector3 spawn, List<Vector3> eyes)
        {
            foreach (var eye in eyes)
                if (Ballistics.ClearLine(eye, spawn + Vector3.up * 1.2f)) return true;
            return false;
        }
    }
}
