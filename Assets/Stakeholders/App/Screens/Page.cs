using System;
using System.Collections.Generic;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    /// <summary>One tab of the game screen. Built once, refreshed whenever a new view arrives.</summary>
    public abstract class Page
    {
        protected readonly GameScreen S;
        protected GameState V => S.View;
        protected int Me => S.Me;
        protected Player MeP => S.MePlayer;
        protected PlayerPrivate MePriv => S.MePriv;
        protected UiRoot Overlay => S.UiRoot;

        public VisualElement Root { get; private set; }
        protected VisualElement Body;
        protected VisualElement HeadRight;
        protected Label Subtitle;

        public abstract string Title { get; }

        protected Page(GameScreen screen) { S = screen; }

        public void Create()
        {
            Root = Ui.Col("grow");
            var head = Ui.Div("page-head");
            var titles = Ui.Col("grow");
            titles.Add(Ui.Text(Title, "h1"));
            Subtitle = Ui.Text("", "small", "dim");
            titles.Add(Subtitle);
            head.Add(titles);
            HeadRight = Ui.Row();
            head.Add(HeadRight);
            Root.Add(head);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("page-scroll");
            Body = Ui.Div("page-body");
            scroll.Add(Body);
            Root.Add(scroll);
            Build();
        }

        protected abstract void Build();
        public abstract void Refresh();

        protected void Send(Command c) => S.Send(c);
        protected string Name(int id) => V.PlayerName(id);

        /// <summary>A row of equally sized columns that wrap on narrow screens.</summary>
        protected static VisualElement Cols(params VisualElement[] children)
        {
            var row = Ui.Div("row-top", "wrap");
            foreach (var c in children)
            {
                if (c == null) continue;
                c.style.flexGrow = 1;
                c.style.flexBasis = 0;
                c.style.minWidth = 300;
                row.Add(c);
            }
            return row;
        }

        protected static VisualElement Kpi(string label, out Label value, out Label sub)
        {
            var k = Ui.Div("kpi");
            k.Add(Ui.Text(label, "kpi-label"));
            value = Ui.Text("-", "kpi-value");
            sub = Ui.Text("", "kpi-sub");
            k.Add(value);
            k.Add(sub);
            return k;
        }

        /// <summary>Creates a table header row from (title, width) pairs. Width 0 means flexible.</summary>
        protected static VisualElement Header(params (string title, float width)[] cols)
        {
            var tr = Ui.Div("tr", "tr-head");
            foreach (var (title, width) in cols)
            {
                var l = Ui.Text(title);
                var td = Ui.Div("td");
                if (width > 0) td.W(width); else td.AddToClassList("grow");
                td.Add(l);
                tr.Add(td);
            }
            return tr;
        }

        protected static Label Cell(VisualElement tr, float width, string cls = null)
        {
            var td = Ui.Div("td");
            if (width > 0) td.W(width); else td.AddToClassList("grow");
            var l = Ui.Text("", cls);
            td.Add(l);
            tr.Add(td);
            return l;
        }

        protected static VisualElement CellBox(VisualElement tr, float width)
        {
            var td = Ui.Div("td", "row");
            if (width > 0) td.W(width); else td.AddToClassList("grow");
            tr.Add(td);
            return td;
        }

        protected static string DaysLeft(int targetDay, int today)
        {
            int d = targetDay - today;
            if (d < 0) return "overdue";
            if (d == 0) return "today";
            return d == 1 ? "1 day" : d + " days";
        }

        protected static string TrendText(TrendPhase p)
        {
            switch (p)
            {
                case TrendPhase.Dormant: return "Dormant";
                case TrendPhase.Emerging: return "Emerging";
                case TrendPhase.Rising: return "Rising fast";
                case TrendPhase.Peak: return "Peak";
                case TrendPhase.Declining: return "Declining";
                default: return "Stable";
            }
        }

        protected static Color TrendColor(TrendPhase p)
        {
            switch (p)
            {
                case TrendPhase.Emerging: return Ui.Accent;
                case TrendPhase.Rising: return Ui.Good;
                case TrendPhase.Peak: return Ui.Gold;
                case TrendPhase.Declining: return Ui.Bad;
                case TrendPhase.Dormant: return Ui.Faint;
                default: return Ui.Dim;
            }
        }
    }

    /// <summary>Right-hand panel: live activity feed and chat (with private messages).</summary>
    public class SidePanel
    {
        readonly GameScreen _s;
        VisualElement _root, _feedHost, _chatHost;
        ScrollView _feedScroll, _chatScroll;
        Button _feedTab, _chatTab;
        KeyedList<LogEntry> _feed;
        KeyedList<ChatMessage> _chat;
        TextField _input;
        Select _to;
        readonly List<int> _toIds = new List<int>();
        int _lastChatId, _seenChatId;
        bool _chatOpen;
        string _lastRecipients, _lastFirstFeed;

        public SidePanel(GameScreen s) { _s = s; }

        public VisualElement Build()
        {
            _root = Ui.Div("side", "col");
            var tabs = Ui.Div("side-tabs");
            var seg = Ui.Div("seg");
            _feedTab = new Button(() => SetTab(false)) { text = "Activity" };
            _feedTab.AddToClassList("seg-btn");
            _chatTab = new Button(() => SetTab(true)) { text = "Chat" };
            _chatTab.AddToClassList("seg-btn");
            seg.Add(_feedTab);
            seg.Add(_chatTab);
            tabs.Add(seg);
            _root.Add(tabs);

            _feedHost = Ui.Col("grow");
            _feedScroll = new ScrollView();
            _feedScroll.style.flexGrow = 1;
            _feedHost.Add(_feedScroll);
            _feed = new KeyedList<LogEntry>(_feedScroll.contentContainer, e => e.Day + "|" + e.Text, CreateFeedRow, "Nothing has happened yet.");
            _root.Add(_feedHost);

            _chatHost = Ui.Col("grow");
            _chatScroll = new ScrollView();
            _chatScroll.style.flexGrow = 1;
            _chatHost.Add(_chatScroll);
            _chat = new KeyedList<ChatMessage>(_chatScroll.contentContainer, m => m.Id.ToString(), CreateChatRow, "No messages yet. Say hi to your partners.");
            var inputRow = Ui.Div("chat-input-row", "col");
            _to = new Select(new[] { "Everyone" }, 0);
            _to.style.marginBottom = 6;
            inputRow.Add(_to);
            var row = Ui.Row();
            _input = Ui.Input("");
            _input.style.flexGrow = 1;
            _input.style.marginRight = 6;
            _input.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { SendChat(); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
            row.Add(_input);
            row.Add(Ui.Btn("Send", SendChat, "btn-sm"));
            inputRow.Add(row);
            _chatHost.Add(inputRow);
            _root.Add(_chatHost);
            SetTab(false);
            return _root;
        }

        void SetTab(bool chat)
        {
            _chatOpen = chat;
            _feedTab.EnableInClassList("on", !chat);
            _chatTab.EnableInClassList("on", chat);
            _feedHost.Show(!chat);
            _chatHost.Show(chat);
            if (chat) _seenChatId = _lastChatId;
            UpdateChatTab();
        }

        public void OpenDirectMessage(int playerId)
        {
            SetTab(true);
            int idx = _toIds.IndexOf(playerId);
            if (idx >= 0) _to.SetIndex(idx);
            _input.Focus();
        }

        ListRow<LogEntry> CreateFeedRow()
        {
            var row = new ListRow<LogEntry>();
            var root = Ui.Div("feed-item");
            var day = Ui.Text("", "feed-day");
            var text = Ui.Text("", "feed-text");
            root.Add(day);
            root.Add(text);
            row.Root = root;
            row.Bind = e =>
            {
                day.SetText(Fmt.ShortDate(e.Day) + (e.ActorId >= 0 ? " · " + _s.View.PlayerName(e.ActorId) : ""));
                text.SetText(e.Text);
                root.EnableInClassList("feed-scandal", e.Kind == LogKind.Scandal);
                root.EnableInClassList("feed-board", e.Kind == LogKind.Board);
            };
            return row;
        }

        ListRow<ChatMessage> CreateChatRow()
        {
            var row = new ListRow<ChatMessage>();
            var root = Ui.Div("chat-msg");
            var head = Ui.Row();
            var dot = Ui.Div("dot");
            var name = Ui.Text("", "chat-name");
            name.style.marginLeft = 6;
            var day = Ui.Text("", "tiny", "faint");
            day.style.marginLeft = 6;
            head.Add(dot);
            head.Add(name);
            head.Add(day);
            var text = Ui.Text("", "chat-text");
            root.Add(head);
            root.Add(text);
            row.Root = root;
            row.Bind = m =>
            {
                var v = _s.View;
                var from = v.GetPlayer(m.FromId);
                dot.style.backgroundColor = from != null ? Ui.PlayerColor(from.Color) : Ui.Faint;
                string to = m.ToId >= 0 ? (m.FromId == _s.Me ? " to " + v.PlayerName(m.ToId) : " (private)") : "";
                name.SetText((from != null ? from.Name : "?") + to);
                name.style.color = from != null ? Ui.PlayerInk(from.Color) : Ui.Dim;
                day.SetText(Fmt.ShortDate(m.Day));
                text.SetText(m.Text);
                root.EnableInClassList("chat-dm", m.ToId >= 0);
            };
            return row;
        }

        public void Refresh()
        {
            var v = _s.View;
            if (v == null) return;
            var entries = new List<LogEntry>();
            for (int i = v.Log.Count - 1; i >= 0 && entries.Count < 80; i--) entries.Add(v.Log[i]);
            _feed.Sync(entries);
            // Newest entries are inserted at the top: stay pinned there unless the player scrolled down to read.
            string first = entries.Count > 0 ? entries[0].Day + "|" + entries[0].Text : null;
            if (first != _lastFirstFeed)
            {
                _lastFirstFeed = first;
                if (_feedScroll.scrollOffset.y < 80) _feedScroll.schedule.Execute(() => _feedScroll.scrollOffset = Vector2.zero).StartingIn(15);
            }

            var msgs = v.Chat;
            int before = _lastChatId;
            _chat.Sync(msgs.Count > 100 ? msgs.GetRange(msgs.Count - 100, 100) : msgs);
            if (msgs.Count > 0) _lastChatId = msgs[msgs.Count - 1].Id;
            if (_lastChatId != before)
            {
                if (_chatOpen) _seenChatId = _lastChatId;
                _chatScroll.schedule.Execute(() => _chatScroll.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(30);
            }
            UpdateChatTab();

            // Recipients
            var names = new List<string> { "Everyone" };
            var ids = new List<int> { -1 };
            foreach (var p in v.Players) if (p.Id != _s.Me) { names.Add("Private: " + p.Name + (p.IsAI ? " (AI)" : "")); ids.Add(p.Id); }
            string key = string.Join("|", names);
            if (key != _lastRecipients)
            {
                _lastRecipients = key;
                int cur = _to.Index;
                _toIds.Clear();
                _toIds.AddRange(ids);
                _to.SetOptions(names, Math.Min(cur, names.Count - 1));
            }
        }

        void UpdateChatTab()
        {
            int unread = 0;
            var v = _s.View;
            if (v != null && !_chatOpen) foreach (var m in v.Chat) if (m.Id > _seenChatId && m.FromId != _s.Me) unread++;
            _chatTab.text = unread > 0 ? $"Chat ({unread})" : "Chat";
        }

        void SendChat()
        {
            string text = _input.value?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            int to = _to.Index >= 0 && _to.Index < _toIds.Count ? _toIds[_to.Index] : -1;
            _s.Send(new Command { Type = CommandType.Chat, Text = text, I = to });
            _input.value = "";
            _input.Focus();
        }

        public void Dispose() { }
    }

    /// <summary>Final results and the Reckoning: every secret revealed.</summary>
    public static class GameOverView
    {
        public static void Show(GameScreen s)
        {
            var v = s.View;
            var sum = v.Summary;
            var m = s.UiRoot.ShowModal("The partnership is over", true);
            m.Panel.style.width = 1020;
            if (sum == null)
            {
                m.Body.Add(Ui.Text("Game over.", "para"));
                m.AddButton("Main menu", () => GameApp.Instance.ShowMainMenu());
                return;
            }
            m.Body.Add(Ui.Text(sum.Reason, "para", "h2", "mb8"));
            m.Body.Add(Ui.Text($"Final company valuation: {Fmt.MoneyFull(sum.FinalValuation)}", "dim"));
            m.Body.Add(Ui.Text($"The company finished as {Stages.InSentence(sum.Stage)}, #{sum.CompanyRank} of {sum.CompanyCount} in the industry{(sum.Ipo ? ", and went public" : "")}.", "dim", "mb12"));
            var table = Ui.Div("inset", "mb12");
            var head = Ui.Div("tr", "tr-head");
            foreach (var (t, w) in new[] { ("#", 30f), ("Partner", 0f), ("Cash", 80f), ("Investments", 90f), ("Equity", 130f), ("Ambition", 170f), ("Net worth", 100f), ("Perf.", 60f) })
            {
                var td = Ui.Div("td");
                if (w > 0) td.W(w); else td.AddToClassList("grow");
                td.Add(Ui.Text(t));
                head.Add(td);
            }
            table.Add(head);
            foreach (var st in sum.Standings)
            {
                var tr = Ui.Div("tr");
                var p = v.GetPlayer(st.PlayerId);
                void C(string text, float w, string cls = null)
                {
                    var td = Ui.Div("td");
                    if (w > 0) td.W(w); else td.AddToClassList("grow");
                    td.Add(Ui.Text(text, cls));
                    tr.Add(td);
                }
                C("#" + st.Rank, 30, st.Rank == 1 ? "gold" : null);
                C(st.Name + (st.PlayerId == s.Me ? " (you)" : "") + (p != null && p.IsAI ? $" · {p.Personality}" : ""), 0, "b");
                C(Fmt.Money(st.PersonalCash), 80);
                C(Fmt.Money(st.Investments), 90);
                C(Fmt.Money(st.EquityValue) + $" ({Fmt.Pct(st.OwnershipPct, 1)})", 130);
                string amb = Ambitions.Get(st.Ambition)?.Name;
                C(amb == null ? "-" : st.AmbitionDone ? $"{amb} +{Fmt.Money(st.AmbitionBonus)}" : amb + " (failed)", 170, st.AmbitionDone ? "good" : "dim");
                C(Fmt.Money(st.NetWorth), 100, "b");
                C(Fmt.Int(st.Performance), 60);
                table.Add(tr);
            }
            m.Body.Add(table);
            var awards = Ui.Div("inset", "mb12");
            awards.Add(Ui.Text("AWARDS", "field-label"));
            foreach (var a in sum.Awards) awards.Add(Ui.Text("• " + a, "para"));
            m.Body.Add(awards);
            var secrets = Ui.Div("inset", "card-secret");
            secrets.Add(Ui.Text("THE RECKONING: EVERY SECRET, REVEALED", "field-label", "secret"));
            foreach (var sc in sum.Secrets) secrets.Add(Ui.Text("• " + sc, "para", "small"));
            m.Body.Add(secrets);
            m.AddButton("Look around", m.Close, "btn-ghost");
            m.AddButton("Main menu", () => GameApp.Instance.ShowMainMenu());
        }
    }
}
