using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Global switch the UI flips while menus are open so gameplay ignores mouse/keyboard.</summary>
    public static class InputGate
    {
        public static bool Blocked;
    }

    /// <summary>
    /// Owner-driven first-person movement (CharacterController). Position is replicated by an owner-authority
    /// NetworkTransform; the authority only sees results, so this is a co-op trust model (see docs/SECURITY.md).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : NetworkBehaviour
    {
        private CharacterController controller;
        private NetworkTransform netTransform;
        private PlayerAvatar avatar;
        private float yaw;
        private float pitch;
        private float verticalVelocity;
        private float crouchBlend;
        private float stepTimer;
        private float teleportGrace;

        // Authority-side plausibility tracking.
        private Vector3 lastServerPos;
        private float suspicion;

        public float Pitch => pitch;
        public float Yaw => yaw;
        public bool IsSprinting { get; private set; }
        public float CurrentSpeed { get; private set; }

        public float EyeHeight
        {
            get
            {
                if (avatar != null && avatar.Life == LifeState.Downed) return 0.55f;
                return Mathf.Lerp(MovementRules.EyeHeight, 1.05f, crouchBlend);
            }
        }

        public Vector3 EyeWorldPosition => transform.position + Vector3.up * EyeHeight;
        public Quaternion AimRotation => Quaternion.Euler(pitch, yaw, 0f);
        public Vector3 AimDirection => AimRotation * Vector3.forward;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            netTransform = GetComponent<NetworkTransform>();
            avatar = GetComponent<PlayerAvatar>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.45f;
        }

        public override void OnNetworkSpawn()
        {
            yaw = transform.eulerAngles.y;
            lastServerPos = transform.position;
            // Remote copies are moved by the NetworkTransform; their controller only acts as a collider.
            controller.enabled = IsOwner;
            if (!IsOwner)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.height = controller.height;
                capsule.radius = controller.radius;
                capsule.center = controller.center;
            }
        }

        public void Teleport(Vector3 position, float newYaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, newYaw, 0f));
            if (netTransform != null && netTransform.CanCommitToTransform) netTransform.Teleport(position, transform.rotation, transform.localScale);
            controller.enabled = true;
            yaw = newYaw;
            pitch = 0f;
            verticalVelocity = 0f;
            teleportGrace = 1f;
        }

        private void Update()
        {
            if (IsServer && !IsOwner) ServerPlausibility(Time.deltaTime);
            if (!IsOwner) return;
            teleportGrace -= Time.deltaTime;

            var match = MatchState.Instance;
            bool playing = match != null && match.Phase == MatchPhase.Playing;
            var life = avatar.Life;
            bool canMove = playing && (life == LifeState.Alive || life == LifeState.Downed) && !InputGate.Blocked;
            var input = KeyboardMouseInput.Instance;

            if (canMove)
            {
                var look = input.Look;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector2 move = canMove ? input.Move : Vector2.zero;
            bool crouch = canMove && input.CrouchHeld;
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouch ? 1f : 0f, Time.deltaTime * 6f);
            IsSprinting = canMove && life == LifeState.Alive && input.SprintHeld && move.y > 0.1f && !crouch && !input.AimHeld;

            float speed = life == LifeState.Downed ? 0.9f :
                crouch ? MovementRules.CrouchSpeed :
                IsSprinting ? MovementRules.SprintSpeed : MovementRules.WalkSpeed;
            if ((avatar.Perks & PerkId.Fleetfoot) != 0) speed *= MovementRules.FleetfootMultiplier;
            if (input.AimHeld && life == LifeState.Alive) speed *= 0.7f;

            var wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (canMove && life == LifeState.Alive && input.JumpPressed)
                    verticalVelocity = Mathf.Sqrt(2f * MovementRules.JumpHeight * -MovementRules.Gravity);
            }
            verticalVelocity += MovementRules.Gravity * Time.deltaTime;

            if (!controller.enabled) return;
            var motion = wish * speed + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
            CurrentSpeed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

            // Safety net: anyone falling out of the world is put back at the start street.
            if (transform.position.y < -30f) Teleport(Geo.V(MapCatalog.PlayerSpawns[0]) + Vector3.up, yaw);

            if (controller.isGrounded && CurrentSpeed > 1f)
            {
                stepTimer -= Time.deltaTime * CurrentSpeed;
                if (stepTimer <= 0f)
                {
                    stepTimer = 2.2f;
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.Footstep, transform.position, 0.35f, 0.15f, 15f);
                }
            }
        }

        /// <summary>
        /// The authority cannot correct an owner-authoritative transform, but it records implausible speed so
        /// obvious speed hacks are logged. (Documented limitation of player-hosted co-op.)
        /// </summary>
        private void ServerPlausibility(float dt)
        {
            if (dt <= 0f) return;
            var pos = transform.position;
            var delta = pos - lastServerPos;
            lastServerPos = pos;
            float horizontal = new Vector2(delta.x, delta.z).magnitude / dt;
            float allowed = MovementRules.MaxSpeed(avatar.Perks) * 1.6f + 2f;
            suspicion = horizontal > allowed && delta.magnitude < 20f ? suspicion + dt : Mathf.Max(0f, suspicion - dt * 0.5f);
            if (suspicion > 2f)
            {
                Debug.LogWarning($"[UZS] Player {OwnerClientId} is moving faster than allowed ({horizontal:0.0} m/s).");
                suspicion = 0f;
            }
        }
    }
}
