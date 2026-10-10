using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// Owns the single scene camera: menu backdrop, lobby fly-over, first person for the local player,
    /// and spectating teammates while dead or after the match.
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance { get; private set; }

        public Camera Camera { get; private set; }
        public bool FirstPerson { get; private set; }

        private ViewModel viewModel;
        private float shake;
        private float orbit;
        private int spectateIndex;
        private float fovCurrent = 80f;

        public static CameraDirector Ensure()
        {
            if (Instance != null) return Instance;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var existing = cam.gameObject.GetComponent<CameraDirector>();
            return existing != null ? existing : cam.gameObject.AddComponent<CameraDirector>();
        }

        private void Awake()
        {
            Instance = this;
            Camera = GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.06f, 0.075f, 0.1f);
            Camera.nearClipPlane = 0.03f;
            Camera.farClipPlane = 400f;
            viewModel = ViewModel.Create(transform);
            PlayerAvatar.LocalHurt += (amount, from) => shake = Mathf.Min(1f, shake + amount / 60f);
        }

        public void CycleSpectate() => spectateIndex++;

        private void LateUpdate()
        {
            var match = MatchState.Instance;
            var local = PlayerAvatar.Local;
            float dt = Time.unscaledDeltaTime;
            shake = Mathf.Max(0f, shake - dt * 2.5f);
            FirstPerson = false;
            float targetFov = GameSettings.Current.FieldOfView;

            if (match == null)
            {
                // Main menu: slow drift over an empty sky / horizon.
                orbit += dt * 2f;
                transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(-4f, orbit, 0f));
            }
            else if (match.Phase == MatchPhase.Lobby || local == null)
            {
                Orbit(new Vector3(5f, 0f, -2f), 45f, 28f, dt * 5f);
            }
            else if (match.Phase == MatchPhase.Victory)
            {
                var ufo = Geo.V(MapCatalog.UfoPosition);
                Orbit(ufo + Vector3.up * 2f, 18f, 8f, dt * 10f);
            }
            else if (local.Life == LifeState.Alive || local.Life == LifeState.Downed)
            {
                FirstPerson = true;
                var motor = local.Motor;
                var pos = motor.EyeWorldPosition;
                var rot = motor.AimRotation;
                if (shake > 0f) rot *= Quaternion.Euler(Random.Range(-1f, 1f) * shake * 2f, Random.Range(-1f, 1f) * shake * 2f, 0f);
                transform.SetPositionAndRotation(pos, rot);
                if (local.Life == LifeState.Alive && KeyboardMouseInput.Instance.AimHeld && !InputGate.Blocked)
                    targetFov *= local.Weapons.View.Active.Weapon == WeaponId.MarksmanRifle ? 0.45f : 0.75f;
            }
            else
            {
                // Spectate a teammate who is still in the fight.
                PlayerAvatar target = null;
                int alive = 0;
                foreach (var p in PlayerAvatar.All) if (p.Life == LifeState.Alive || p.Life == LifeState.Downed) alive++;
                if (alive > 0)
                {
                    int want = spectateIndex % alive, i = 0;
                    foreach (var p in PlayerAvatar.All)
                    {
                        if (p.Life != LifeState.Alive && p.Life != LifeState.Downed) continue;
                        if (i++ == want) target = p;
                    }
                }
                if (target != null)
                {
                    var look = target.transform.position + Vector3.up * 1.6f;
                    var back = Quaternion.Euler(20f, target.transform.eulerAngles.y, 0f) * new Vector3(0f, 0f, -4f);
                    var desired = look + back;
                    if (Physics.Linecast(look, desired, out var hit, ~0, QueryTriggerInteraction.Ignore)) desired = hit.point + hit.normal * 0.2f;
                    transform.position = Vector3.Lerp(transform.position, desired, dt * 8f);
                    transform.rotation = Quaternion.LookRotation(look - transform.position);
                }
                else
                {
                    Orbit(new Vector3(5f, 0f, -2f), 45f, 28f, dt * 5f);
                }
            }

            fovCurrent = Mathf.Lerp(fovCurrent, targetFov, dt * 12f);
            Camera.fieldOfView = fovCurrent;
            viewModel.SetVisible(FirstPerson && local != null && local.Life == LifeState.Alive);
        }

        private void Orbit(Vector3 center, float radius, float height, float speedDeg)
        {
            orbit += speedDeg;
            var offset = Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, height, -radius);
            transform.position = center + offset;
            transform.rotation = Quaternion.LookRotation(center - transform.position);
        }
    }
}
