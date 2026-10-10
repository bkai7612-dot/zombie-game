using System.Collections.Generic;
using UnityEngine;
using UZS.Core;
using UZS.Gameplay;

namespace UZS.UI
{
    /// <summary>
    /// Top-level UI state machine (menus, lobby, HUD, pause, end screen) drawn with IMGUI, plus cursor/input gating.
    /// Split across partial files: UIRoot.Menus.cs, UIRoot.Hud.cs.
    /// </summary>
    public sealed partial class UIRoot : MonoBehaviour
    {
        private enum Page
        {
            Main,
            Online,
            Direct,
            Characters,
            Settings,
        }

        private Page screen = Page.Main;
        private Page settingsReturn = Page.Main;
        private bool paused;
        private bool settingsFromPause;
        private bool confirmLeave;
        private string modalMessage = string.Empty;
        private string statusLine = string.Empty;

        private readonly List<(string text, float until, bool major)> feed = new List<(string, float, bool)>();

        private void OnEnable()
        {
            MatchState.Announcement += OnAnnouncement;
            PlayerInteractor.LocalResult += OnLocalResult;
            PlayerAvatar.LocalSpawned += OnLocalSpawned;
            PlayerAvatar.LocalHurt += OnLocalHurt;
        }

        private void OnDisable()
        {
            MatchState.Announcement -= OnAnnouncement;
            PlayerInteractor.LocalResult -= OnLocalResult;
            PlayerAvatar.LocalSpawned -= OnLocalSpawned;
            PlayerAvatar.LocalHurt -= OnLocalHurt;
            var cc = ConnectionController.Instance;
            if (cc != null) cc.Disconnected -= OnDisconnected;
        }

        private void Start()
        {
            var cc = ConnectionController.Instance;
            if (cc != null) cc.Disconnected += OnDisconnected;
        }

        private void OnDisconnected(string reason)
        {
            paused = false;
            confirmLeave = false;
            screen = Page.Main;
            if (!string.IsNullOrEmpty(reason)) modalMessage = reason;
        }

        private void OnAnnouncement(string text, bool major)
        {
            feed.Add((text, Time.unscaledTime + (major ? 5f : 4f), major));
            if (feed.Count > 6) feed.RemoveAt(0);
            if (major && AudioManager.Instance != null && text.StartsWith("ROUND")) AudioManager.Instance.Play2D(Sfx.RoundStart, 0.8f);
        }

        private void OnLocalResult(string text, bool success)
        {
            feed.Add((text, Time.unscaledTime + 2.5f, false));
            if (feed.Count > 6) feed.RemoveAt(0);
        }

        private bool InMatch => MatchState.Instance != null && PlayerAvatar.Local != null;

        private bool GameplayActive =>
            InMatch && MatchState.Instance.Phase == MatchPhase.Playing && !paused && string.IsNullOrEmpty(modalMessage);

        private void Update()
        {
            var match = MatchState.Instance;
            if (InMatch && match.Phase == MatchPhase.Playing && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (waitingForKey != null) waitingForKey = null;
                else if (settingsFromPause) settingsFromPause = false;
                else SetPaused(!paused);
            }
            if (!InMatch && paused) SetPaused(false);
            if (match != null && match.IsOver && paused) SetPaused(false);

            bool gameplay = GameplayActive;
            InputGate.Blocked = !gameplay;
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;

            if (gameplay && UnityEngine.Input.GetKeyDown(KeyCode.Mouse0) && PlayerAvatar.Local != null && PlayerAvatar.Local.Life == LifeState.Dead)
            {
                var director = CameraDirector.Instance;
                if (director != null) director.CycleSpectate();
            }

            for (int i = feed.Count - 1; i >= 0; i--)
                if (Time.unscaledTime > feed[i].until) feed.RemoveAt(i);
            UpdateHudTimers();
        }

        private void SetPaused(bool value)
        {
            paused = value;
            confirmLeave = false;
            settingsFromPause = false;
            // Only single-player actually stops time; online games keep running for everyone else.
            var match = MatchState.Instance;
            bool solo = match != null && match.IsSinglePlayer;
            Time.timeScale = paused && solo ? 0f : 1f;
            if (AudioManager.Instance != null) AudioListener.pause = paused && solo;
        }

        private void OnGUI()
        {
            UISkin.Begin();
            float w = UISkin.VirtualWidth, h = UISkin.VirtualHeight;
            var match = MatchState.Instance;

            if (!InMatch)
            {
                if (match == null) DrawMenus(w, h);
                else DrawConnecting(w, h);
            }
            else if (match.Phase == MatchPhase.Lobby)
            {
                DrawLobby(w, h, match);
            }
            else
            {
                DrawHud(w, h, match);
                if (match.IsOver) DrawEndScreen(w, h, match);
                else if (paused) DrawPause(w, h, match);
            }

            if (!string.IsNullOrEmpty(modalMessage)) DrawModal(w, h);
        }

        private void DrawModal(float w, float h)
        {
            var r = new Rect(w / 2 - 380, h / 2 - 140, 760, 280);
            UISkin.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.6f));
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            GUI.Label(new Rect(r.x + 30, r.y + 25, r.width - 60, 160), modalMessage, UISkin.Label);
            if (GUI.Button(new Rect(r.x + r.width / 2 - 120, r.yMax - 80, 240, 56), "OK", UISkin.Button)) modalMessage = string.Empty;
        }

        private void DrawConnecting(float w, float h)
        {
            var r = new Rect(w / 2 - 360, h / 2 - 120, 720, 240);
            GUI.Box(r, GUIContent.none, UISkin.Panel);
            var session = OnlineServices.Session;
            string text = session.IsAvailable && session.IsBusy ? session.Status : "Connecting to the game...";
            GUI.Label(new Rect(r.x + 30, r.y + 30, r.width - 60, 100), text, UISkin.Center);
            if (GUI.Button(new Rect(r.x + r.width / 2 - 120, r.yMax - 80, 240, 56), "Cancel", UISkin.Button))
                LeaveGame();
        }

        private void LeaveGame()
        {
            SetPaused(false);
            var session = OnlineServices.Session;
            if (session.IsAvailable && ConnectionController.Instance != null && ConnectionController.Instance.Mode == SessionMode.Online)
                _ = session.LeaveAsync();
            else if (ConnectionController.Instance != null)
                ConnectionController.Instance.Leave();
            screen = Page.Main;
        }
    }
}
