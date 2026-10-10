using System.Threading.Tasks;

namespace UZS.Gameplay
{
    public enum SessionState
    {
        Idle,
        SigningIn,
        Searching,
        Creating,
        Joining,
        Connected,
        Failed,
        Cancelled,
    }

    /// <summary>
    /// Online matchmaking / lobby abstraction. The UI only talks to this interface; the Unity Gaming Services
    /// implementation registers itself at startup, so the transport/service stack can be swapped later.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>False when no online backend is compiled in or configured (UI then explains how to enable it).</summary>
        bool IsAvailable { get; }
        string UnavailableReason { get; }
        SessionState State { get; }
        string Status { get; }
        /// <summary>Join code of the current session (shown to the host of a private game).</summary>
        string JoinCode { get; }
        bool IsBusy { get; }

        /// <summary>Quick-join a public game with free slots, or create a new public one if none is found.</summary>
        Task FindMatchAsync();
        Task CreatePrivateAsync();
        Task JoinByCodeAsync(string code);
        void Cancel();
        Task LeaveAsync();
    }

    public static class OnlineServices
    {
        private static ISessionService provider;

        public static ISessionService Session => provider ?? (provider = new UnavailableSessionService(
            "Online services are not installed. Add the com.unity.services.multiplayer package (see docs/MULTIPLAYER_SETUP.md)."));

        public static void Register(ISessionService service) => provider = service;
    }

    /// <summary>Placeholder used when no backend is present. Never pretends to search.</summary>
    public sealed class UnavailableSessionService : ISessionService
    {
        public UnavailableSessionService(string reason) => UnavailableReason = reason;

        public bool IsAvailable => false;
        public string UnavailableReason { get; }
        public SessionState State => SessionState.Failed;
        public string Status => UnavailableReason;
        public string JoinCode => string.Empty;
        public bool IsBusy => false;
        public Task FindMatchAsync() => Task.CompletedTask;
        public Task CreatePrivateAsync() => Task.CompletedTask;
        public Task JoinByCodeAsync(string code) => Task.CompletedTask;
        public void Cancel() { }
        public Task LeaveAsync() => Task.CompletedTask;
    }
}
