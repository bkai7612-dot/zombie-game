using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    public enum SessionMode
    {
        None,
        SinglePlayer,
        DirectHost,
        DirectClient,
        Online,
    }

    /// <summary>Implemented by the online-services layer so gameplay can tell it the local player left.</summary>
    public interface ISessionLink
    {
        void OnLocalSessionEnded();
    }

    /// <summary>
    /// Starts/stops Netcode for every mode (single-player uses a local-only host so all rules are shared),
    /// performs connection approval, spawns the match object and reports disconnects to the UI.
    /// </summary>
    public sealed class ConnectionController : MonoBehaviour
    {
        public static ConnectionController Instance { get; private set; }

        /// <summary>Raised on the local peer when it leaves or is disconnected, with a human-readable reason (may be empty).</summary>
        public event Action<string> Disconnected;

        public SessionMode Mode { get; private set; }
        public bool IsSinglePlayer => Mode == SessionMode.SinglePlayer;
        public ReconnectLedger Ledger { get; } = new ReconnectLedger();
        public ISessionLink SessionLink { get; set; }

        private NetworkManager nm;
        private UnityTransport transport;
        private readonly Dictionary<ulong, ConnectionPayload> pendingPayloads = new Dictionary<ulong, ConnectionPayload>();
        private bool shuttingDown;

        public NetworkManager Network => nm;

        public void Init(NetworkManager networkManager)
        {
            Instance = this;
            nm = networkManager;
            transport = nm.GetComponent<UnityTransport>();
            if (transport == null) transport = nm.gameObject.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
            nm.OnServerStarted += OnServerStarted;
            nm.OnClientDisconnectCallback += OnClientDisconnect;
            nm.OnTransportFailure += OnTransportFailure;
            nm.OnServerStopped += OnServerStopped;
        }

        private void OnDestroy()
        {
            if (nm == null) return;
            nm.OnServerStarted -= OnServerStarted;
            nm.OnClientDisconnectCallback -= OnClientDisconnect;
            nm.OnTransportFailure -= OnTransportFailure;
            nm.OnServerStopped -= OnServerStopped;
        }

        public bool IsActive => nm != null && (nm.IsServer || nm.IsClient);

        // ------------------------------------------------------------------ starting

        /// <summary>Offline single-player: a host bound to loopback that rejects every remote connection.</summary>
        public bool StartSinglePlayer()
        {
            if (IsActive) return false;
            Mode = SessionMode.SinglePlayer;
            PreparePayload();
            var rng = new System.Random();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                ushort port = (ushort)rng.Next(20000, 60000);
                transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
                if (nm.StartHost()) return true;
                nm.Shutdown();
            }
            Mode = SessionMode.None;
            return false;
        }

        /// <summary>Hosts on a UDP port for LAN / port-forwarded play or local two-instance testing.</summary>
        public bool StartDirectHost(ushort port)
        {
            if (IsActive) return false;
            Mode = SessionMode.DirectHost;
            PreparePayload();
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            if (nm.StartHost()) return true;
            Mode = SessionMode.None;
            return false;
        }

        public bool StartDirectClient(string address, ushort port)
        {
            if (IsActive) return false;
            Mode = SessionMode.DirectClient;
            PreparePayload();
            transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port);
            if (nm.StartClient()) return true;
            Mode = SessionMode.None;
            return false;
        }

        /// <summary>Called by the online layer before it lets the Multiplayer Services SDK start the host/client.</summary>
        public void PrepareForServiceSession()
        {
            Mode = SessionMode.Online;
            PreparePayload();
        }

        private void PreparePayload()
        {
            var s = GameSettings.Current;
            nm.NetworkConfig.ConnectionData = new ConnectionPayload
            {
                PlayerKey = GameSettings.PlayerKey,
                DisplayName = s.PlayerName,
                CharacterId = s.CharacterId,
            }.Encode();
        }

        // ------------------------------------------------------------------ stopping

        /// <summary>Leaves the current game (host: ends it for everyone).</summary>
        public void Leave(string reason = "")
        {
            if (shuttingDown) return;
            shuttingDown = true;
            try
            {
                if (nm != null && (nm.IsServer || nm.IsClient)) nm.Shutdown();
                SessionLink?.OnLocalSessionEnded();
            }
            finally
            {
                Mode = SessionMode.None;
                pendingPayloads.Clear();
                Ledger.Clear();
                Time.timeScale = 1f;
                InputGate.Blocked = false;
                MapBuilder.Destroy();
                shuttingDown = false;
                Disconnected?.Invoke(reason);
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (nm == null) return;
            if (nm.IsServer)
            {
                pendingPayloads.Remove(clientId);
                return;
            }
            if (clientId != nm.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            string reason = nm.DisconnectReason;
            if (string.IsNullOrEmpty(reason))
                reason = "Disconnected from the host. If the host left, the session has ended (host migration is not supported).";
            Leave(reason);
        }

        private void OnTransportFailure()
        {
            Leave("Network transport failure - the connection was lost.");
        }

        private void OnServerStopped(bool wasHost)
        {
            pendingPayloads.Clear();
        }

        // ------------------------------------------------------------------ authority

        private void OnServerStarted()
        {
            var content = GameContent.Instance;
            if (content == null || content.MatchPrefab == null)
            {
                Debug.LogError("[UZS] GameContent / MatchPrefab missing. Run Tools > UZS > Set Up Project in the editor.");
                return;
            }
            if (MatchState.Instance != null) return;
            var match = Instantiate(content.MatchPrefab);
            match.GetComponent<NetworkObject>().Spawn(true);
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool valid = ConnectionPayload.TryDecode(request.Payload, out var payload);
            bool isHostSelf = request.ClientNetworkId == NetworkManager.ServerClientId;
            var phase = MatchState.Instance != null ? MatchState.Instance.Phase : MatchPhase.Lobby;
            int connected = nm.ConnectedClientsIds.Count;
            var result = ApprovalRules.Evaluate(payload, valid, isHostSelf, IsSinglePlayer, connected, phase);

            response.Approved = result == ApprovalResult.Approved;
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            if (!response.Approved)
            {
                response.Reason = ApprovalRules.Describe(result);
                return;
            }
            var spawn = MapCatalog.PlayerSpawns[connected % MapCatalog.PlayerSpawns.Length];
            response.Position = Geo.V(spawn) + Vector3.up * 0.2f;
            response.Rotation = Quaternion.Euler(0f, 90f, 0f);
            pendingPayloads[request.ClientNetworkId] = payload;
        }

        public bool ServerTakePayload(ulong clientId, out ConnectionPayload payload)
        {
            if (pendingPayloads.TryGetValue(clientId, out payload))
            {
                pendingPayloads.Remove(clientId);
                return true;
            }
            return false;
        }
    }
}
