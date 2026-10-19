using System.Collections.Generic;
using Stakeholders.Core;

namespace Stakeholders.App
{
    /// <summary>
    /// What the UI talks to. The UI only ever reads <see cref="View"/> (a per-player filtered state)
    /// and sends <see cref="Command"/>s, so solo and multiplayer run the exact same UI code.
    /// </summary>
    public abstract class GameSession
    {
        public GameState View { get; protected set; }
        public int LocalPlayerId { get; protected set; }
        /// <summary>Increments whenever <see cref="View"/> is replaced.</summary>
        public int ViewVersion { get; protected set; }
        public abstract bool IsHost { get; }
        public virtual bool IsMultiplayer => false;
        public virtual string StatusText => null;
        /// <summary>The full state (solo/host only), used for saving.</summary>
        public virtual GameState Authoritative => null;
        public abstract void Send(Command command);
        public abstract void Tick(float deltaTime);
        public virtual void Shutdown() { }
    }

    /// <summary>Solo play: the simulation runs locally.</summary>
    public class LocalSession : GameSession
    {
        public readonly GameSimulation Sim;
        long _viewRevision = -1;
        float _throttle;

        public LocalSession(GameState state, int localPlayerId)
        {
            Sim = new GameSimulation(state);
            LocalPlayerId = localPlayerId;
            RebuildView();
        }

        public override bool IsHost => true;
        public override GameState Authoritative => Sim.State;

        public override void Send(Command command)
        {
            command.PlayerId = LocalPlayerId;
            command.Approved = false;
            Sim.Execute(command);
            RebuildView();
        }

        public override void Tick(float deltaTime)
        {
            Sim.Advance(deltaTime);
            _throttle -= deltaTime;
            if (Sim.State.Revision != _viewRevision && _throttle <= 0)
            {
                RebuildView();
                _throttle = 0.1f;
            }
        }

        protected void RebuildView()
        {
            View = ViewBuilder.Build(Sim.State, LocalPlayerId);
            _viewRevision = Sim.State.Revision;
            ViewVersion++;
        }
    }

    /// <summary>Multiplayer host: runs the simulation, applies client commands, streams each client its own view.</summary>
    public class HostSession : LocalSession
    {
        readonly NetBridge _net;
        readonly Dictionary<ulong, int> _clientToPlayer;
        float _sendTimer;
        long _sentRevision = -1;

        public HostSession(GameState state, NetBridge net, Dictionary<ulong, int> clientToPlayer) : base(state, 0)
        {
            _net = net;
            _clientToPlayer = clientToPlayer;
            _net.MessageReceived += OnMessage;
            _net.ClientDisconnected += OnClientDisconnected;
            BroadcastViews();
        }

        public override bool IsMultiplayer => true;
        public override string StatusText => $"Hosting · {_clientToPlayer.Count} connected" + (string.IsNullOrEmpty(_net.JoinCode) ? "" : $" · code {_net.JoinCode}");

        float _keepAlive;

        public override void Tick(float deltaTime)
        {
            base.Tick(deltaTime);
            _sendTimer -= deltaTime;
            _keepAlive -= deltaTime;
            // Send on change (throttled), and at least every few seconds so a dropped view heals itself.
            if (_sendTimer <= 0 && (Sim.State.Revision != _sentRevision || _keepAlive <= 0))
            {
                BroadcastViews();
                _sendTimer = 0.3f;
                _keepAlive = 3f;
            }
        }

        void BroadcastViews()
        {
            foreach (var kv in _clientToPlayer) SendView(kv.Key, kv.Value);
            _sentRevision = Sim.State.Revision;
        }

        void SendView(ulong clientId, int playerId)
        {
            var view = ViewBuilder.Build(Sim.State, playerId);
            NetTrim.Apply(view);
            _net.Send(clientId, NetEnvelope.Of("view", view));
        }

