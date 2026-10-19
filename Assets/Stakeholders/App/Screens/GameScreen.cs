using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    /// <summary>The in-game shell: top bar, navigation, pages, activity/chat panel and toasts.</summary>
    public class GameScreen : AppScreen
    {
        public GameState View => App.Session?.View;
        public int Me => App.Session != null ? App.Session.LocalPlayerId : 0;
        public Player MePlayer => View?.GetPlayer(Me);
        public PlayerPrivate MePriv => View?.GetPrivate(Me);

        GameSimulation _query;
        GameState _queryView;

        /// <summary>
        /// Read-only rule queries (ranks, unlocks, costs) over the current view. Never call anything that
        /// changes state on it: all actions go through <see cref="Send"/>.
        /// </summary>
        public GameSimulation Q
        {
            get
            {
                var v = View;
                if (v == null) return null;
                if (_query == null || _queryView != v) { _query = new GameSimulation(v); _queryView = v; }
                return _query;
            }
        }

        readonly List<Page> _pages = new List<Page>();
        readonly Dictionary<Page, Button> _navButtons = new Dictionary<Page, Button>();
        readonly Dictionary<Page, Label> _navBadges = new Dictionary<Page, Label>();
        Page _active;
        VisualElement _pageHost;
        int _lastVersion = -1;
        float _refreshTimer;
        int _lastNoticeId;
        int _lastMeeting = -1;
        bool _gameOverShown;
        bool _lostShown;
        bool _ambitionAsked;
        float _achievementTimer;

        // top bar
        Label _brand, _phase, _date, _cash, _valuation, _sat, _rep, _meeting, _myName, _myCash, _myLevel, _netStatus;
        VisualElement _myAvatar, _xpBar, _speedSeg;
        Button _pauseBtn;
        readonly List<Button> _speedButtons = new List<Button>();

        // side panel
        SidePanel _side;

        public DashboardPage Dashboard;
        public GrowthPage Growth;
        public RivalsPage Rivals;
        public SituationsPage Situations;
        public ProductsPage Products;
        public MarketPage Market;
        public SuppliersPage Suppliers;
        public MarketingPage Marketing;
        public OperationsPage Operations;
        public FinancePage Finance;
        public PartnersPage Partners;
        public BoardPage Board;
        public CareerPage Career;
        public PrivatePage Private;

        public GameScreen(GameApp app) : base(app) { }

        public void Send(Command c) => App.Session?.Send(c);
        public UiRoot UiRoot => App.Ui;
        public void OpenDm(int playerId) => _side?.OpenDirectMessage(playerId);

        public override void Build()
        {
            Root = Ui.Div("col", "grow");
            Root.Add(BuildTopBar());
            var body = Ui.Div("body");
            body.Add(BuildNav());
            var content = Ui.Div("content");
            _pageHost = Ui.Div("col", "grow");
            content.Add(_pageHost);
            body.Add(content);
            _side = new SidePanel(this);
            body.Add(_side.Build());
            Root.Add(body);
            Root.RegisterCallback<KeyDownEvent>(OnKey);
            Root.RegisterCallback<ClickEvent>(e => { if (e.target is Button) Sfx.Click(); }, TrickleDown.TrickleDown);
            Root.focusable = true;
            _lastNoticeId = HighestNoticeId();
            Navigate(Dashboard);
            if (!Automation.Active) Root.schedule.Execute(() => HelpView.ShowIfFirstTime(this)).StartingIn(300);
        }

        int HighestNoticeId()
        {
            int max = 0;
            if (View != null) foreach (var n in View.Notifications) if (n.Id > max) max = n.Id;
            return max;
        }

        VisualElement BuildTopBar()
        {
            var bar = Ui.Div("topbar");
            var brand = Ui.Col();
            _brand = Ui.Text("", "brand");
            _netStatus = Ui.Text("", "brand-sub");
            brand.Add(_brand);
            brand.Add(_netStatus);
            brand.style.minWidth = 190;
            bar.Add(brand);

            var clock = Ui.Div("clock");
            _date = Ui.Text("", "clock-date");
            clock.Add(_date);
            _pauseBtn = Ui.Btn("Pause", () => Send(new Command { Type = CommandType.TogglePause }), "btn-ghost", "btn-sm");
            clock.Add(_pauseBtn);
            _speedSeg = Ui.Div("seg");
            foreach (int s in new[] { 1, 2, 4 })
            {
                int speed = s;
                var b = new Button(() => Send(new Command { Type = CommandType.SetSpeed, I = speed })) { text = s + "x" };
                b.AddToClassList("seg-btn");
                _speedButtons.Add(b);
                _speedSeg.Add(b);
            }
            clock.Add(_speedSeg);
            _phase = Ui.Badge("", "accent");
            _phase.AddToClassList("phase-badge");
            clock.Add(_phase);
            bar.Add(clock);

            bar.Add(TopKpi("COMPANY CASH", out _cash));
            bar.Add(TopKpi("VALUATION", out _valuation));
            bar.Add(TopKpi("SATISFACTION", out _sat));
            bar.Add(TopKpi("REPUTATION", out _rep));
            bar.Add(TopKpi("BOARD MEETING", out _meeting));
            bar.Add(Ui.Grow());

            var me = Ui.Row();
            _myAvatar = Ui.Avatar(null);
            me.Add(_myAvatar);
            me.Add(Ui.Spacer(8));
            var meCol = Ui.Col();
            var nameRow = Ui.Row();
            _myName = Ui.Text("", "b");
            _myName.style.marginRight = 8;
            _myLevel = Ui.Text("", "badge", "badge-gold");
            nameRow.Add(_myName);
            nameRow.Add(_myLevel);
            var cashRow = Ui.Row();
            _myCash = Ui.Text("", "small", "gold");
            _myCash.style.marginRight = 8;
            _xpBar = Ui.Bar(0, Ui.Gold, "bar-thin");
            _xpBar.style.width = 70;
            cashRow.Add(_myCash);
            cashRow.Add(_xpBar);
            meCol.Add(nameRow);
            meCol.Add(Ui.Spacer(4));
            meCol.Add(cashRow);
            me.Add(meCol);
            me.Add(Ui.Spacer(12));
            me.Add(Ui.Btn("Help", () => HelpView.Show(this), "btn-ghost", "btn-sm"));
            me.Add(Ui.Btn("Menu", OpenMenu, "btn-ghost", "btn-sm"));
            bar.Add(me);
            return bar;
        }

        static VisualElement TopKpi(string label, out Label value)
        {
            var k = Ui.Col("top-kpi");
            k.Add(Ui.Text(label, "top-kpi-label"));
            value = Ui.Text("", "top-kpi-value");
            k.Add(value);
            return k;
        }

        VisualElement BuildNav()
        {
            var nav = Ui.Div("nav");
            Dashboard = new DashboardPage(this);
            Growth = new GrowthPage(this);
            Rivals = new RivalsPage(this);
            Situations = new SituationsPage(this);
            Products = new ProductsPage(this);
            Market = new MarketPage(this);
            Suppliers = new SuppliersPage(this);
            Marketing = new MarketingPage(this);
            Operations = new OperationsPage(this);
            Finance = new FinancePage(this);
            Partners = new PartnersPage(this);
            Board = new BoardPage(this);
            Career = new CareerPage(this);
            Private = new PrivatePage(this);

            nav.Add(Ui.Text("COMPANY", "nav-section"));
            AddNav(nav, Dashboard, "D", "#4F8CFF");
            AddNav(nav, Situations, "!", "#FF7A59");
            AddNav(nav, Products, "P", "#2ECC71");
            AddNav(nav, Market, "R", "#00C2D1");
            AddNav(nav, Suppliers, "S", "#7FB2FF");
            AddNav(nav, Marketing, "M", "#FF5AA5");
            AddNav(nav, Operations, "O", "#A3E635");
            AddNav(nav, Finance, "$", "#F2C94C");
            nav.Add(Ui.Text("STRATEGY", "nav-section"));
            AddNav(nav, Growth, "G", "#F5A623");
            AddNav(nav, Rivals, "V", "#FF5A5F");
            nav.Add(Ui.Text("PEOPLE", "nav-section"));
            AddNav(nav, Partners, "#", "#FF8A5C");
            AddNav(nav, Board, "B", "#9B7BFF");
            AddNav(nav, Career, "C", "#F2C94C");
            AddNav(nav, Private, "?", "#B06CFF");
            return nav;
        }

        void AddNav(VisualElement nav, Page page, string icon, string color)
        {
            _pages.Add(page);
            // Notebook.uss draws the doodle icon for "icon-<page>" (the letter and color arguments predate it).
            string key = Notebook.PageKey(page.Title);
            var b = new Button(() => Navigate(page));
            b.AddToClassList("nav-btn");
            b.AddToClassList("nav-" + key);
            var ic = Ui.Text("", "nav-icon", "icon-" + key);
            b.Add(ic);
            b.Add(Ui.Text(page.Title, "nav-label"));
            var badge = Ui.Text("", "nav-badge");
            if (page == Private) badge.AddToClassList("secret");
            badge.Show(false);
            b.Add(badge);
            nav.Add(b);
            _navButtons[page] = b;
            _navBadges[page] = badge;
        }

        public void Navigate(Page page)
        {
            if (page == null || View == null) return;
            if (page.Root == null)
            {
                page.Create();
                page.Root.AddToClassList("page-" + Notebook.PageKey(page.Title));
            }
            _pageHost.Clear();
            _pageHost.Add(page.Root);
            _active = page;
            foreach (var kv in _navButtons) kv.Value.EnableInClassList("active", kv.Key == page);
            page.Refresh();
        }

        public override void Tick(float dt)
        {
            var session = App.Session;
            if (session == null) return;
            if (session is ClientSession cs && cs.Lost && !_lostShown)
            {
                _lostShown = true;
                var m = App.Ui.ShowModal("Disconnected");
                m.Body.Add(Ui.Text(cs.KickReason ?? "The connection to the host was lost.", "para"));
                m.Body.Add(Ui.Text("If the host is still running, rejoin from the main menu with the same name to get your seat back.", "para", "dim", "small", "mt8"));
                m.AddButton("Main menu", App.ShowMainMenu);
            }
            if (View == null) return;
            if (_active == null)
            {
                // Clients build the screen before the host's first view arrives.
                _lastNoticeId = HighestNoticeId();
                Navigate(Dashboard);
            }
            // Ask for a secret ambition once the welcome screens are out of the way.
            if (!_ambitionAsked && !Automation.Active && !App.Ui.HasModal && MePriv != null && string.IsNullOrEmpty(MePriv.Ambition) && MePriv.AmbitionChoices.Count > 0 && View.Phase == GamePhase.Running)
            {
                _ambitionAsked = true;
                AmbitionPicker.Show(this);
            }
            _achievementTimer -= dt;
            if (_achievementTimer <= 0)
            {
                _achievementTimer = 2f;
                Achievements.Check(this);
            }
            _refreshTimer -= dt;
            if (session.ViewVersion == _lastVersion || _refreshTimer > 0) return;
            _lastVersion = session.ViewVersion;
            _refreshTimer = 0.12f;
            RefreshAll();
        }

        void RefreshAll()
        {
            var v = View;
            RefreshTopBar(v);
            RefreshBadges(v);
            _side.Refresh();
            _active?.Refresh();
            App.Ui.RefreshModals();
            ShowNewNotifications(v);
            if (v.Phase == GamePhase.BoardMeeting && v.Board.MeetingNumber != _lastMeeting)
            {
                _lastMeeting = v.Board.MeetingNumber;
                if (_active != Board) Navigate(Board);
            }
            if (v.Phase == GamePhase.GameOver && !_gameOverShown)
            {
                _gameOverShown = true;
                GameOverView.Show(this);
            }
        }

        void RefreshTopBar(GameState v)
        {
            var c = v.Company;
            _brand.SetText(c.Name);
            _netStatus.SetText(App.Session.StatusText ?? (App.Session.IsMultiplayer ? "Multiplayer" : "Solo game"));
            _date.SetText(Fmt.Date(v.Day));
            bool meeting = v.Phase == GamePhase.BoardMeeting;
            _pauseBtn.text = v.Paused ? "Resume" : "Pause";
            _pauseBtn.EnableInClassList("btn-warn", v.Paused);
            for (int i = 0; i < _speedButtons.Count; i++)
                _speedButtons[i].EnableInClassList("on", !v.Paused && v.Speed == new[] { 1, 2, 4 }[i]);
            if (meeting) { _phase.SetText("BOARD MEETING"); _phase.Show(true); }
            else if (v.Phase == GamePhase.GameOver) { _phase.SetText("GAME OVER"); _phase.Show(true); }
            else if (v.Paused) { _phase.SetText("PAUSED"); _phase.Show(true); }
            else _phase.Show(false);

            _cash.SetText(Fmt.Money(c.Cash));
            _cash.style.color = c.Cash < 0 ? Ui.Bad : Ui.TextColor;
            _valuation.SetText(Fmt.Money(c.Valuation));
            _sat.SetText(Fmt.Int(c.Satisfaction) + "%");
            _sat.style.color = c.Satisfaction >= 70 ? Ui.Good : c.Satisfaction >= 50 ? Ui.TextColor : Ui.Bad;
            _rep.SetText(Fmt.Int(c.Reputation));
            int days = v.Board.NextMeetingDay - v.Day;
            _meeting.SetText(meeting ? "NOW" : days <= 0 ? "today" : Fmt.Days(days));
            _meeting.style.color = meeting ? Ui.Secret : days <= 7 ? Ui.Warn : Ui.TextColor;

            var me = MePlayer;
            var pr = MePriv;
            if (me != null)
            {
                Ui.UpdateAvatar(_myAvatar, me);
                _myName.SetText(me.Name);
                _myCash.SetText(pr != null ? Fmt.Money(pr.Cash) + " personal" : "");
                int cur = GameSimulation.TotalXpForLevel(me.Level), next = GameSimulation.TotalXpForLevel(me.Level + 1);
                _myLevel.SetText($"Lv {me.Level}" + (me.SkillPoints > 0 ? $" (+{me.SkillPoints})" : ""));
                Ui.SetBar(_xpBar, (me.Xp - cur) / (float)Math.Max(1, next - cur));
            }
        }

        void RefreshBadges(GameState v)
        {
            int open = v.Events.Count;
            SetBadge(Situations, open, open > 0 && v.Events.Exists(e => e.Severity >= 3) ? null : "info");
            int votes = 0;
            foreach (var p in v.Board.Agenda) if (p.Status == ProposalStatus.Pending && !p.Votes.Exists(x => x.PlayerId == Me)) votes++;
            foreach (var a in v.Approvals) if (a.Status == ProposalStatus.Pending && !a.Votes.Exists(x => x.PlayerId == Me)) votes++;
            SetBadge(Board, votes, null);
            int shareOffers = v.ShareOffers.FindAll(o => o.ToId == Me && o.Status == OfferStatus.Pending).Count;
            SetBadge(Partners, shareOffers, "info");
            var me = MePlayer;
            SetBadge(Career, me != null ? me.SkillPoints : 0, "info");
            int secrets = v.Offers.FindAll(o => o.TargetId == Me && o.Status == OfferStatus.Pending).Count;
            secrets += v.Evidence.FindAll(e => !e.Exposed && e.DiscoveredBy.Contains(Me) && !e.Silenced.Contains(Me)).Count;
            SetBadge(Private, secrets, null);
            int candidates = v.Candidates.Count;
            SetBadge(Operations, candidates, "info");
            int neg = v.Negotiations.FindAll(n => n.PlayerId == Me && n.Status == NegotiationStatus.Active).Count;
            SetBadge(Suppliers, neg, "info");
            SetBadge(Growth, v.Company.Building.Count, "info");
            var q = Q;
            int chances = v.Rivals.Count(r => q.CanAcquire(r, out _)) + (v.Company.Stage >= Stages.Max && !v.Company.IpoPending && !v.Company.WentPublic ? 1 : 0);
            SetBadge(Rivals, chances, "info");
        }

        void SetBadge(Page page, int count, string variant)
        {
            var b = _navBadges[page];
            b.Show(count > 0);
            b.SetText(count > 99 ? "99+" : count.ToString());
            if (page != Private)
            {
                b.EnableInClassList("info", variant == "info");
            }
        }

        void ShowNewNotifications(GameState v)
        {
            int shown = 0;
            int max = _lastNoticeId;
            NoticeKind? loudest = null;
            foreach (var n in v.Notifications)
            {
                if (n.Id <= _lastNoticeId) continue;
                if (n.Id > max) max = n.Id;
                if (n.Kind == NoticeKind.Celebration)
                {
                    Celebration.Show(App.Ui, n.Text);
                    continue;
                }
                if (shown++ < 4) App.Ui.Toast(n.Text, n.Kind);
                if (loudest == null || SoundRank(n.Kind) > SoundRank(loudest.Value)) loudest = n.Kind;
            }
            _lastNoticeId = max;
            if (loudest.HasValue) Sfx.ForNotice(loudest.Value);
        }

        static int SoundRank(NoticeKind k)
        {
            switch (k)
            {
                case NoticeKind.Danger: return 5;
                case NoticeKind.Private: return 4;
                case NoticeKind.Warning: return 3;
                case NoticeKind.Success: return 2;
                case NoticeKind.Xp: return 1;
                default: return 0;
            }
        }

        void OnKey(KeyDownEvent e)
        {
            if (e.target is TextField || (e.target is VisualElement ve && ve.GetFirstAncestorOfType<TextField>() != null)) return;
            if (e.keyCode == KeyCode.Space) { Send(new Command { Type = CommandType.TogglePause }); e.StopPropagation(); }
            else if (e.keyCode == KeyCode.Alpha1) Send(new Command { Type = CommandType.SetSpeed, I = 1 });
            else if (e.keyCode == KeyCode.Alpha2) Send(new Command { Type = CommandType.SetSpeed, I = 2 });
            else if (e.keyCode == KeyCode.Alpha3) Send(new Command { Type = CommandType.SetSpeed, I = 4 });
        }

        void OpenMenu()
        {
            var m = App.Ui.ShowModal("Menu");
            var s = App.Session;
            m.Body.Add(Ui.Text(s.IsMultiplayer ? (s.IsHost ? "You are hosting this game. Leaving ends it for everyone." : "You can rejoin with the same name while the host is running.") : "Your game is autosaved every month.", "para", "dim"));
            m.Body.Add(Ui.Spacer(8));
            m.Body.Add(Ui.Field("Sound effects", Ui.Segmented(new[] { "On", "Off" }, Sfx.Enabled ? 0 : 1, i => Sfx.Enabled = i == 0)));
            var v = View;
            if (v != null && !s.IsMultiplayer)
                m.Body.Add(Ui.Field("Pause automatically for critical situations", Ui.Segmented(new[] { "On", "Off" }, v.Settings.AutoPause ? 0 : 1,
                    i => Send(new Command { Type = CommandType.SetOption, A = "autopause", Flag = i == 0 }))));
            m.Body.Add(Ui.Text("Shortcuts: Space pauses, 1/2/3 change speed.", "small", "faint"));
            if (!s.IsMultiplayer) m.AddButton("Save game", () => { App.SaveNow(); m.Close(); }, "btn-ghost");
            m.AddButton("Main menu", () => { m.Close(); App.ShowMainMenu(); }, "btn-bad");
            m.AddButton("Resume", m.Close);
        }

        public override void Dispose()
        {
            _side?.Dispose();
        }
    }
}
