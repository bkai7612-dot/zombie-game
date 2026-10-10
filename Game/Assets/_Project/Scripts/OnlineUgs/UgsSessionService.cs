// Unity Gaming Services implementation of ISessionService (Authentication + Multiplayer Services sessions
// with Relay). Lives in Assembly-CSharp (no asmdef) so it automatically references the UGS package
// assemblies; it registers itself before the first scene loads.
//
// Requirements: the project must be linked to a Unity Cloud project (Edit > Project Settings > Services)
// with Lobby and Relay enabled in the Unity Cloud dashboard. See docs/MULTIPLAYER_SETUP.md.
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UZS.Core;
using UZS.Gameplay;

public sealed class UgsSessionService : ISessionService, ISessionLink
{
    private ISession session;
    private bool cancelRequested;
    private SessionState state = SessionState.Idle;
    private string status = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        OnlineServices.Register(new UgsSessionService());
    }

    public bool IsAvailable => true;
    public string UnavailableReason => string.Empty;
    public SessionState State => state;
    public string Status => status;
    public string JoinCode => session != null ? session.Code : string.Empty;
    public bool IsBusy => state == SessionState.SigningIn || state == SessionState.Searching || state == SessionState.Creating || state == SessionState.Joining;

    private void Set(SessionState s, string message)
    {
        state = s;
        status = message;
        Debug.Log($"[UZS-Online] {s}: {message}");
    }

    private async Task<bool> EnsureSignedInAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Set(SessionState.SigningIn, "Connecting to online services...");
                var options = new InitializationOptions();
                // Each running copy needs its own anonymous identity so two instances on one PC can play together.
                options.SetProfile(ProfileForThisProcess());
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Set(SessionState.SigningIn, "Signing in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            return true;
        }
        catch (Exception e)
        {
            Set(SessionState.Failed, "Could not reach online services. Check your internet connection and that this build is linked to a Unity Cloud project. (" + e.Message + ")");
            return false;
        }
    }

    private static string profile;

    private static string ProfileForThisProcess()
    {
        if (profile != null) return profile;
        string named = GameSettings.ProfileName;
        var raw = string.IsNullOrEmpty(named) ? "p" + Guid.NewGuid().ToString("N").Substring(0, 12) : named;
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
            if ((char.IsLetterOrDigit(c) || c == '-' || c == '_') && sb.Length < 30) sb.Append(c);
        profile = sb.Length > 0 ? sb.ToString() : "default";
        return profile;
    }

    private static SessionOptions NewSessionOptions(bool isPrivate)
    {
        return new SessionOptions { MaxPlayers = GameInfo.MaxPlayers, IsPrivate = isPrivate }.WithRelayNetwork();
    }

    public async Task FindMatchAsync()
    {
        if (IsBusy || session != null) return;
        cancelRequested = false;
        if (!await EnsureSignedInAsync() || CheckCancelled()) return;
        try
        {
            Set(SessionState.Searching, "Searching for a public game with a free slot...");
            ConnectionController.Instance.PrepareForServiceSession();
            var quick = new QuickJoinOptions { Timeout = TimeSpan.FromSeconds(6), CreateSession = true };
            session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, NewSessionOptions(false));
            if (CheckCancelled()) return;
            bool hosting = ConnectionController.Instance.Network != null && ConnectionController.Instance.Network.IsHost;
            Set(SessionState.Connected, hosting
                ? "No open games found - created a new public game. Waiting for players..."
                : "Joined a public game.");
        }
        catch (Exception e)
        {
            await Fail("Matchmaking failed: " + e.Message);
        }
    }

    public async Task CreatePrivateAsync()
    {
        if (IsBusy || session != null) return;
        cancelRequested = false;
        if (!await EnsureSignedInAsync() || CheckCancelled()) return;
        try
        {
            Set(SessionState.Creating, "Creating a private game...");
            ConnectionController.Instance.PrepareForServiceSession();
            session = await MultiplayerService.Instance.CreateSessionAsync(NewSessionOptions(true));
            if (CheckCancelled()) return;
            Set(SessionState.Connected, "Private game created. Share the join code: " + session.Code);
        }
        catch (Exception e)
        {
            await Fail("Could not create the game: " + e.Message);
        }
    }

    public async Task JoinByCodeAsync(string code)
    {
        if (IsBusy || session != null) return;
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            Set(SessionState.Failed, "Enter a join code first.");
            return;
        }
        cancelRequested = false;
        if (!await EnsureSignedInAsync() || CheckCancelled()) return;
        try
        {
            Set(SessionState.Joining, "Joining game " + code + "...");
            ConnectionController.Instance.PrepareForServiceSession();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            if (CheckCancelled()) return;
            Set(SessionState.Connected, "Joined game " + code + ".");
        }
        catch (Exception e)
        {
            await Fail("Could not join " + code + ": " + e.Message);
        }
    }

    /// <summary>The SDK calls cannot be aborted mid-flight; we leave as soon as the pending call returns.</summary>
    public void Cancel()
    {
        if (!IsBusy) return;
        cancelRequested = true;
        Set(SessionState.Cancelled, "Cancelling...");
    }

    private bool CheckCancelled()
    {
        if (!cancelRequested) return false;
        cancelRequested = false;
        _ = LeaveAsync();
        Set(SessionState.Cancelled, "Cancelled.");
        return true;
    }

    private async Task Fail(string message)
    {
        await LeaveAsync();
        Set(SessionState.Failed, message);
    }

    public async Task LeaveAsync()
    {
        var s = session;
        session = null;
        if (ConnectionController.Instance != null && ConnectionController.Instance.Mode == SessionMode.Online && ConnectionController.Instance.IsActive)
            ConnectionController.Instance.Leave();
        if (s == null) return;
        try
        {
            await s.LeaveAsync();
        }
        catch (Exception e)
        {
            // The session may already be gone (host left, network lost); nothing else to clean up.
            Debug.Log("[UZS-Online] Leave: " + e.Message);
        }
        if (state == SessionState.Connected) Set(SessionState.Idle, string.Empty);
    }

    // Gameplay tells us the network session ended (left, kicked, host quit) so the lobby entry is released.
    public void OnLocalSessionEnded()
    {
        if (session == null) return;
        _ = LeaveAsync();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Link()
    {
        if (ConnectionController.Instance != null && OnlineServices.Session is UgsSessionService ugs)
            ConnectionController.Instance.SessionLink = ugs;
    }
}
