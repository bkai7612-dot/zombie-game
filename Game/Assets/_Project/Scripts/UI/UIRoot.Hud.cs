using UnityEngine;
using UZS.Core;
using UZS.Gameplay;

namespace UZS.UI
{
    public sealed partial class UIRoot
    {
        private float hitMarker;
        private bool hitKill;
        private bool hitHead;
        private float hurtFlash;
        private Vector3 hurtFrom;
        private PlayerWeapons boundWeapons;

        private void OnLocalSpawned(PlayerAvatar avatar)
        {
            if (boundWeapons != null) boundWeapons.LocalHitConfirmed -= OnHit;
            boundWeapons = avatar.Weapons;
            boundWeapons.LocalHitConfirmed += OnHit;
        }

        private void OnHit(bool kill, bool head)
        {
            hitMarker = 0.25f;
            hitKill = kill;
            hitHead = head;
        }

        private void OnLocalHurt(float amount, Vector3 from)
        {
            hurtFlash = Mathf.Min(1f, hurtFlash + amount / 50f);
            hurtFrom = from;
        }

        private void UpdateHudTimers()
        {
            float dt = Time.unscaledDeltaTime;
            hitMarker = Mathf.Max(0f, hitMarker - dt);
            hurtFlash = Mathf.Max(0f, hurtFlash - dt * 0.8f);
        }

        private void DrawHud(float w, float h, MatchState match)
        {
            var me = PlayerAvatar.Local;
            if (me == null) return;

            // Damage vignette, stronger at low health.
            float lowHealth = 1f - Mathf.Clamp01(me.Health / Mathf.Max(1f, me.MaxHealth));
            float vignette = Mathf.Clamp01(hurtFlash * 0.6f + (me.Life == LifeState.Alive ? lowHealth * 0.45f : 0f));
            if (vignette > 0.01f)
            {
                var c = new Color(0.6f, 0f, 0f, vignette * 0.55f);
                UISkin.Fill(new Rect(0, 0, w, 90), c);
                UISkin.Fill(new Rect(0, h - 90, w, 90), c);
                UISkin.Fill(new Rect(0, 0, 90, h), c);
                UISkin.Fill(new Rect(w - 90, 0, 90, h), c);
                DrawHurtDirection(w, h, me);
            }

            // Round counter.
            UISkin.Shadowed(new Rect(40, 20, 400, 100), match.Round > 0 ? match.Round.ToString() : "-", UISkin.RoundNumber, UISkin.Accent);
            string sub = match.IntermissionLeft > 0.05f ? $"Next round in {Mathf.CeilToInt(match.IntermissionLeft)}" : $"Zombies left: {match.ZombiesLeft}";
            UISkin.Shadowed(new Rect(44, 118, 500, 36), sub, UISkin.Label, Color.white);

            // Escape objective.
            if (match.EscapeActive)
            {
                string t = match.EscapeSecondsLeft > 0.01f
                    ? $"SAUCER LAUNCH IN {Mathf.CeilToInt(match.EscapeSecondsLeft)}s  -  HOLD OUT"
                    : match.BoardingSecondsLeft >= 0f
                        ? $"BOARDING - LIFT-OFF IN {Mathf.CeilToInt(match.BoardingSecondsLeft)}s - EVERYONE INTO THE GREEN CIRCLE"
                        : "SAUCER READY - EVERYONE INTO THE GREEN CIRCLE (Hangar)";
                if (match.BoardingSecondsLeft >= 0f && MatchState.IsAboard(me)) t += "  -  YOU'RE ABOARD";
                UISkin.Shadowed(new Rect(0, 30, w, 50), t, UISkin.Prompt, UISkin.Alien);
            }
            else if (match.EscapeFunded > 0)
            {
                UISkin.Shadowed(new Rect(0, 30, w, 40), $"Saucer fuel: {match.EscapeFunded} / {match.EscapeCost}", UISkin.Prompt, UISkin.Alien);
            }

            // Active power-ups.
            float py = 80;
            if (match.InstaKillLeft > 0f)
            {
                UISkin.Shadowed(new Rect(0, py, w, 36), $"INSTA-KILL  {Mathf.CeilToInt(match.InstaKillLeft)}", UISkin.Prompt, new Color(1f, 0.35f, 0.3f));
                py += 36;
            }
            if (match.DoublePointsLeft > 0f)
                UISkin.Shadowed(new Rect(0, py, w, 36), $"DOUBLE POINTS  {Mathf.CeilToInt(match.DoublePointsLeft)}", UISkin.Prompt, new Color(0.4f, 1f, 0.55f));

            DrawTeam(w, me);
            DrawSelf(w, h, me);
            DrawCrosshair(w, h, me);

            // Interaction prompt.
            if (!string.IsNullOrEmpty(PlayerInteractor.LocalPrompt))
            {
                var color = PlayerInteractor.LocalPromptAffordable ? Color.white : new Color(1f, 0.55f, 0.5f);
                UISkin.Shadowed(new Rect(0, h * 0.62f, w, 50), PlayerInteractor.LocalPrompt, UISkin.Prompt, color);
                var target = me.Interactor.ReviveTarget;
                if (target != null && target.ReviveProgress > 0f)
                    UISkin.Bar(new Rect(w / 2 - 200, h * 0.62f + 56, 400, 14), target.ReviveProgress, UISkin.Alien, new Color(0, 0, 0, 0.6f));
            }

            // Downed / dead state.
            if (me.Life == LifeState.Downed)
            {
                // Kept below the announcement feed (0.14h-0.36h) so the two never overlap.
                UISkin.Shadowed(new Rect(0, h * 0.40f, w, 70), "YOU ARE DOWN", UISkin.Huge, UISkin.Accent);
                string hint = match.IsSinglePlayer ? string.Empty : $"Bleeding out in {Mathf.CeilToInt(me.BleedoutLeft)}s - you can still shoot while a teammate revives you";
                UISkin.Shadowed(new Rect(0, h * 0.40f + 80, w, 40), hint, UISkin.Center, Color.white);
                if (me.ReviveProgress > 0f) UISkin.Bar(new Rect(w / 2 - 250, h * 0.40f + 130, 500, 16), me.ReviveProgress, UISkin.Alien, new Color(0, 0, 0, 0.6f));
            }
            else if (me.Life == LifeState.Dead && !match.IsOver)
            {
                UISkin.Shadowed(new Rect(0, h * 0.2f, w, 60), "YOU DIED - SPECTATING", UISkin.Prompt, UISkin.Accent);
                UISkin.Shadowed(new Rect(0, h * 0.2f + 50, w, 40), "You will respawn when your team survives this round. Click to switch view.", UISkin.Center, Color.white);
            }

            // Announcement feed.
            float fy = h * 0.14f;
            foreach (var item in feed)
            {
                UISkin.Shadowed(new Rect(0, fy, w, 40), item.text, item.major ? UISkin.Prompt : UISkin.Center, item.major ? UISkin.Gold : Color.white);
                fy += item.major ? 44 : 34;
            }

            if (KeyboardMouseInput.Instance.ScoreboardHeld && !match.IsOver)
            {
                float sw = Mathf.Min(1400f, w - 40f);
                var r = new Rect(w / 2 - sw / 2, 200, sw, 80 + PlayerAvatar.All.Count * 48);
                GUI.Box(r, GUIContent.none, UISkin.Panel);
                ScoreboardRows(r, match);
            }
        }