        void OnMessage(ulong clientId, NetEnvelope env)
        {
            if (env.T == "sync")
            {
                if (_clientToPlayer.TryGetValue(clientId, out int pid)) SendView(clientId, pid);
            }
            else if (env.T == "cmd")
            {
                if (!_clientToPlayer.TryGetValue(clientId, out int playerId)) return;
                var cmd = env.Read<Command>();
                if (cmd == null) return;
                cmd.PlayerId = playerId; // never trust the client about who it is
                cmd.Approved = false;
                if (cmd.Type == CommandType.AdvanceDays) return;
                Sim.Execute(cmd);
                _sendTimer = 0;
            }
            else if (env.T == "hello")
            {
                // Rejoin an existing seat by name.
                var hello = env.Read<HelloMessage>();
                if (hello == null) return;
                foreach (var p in Sim.State.Players)
                {
                    if (p.IsAI || p.Connected || !string.Equals(p.Name, hello.Name, System.StringComparison.OrdinalIgnoreCase)) continue;
                    _clientToPlayer[clientId] = p.Id;
                    p.Connected = true;
                    Sim.Log(LogKind.Info, p.Id, $"{p.Name} reconnected.");
                    Sim.State.Revision++;
                    _net.Send(clientId, NetEnvelope.Of("start", new StartMessage { PlayerId = p.Id }));
                    _sendTimer = 0;
                    return;
                }
                _net.Send(clientId, NetEnvelope.Of("kick", new KickMessage { Reason = "That game has already started." }));
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (!_clientToPlayer.TryGetValue(clientId, out int playerId)) return;
            _clientToPlayer.Remove(clientId);
            var p = Sim.State.GetPlayer(playerId);
            if (p != null)
            {
                p.Connected = false;
                p.Ready = false;
                Sim.Log(LogKind.Info, p.Id, $"{p.Name} disconnected. They can rejoin with the same name.");
                Sim.NotifyAll(NoticeKind.Warning, $"{p.Name} disconnected.");
                Sim.State.Revision++;
            }
        }

        public override void Shutdown()
        {
            _net.MessageReceived -= OnMessage;
            _net.ClientDisconnected -= OnClientDisconnected;
            foreach (var kv in _clientToPlayer) _net.Send(kv.Key, NetEnvelope.Of("kick", new KickMessage { Reason = "The host ended the game." }));
            _net.Shutdown();
        }
    }

    /// <summary>Multiplayer client: sends commands, renders whatever view the host sends.</summary>
    public class ClientSession : GameSession
    {
        readonly NetBridge _net;
        string _status = "Connected";

        public ClientSession(NetBridge net, int playerId)
        {
            _net = net;
            LocalPlayerId = playerId;
            _net.MessageReceived += OnMessage;
            _net.LocalDisconnected += OnLocalDisconnected;
            _net.SendToHost(NetEnvelope.Of("sync", null));
        }

        public override bool IsHost => false;
        public override bool IsMultiplayer => true;
        public override string StatusText => _status;
        public bool Lost { get; private set; }
        public string KickReason { get; private set; }

        public override void Send(Command command)
        {
            command.PlayerId = LocalPlayerId;
            _net.SendToHost(NetEnvelope.Of("cmd", command));
        }

        public override void Tick(float deltaTime) { }

        void OnMessage(ulong from, NetEnvelope env)
        {
            if (env.T == "view")
            {
                var v = env.Read<GameState>();
                if (v != null)
                {
                    View = v;
                    ViewVersion++;
                }
            }
            else if (env.T == "kick")
            {
                KickReason = env.Read<KickMessage>()?.Reason ?? "Disconnected.";
                _status = KickReason;
                Lost = true;
            }
        }

        void OnLocalDisconnected(string reason)
        {
            Lost = true;
            _status = "Connection lost" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason);
            KickReason ??= _status;
        }

        public override void Shutdown()
        {
            _net.MessageReceived -= OnMessage;
            _net.LocalDisconnected -= OnLocalDisconnected;
            _net.Shutdown();
        }
    }

    public class HelloMessage { public string Name; public int Version = NetBridge.ProtocolVersion; }
    public class StartMessage { public int PlayerId; }
    public class KickMessage { public string Reason; }

    /// <summary>Keeps network views small: clients only need recent history.</summary>
    public static class NetTrim
    {
        public static void Apply(GameState v)
        {
            v.Log = Tail(v.Log, 120);
            v.Chat = Tail(v.Chat, 120);
            v.Notifications = Tail(v.Notifications, 40);
            v.ResolvedEvents = Tail(v.ResolvedEvents, 15);
            var c = v.Company.ShallowCopy();
            c.Ledger = Tail(c.Ledger, 150);
            v.Company = c;
            var b = v.Board.ShallowCopy();
            b.History = Tail(b.History, 20);
            v.Board = b;
        }

        static List<T> Tail<T>(List<T> list, int n) => list.Count <= n ? list : list.GetRange(list.Count - n, n);
    }
}
