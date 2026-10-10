// Compile-only stand-ins for the parts of Netcode for GameObjects 2.4.4 / Unity Transport / Unity.Collections
// that this project calls. Every signature below was copied from the NGO 2.4.4 source
// (github.com/Unity-Technologies/com.unity.netcode.gameobjects, tag ngo/2.4.4). They let CI type-check the
// gameplay code without a Unity install. They are NOT shipped and contain no behaviour.
#pragma warning disable CS0067, CS0649, CS0414
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Collections
{
    public struct FixedString64Bytes
    {
        private string value;
        public FixedString64Bytes(string source) { value = source; }
        public static implicit operator FixedString64Bytes(string b) => new FixedString64Bytes(b);
        public override string ToString() => value ?? string.Empty;
    }
}

namespace Unity.Networking.Transport
{
    public struct NetworkEndpoint { }
}

namespace Unity.Netcode
{
    public enum NetworkVariableReadPermission { Everyone, Owner }
    public enum NetworkVariableWritePermission { Server, Owner }

    public abstract class NetworkVariableBase : IDisposable
    {
        protected NetworkVariableBase(NetworkVariableReadPermission readPerm, NetworkVariableWritePermission writePerm) { }
        public void Dispose() { }
    }

    public class NetworkVariable<T> : NetworkVariableBase
    {
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);
        public OnValueChangedDelegate OnValueChanged;
        public NetworkVariable(T value = default, NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server) : base(readPerm, writePerm) { }
        public virtual T Value { get; set; }
    }

    public enum SendTo { Owner, NotOwner, Server, NotServer, Me, NotMe, Everyone, ClientsAndHost, Authority, NotAuthority, SpecifiedInParams }

    [AttributeUsage(AttributeTargets.Method)]
    public class RpcAttribute : Attribute
    {
        public bool RequireOwnership;
        public bool DeferLocal;
        public bool AllowTargetOverride;
        public RpcAttribute(SendTo target) { }
    }

    public struct RpcReceiveParams { public ulong SenderClientId; }
    public struct RpcParams { public RpcReceiveParams Receive; }

    public struct NetworkTime { public double Time => 0; }

    public abstract class NetworkTransport : MonoBehaviour
    {
        public abstract ulong GetCurrentRtt(ulong clientId);
    }

    public class NetworkPrefabs
    {
        public bool Contains(GameObject prefab) => false;
    }

    [Serializable]
    public class NetworkConfig
    {
        public NetworkTransport NetworkTransport = null;
        public GameObject PlayerPrefab;
        public NetworkPrefabs Prefabs = new NetworkPrefabs();
        public uint TickRate = 30;
        public bool ConnectionApproval = false;
        public byte[] ConnectionData = new byte[0];
    }

    public class NetworkObject : MonoBehaviour
    {
        public ulong NetworkObjectId { get; internal set; }
        public ulong OwnerClientId { get; internal set; }
        public bool IsSpawned { get; internal set; }
        public bool DontDestroyWithOwner;
        public bool SynchronizeTransform = true;
        public bool AutoObjectParentSync = true;
        public void Spawn(bool destroyWithScene = false) { }
        public void SpawnWithOwnership(ulong clientId, bool destroyWithScene = false) { }
        public void Despawn(bool destroy = true) { }
    }

    public abstract class NetworkBehaviour : MonoBehaviour
    {
        public NetworkManager NetworkManager => null;
        public bool IsOwner { get; internal set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsHost { get; private set; }
        public bool IsSpawned { get; internal set; }
        public NetworkObject NetworkObject => null;
        public ulong NetworkObjectId { get; internal set; }
        public ulong OwnerClientId { get; internal set; }
        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }
    }

    public class NetworkManager : MonoBehaviour
    {
        public const ulong ServerClientId = 0;
        public static NetworkManager Singleton { get; private set; }
        public NetworkConfig NetworkConfig;
        public bool IsServer => false;
        public bool IsClient => false;
        public bool IsHost => false;
        public bool IsListening => false;
        public ulong LocalClientId => 0;
        public IReadOnlyList<ulong> ConnectedClientsIds => null;
        public string DisconnectReason => null;
        public NetworkTime ServerTime => default;

        public event Action OnServerStarted = null;
        public event Action OnClientStarted = null;
        public event Action<bool> OnServerStopped = null;
        public event Action<bool> OnClientStopped = null;
        public event Action<ulong> OnClientConnectedCallback { add { } remove { } }
        public event Action<ulong> OnClientDisconnectCallback { add { } remove { } }
        public event Action OnTransportFailure { add { } remove { } }

        public Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback { get; set; }

        public bool StartServer() => false;
        public bool StartClient() => false;
        public bool StartHost() => false;
        public void Shutdown(bool discardMessageQueue = false) { }
        public void AddNetworkPrefab(GameObject prefab) { }
        public void DisconnectClient(ulong clientId) { }
        public void DisconnectClient(ulong clientId, string reason = null) { }

        public class ConnectionApprovalResponse
        {
            public bool Approved;
            public bool CreatePlayerObject;
            public uint? PlayerPrefabHash;
            public Vector3? Position;
            public Quaternion? Rotation;
            public bool Pending;
            public string Reason;
        }

        public struct ConnectionApprovalRequest
        {
            public byte[] Payload;
            public ulong ClientNetworkId;
        }
    }
}

namespace Unity.Netcode.Components
{
    public class NetworkTransform : Unity.Netcode.NetworkBehaviour
    {
        public enum AuthorityModes { Server, Owner }
        public AuthorityModes AuthorityMode;
        public bool SyncPositionX = true, SyncPositionY = true, SyncPositionZ = true;
        public bool SyncRotAngleX = true, SyncRotAngleY = true, SyncRotAngleZ = true;
        public bool SyncScaleX = true, SyncScaleY = true, SyncScaleZ = true;
        public bool Interpolate = true;
        public bool InLocalSpace = false;
        public bool UseHalfFloatPrecision = false;
        public float PositionThreshold;
        public float RotAngleThreshold;
        public bool CanCommitToTransform { get; protected set; }
        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale) { }
    }
}

namespace Unity.Netcode.Transports.UTP
{
    public class UnityTransport : Unity.Netcode.NetworkTransport
    {
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null) { }
        public void SetConnectionData(Unity.Networking.Transport.NetworkEndpoint endPoint, Unity.Networking.Transport.NetworkEndpoint listenEndPoint = default) { }
    }
}

