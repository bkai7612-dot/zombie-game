// Compile-only stand-ins for the Unity Gaming Services APIs used by UgsSessionService
// (com.unity.services.core, .authentication, .multiplayer 1.x). Signatures follow Unity's published
// API reference for Multiplayer Services 1.0/1.1; they could not be checked against package source
// because packages.unity.com is not reachable from this environment. NOT shipped.
#pragma warning disable CS0067
using System;
using System.Threading.Tasks;

namespace Unity.Services.Core
{
    public enum ServicesInitializationState { Uninitialized, Initializing, Initialized }
    public class InitializationOptions { }
    public static class UnityServices
    {
        public static ServicesInitializationState State => ServicesInitializationState.Uninitialized;
        public static Task InitializeAsync() => Task.CompletedTask;
        public static Task InitializeAsync(InitializationOptions options) => Task.CompletedTask;
    }
}

namespace Unity.Services.Authentication
{
    using Unity.Services.Core;
    public class SignInOptions { }
    public interface IAuthenticationService
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        Task SignInAnonymouslyAsync(SignInOptions options = null);
    }
    public static class AuthenticationService
    {
        public static IAuthenticationService Instance => null;
    }
    public static class AuthenticationExtensions
    {
        public static InitializationOptions SetProfile(this InitializationOptions options, string profile) => options;
    }
}

namespace Unity.Services.Multiplayer
{
    public interface ISession
    {
        string Id { get; }
        string Code { get; }
        Task LeaveAsync();
    }
    public interface IHostSession : ISession { }
    public class JoinSessionOptions { }
    public abstract class BaseSessionOptions { }
    public class SessionOptions : BaseSessionOptions
    {
        public int MaxPlayers { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsLocked { get; set; }
        public string Name { get; set; }
    }
    public static class SessionOptionsExtensions
    {
        public static T WithRelayNetwork<T>(this T options, string region = null) where T : SessionOptions => options;
    }
    public class QuickJoinOptions
    {
        public TimeSpan Timeout { get; set; }
        public bool CreateSession { get; set; }
    }
    public interface IMultiplayerService
    {
        Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions);
        Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions options = null);
        Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions);
    }
    public static class MultiplayerService
    {
        public static IMultiplayerService Instance => null;
    }
}
