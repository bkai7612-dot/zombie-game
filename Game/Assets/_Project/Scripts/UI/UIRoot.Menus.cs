using System;
using UnityEngine;
using UZS.Core;
using UZS.Gameplay;

namespace UZS.UI
{
    public sealed partial class UIRoot
    {
        private string joinCode = string.Empty;
        private string directAddress = "127.0.0.1";
        private string directPort = GameInfo.DefaultDirectPort.ToString();
        private string waitingForKey;
        private Vector2 scroll;

        // ------------------------------------------------------------------ main menus

        private void DrawMenus(float w, float h)
        {
            UISkin.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.35f));
            GUI.Label(new Rect(0, 70, w, 100), GameInfo.DisplayTitle.ToUpperInvariant(), UISkin.Title);
            GUI.Label(new Rect(0, 160, w, 40), "Hollow Pines  -  co-op zombie survival", UISkin.Center);

            switch (screen)
            {
                case Page.Online: DrawOnline(w, h); break;
                case Page.Direct: DrawDirect(w, h); break;
                case Page.Characters: DrawCharacters(w, h, false); break;
                case Page.Settings: DrawSettings(w, h); break;
                default: DrawMain(w, h); break;
            }

            GUI.Label(new Rect(20, h - 44, w - 40, 34),
                $"v{GameInfo.ProtocolVersion}  -  Pre-release build. Art and audio are original placeholders.", UISkin.Small);
        }

        private void DrawMain(float w, float h)
        {
            float bw = 460, bh = 72, x = w / 2 - bw / 2, y = 280;
            var cc = ConnectionController.Instance;

            if (GUI.Button(new Rect(x, y, bw, bh), "SINGLE PLAYER", UISkin.BigButton))
            {
                if (cc == null || !cc.StartSinglePlayer()) modalMessage = "Could not start a local game. Another program may be blocking local networking.";
            }
            y += bh + 16;
            if (GUI.Button(new Rect(x, y, bw, bh), "PLAY ONLINE", UISkin.BigButton)) screen = Page.Online;
            y += bh + 16;
            if (GUI.Button(new Rect(x, y, bw, bh), "LAN / DIRECT CONNECT", UISkin.Button)) screen = Page.Direct;
            y += bh + 16;
            if (GUI.Button(new Rect(x, y, bw, bh), "SURVIVORS", UISkin.Button)) screen = Page.Characters;
            y += bh + 16;
            if (GUI.Button(new Rect(x, y, bw, bh), "SETTINGS", UISkin.Button))
            {
                settingsReturn = Page.Main;
                screen = Page.Settings;
            }
            y += bh + 16;
            if (GUI.Button(new Rect(x, y, bw, bh), "QUIT", UISkin.Button)) Application.Quit();

            var s = GameSettings.Current;
            var def = CharacterCatalog.Get(s.CharacterId);
            GUI.Label(new Rect(x - 300, y + bh + 30, bw + 600, 40), $"Playing as <b>{s.PlayerName}</b>  -  {def.Name}, {def.Role}", UISkin.Center);
        }

        private void PlayerNameField(Rect r)
        {
            var s = GameSettings.Current;
            GUI.Label(new Rect(r.x, r.y, 200, r.height), "Your name", UISkin.Label);
            string next = GUI.TextField(new Rect(r.x + 200, r.y, r.width - 200, r.height), s.PlayerName, ConnectionPayload.MaxNameLength, UISkin.Field);
            if (next != s.PlayerName)
            {
                s.PlayerName = next;
                s.Save();
            }
        }

        private void DrawOnline(float w, float h)
        {
            var session = OnlineServices.Session;
            var r = new Rect(w / 2 - 480, 240, 960, 640);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            float x = r.x + 40, y = r.y + 30, cw = r.width - 80;
            GUI.Label(new Rect(x, y, cw, 44), "PLAY ONLINE (2-4 players)", UISkin.Subtitle);
            y += 64;

            if (!session.IsAvailable)
            {
                GUI.Label(new Rect(x, y, cw, 200), session.UnavailableReason, UISkin.Label);
                if (GUI.Button(new Rect(x, r.yMax - 90, 220, 60), "Back", UISkin.Button)) screen = Page.Main;
                return;
            }

            PlayerNameField(new Rect(x, y, cw, 50));
            y += 80;
            GUI.enabled = !session.IsBusy;
            if (GUI.Button(new Rect(x, y, cw, 70), "FIND MATCH  (join a random public game)", UISkin.BigButton)) _ = session.FindMatchAsync();
            y += 90;
            if (GUI.Button(new Rect(x, y, cw, 60), "CREATE PRIVATE GAME  (friends join with a code)", UISkin.Button)) _ = session.CreatePrivateAsync();
            y += 80;
            GUI.Label(new Rect(x, y + 8, 200, 44), "Join code", UISkin.Label);
            joinCode = GUI.TextField(new Rect(x + 200, y, 360, 56), joinCode, 12, UISkin.Field).ToUpperInvariant();
            if (GUI.Button(new Rect(x + 580, y, cw - 580, 56), "JOIN", UISkin.Button)) _ = session.JoinByCodeAsync(joinCode);
            GUI.enabled = true;
            y += 90;

            string status = session.Status;
            if (!string.IsNullOrEmpty(status))
            {
                var color = session.State == SessionState.Failed ? new Color(1f, 0.5f, 0.45f) : UISkin.Gold;
                UISkin.Shadowed(new Rect(x, y, cw, 90), status, UISkin.Label, color);
            }

            if (session.IsBusy)
            {
                if (GUI.Button(new Rect(x, r.yMax - 90, 220, 60), "Cancel", UISkin.Button)) session.Cancel();
            }
            else if (GUI.Button(new Rect(x, r.yMax - 90, 220, 60), "Back", UISkin.Button))
            {
                screen = Page.Main;
            }
        }

        private void DrawDirect(float w, float h)
        {
            var r = new Rect(w / 2 - 480, 240, 960, 560);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            float x = r.x + 40, y = r.y + 30, cw = r.width - 80;
            GUI.Label(new Rect(x, y, cw, 44), "LAN / DIRECT CONNECT", UISkin.Subtitle);
            y += 56;
            GUI.Label(new Rect(x, y, cw, 70), "For local networks, port-forwarded hosts or two copies on one PC. Use PLAY ONLINE for internet games without port forwarding.", UISkin.Small);
            y += 80;
            PlayerNameField(new Rect(x, y, cw, 50));
            y += 70;
            GUI.Label(new Rect(x, y + 8, 200, 44), "Port", UISkin.Label);
            directPort = GUI.TextField(new Rect(x + 200, y, 200, 56), directPort, 5, UISkin.Field);
            y += 76;
            var cc = ConnectionController.Instance;
            if (GUI.Button(new Rect(x, y, cw, 64), "HOST GAME", UISkin.BigButton) && cc != null)
            {
                if (!cc.StartDirectHost(ParsePort())) modalMessage = "Could not host on port " + directPort + ". It may already be in use.";
            }
            y += 84;
            GUI.Label(new Rect(x, y + 8, 200, 44), "Host address", UISkin.Label);
            directAddress = GUI.TextField(new Rect(x + 200, y, 360, 56), directAddress, 64, UISkin.Field);
            if (GUI.Button(new Rect(x + 580, y, cw - 580, 56), "JOIN", UISkin.Button) && cc != null)
            {
                if (!cc.StartDirectClient(directAddress, ParsePort())) modalMessage = "Could not start connecting to " + directAddress + ".";
            }
            if (GUI.Button(new Rect(x, r.yMax - 80, 220, 56), "Back", UISkin.Button)) screen = Page.Main;
        }

        private ushort ParsePort() => ushort.TryParse(directPort, out var p) && p > 0 ? p : GameInfo.DefaultDirectPort;

        private void DrawCharacters(float w, float h, bool inLobby)
        {
            var s = GameSettings.Current;
            // Cards shrink to fit narrow aspect ratios (4:3 / 5:4) instead of running off screen.
            float gap = 14, cardH = 150;
            float cardW = Mathf.Min(330f, (w - 40f - 4 * gap) / 5f);
            float total = 5 * cardW + 4 * gap;
            float x0 = w / 2 - total / 2, y0 = inLobby ? 420 : 250;
            for (int i = 0; i < CharacterCatalog.Count; i++)
            {
                var def = CharacterCatalog.All[i];
                var r = new Rect(x0 + (i % 5) * (cardW + gap), y0 + (i / 5) * (cardH + gap), cardW, cardH);
                bool selected = s.CharacterId == i;
                UISkin.Fill(r, selected ? new Color(0.45f, 0.08f, 0.06f, 0.95f) : new Color(0.05f, 0.05f, 0.06f, 0.88f));
                UISkin.Fill(new Rect(r.x, r.y, 14, r.height), new Color(def.Jacket.R, def.Jacket.G, def.Jacket.B));
                UISkin.Fill(new Rect(r.x + 14, r.y, 8, r.height), new Color(def.Accent.R, def.Accent.G, def.Accent.B));
                GUI.Label(new Rect(r.x + 34, r.y + 8, r.width - 40, 34), $"<b>{def.Name}</b>", UISkin.Label);
                GUI.Label(new Rect(r.x + 34, r.y + 40, r.width - 40, 28), def.Role, UISkin.Small);
                GUI.Label(new Rect(r.x + 34, r.y + 68, r.width - 40, 80), def.Bio, UISkin.Small);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    s.CharacterId = i;
                    s.Save();
                    if (inLobby && PlayerAvatar.Local != null) PlayerAvatar.Local.SetCharacterRpc(i);
                }
            }
            if (!inLobby)
            {
                GUI.Label(new Rect(0, y0 + 2 * (cardH + gap) + 10, w, 40), "Survivor models are placeholder primitive art.", UISkin.Center);
                if (GUI.Button(new Rect(w / 2 - 110, y0 + 2 * (cardH + gap) + 70, 220, 60), "Back", UISkin.Button)) screen = Page.Main;
            }
        }

        // ------------------------------------------------------------------ settings

        private void DrawSettings(float w, float h)
        {
            var s = GameSettings.Current;
            var r = new Rect(w / 2 - 560, 200, 1120, 800);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            float x = r.x + 40, y = r.y + 24, lw = 330, cw = 480;
            GUI.Label(new Rect(x, y, 400, 44), "SETTINGS", UISkin.Subtitle);
            y += 60;

            scroll = GUI.BeginScrollView(new Rect(x, y, r.width - 60, r.height - 180), scroll, new Rect(0, 0, r.width - 90, 1260));
            float yy = 0;
            s.MasterVolume = Slider(ref yy, "Master volume", s.MasterVolume, 0f, 1f, lw, cw, "P0");
            s.EffectsVolume = Slider(ref yy, "Effects volume", s.EffectsVolume, 0f, 1f, lw, cw, "P0");
            s.AmbienceVolume = Slider(ref yy, "Ambience volume", s.AmbienceVolume, 0f, 1f, lw, cw, "P0");
            s.MouseSensitivity = Slider(ref yy, "Mouse sensitivity", s.MouseSensitivity, 0.2f, 8f, lw, cw, "0.0");
            s.FieldOfView = Mathf.Round(Slider(ref yy, "Field of view", s.FieldOfView, 60f, 110f, lw, cw, "0"));
            s.InvertY = GUI.Toggle(new Rect(lw, yy, cw, 40), s.InvertY, " Invert mouse Y", UISkin.Toggle);
            yy += 50;

            GUI.Label(new Rect(0, yy, lw, 44), "Resolution", UISkin.Label);
            string res = s.ResolutionWidth > 0 ? $"{s.ResolutionWidth} x {s.ResolutionHeight}" : "Desktop";
            if (GUI.Button(new Rect(lw, yy, cw, 44), res, UISkin.Button)) CycleResolution(s);
            yy += 54;
            GUI.Label(new Rect(0, yy, lw, 44), "Display mode", UISkin.Label);
            if (GUI.Button(new Rect(lw, yy, cw, 44), s.ScreenMode.ToString(), UISkin.Button))
                s.ScreenMode = s.ScreenMode == FullScreenMode.FullScreenWindow ? FullScreenMode.Windowed :
                    s.ScreenMode == FullScreenMode.Windowed ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
            yy += 54;
            s.VSync = GUI.Toggle(new Rect(lw, yy, cw, 40), s.VSync, " V-Sync", UISkin.Toggle);
            yy += 50;
            GUI.Label(new Rect(0, yy, lw, 44), "Frame-rate cap (V-Sync off)", UISkin.Label);
            if (GUI.Button(new Rect(lw, yy, cw, 44), s.FrameCap <= 0 ? "Unlimited" : s.FrameCap + " FPS", UISkin.Button))
            {
                int idx = Array.IndexOf(GameSettings.FrameCaps, s.FrameCap);
                s.FrameCap = GameSettings.FrameCaps[(idx + 1) % GameSettings.FrameCaps.Length];
            }
            yy += 64;

            GUI.Label(new Rect(0, yy, lw * 2, 40), "<b>Controls</b> (click, then press a key)", UISkin.Label);
            yy += 48;
            s.KeyForward = KeyRow(ref yy, "Move forward", "fwd", s.KeyForward, lw, cw);
            s.KeyBack = KeyRow(ref yy, "Move back", "back", s.KeyBack, lw, cw);
            s.KeyLeft = KeyRow(ref yy, "Strafe left", "left", s.KeyLeft, lw, cw);
            s.KeyRight = KeyRow(ref yy, "Strafe right", "right", s.KeyRight, lw, cw);
            s.KeyJump = KeyRow(ref yy, "Jump", "jump", s.KeyJump, lw, cw);
            s.KeySprint = KeyRow(ref yy, "Sprint", "sprint", s.KeySprint, lw, cw);
            s.KeyCrouch = KeyRow(ref yy, "Crouch", "crouch", s.KeyCrouch, lw, cw);
            s.KeyReload = KeyRow(ref yy, "Reload", "reload", s.KeyReload, lw, cw);
            s.KeyInteract = KeyRow(ref yy, "Interact / buy / revive", "interact", s.KeyInteract, lw, cw);
            s.KeyMelee = KeyRow(ref yy, "Knife", "melee", s.KeyMelee, lw, cw);
            s.KeySwap = KeyRow(ref yy, "Swap weapon", "swap", s.KeySwap, lw, cw);
            GUI.Label(new Rect(0, yy, lw * 2, 40), "Fire: Left mouse  -  Aim: Right mouse  -  Scoreboard: Tab  -  Pause: Esc", UISkin.Small);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(r.x + 40, r.yMax - 84, 260, 60), "Apply & Back", UISkin.Button))
            {
                s.Save();
                s.Apply(true);
                waitingForKey = null;
                if (settingsFromPause) settingsFromPause = false;
                else screen = settingsReturn;
            }
            if (GUI.Button(new Rect(r.x + 320, r.yMax - 84, 260, 60), "Apply", UISkin.Button))
            {
                s.Save();
                s.Apply(true);
            }
        }

        private static float Slider(ref float y, string label, float value, float min, float max, float lw, float cw, string format)
        {
            GUI.Label(new Rect(0, y, lw, 44), label, UISkin.Label);
            float v = GUI.HorizontalSlider(new Rect(lw, y + 16, cw, 30), value, min, max);
            GUI.Label(new Rect(lw + cw + 20, y, 140, 44), v.ToString(format), UISkin.Label);
            y += 50;
            return v;
        }

        private KeyCode KeyRow(ref float y, string label, string id, KeyCode current, float lw, float cw)
        {
            GUI.Label(new Rect(0, y, lw, 40), label, UISkin.Label);
            bool waiting = waitingForKey == id;
            if (GUI.Button(new Rect(lw, y, cw * 0.6f, 40), waiting ? "Press a key..." : current.ToString(), UISkin.Button))
                waitingForKey = id;
            y += 46;
            if (waiting)
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
                {
                    waitingForKey = null;
                    if (e.keyCode == KeyCode.Escape) return current;
                    e.Use();
                    return e.keyCode;
                }
            }
            return current;
        }

        private static void CycleResolution(GameSettings s)
        {
            var list = UnityEngine.Screen.resolutions;
            if (list == null || list.Length == 0) return;
            int idx = -1;
            for (int i = 0; i < list.Length; i++)
                if (list[i].width == s.ResolutionWidth && list[i].height == s.ResolutionHeight) idx = i;
            // Skip duplicates that differ only by refresh rate.
            for (int step = 1; step <= list.Length; step++)
            {
                var next = list[(idx + step + list.Length) % list.Length];
                if (next.width == s.ResolutionWidth && next.height == s.ResolutionHeight) continue;
                s.ResolutionWidth = next.width;
                s.ResolutionHeight = next.height;
                return;
            }
        }

        // ------------------------------------------------------------------ lobby / pause / end

        private void DrawLobby(float w, float h, MatchState match)
        {
            var nm = ConnectionController.Instance != null ? ConnectionController.Instance.Network : null;
            bool isHost = nm != null && nm.IsHost;
            GUI.Label(new Rect(0, 40, w, 80), "LOBBY  -  " + MapCatalog.MapName.ToUpperInvariant(), UISkin.Title);

            float lw = Mathf.Min(1720f, w - 40f);
            var r = new Rect(w / 2 - lw / 2, 140, lw, 260);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            float x = r.x + 30, y = r.y + 20;
            var session = OnlineServices.Session;
            string mode = match.IsSinglePlayer ? "Single player (offline)" :
                session.IsAvailable && !string.IsNullOrEmpty(session.JoinCode) ? "Online  -  join code: <b>" + session.JoinCode + "</b>" :
                ConnectionController.Instance != null && ConnectionController.Instance.Mode == SessionMode.DirectHost ? "LAN / direct host" : "Connected";
            GUI.Label(new Rect(x, y, r.width - 60, 40), mode, UISkin.Label);
            y += 50;
            foreach (var p in PlayerAvatar.All)
            {
                var def = CharacterCatalog.Get(p.CharacterId);
                string you = p == PlayerAvatar.Local ? "  (you)" : string.Empty;
                string ready = p.Ready ? "<color=#7CFF9A>READY</color>" : "<color=#AAAAAA>not ready</color>";
                GUI.Label(new Rect(x, y, r.width - 60, 40), $"<b>{p.DisplayName}</b>{you}  -  {def.Name}  -  {ready}", UISkin.Label);
                y += 42;
            }

            GUI.Label(new Rect(0, 390, w, 30), "Choose your survivor:", UISkin.Center);
            DrawCharacters(w, h, true);

            float by = h - 130;
            var local = PlayerAvatar.Local;
            if (!match.IsSinglePlayer && GUI.Button(new Rect(w / 2 - 520, by, 320, 70), local.Ready ? "NOT READY" : "READY", UISkin.Button))
                local.SetReadyRpc(!local.Ready);
            if (isHost)
            {
                int readyCount = 0;
                foreach (var p in PlayerAvatar.All) if (p.Ready || p == local) readyCount++;
                string label = match.IsSinglePlayer ? "START GAME" : $"START GAME  ({readyCount}/{PlayerAvatar.All.Count} ready)";
                if (GUI.Button(new Rect(w / 2 - 180, by, 360, 70), label, UISkin.BigButton)) match.StartMatchRpc();
            }
            else
            {
                GUI.Label(new Rect(w / 2 - 180, by, 360, 70), "Waiting for the host to start...", UISkin.Center);
            }
            if (GUI.Button(new Rect(w / 2 + 200, by, 320, 70), "LEAVE", UISkin.Button)) LeaveGame();
        }

        private void DrawPause(float w, float h, MatchState match)
        {
            UISkin.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.55f));
            if (settingsFromPause)
            {
                DrawSettings(w, h);
                return;
            }
            GUI.Label(new Rect(0, 160, w, 90), match.IsSinglePlayer ? "PAUSED" : "MENU", UISkin.Title);
            if (!match.IsSinglePlayer) GUI.Label(new Rect(0, 245, w, 40), "Online games keep running while this menu is open.", UISkin.Center);
            float bw = 420, x = w / 2 - bw / 2, y = 320;
            if (GUI.Button(new Rect(x, y, bw, 70), "RESUME", UISkin.BigButton)) SetPaused(false);
            y += 86;
            if (GUI.Button(new Rect(x, y, bw, 64), "SETTINGS", UISkin.Button)) settingsFromPause = true;
            y += 80;
            var nm = ConnectionController.Instance != null ? ConnectionController.Instance.Network : null;
            bool host = nm != null && nm.IsHost && !match.IsSinglePlayer;
            if (!confirmLeave)
            {
                if (GUI.Button(new Rect(x, y, bw, 64), host ? "END GAME FOR EVERYONE" : "LEAVE GAME", UISkin.Button)) confirmLeave = true;
            }
            else
            {
                GUI.Label(new Rect(x - 100, y - 4, bw + 200, 30), host ? "You are the host - leaving ends the session for all players." : "Leave this game?", UISkin.Center);
                if (GUI.Button(new Rect(x, y + 30, bw / 2 - 8, 60), "YES", UISkin.Button)) LeaveGame();
                if (GUI.Button(new Rect(x + bw / 2 + 8, y + 30, bw / 2 - 8, 60), "NO", UISkin.Button)) confirmLeave = false;
            }
            y += 110;
            if (GUI.Button(new Rect(x, y, bw, 64), "QUIT TO DESKTOP", UISkin.Button)) Application.Quit();
        }

        private void DrawEndScreen(float w, float h, MatchState match)
        {
            UISkin.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.6f));
            bool win = match.Phase == MatchPhase.Victory;
            bool iEscaped = PlayerAvatar.Local != null && PlayerAvatar.Local.Life == LifeState.Escaped;
            string title = !win ? "GAME OVER" : iEscaped ? "ESCAPED" : "LEFT BEHIND";
            UISkin.Shadowed(new Rect(0, 120, w, 100), title, UISkin.Title, win && iEscaped ? UISkin.Alien : UISkin.Accent);
            int minutes = (int)(match.MatchSeconds / 60f), seconds = (int)(match.MatchSeconds % 60f);
            // On defeat the round you fell in doesn't count as survived.
            int survived = win ? match.Round : Mathf.Max(0, match.Round - 1);
            string sub = win && !iEscaped ? "Your team escaped without you. " : string.Empty;
            GUI.Label(new Rect(0, 230, w, 40), $"{sub}Survived {survived} round{(survived == 1 ? "" : "s")}  -  {minutes}:{seconds:00}", UISkin.Center);

            float sw = Mathf.Min(1400f, w - 40f);
            var r = new Rect(w / 2 - sw / 2, 300, sw, 80 + PlayerAvatar.All.Count * 48);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            ScoreboardRows(r, match);

            if (GUI.Button(new Rect(w / 2 - 200, r.yMax + 40, 400, 70), "RETURN TO MENU", UISkin.BigButton)) LeaveGame();
        }

        private static void ScoreboardRows(Rect r, MatchState match)
        {
            // Column positions are laid out for 1400 px and scaled to the panel width.
            float k = r.width / 1400f;
            float[] cols = { 30 * k, 470 * k, 640 * k, 800 * k, 960 * k, 1110 * k, 1250 * k };
            string[] heads = { "Survivor", "Points", "Kills", "Headshots", "Revives", "Downs", "Status" };
            float y = r.y + 20;
            for (int i = 0; i < heads.Length; i++) GUI.Label(new Rect(r.x + cols[i], y, 200, 36), "<b>" + heads[i] + "</b>", UISkin.Label);
            y += 46;
            foreach (var p in PlayerAvatar.All)
            {
                string status = p.Life == LifeState.Escaped ? "<color=#7CFF9A>Escaped</color>" :
                    match.Phase == MatchPhase.Victory ? "<color=#FFB060>Left behind</color>" :
                    p.Life == LifeState.Alive ? "Alive" : p.Life == LifeState.Downed ? "<color=#FFB060>Downed</color>" : "<color=#FF6060>Dead</color>";
                string[] vals = { p.DisplayName, p.Points.ToString(), p.Kills.ToString(), p.Headshots.ToString(), p.Revives.ToString(), p.Downs.ToString(), status };
                for (int i = 0; i < vals.Length; i++) GUI.Label(new Rect(r.x + cols[i], y, (i == 0 ? 430 : 160) * k, 36), vals[i], UISkin.Label);
                y += 46;
            }
        }
    }
}
