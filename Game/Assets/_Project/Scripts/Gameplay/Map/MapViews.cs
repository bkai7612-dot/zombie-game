using UnityEngine;
using UnityEngine.AI;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Visual + collision state of a purchasable door. Opening is driven by the synchronised door mask.</summary>
    public sealed class DoorView : MonoBehaviour
    {
        public int DoorId;
        public Transform Panel;
        public float Height;
        public NavMeshObstacle Obstacle;
        public Light LockLight;

        private bool open;
        private float animT = -1f;
        private Vector3 closedPos;

        public bool IsOpen => open;

        public void Init(int id, Transform panel, float height, NavMeshObstacle obstacle, Light lockLight)
        {
            DoorId = id;
            Panel = panel;
            Height = height;
            Obstacle = obstacle;
            LockLight = lockLight;
            closedPos = panel.localPosition;
        }

        public void SetOpen(bool value, bool instant)
        {
            if (open == value) return;
            open = value;
            if (!open) return; // Doors never re-lock during a run.
            if (Obstacle != null) Obstacle.enabled = false;
            foreach (var c in Panel.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (LockLight != null) LockLight.color = new Color(0.3f, 1f, 0.4f);
            if (instant)
            {
                Panel.gameObject.SetActive(false);
            }
            else
            {
                animT = 0f;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.DoorOpen, transform.position, 1f);
            }
        }

        private void Update()
        {
            if (animT < 0f) return;
            animT += Time.deltaTime / 1.6f;
            float e = animT * animT;
            Panel.localPosition = closedPos + Vector3.down * (Height + 0.2f) * Mathf.Clamp01(e);
            if (animT >= 1f)
            {
                Panel.gameObject.SetActive(false);
                animT = -1f;
            }
        }
    }

    /// <summary>Mystery box visuals: follows the synchronised location and shows the rolled weapon.</summary>
    public sealed class BoxView : MonoBehaviour
    {
        public Transform Lid;
        public Transform OfferAnchor;
        public Light Beam;

        private int location = -1;
        private BoxState lastState;
        private WeaponId shownWeapon;
        private GameObject offerModel;
        private float spinTimer;
        private bool shown = true;

        public void Sync(MatchState match)
        {
            if (match == null) return;
            if (match.BoxLocation != location)
            {
                location = Mathf.Clamp(match.BoxLocation, 0, MapCatalog.BoxLocations.Length - 1);
                var def = MapCatalog.BoxLocations[location];
                transform.position = Geo.V(def.Position);
                transform.rotation = Quaternion.Euler(0f, def.Yaw, 0f);
            }

            var state = match.BoxState;
            bool visible = state != BoxState.Moving;
            if (visible != shown)
            {
                shown = visible;
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
                if (Beam != null) Beam.enabled = visible;
            }

            if (state != lastState && AudioManager.Instance != null)
            {
                if (state == BoxState.Rolling) AudioManager.Instance.PlayAt(Sfx.BoxRoll, transform.position);
                if (state == BoxState.Offering) AudioManager.Instance.PlayAt(Sfx.BoxOffer, transform.position);
            }
            lastState = state;

            if (Lid != null) Lid.localRotation = Quaternion.Euler(state == BoxState.Idle || state == BoxState.Moving ? 0f : -70f, 0f, 0f);

            WeaponId want = WeaponId.None;
            if (state == BoxState.Rolling)
            {
                spinTimer += Time.deltaTime;
                var all = new[] { WeaponId.Smg, WeaponId.Shotgun, WeaponId.AssaultRifle, WeaponId.MarksmanRifle, WeaponId.PlasmaRifle };
                want = all[(int)(spinTimer * 8f) % all.Length];
            }
            else if (state == BoxState.Offering)
            {
                want = match.BoxOfferWeapon;
            }

            if (want != shownWeapon)
            {
                if (offerModel != null) Destroy(offerModel);
                offerModel = want != WeaponId.None ? WeaponModels.Build(OfferAnchor, want, false, 1.4f) : null;
                shownWeapon = want;
            }
            if (offerModel != null)
            {
                OfferAnchor.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                OfferAnchor.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(Time.time * 2f) * 0.05f, 0f);
            }
        }
    }

    /// <summary>The escape saucer: idles, powers up during the countdown and lifts off on victory.</summary>
    public sealed class UfoView : MonoBehaviour
    {
        public Transform Saucer;
        public Transform HatchA;
        public Transform HatchB;
        public Light Glow;
        public Transform BeamCone;

        private bool charging;
        private bool launched;
        private float launchT;
        private Vector3 saucerStart;

        private void Start()
        {
            if (Saucer != null) saucerStart = Saucer.localPosition;
        }

        public void Sync(MatchState match)
        {
            if (match == null || Saucer == null) return;
            bool nowCharging = match.EscapeActive;
            if (nowCharging && !charging && AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.UfoCharge, Saucer.position, 1f, 0f, 80f);
            charging = nowCharging;

            if (match.Phase == MatchPhase.Victory && !launched)
            {
                launched = true;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.UfoLaunch, Saucer.position, 1f, 0f, 120f);
            }

            Saucer.Rotate(0f, (charging ? 120f : 15f) * Time.deltaTime, 0f, Space.Self);
            if (Glow != null) Glow.intensity = charging ? 3f + Mathf.Sin(Time.time * 8f) * 1.2f : 1.2f;
            if (BeamCone != null) BeamCone.gameObject.SetActive(charging || launched);

            float hatchOpen = charging || launched ? 1f : 0f;
            if (HatchA != null) HatchA.localPosition = Vector3.Lerp(HatchA.localPosition, new Vector3(-3f - 6.5f * hatchOpen, 0f, 0f), Time.deltaTime * 0.8f);
            if (HatchB != null) HatchB.localPosition = Vector3.Lerp(HatchB.localPosition, new Vector3(3f + 6.5f * hatchOpen, 0f, 0f), Time.deltaTime * 0.8f);

            if (launched)
            {
                launchT += Time.deltaTime;
                Saucer.localPosition = saucerStart + Vector3.up * (launchT * launchT * 3f);
            }
        }
    }

    /// <summary>Floating power-up pickup; mirrors the single replicated drop slot in <see cref="MatchState"/>.</summary>
    public sealed class PowerUpView : MonoBehaviour
    {
        private PowerUpType shown;
        private GameObject model;
        private Light glow;
        private TextMesh label;

        public void Sync(MatchState match)
        {
            var type = match != null ? match.DropType : PowerUpType.None;
            if (type != shown)
            {
                if (model != null) Destroy(model);
                model = null;
                shown = type;
                if (type != PowerUpType.None)
                {
                    model = Geo.Group(transform, "Drop").gameObject;
                    var color = Color(type);
                    var mat = Palette.Emit("power" + (int)type, color, color * 2.5f);
                    switch (type)
                    {
                        case PowerUpType.MaxAmmo:
                            Geo.Box(model.transform, "Box", Vector3.zero, new Vector3(0.5f, 0.35f, 0.35f), mat, false);
                            break;
                        case PowerUpType.Nuke:
                            Geo.Sphere(model.transform, "Bomb", Vector3.zero, Vector3.one * 0.5f, mat);
                            break;
                        default:
                            Geo.Box(model.transform, "Token", Vector3.zero, new Vector3(0.5f, 0.5f, 0.08f), mat, false);
                            break;
                    }
                    glow = Geo.PointLight(model.transform, Vector3.zero, color, 2.5f, 5f);
                    label = Geo.Label(model.transform, PowerUpRules.Name(type), new Vector3(0, 0.55f, 0), 0f, 0.03f, color);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(Sfx.BoxOffer, match.DropPosition, 1f);
                }
            }
            if (model == null) return;
            transform.position = match.DropPosition + Vector3.up * Mathf.Sin(Time.time * 3f) * 0.12f;
            model.transform.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            if (glow != null) glow.intensity = 2f + Mathf.Sin(Time.time * 6f);
            var cam = CameraDirector.Instance != null ? CameraDirector.Instance.transform : null;
            if (label != null && cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.position);
        }

        private static Color Color(PowerUpType t)
        {
            switch (t)
            {
                case PowerUpType.MaxAmmo: return new Color(1f, 0.8f, 0.2f);
                case PowerUpType.InstaKill: return new Color(1f, 0.25f, 0.2f);
                case PowerUpType.DoublePoints: return new Color(0.3f, 1f, 0.5f);
                default: return new Color(1f, 0.55f, 0.1f);
            }
        }
    }
}
