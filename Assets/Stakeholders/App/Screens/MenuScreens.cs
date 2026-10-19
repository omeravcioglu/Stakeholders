using System;
using System.Collections.Generic;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    public class MainMenuScreen : AppScreen
    {
        TextField _name, _code, _ip;
        Label _status;

        public MainMenuScreen(GameApp app) : base(app) { }

        public override void Build()
        {
            Root = Ui.Div("menu-bg", "grow");
            var wrap = Ui.Row();
            wrap.style.alignItems = Align.FlexStart;

            // Left: identity
            var left = Ui.Div("menu-panel");
            left.style.marginRight = 20;
            left.Add(Ui.Text("STAKEHOLDERS", "h0"));
            left.Add(Ui.Text("Build the company together. Build your fortune separately.", "tagline"));
            left.Add(Ui.Spacer(16));
            left.Add(Ui.Text(
                "Run a shared e-commerce company with friends or AI partners. Everyone profits when the company wins, but you each have your own " +
                "cash, salary, shares, skills and secret opportunities. Cooperate. Compete. Maybe betray.", "para", "dim"));
            left.Add(Ui.Spacer(16));
            _name = Ui.Input(App.PlayerName);
            left.Add(Ui.Field("Your name", _name));
            left.Add(Ui.Spacer(8));
            left.Add(Ui.Btn("Solo game", () => Go(SetupMode.Solo), "btn-lg", "btn-block"));
            if (SaveSystem.Exists(SaveSystem.AutoSlot))
                left.Add(Ui.Btn("Continue: " + (SaveSystem.Describe(SaveSystem.AutoSlot) ?? "last game"), () => Load(SaveSystem.AutoSlot), "btn-ghost", "btn-block"));
            if (SaveSystem.Exists(SaveSystem.ManualSlot))
                left.Add(Ui.Btn("Load saved game: " + (SaveSystem.Describe(SaveSystem.ManualSlot) ?? "save 1"), () => Load(SaveSystem.ManualSlot), "btn-ghost", "btn-block"));
            left.Add(Ui.Btn($"Achievements ({Achievements.UnlockedCount}/{Achievements.All.Length})", () => Achievements.ShowList(App.Ui), "btn-ghost", "btn-block"));
            left.Add(Ui.Btn("Quit", Application.Quit, "btn-ghost", "btn-block"));

            // Right: multiplayer
            var right = Ui.Div("menu-panel");
            right.Add(Ui.Text("PLAY WITH FRIENDS", "h3"));
            right.Add(Ui.Spacer(8));
            right.Add(Ui.Text("Online (Unity Relay)", "h2"));
            right.Add(Ui.Text("Host a game and share the join code. Works across the internet, no port forwarding.", "para", "dim", "small"));
            right.Add(Ui.Spacer(8));
            right.Add(Ui.Btn("Host online game", () => Go(SetupMode.HostRelay), "btn-block"));
            var joinRow = Ui.Row();
            _code = Ui.Input("");
            _code.style.flexGrow = 1;
            _code.style.marginRight = 6;
            joinRow.Add(_code);
            joinRow.Add(Ui.Btn("Join with code", JoinRelay, "btn-good"));
            right.Add(Ui.Field("Join code", joinRow));

            right.Add(Ui.Div("divider"));
            right.Add(Ui.Text("Local network / direct IP", "h2"));
            right.Add(Ui.Text($"Host on your LAN (port {NetBridge.DefaultPort}) or join by IP address. Great for testing two copies on one PC.", "para", "dim", "small"));
            right.Add(Ui.Spacer(8));
            right.Add(Ui.Btn("Host on LAN", () => Go(SetupMode.HostDirect), "btn-ghost", "btn-block"));
            var ipRow = Ui.Row();
            _ip = Ui.Input(PlayerPrefs.GetString("stk_ip", "127.0.0.1"));
            _ip.style.flexGrow = 1;
            _ip.style.marginRight = 6;
            ipRow.Add(_ip);
            ipRow.Add(Ui.Btn("Join by IP", JoinDirect, "btn-ghost"));
            right.Add(Ui.Field("Host address", ipRow));
            _status = Ui.Text("", "small", "warn", "para");
            right.Add(_status);

            wrap.Add(left);
            wrap.Add(right);
            Root.Add(wrap);
        }

        void RememberName()
        {
            string n = _name.value?.Trim();
            if (string.IsNullOrEmpty(n)) n = "Founder";
            if (n.Length > 20) n = n.Substring(0, 20);
            App.PlayerName = n;
        }

        void Go(SetupMode mode)
        {
            RememberName();
            App.Show(new SetupScreen(App, mode));
        }

        void Load(string slot)
        {
            if (!App.LoadSolo(slot)) _status.text = "That save could not be loaded.";
        }

        async void JoinRelay()
        {
            RememberName();
            string code = _code.value?.Trim();
            if (string.IsNullOrEmpty(code)) { _status.text = "Enter the host's join code."; return; }
            _status.text = "Connecting...";
            var net = App.EnsureNet();
            var lobby = new LobbyScreen(App, null, new LobbyClient(net, App.PlayerName));
            bool ok = await net.JoinRelay(code);
            if (!ok) { lobby.Dispose(); _status.text = "Could not join: " + net.LastError; net.Shutdown(); return; }
            App.Show(lobby);
        }

        void JoinDirect()
        {
            RememberName();
            string ip = _ip.value?.Trim();
            PlayerPrefs.SetString("stk_ip", ip ?? "");
            var net = App.EnsureNet();
            var lobby = new LobbyScreen(App, null, new LobbyClient(net, App.PlayerName));
            if (!net.StartClientDirect(ip, NetBridge.DefaultPort)) { lobby.Dispose(); _status.text = net.LastError; return; }
            App.Show(lobby);
        }
    }

    public enum SetupMode { Solo, HostRelay, HostDirect }

    /// <summary>Game settings (and AI partners for solo).</summary>
    public class SetupScreen : AppScreen
    {
        readonly SetupMode _mode;
        readonly GameSettings _settings = new GameSettings();
        readonly List<SeatConfig> _ai = new List<SeatConfig>();
        VisualElement _seats;
        Label _status;
        TextField _company;
        bool _starting;

        static readonly double[] Capital = { 50000, 100000, 250000 };
        static readonly int[] Years = { 1, 2, 3, 5, 0 };
        static readonly double[] DayLength = { 2.0, 3.0, 4.0 };
        static readonly double[] Meeting = { 120, 240, 360, 0 };

        public SetupScreen(GameApp app, SetupMode mode) : base(app)
        {
            _mode = mode;
            _settings.Seed = UnityEngine.Random.Range(1, 1000000);
            if (mode == SetupMode.Solo)
            {
                var pool = new List<Personality>(Catalog.AllPersonalities());
                for (int i = 0; i < 3; i++)
                    _ai.Add(new SeatConfig { Name = Catalog.AINames[(_settings.Seed + i * 3) % Catalog.AINames.Length], IsAI = true, Personality = pool[(_settings.Seed + i * 5) % pool.Count] });
            }
            _settings.MeetingSeconds = mode == SetupMode.Solo ? 0 : 240;
        }

        public override void Build()
        {
            Root = Ui.Div("menu-bg", "grow");
            var panel = Ui.Div("setup-panel");
            var head = Ui.Row("mb12");
            head.Add(Ui.Text(_mode == SetupMode.Solo ? "New solo game" : "Host a game", "h1", "grow"));
            head.Add(Ui.Btn("Back", App.ShowMainMenu, "btn-ghost"));
            panel.Add(head);

            var cols = Ui.Row();
            cols.style.alignItems = Align.FlexStart;
            var left = Ui.Col();
            left.style.width = Length.Percent(50);
            left.style.paddingRight = 20;
            var right = Ui.Col();
            right.style.width = Length.Percent(50);

            _company = Ui.Input(_settings.CompanyName, v => _settings.CompanyName = v);
            left.Add(Ui.Field("Company name", _company));
            left.Add(Ui.Field("Starting company cash", Ui.Segmented(new[] { "$50K", "$100K", "$250K" }, 1, i => _settings.StartingCash = Capital[i])));
            left.Add(Ui.Field("Game length", Ui.Segmented(new[] { "1 year", "2 years", "3 years", "5 years", "Endless" }, 2, i => _settings.GameYears = Years[i])));
            left.Add(Ui.Field("Difficulty", Ui.Segmented(new[] { "Relaxed", "Normal", "Cutthroat" }, 1, i => _settings.Difficulty = i)));
            left.Add(Ui.Field("Day length (real seconds per game day)", Ui.Segmented(new[] { "2s (fast)", "3s", "4s (calm)" }, 1, i => _settings.SecondsPerDay = DayLength[i])));
            left.Add(Ui.Field("Intrigue (private offers, bribes, blackmail)", Ui.Segmented(new[] { "On", "Off" }, 0, i => _settings.Intrigue = i == 0)));
            left.Add(Ui.Field("Performance equity (1% of shares per quarter, by XP)", Ui.Segmented(new[] { "On", "Off" }, 0, i => _settings.PerformanceEquityPct = i == 0 ? 1.0 : 0)));
            if (_mode != SetupMode.Solo)
                left.Add(Ui.Field("Board meeting time limit", Ui.Segmented(new[] { "2 min", "4 min", "6 min", "None" }, 1, i => _settings.MeetingSeconds = Meeting[i])));

            if (_mode == SetupMode.Solo)
            {
                right.Add(Ui.Text("PARTNERS", "field-label"));
                right.Add(Ui.Text("Play alone with 100% ownership, or bring AI partners who help run the company and have their own agendas.", "para", "dim", "small", "mb8"));
                _seats = Ui.Col();
                right.Add(_seats);
                var addRow = Ui.Row("mt8");
                addRow.Add(Ui.Btn("Add AI partner", AddAi, "btn-ghost"));
                right.Add(addRow);
                RebuildSeats();
            }
            else
            {
                right.Add(Ui.Text("HOW IT WORKS", "field-label"));
                right.Add(Ui.Text(_mode == SetupMode.HostRelay
                    ? "After you create the lobby you get a short join code. Friends enter it from the main menu. You can fill empty seats with AI partners before starting."
                    : $"Friends on your network join with your IP address (port {NetBridge.DefaultPort}). You can fill empty seats with AI partners before starting.", "para", "dim"));
                right.Add(Ui.Spacer(12));
                right.Add(Ui.Text("Board meetings are time-limited in multiplayer so nobody can stall the game. Any partner can pause the clock.", "para", "dim", "small"));
            }

            cols.Add(left);
            cols.Add(right);
            var scroll = new ScrollView();
            scroll.style.flexShrink = 1;
            scroll.Add(cols);
            panel.Add(scroll);
            var foot = Ui.Row("mt12");
            _status = Ui.Text("", "warn", "small", "grow");
            foot.Add(_status);
            foot.Add(Ui.Btn(_mode == SetupMode.Solo ? "Start company" : "Create lobby", Start, "btn-lg", "btn-good"));
            panel.Add(foot);
            Root.Add(panel);
        }

        void RebuildSeats()
        {
            _seats.Clear();
            var you = Ui.Div("seat-row");
            you.Add(Ui.Dot(Ui.PlayerColor(0)));
            you.Add(Ui.Spacer(8));
            you.Add(Ui.Text(App.PlayerName + " (you)", "b", "grow"));
            you.Add(Ui.Badge(_ai.Count == 0 ? "100% owner" : $"{Fmt.Pct(1.0 / (_ai.Count + 1), 1)}", "accent"));
            _seats.Add(you);
            var names = new List<string>();
            var list = new List<Personality>(Catalog.AllPersonalities());
            foreach (var p in list) names.Add(p.ToString());
            for (int i = 0; i < _ai.Count; i++)
            {
                int idx = i;
                var seat = _ai[i];
                var row = Ui.Div("seat-row");
                row.Add(Ui.Dot(Ui.PlayerColor(i + 1)));
                row.Add(Ui.Spacer(8));
                var nameCol = Ui.Col("grow");
                nameCol.style.marginRight = 8;
                nameCol.Add(Ui.Text(seat.Name, "b"));
                nameCol.Add(Ui.Text(Catalog.PersonalityDescription(seat.Personality), "tiny", "dim", "para"));
                row.Add(nameCol);
                var sel = new Select(names, list.IndexOf(seat.Personality), k => { seat.Personality = list[k]; RebuildSeats(); });
                sel.style.width = 150;
                sel.style.marginRight = 6;
                row.Add(sel);
                row.Add(Ui.Btn("Remove", () => { _ai.RemoveAt(idx); RebuildSeats(); }, "btn-ghost", "btn-sm"));
                _seats.Add(row);
            }
        }

        void AddAi()
        {
            if (_ai.Count >= 7) return;
            var used = new HashSet<string>();
            foreach (var s in _ai) used.Add(s.Name);
            string name = "AI Partner";
            foreach (var n in Catalog.AINames) if (!used.Contains(n)) { name = n; break; }
            var pool = new List<Personality>(Catalog.AllPersonalities());
            _ai.Add(new SeatConfig { Name = name, IsAI = true, Personality = pool[UnityEngine.Random.Range(0, pool.Count)] });
            RebuildSeats();
        }

        async void Start()
        {
            if (_starting) return;
            _settings.CompanyName = string.IsNullOrWhiteSpace(_company.value) ? "Nimbus Goods" : _company.value.Trim();
            if (_mode == SetupMode.Solo)
            {
                _settings.Seats.Clear();
                _settings.Seats.Add(new SeatConfig { Name = App.PlayerName, IsAI = false });
                foreach (var s in _ai) _settings.Seats.Add(new SeatConfig { Name = s.Name, IsAI = true, Personality = s.Personality });
                App.StartSolo(_settings);
                return;
            }
            _starting = true;
            _status.text = "Starting the host...";
            var net = App.EnsureNet();
            bool ok = _mode == SetupMode.HostRelay ? await net.StartHostRelay(8) : net.StartHostDirect(NetBridge.DefaultPort);
            _starting = false;
            if (!ok)
            {
                _status.text = "Could not host: " + net.LastError;
                net.Shutdown();
                return;
            }
            var host = new LobbyHost(net, App.PlayerName, _settings);
            if (_mode == SetupMode.HostDirect) host.Info.Address = LocalAddress() + ":" + NetBridge.DefaultPort;
            App.Show(new LobbyScreen(App, host, null));
        }

        static string LocalAddress()
        {
            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip)) return ip.ToString();
            }
            catch (Exception) { }
            return "127.0.0.1";
        }
    }

    /// <summary>Pre-game lobby for both host and clients.</summary>
    public class LobbyScreen : AppScreen
    {
        readonly LobbyHost _host;
        readonly LobbyClient _client;
        VisualElement _seats;
        Label _title, _code, _status, _summary;
        Button _startBtn;
        bool _dirty = true;
        float _connectTimer;

        public LobbyScreen(GameApp app, LobbyHost host, LobbyClient client) : base(app)
        {
            _host = host;
            _client = client;
            if (_host != null) _host.Changed += () => _dirty = true;
            if (_client != null) _client.Changed += () => _dirty = true;
        }

        public override void Build()
        {
            Root = Ui.Div("menu-bg", "grow");
            var panel = Ui.Div("setup-panel");
            panel.style.width = 760;
            var head = Ui.Row("mb12");
            _title = Ui.Text(_host != null ? "Lobby" : "Joining...", "h1", "grow");
            head.Add(_title);
            head.Add(Ui.Btn("Leave", Leave, "btn-ghost"));
            panel.Add(head);

            var codeCard = Ui.Div("inset", "mb12");
            codeCard.Add(Ui.Text(_host != null ? "SHARE THIS WITH YOUR FRIENDS" : "STATUS", "field-label"));
            _code = Ui.Text("", "huge", "accent");
            codeCard.Add(_code);
            panel.Add(codeCard);

            _summary = Ui.Text("", "small", "dim", "para", "mb8");
            panel.Add(_summary);
            panel.Add(Ui.Text("PARTNERS", "field-label"));
            _seats = Ui.Col();
            panel.Add(_seats);

            var foot = Ui.Row("mt12");
            _status = Ui.Text("", "small", "warn", "grow", "para");
            foot.Add(_status);
            if (_host != null)
            {
                var names = new List<string>();
                var list = new List<Personality>(Catalog.AllPersonalities());
                foreach (var p in list) names.Add(p.ToString());
                var sel = new Select(names, 0);
                sel.style.width = 150;
                sel.style.marginRight = 6;
                foot.Add(sel);
                foot.Add(Ui.Btn("Add AI partner", () => _host.AddAI(list[sel.Index]), "btn-ghost"));
                _startBtn = Ui.Btn("Start game", StartGame, "btn-good", "btn-lg");
                foot.Add(_startBtn);
            }
            panel.Add(foot);
            Root.Add(panel);
        }

        public override void Tick(float dt)
        {
            if (_client != null)
            {
                if (_client.StartPlayerId >= 0)
                {
                    _client.Detach();
                    App.BeginSession(new ClientSession(App.Net, _client.StartPlayerId));
                    return;
                }
                if (_client.Info == null && _client.Error == null)
                {
                    _connectTimer += dt;
                    if (_connectTimer > 25) { _status.text = "The host is not answering. Check the code or address."; }
                }
            }
            if (!_dirty) return;
            _dirty = false;
            Refresh();
        }

        void Refresh()
        {
            LobbyInfo info = _host != null ? _host.Info : _client?.Info;
            if (_client != null && _client.Error != null)
            {
                _title.text = "Disconnected";
                _code.text = "";
                _status.text = _client.Error;
                return;
            }
            if (info == null)
            {
                _code.text = "Connecting to host...";
                return;
            }
            _title.text = _host != null ? "Your lobby" : "Lobby";
            if (_host != null) _code.text = !string.IsNullOrEmpty(info.JoinCode) ? info.JoinCode : (info.Address ?? "");
            else _code.text = "Waiting for the host to start...";
            var st = info.Settings;
            _summary.text = $"{st.CompanyName} · starting cash {Fmt.Money(st.StartingCash)} · {(st.GameYears > 0 ? st.GameYears + " years" : "endless")} · " +
                            $"{(st.Difficulty == 0 ? "relaxed" : st.Difficulty == 2 ? "cutthroat" : "normal")} · intrigue {(st.Intrigue ? "on" : "off")}";
            _seats.Clear();
            var list = new List<Personality>(Catalog.AllPersonalities());
            var names = new List<string>();
            foreach (var p in list) names.Add(p.ToString());
            for (int i = 0; i < info.Seats.Count; i++)
            {
                int idx = i;
                var seat = info.Seats[i];
                var row = Ui.Div("seat-row");
                row.Add(Ui.Dot(Ui.PlayerColor(i)));
                row.Add(Ui.Spacer(8));
                row.Add(Ui.Text(seat.Name + (i == info.YourSeat ? " (you)" : ""), "b", "grow"));
                row.Add(Ui.Badge(seat.IsHost ? "Host" : seat.IsAI ? "AI" : "Human", seat.IsAI ? null : "accent"));
                if (seat.IsAI)
                {
                    if (_host != null)
                    {
                        var sel = new Select(names, list.IndexOf(seat.Personality), k => _host.SetPersonality(idx, list[k]));
                        sel.style.width = 140;
                        sel.style.marginRight = 6;
                        row.Add(sel);
                    }
                    else row.Add(Ui.Badge(seat.Personality.ToString()));
                }
                if (_host != null && i > 0) row.Add(Ui.Btn("Remove", () => _host.RemoveSeat(idx), "btn-ghost", "btn-sm"));
                _seats.Add(row);
            }
            if (_host != null) _status.text = info.Seats.Count < 2 ? "Waiting for partners. You can also add AI partners." : $"{info.Seats.Count} partners ready.";
        }

        void StartGame()
        {
            var session = _host.StartGame();
            App.BeginSession(session);
        }

        void Leave()
        {
            Dispose();
            App.ShowMainMenu();
        }

        public override void Dispose()
        {
            _host?.Detach();
            _client?.Detach();
        }
    }
}
