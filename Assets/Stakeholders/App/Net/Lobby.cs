using System;
using System.Collections.Generic;
using Stakeholders.Core;
using Unity.Netcode;

namespace Stakeholders.App
{
    public class LobbySeat
    {
        public string Name;
        public bool IsAI;
        public Personality Personality;
        public bool IsHost;
        public ulong ClientId;
    }

    /// <summary>What everyone in the lobby sees before the game starts.</summary>
    public class LobbyInfo
    {
        public List<LobbySeat> Seats = new List<LobbySeat>();
        public GameSettings Settings = new GameSettings();
        public string JoinCode;
        public string Address;
        public int YourSeat = -1;
        public int MaxSeats = 8;
    }

    /// <summary>Host side of the pre-game lobby.</summary>
    public class LobbyHost
    {
        public readonly LobbyInfo Info = new LobbyInfo();
        readonly NetBridge _net;
        public event Action Changed;

        public LobbyHost(NetBridge net, string hostName, GameSettings settings)
        {
            _net = net;
            Info.Settings = settings;
            Info.Settings.Multiplayer = true;
            Info.JoinCode = net.JoinCode;
            Info.Seats.Add(new LobbySeat { Name = string.IsNullOrWhiteSpace(hostName) ? "Host" : hostName.Trim(), IsHost = true, ClientId = NetworkManager.ServerClientId });
            _net.MessageReceived += OnMessage;
            _net.ClientDisconnected += OnDisconnected;
        }

        void OnMessage(ulong clientId, NetEnvelope env)
        {
            if (env.T != "hello") return;
            var hello = env.Read<HelloMessage>();
            if (hello == null) return;
            if (hello.Version != NetBridge.ProtocolVersion)
            {
                _net.Send(clientId, NetEnvelope.Of("kick", new KickMessage { Reason = "Version mismatch: update the game." }));
                return;
            }
            if (Info.Seats.Count >= Info.MaxSeats)
            {
                _net.Send(clientId, NetEnvelope.Of("kick", new KickMessage { Reason = "The lobby is full." }));
                return;
            }
            string name = string.IsNullOrWhiteSpace(hello.Name) ? "Partner" : hello.Name.Trim();
            if (name.Length > 20) name = name.Substring(0, 20);
            // Keep names unique so rejoining by name is unambiguous.
            string unique = name;
            int n = 2;
            while (Info.Seats.Exists(s => string.Equals(s.Name, unique, StringComparison.OrdinalIgnoreCase))) unique = name + " " + n++;
            Info.Seats.Add(new LobbySeat { Name = unique, ClientId = clientId });
            Broadcast();
        }

        void OnDisconnected(ulong clientId)
        {
            int removed = Info.Seats.RemoveAll(s => !s.IsAI && !s.IsHost && s.ClientId == clientId);
            if (removed > 0) Broadcast();
        }

        public void AddAI(Personality p)
        {
            if (Info.Seats.Count >= Info.MaxSeats) return;
            var used = new HashSet<string>();
            foreach (var s in Info.Seats) used.Add(s.Name);
            string name = "AI Partner";
            foreach (var candidate in Catalog.AINames) if (!used.Contains(candidate)) { name = candidate; break; }
            Info.Seats.Add(new LobbySeat { Name = name, IsAI = true, Personality = p });
            Broadcast();
        }

        public void RemoveSeat(int index)
        {
            if (index <= 0 || index >= Info.Seats.Count) return;
            var seat = Info.Seats[index];
            if (!seat.IsAI) _net.Send(seat.ClientId, NetEnvelope.Of("kick", new KickMessage { Reason = "The host removed you from the lobby." }));
            Info.Seats.RemoveAt(index);
            Broadcast();
        }

        public void SetPersonality(int index, Personality p)
        {
            if (index < 0 || index >= Info.Seats.Count || !Info.Seats[index].IsAI) return;
            Info.Seats[index].Personality = p;
            Broadcast();
        }

        public void Broadcast()
        {
            for (int i = 0; i < Info.Seats.Count; i++)
            {
                var seat = Info.Seats[i];
                if (seat.IsAI || seat.IsHost) continue;
                Info.YourSeat = i;
                _net.Send(seat.ClientId, NetEnvelope.Of("lobby", Info));
            }
            Info.YourSeat = 0;
            Changed?.Invoke();
        }

        /// <summary>Create the game and hand every client its seat.</summary>
        public HostSession StartGame()
        {
            var st = Info.Settings;
            st.Multiplayer = true;
            st.Seats.Clear();
            foreach (var seat in Info.Seats) st.Seats.Add(new SeatConfig { Name = seat.Name, IsAI = seat.IsAI, Personality = seat.Personality });
            if (st.MeetingSeconds <= 0) st.MeetingSeconds = 240;
            var state = GameFactory.Create(st);
            var map = new Dictionary<ulong, int>();
            for (int i = 0; i < Info.Seats.Count; i++)
            {
                var seat = Info.Seats[i];
                if (seat.IsAI || seat.IsHost) continue;
                map[seat.ClientId] = i;
                _net.Send(seat.ClientId, NetEnvelope.Of("start", new StartMessage { PlayerId = i }));
            }
            Detach();
            return new HostSession(state, _net, map);
        }

        public void Detach()
        {
            _net.MessageReceived -= OnMessage;
            _net.ClientDisconnected -= OnDisconnected;
        }
    }

    /// <summary>Client side of the pre-game lobby.</summary>
    public class LobbyClient
    {
        public LobbyInfo Info { get; private set; }
        public string Error { get; private set; }
        public int StartPlayerId { get; private set; } = -1;
        readonly NetBridge _net;
        readonly string _name;
        public event Action Changed;

        public LobbyClient(NetBridge net, string name)
        {
            _net = net;
            _name = name;
            _net.MessageReceived += OnMessage;
            _net.ConnectedToHost += SayHello;
            _net.LocalDisconnected += OnLost;
            if (_net.IsConnectedClient) SayHello();
        }

        void SayHello() => _net.SendToHost(NetEnvelope.Of("hello", new HelloMessage { Name = _name }));

        void OnMessage(ulong from, NetEnvelope env)
        {
            switch (env.T)
            {
                case "lobby": Info = env.Read<LobbyInfo>(); Changed?.Invoke(); break;
                case "start": StartPlayerId = env.Read<StartMessage>().PlayerId; Changed?.Invoke(); break;
                case "kick": Error = env.Read<KickMessage>()?.Reason ?? "Disconnected."; Changed?.Invoke(); break;
            }
        }

        void OnLost(string reason)
        {
            Error ??= "Lost connection to the host." + (string.IsNullOrEmpty(reason) ? "" : " " + reason);
            Changed?.Invoke();
        }

        public void Detach()
        {
            _net.MessageReceived -= OnMessage;
            _net.ConnectedToHost -= SayHello;
            _net.LocalDisconnected -= OnLost;
        }
    }
}
