using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Stakeholders.App
{
    /// <summary>
    /// Thin wrapper over Netcode for GameObjects. The game does not use NetworkObjects at all:
    /// it sends compressed JSON messages (commands up, per-player views down) over named messages.
    /// Supports Unity Relay (join codes, via Multiplayer Services) and direct IP.
    /// </summary>
    public class NetBridge : MonoBehaviour
    {
        public const int ProtocolVersion = 1;
        public const ushort DefaultPort = 7777;
        const string MessageName = "stk";

        public NetworkManager Manager { get; private set; }
        public UnityTransport Transport { get; private set; }
        public string JoinCode { get; private set; }
        public string LastError { get; private set; }

        public event Action<ulong> ClientConnected;
        public event Action<ulong> ClientDisconnected;
        public event Action<ulong, NetEnvelope> MessageReceived;
        /// <summary>Raised on a client when its own connection is lost.</summary>
        public event Action<string> LocalDisconnected;
        /// <summary>Raised on a client once it is connected to the host.</summary>
        public event Action ConnectedToHost;

        ISession _session;
        bool _handlersRegistered;

        public bool IsHost => Manager != null && Manager.IsServer;
        public bool IsConnectedClient => Manager != null && Manager.IsConnectedClient;

        public static NetBridge Create()
        {
            var existing = FindAnyObjectByType<NetBridge>();
            if (existing != null) return existing;
            var go = new GameObject("Stakeholders Network");
            DontDestroyOnLoad(go);
            var bridge = go.AddComponent<NetBridge>();
            bridge.Setup();
            return bridge;
        }

        void Setup()
        {
            Transport = gameObject.AddComponent<UnityTransport>();
            Manager = gameObject.AddComponent<NetworkManager>();
            Manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = Transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
                TickRate = 20,
                ClientConnectionBufferTimeout = 15,
            };
            Transport.DisconnectTimeoutMS = 20000;
            Manager.OnClientConnectedCallback += OnClientConnected;
            Manager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        // ------------------------------------------------------------------ starting

        /// <param name="listenAddress">"0.0.0.0" accepts LAN players; "127.0.0.1" only this machine.</param>
        public bool StartHostDirect(ushort port, string listenAddress = "0.0.0.0")
        {
            LastError = null;
            JoinCode = null;
            Transport.SetConnectionData("127.0.0.1", port, listenAddress);
            if (!Manager.StartHost()) { LastError = $"Could not start a host on port {port}. Is it already in use?"; return false; }
            RegisterHandlers();
            return true;
        }

        public bool StartClientDirect(string address, ushort port)
        {
            LastError = null;
            Transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), port);
            if (!Manager.StartClient()) { LastError = "Could not start the client."; return false; }
            RegisterHandlers();
            return true;
        }

        public async Task<bool> StartHostRelay(int maxPlayers)
        {
            LastError = null;
            try
            {
                await InitServices();
                var options = new SessionOptions { MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16), IsPrivate = true, Name = "Stakeholders " + UnityEngine.Random.Range(1000, 9999) }
                    .WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                JoinCode = _session.Code;
                RegisterHandlers();
                return Manager.IsServer;
            }
            catch (Exception ex)
            {
                LastError = FriendlyError(ex);
                Debug.LogException(ex);
                return false;
            }
        }

        public async Task<bool> JoinRelay(string code)
        {
            LastError = null;
            try
            {
                await InitServices();
                RegisterHandlers();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                JoinCode = _session.Code;
                return true;
            }
            catch (Exception ex)
            {
                LastError = FriendlyError(ex);
                Debug.LogException(ex);
                return false;
            }
        }

        static async Task InitServices()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // A fresh profile per launch lets two copies of the game run on one machine (testing).
                var options = new InitializationOptions();
                options.SetProfile("p" + UnityEngine.Random.Range(100000, 999999));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string FriendlyError(Exception ex)
        {
            string msg = ex.Message ?? ex.GetType().Name;
            if (msg.Contains("project") || msg.Contains("Project") || msg.Contains("cloud") || msg.Contains("403") || msg.Contains("401"))
                return "Unity Gaming Services refused the request. Make sure the project is linked to a Unity Cloud project with Relay enabled (Edit > Project Settings > Services). " + msg;
            return msg;
        }

        CustomMessagingManager _registeredWith;

        void RegisterHandlers()
        {
            var cmm = Manager.CustomMessagingManager;
            if (cmm == null || ReferenceEquals(cmm, _registeredWith)) return;
            cmm.RegisterNamedMessageHandler(MessageName, OnNamedMessage);
            _registeredWith = cmm;
            _handlersRegistered = true;
        }

        void Update()
        {
            // The messaging manager only exists once NGO has started (and is recreated on every start).
            if (Manager != null && Manager.CustomMessagingManager != null && !ReferenceEquals(Manager.CustomMessagingManager, _registeredWith)) RegisterHandlers();
        }

        // ------------------------------------------------------------------ messaging

        void OnNamedMessage(ulong sender, FastBufferReader reader)
        {
            try
            {
                reader.ReadValueSafe(out byte[] data);
                var env = NetEnvelope.Decode(data);
                MessageReceived?.Invoke(sender, env);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Stakeholders: bad network message: " + ex.Message);
            }
        }

        public void Send(ulong clientId, NetEnvelope env)
        {
            if (Manager == null || Manager.CustomMessagingManager == null) return;
            if (clientId == NetworkManager.ServerClientId && Manager.IsServer) return; // never message ourselves
            var data = env.Encode();
            using (var writer = new FastBufferWriter(data.Length + 16, Allocator.Temp))
            {
                writer.WriteValueSafe(data);
                Manager.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        public void SendToHost(NetEnvelope env)
        {
            if (Manager == null || Manager.CustomMessagingManager == null || !Manager.IsConnectedClient) return;
            var data = env.Encode();
            using (var writer = new FastBufferWriter(data.Length + 16, Allocator.Temp))
            {
                writer.WriteValueSafe(data);
                Manager.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        void OnClientConnected(ulong clientId)
        {
            if (Manager.IsServer)
            {
                if (clientId != NetworkManager.ServerClientId) ClientConnected?.Invoke(clientId);
            }
            else if (clientId == Manager.LocalClientId)
            {
                ConnectedToHost?.Invoke();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (Manager.IsServer)
            {
                if (clientId != NetworkManager.ServerClientId) ClientDisconnected?.Invoke(clientId);
            }
            else
            {
                LocalDisconnected?.Invoke(Manager.DisconnectReason);
            }
        }

        public void Shutdown()
        {
            try
            {
                if (_handlersRegistered && Manager != null && Manager.CustomMessagingManager != null)
                    Manager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }
            catch (Exception) { }
            _handlersRegistered = false;
            _registeredWith = null;
            if (Manager != null && (Manager.IsListening || Manager.IsClient || Manager.IsServer)) Manager.Shutdown();
            if (_session != null)
            {
                var s = _session;
                _session = null;
                _ = s.LeaveAsync();
            }
            JoinCode = null;
        }

        void OnDestroy()
        {
            if (Manager != null)
            {
                Manager.OnClientConnectedCallback -= OnClientConnected;
                Manager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }
}
