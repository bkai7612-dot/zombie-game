using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Marks a collider as part of a zombie for hit detection.</summary>
    public sealed class Hitbox : MonoBehaviour
    {
        public ZombieAgent Owner;
        public bool IsHead;
    }

    /// <summary>
    /// A server-simulated zombie. The host runs NavMesh pathing, attacks and health; clients only receive the
    /// synchronised transform and a few state flags, and animate the primitive model procedurally.
    /// </summary>
    public sealed class ZombieAgent : NetworkBehaviour
    {
        public static readonly List<ZombieAgent> All = new List<ZombieAgent>();

        public static int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var z in All) if (!z.IsDead) n++;
                return n;
            }
        }

        private readonly NetworkVariable<ZombieSpeed> speed = new NetworkVariable<ZombieSpeed>(ZombieSpeed.Walk);
        private readonly NetworkVariable<byte> variant = new NetworkVariable<byte>(0);
        private readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false);

        public bool IsDead => dead.Value;
        public ZombieSpeed Speed => speed.Value;

        // Authority-only.
        private RoundDirector director;
        private float health;
        private float damage;
        private NavMeshAgent agent;
        private PlayerAvatar target;
        private float repathTimer;
        private float attackCooldown;
        private float windup = -1f;
        private float despawnTimer = -1f;
        private Vector3 lastProgressPos;
        private float stuckTimer;
        private ZombieSpeed pendingSpeed;
        private byte pendingVariant;

        // Visuals.
        private ZombieModel model;
        private Vector3 lastPos;
        private float groanTimer;
        private float attackAnim;

        public void ServerConfigure(RoundDirector owner, float hp, float dmg, ZombieSpeed tier, byte look)
        {
            director = owner;
            health = hp;
            damage = dmg;
            pendingSpeed = tier;
            pendingVariant = look;
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer)
            {
                speed.Value = pendingSpeed;
                variant.Value = pendingVariant;
            }
            model = ZombieModel.Build(transform, this, variant.Value);
            lastPos = transform.position;
            groanTimer = Random.Range(1f, 6f);

            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            if (IsServer)
            {
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.4f;
                agent.height = 1.8f;
                agent.speed = RoundRules.MoveSpeed(speed.Value);
                agent.acceleration = 14f;
                agent.angularSpeed = 540f;
                agent.stoppingDistance = ZombieRules.AttackRange * 0.7f;
                agent.autoBraking = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(30, 70);
                if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas)) agent.Warp(hit.position);
                lastProgressPos = transform.position;
            }
            dead.OnValueChanged += OnDeadChanged;
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            dead.OnValueChanged -= OnDeadChanged;
            LagCompensation.Forget(this);
        }

        private void OnDeadChanged(bool previous, bool current)
        {
            if (!current) return;
            if (model != null) model.SetDead();
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.ZombieDeath, transform.position + Vector3.up, 0.8f);
        }

        private void Update()
        {
            AnimateVisuals();
            if (!IsServer) return;

            if (despawnTimer >= 0f)
            {
                despawnTimer -= Time.deltaTime;
                if (despawnTimer < 0f && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                return;
            }

            var match = MatchState.Instance;
            if (match == null || match.Phase != MatchPhase.Playing)
            {
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
            LagCompensation.Record(this);
            ServerThink(Time.deltaTime);
        }

        // ------------------------------------------------------------------ AI (authority)

        private void ServerThink(float dt)
        {
            if (IsDead || agent == null) return;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas)) agent.Warp(hit.position);
                else Recycle();
                return;
            }

            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                repathTimer = ZombieRules.RepathInterval;
                target = FindTarget();
                if (target != null) agent.SetDestination(target.transform.position);
                agent.isStopped = target == null;
            }

            attackCooldown -= dt;
            if (target != null)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < ZombieRules.AttackRange + 1f)
                {
                    var look = target.transform.position - transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 720f * dt);
                }

                if (windup >= 0f)
                {
                    windup -= dt;
                    if (windup < 0f)
                    {
                        if (dist <= ZombieRules.AttackRange + 0.4f && target.Life == LifeState.Alive) target.ServerTakeDamage(damage, transform.position);
                        attackCooldown = ZombieRules.AttackCooldown;
                    }
                }
                else if (attackCooldown <= 0f && dist <= ZombieRules.AttackRange && target.Life == LifeState.Alive)
                {
                    windup = ZombieRules.AttackWindup;
                    AttackRpc();
                }
            }

            // Stuck detection: no progress for a long time while far from everyone -> back to the pool.
            if ((transform.position - lastProgressPos).sqrMagnitude > 1f)
            {
                lastProgressPos = transform.position;
                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += dt;
                if (stuckTimer > ZombieRules.StuckSeconds && NearestPlayerDistance() > ZombieRules.StuckDistance) Recycle();
            }
        }

        private PlayerAvatar FindTarget()
        {
            PlayerAvatar best = null;
            float bestDist = float.MaxValue;
            foreach (var p in PlayerAvatar.All)
            {
                if (p.Life != LifeState.Alive) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        private float NearestPlayerDistance()
        {
            float best = float.MaxValue;
            foreach (var p in PlayerAvatar.All) best = Mathf.Min(best, Vector3.Distance(p.transform.position, transform.position));
            return best;
        }

        private void Recycle()
        {
            if (IsDead || despawnTimer >= 0f) return;
            if (director != null) director.ServerRecycle();
            dead.Value = true;
            despawnTimer = 0f;
        }

        /// <summary>Applies authority-computed damage. Returns true if this hit killed the zombie.</summary>
        public bool ServerTakeDamage(float amount, PlayerAvatar attacker, bool headshot, bool melee)
        {
            if (!IsServer || IsDead || amount <= 0f) return false;
            health -= amount;
            if (health > 0f) return false;

            dead.Value = true;
            despawnTimer = 2.5f;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            if (attacker != null) attacker.ServerRecordKill(headshot, melee);
            return true;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void AttackRpc()
        {
            attackAnim = 1f;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.ZombieAttack, transform.position + Vector3.up * 1.5f, 0.9f);
        }

        // ------------------------------------------------------------------ visuals (everyone)

        private void AnimateVisuals()
        {
            if (model == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            float velocity = (transform.position - lastPos).magnitude / dt;
            lastPos = transform.position;
            attackAnim = Mathf.Max(0f, attackAnim - dt * 2f);
            model.Animate(velocity, attackAnim, speed.Value);

            if (IsDead) return;
            groanTimer -= dt;
            if (groanTimer <= 0f)
            {
                groanTimer = Random.Range(3f, 9f);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.ZombieGroan, transform.position + Vector3.up * 1.6f, 0.55f, 0.2f, 28f);
            }
        }
    }
}