        private void DrawHurtDirection(float w, float h, PlayerAvatar me)
        {
            if (hurtFlash < 0.05f) return;
            var to = hurtFrom - me.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            float angle = Vector3.SignedAngle(me.Motor.AimDirection.WithY0(), to, Vector3.up);
            var center = new Vector2(w / 2, h / 2);
            var dir = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), -Mathf.Cos(angle * Mathf.Deg2Rad));
            var p = center + dir * 180f;
            UISkin.Fill(new Rect(p.x - 18, p.y - 18, 36, 36), new Color(1f, 0.1f, 0.05f, hurtFlash));
        }

        private static void DrawTeam(float w, PlayerAvatar me)
        {
            float y = 30;
            foreach (var p in PlayerAvatar.All)
            {
                var def = CharacterCatalog.Get(p.CharacterId);
                var r = new Rect(w - 420, y, 390, 46);
                UISkin.Fill(r, new Color(0, 0, 0, 0.45f));
                UISkin.Fill(new Rect(r.x, r.y, 8, r.height), new Color(def.Jacket.R, def.Jacket.G, def.Jacket.B));
                string state = p.Life == LifeState.Downed ? " <color=#FFB060>DOWN</color>" : p.Life == LifeState.Dead ? " <color=#FF6060>DEAD</color>" :
                    p.Life == LifeState.Escaped ? " <color=#7CFF9A>ESCAPED</color>" : string.Empty;
                GUI.Label(new Rect(r.x + 18, r.y + 6, 250, 36), (p == me ? "<b>" + p.DisplayName + "</b>" : p.DisplayName) + state, UISkin.Label);
                var style = UISkin.Label;
                var old = style.alignment;
                style.alignment = TextAnchor.MiddleRight;
                UISkin.Shadowed(new Rect(r.x + 200, r.y + 4, 175, 38), p.Points.ToString(), style, UISkin.Gold);
                style.alignment = old;
                y += 52;
            }
        }

        private static void DrawSelf(float w, float h, PlayerAvatar me)
        {
            // Health.
            var hr = new Rect(40, h - 90, 380, 22);
            UISkin.Bar(hr, me.Health / Mathf.Max(1f, me.MaxHealth), new Color(0.75f, 0.12f, 0.1f), new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(40, h - 130, 380, 36), $"Health {Mathf.CeilToInt(me.Health)}", UISkin.Label);

            // Perks.
            float px = 40;
            foreach (var perk in PerkCatalog.All)
            {
                if ((me.Perks & perk) == 0) continue;
                var r = new Rect(px, h - 170, 120, 32);
                UISkin.Fill(r, new Color(0.15f, 0.15f, 0.17f, 0.85f));
                GUI.Label(r, PerkCatalog.Name(perk).Split(' ')[0], UISkin.Small);
                px += 128;
            }

            // Ammo.
            var weapons = me.Weapons;
            var active = weapons.View.Active;
            if (!active.IsEmpty)
            {
                var style = UISkin.Huge;
                var old = style.alignment;
                style.alignment = TextAnchor.LowerRight;
                int mag = weapons.PredictedMagazine;
                var color = mag == 0 ? UISkin.Accent : Color.white;
                UISkin.Shadowed(new Rect(w - 520, h - 140, 340, 90), mag.ToString(), style, color);
                style.alignment = old;
                GUI.Label(new Rect(w - 170, h - 98, 140, 50), "/ " + active.Reserve, UISkin.Subtitle);
                var nameColor = active.Upgraded ? new Color(0.85f, 0.55f, 1f) : Color.white;
                var label = UISkin.Label;
                var la = label.alignment;
                label.alignment = TextAnchor.MiddleRight;
                UISkin.Shadowed(new Rect(w - 620, h - 46, 590, 36), WeaponCatalog.NameOf(active.Weapon, active.Upgraded) + (weapons.IsReloading ? "  -  reloading" : string.Empty), label, nameColor);
                var other = weapons.View[1 - weapons.ActiveSlot];
                if (!other.IsEmpty)
                    UISkin.Shadowed(new Rect(w - 620, h - 180, 590, 30), $"[{GameSettings.Current.KeySwap}] " + WeaponCatalog.NameOf(other.Weapon, other.Upgraded), label, UISkin.Dim);
                label.alignment = la;
                if (mag == 0 && active.Reserve > 0 && !weapons.IsReloading)
                    UISkin.Shadowed(new Rect(0, h * 0.56f, w, 40), $"Press [{GameSettings.Current.KeyReload}] to reload", UISkin.Center, UISkin.Gold);
            }
        }

        private void DrawCrosshair(float w, float h, PlayerAvatar me)
        {
            if (me.Life != LifeState.Alive && me.Life != LifeState.Downed) return;
            float cx = w / 2, cy = h / 2;
            var c = new Color(1f, 1f, 1f, 0.85f);
            float gap = KeyboardMouseInput.Instance.AimHeld ? 4f : 10f;
            UISkin.Fill(new Rect(cx - gap - 12, cy - 1, 12, 2), c);
            UISkin.Fill(new Rect(cx + gap, cy - 1, 12, 2), c);
            UISkin.Fill(new Rect(cx - 1, cy - gap - 12, 2, 12), c);
            UISkin.Fill(new Rect(cx - 1, cy + gap, 2, 12), c);
            if (hitMarker > 0f)
            {
                var hc = hitKill ? UISkin.Accent : (hitHead ? UISkin.Gold : Color.white);
                hc.a = hitMarker / 0.25f;
                float s = hitKill ? 16f : 12f;
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy) * UISkin.Scale);
                UISkin.Fill(new Rect(cx - s - 6, cy - 1.5f, s, 3), hc);
                UISkin.Fill(new Rect(cx + 6, cy - 1.5f, s, 3), hc);
                UISkin.Fill(new Rect(cx - 1.5f, cy - s - 6, 3, s), hc);
                UISkin.Fill(new Rect(cx - 1.5f, cy + 6, 3, s), hc);
                GUI.matrix = m;
            }
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 WithY0(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
