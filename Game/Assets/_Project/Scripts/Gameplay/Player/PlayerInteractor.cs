using System;
using Unity.Netcode;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Finds the nearest usable thing for the owner, shows a prompt, and sends purchase / revive requests.
    /// The authority re-validates range, phase, price and points for every request.
    /// </summary>
    public sealed class PlayerInteractor : NetworkBehaviour
    {
        public static string LocalPrompt { get; private set; } = string.Empty;
        public static bool LocalPromptAffordable { get; private set; }
        public static event Action<string, bool> LocalResult;

        private PlayerAvatar avatar;
        private float requestCooldown;
        private float reviveSendTimer;
        private double lastServerRequest;

        public PlayerAvatar ReviveTarget { get; private set; }

        private void Awake()
        {
            avatar = GetComponent<PlayerAvatar>();
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner) LocalPrompt = string.Empty;
        }

        private void Update()
        {
            if (!IsOwner) return;
            requestCooldown -= Time.deltaTime;
            LocalPrompt = string.Empty;
            ReviveTarget = null;

            var match = MatchState.Instance;
            if (match == null || match.Phase != MatchPhase.Playing || avatar.Life != LifeState.Alive) return;
            string key = GameSettings.Current.KeyInteract.ToString();
            var input = KeyboardMouseInput.Instance;

            // Reviving a downed teammate takes priority over purchases.
            ReviveTarget = FindDownedTeammate();
            if (ReviveTarget != null)
            {
                LocalPrompt = $"Hold [{key}] to revive {ReviveTarget.DisplayName}";
                LocalPromptAffordable = true;
                if (input.InteractHeld && !InputGate.Blocked)
                {
                    reviveSendTimer -= Time.deltaTime;
                    if (reviveSendTimer <= 0f)
                    {
                        reviveSendTimer = 0.1f;
                        ReviveHoldRpc(ReviveTarget.NetworkObjectId);
                    }
                }
                return;
            }

            var target = FindInteractable(match);
            if (target == null) return;
            var quote = target.Quote(match, avatar);
            LocalPrompt = target.Describe(match, avatar, key);
            LocalPromptAffordable = quote.Affordable(avatar.Points);

            if (input.InteractPressed && !InputGate.Blocked && requestCooldown <= 0f)
            {
                requestCooldown = InteractionRules.RequestCooldownSeconds;
                if (quote.Available && !quote.Affordable(avatar.Points))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.Play2D(Sfx.Denied, 0.7f);
                    LocalResult?.Invoke("Not enough points", false);
                    return;
                }
                if (quote.Available) InteractRpc(target.Id);
            }
        }

        private PlayerAvatar FindDownedTeammate()
        {
            foreach (var p in PlayerAvatar.All)
            {
                if (p == avatar || p.Life != LifeState.Downed) continue;
                if (Vector3.Distance(p.transform.position, transform.position) <= ReviveRules.ReviveRange) return p;
            }
            return null;
        }

        private Interactable FindInteractable(MatchState match)
        {
            Interactable best = null;
            float bestScore = float.MaxValue;
            var center = avatar.Center;
            var aim = avatar.Motor.AimDirection;
            foreach (var it in Interactable.All)
            {
                if (!it.IsActive(match)) continue;
                float d = it.DistanceTo(center);
                if (d > InteractionRules.PromptRange) continue;
                // Prefer what the player is looking at when several things are close.
                var to = (it.Point - avatar.EyePosition).normalized;
                float score = d - Vector3.Dot(aim, to) * 0.8f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = it;
                }
            }
            return best;
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void InteractRpc(int interactableId)
        {
            double now = Time.timeAsDouble;
            if (now - lastServerRequest < InteractionRules.RequestCooldownSeconds * 0.8) return;
            lastServerRequest = now;
            bool ok = ServerTryInteract(interactableId, out var message);
            ResultRpc(ok ? string.Empty : message, ok);
        }

        /// <summary>Full authority-side validation path for a purchase/use request (also used by the smoke test).</summary>
        public bool ServerTryInteract(int interactableId, out string message)
        {
            message = string.Empty;
            var match = MatchState.Instance;
            if (!IsServer || match == null || avatar.Life != LifeState.Alive) return false;
            if (!Interactable.TryGet(interactableId, out var target))
            {
                message = "Unknown object";
                return false;
            }
            // Range is checked against the authority's own copy of the map and its view of the player.
            if (!InteractionRules.InRange(target.DistanceTo(avatar.Center)))
            {
                message = "Too far away";
                return false;
            }
            return match.ServerHandleInteract(avatar, target, out message);
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void ReviveHoldRpc(ulong targetObjectId)
        {
            foreach (var p in PlayerAvatar.All)
            {
                if (p.NetworkObjectId != targetObjectId) continue;
                p.ServerReviveHold(avatar);
                return;
            }
        }

        [Rpc(SendTo.Owner)]
        private void ResultRpc(string message, bool success)
        {
            if (!success && AudioManager.Instance != null) AudioManager.Instance.Play2D(Sfx.Denied, 0.7f);
            if (!string.IsNullOrEmpty(message)) LocalResult?.Invoke(message, success);
        }
    }
}
