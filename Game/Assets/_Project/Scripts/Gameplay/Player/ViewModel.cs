using UnityEngine;
using UnityEngine.Rendering;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>First-person weapon model: bob, sway, recoil, reload dip, knife swing and muzzle flash.</summary>
    public sealed class ViewModel : MonoBehaviour
    {
        private Transform anchor;
        private Transform knifeAnchor;
        private GameObject model;
        private GameObject knife;
        private Light flash;
        private WeaponId shownWeapon = (WeaponId)(-1);
        private bool shownUpgraded;
        private PlayerAvatar boundTo;
        private float recoil;
        private float knifeT = -1f;
        private float bob;
        private bool visible = true;

        private static readonly Vector3 Rest = new Vector3(0.22f, -0.22f, 0.42f);

        public static ViewModel Create(Transform camera)
        {
            var go = new GameObject("ViewModel");
            go.transform.SetParent(camera, false);
            var vm = go.AddComponent<ViewModel>();
            vm.anchor = Geo.Group(go.transform, "Anchor", Rest).transform;
            vm.knifeAnchor = Geo.Group(go.transform, "KnifeAnchor", new Vector3(0.15f, -0.25f, 0.35f)).transform;
            vm.knife = WeaponModels.Knife(vm.knifeAnchor);
            vm.knife.SetActive(false);
            vm.flash = Geo.PointLight(go.transform, new Vector3(0.2f, -0.1f, 0.9f), new Color(1f, 0.8f, 0.45f), 0f, 8f);
            return vm;
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            anchor.gameObject.SetActive(value);
            if (!value) knife.SetActive(false);
        }

        private void Update()
        {
            var local = PlayerAvatar.Local;
            if (local != boundTo)
            {
                if (boundTo != null)
                {
                    boundTo.Weapons.LocalFired -= OnFired;
                    boundTo.Weapons.LocalMelee -= OnMelee;
                }
                boundTo = local;
                if (boundTo != null)
                {
                    boundTo.Weapons.LocalFired += OnFired;
                    boundTo.Weapons.LocalMelee += OnMelee;
                }
            }
            if (local == null || !visible) return;

            var active = local.Weapons.View.Active;
            if (active.Weapon != shownWeapon || active.Upgraded != shownUpgraded)
            {
                if (model != null) Destroy(model);
                shownWeapon = active.Weapon;
                shownUpgraded = active.Upgraded;
                model = shownWeapon != WeaponId.None ? WeaponModels.Build(anchor, shownWeapon, shownUpgraded, 1f) : null;
                if (model != null)
                {
                    foreach (var r in model.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    var muzzle = model.transform.Find("Muzzle");
                    Effects.LocalMuzzle = muzzle;
                }
            }

            float dt = Time.deltaTime;
            float speed = local.Motor.CurrentSpeed;
            bob += dt * (speed > 0.5f ? 6f + speed : 1.5f);
            float bobAmount = Mathf.Clamp01(speed / 6f) * (local.Motor.IsSprinting ? 0.03f : 0.015f) + 0.002f;
            var offset = new Vector3(Mathf.Cos(bob) * bobAmount, Mathf.Abs(Mathf.Sin(bob)) * bobAmount, 0f);

            bool aiming = KeyboardMouseInput.Instance.AimHeld && !InputGate.Blocked;
            var rest = aiming ? new Vector3(0f, -0.13f, 0.32f) : Rest;
            if (local.Motor.IsSprinting) rest += new Vector3(-0.05f, -0.05f, 0f);

            recoil = Mathf.MoveTowards(recoil, 0f, dt * 8f);
            float reloadDip = local.Weapons.IsReloading ? 1f : 0f;
            anchor.localPosition = Vector3.Lerp(anchor.localPosition, rest + offset + new Vector3(0f, -0.25f * reloadDip, -0.06f * recoil), dt * 14f);
            anchor.localRotation = Quaternion.Slerp(anchor.localRotation,
                Quaternion.Euler(-12f * recoil + 35f * reloadDip, local.Motor.IsSprinting ? -25f : 0f, 10f * reloadDip), dt * 14f);

            flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, dt * 60f);

            if (knifeT >= 0f)
            {
                knifeT += dt / 0.3f;
                knife.SetActive(true);
                knifeAnchor.localRotation = Quaternion.Euler(10f, Mathf.Lerp(60f, -70f, knifeT), 0f);
                if (knifeT >= 1f)
                {
                    knifeT = -1f;
                    knife.SetActive(false);
                }
            }
        }

        private void OnFired(WeaponId weapon, bool upgraded)
        {
            recoil = Mathf.Min(1f, recoil + (weapon == WeaponId.Shotgun || weapon == WeaponId.MarksmanRifle ? 1f : 0.35f));
            flash.color = weapon == WeaponId.PlasmaRifle || upgraded ? new Color(0.5f, 1f, 0.7f) : new Color(1f, 0.8f, 0.45f);
            flash.intensity = 4f;
        }

        private void OnMelee() => knifeT = 0f;
    }
}
