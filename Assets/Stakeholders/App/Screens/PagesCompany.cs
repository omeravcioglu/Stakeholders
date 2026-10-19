using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    // ====================================================================== Dashboard
    public class DashboardPage : Page
    {
        public override string Title => "Dashboard";
        Label _kCash, _kCashSub, _kRev, _kRevSub, _kProfit, _kProfitSub, _kVal, _kValSub, _kSat, _kSatSub, _kRep, _kRepSub, _forecast;
        Label _stage, _stageNext, _rank, _ambition;
        VisualElement _stageBar, _ambitionBar;
        LineChart _revChart, _cashChart, _valChart;
        VisualElement _banners;
        HqScene _hq;
        Label _hqSub;
        KeyedList<Player> _partners;
        KeyedList<CompanyProduct> _products;
        KeyedList<GameEvent> _attention;
        KeyedList<NextStep> _steps;

        public DashboardPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            _banners = Ui.Col();
            Body.Add(_banners);
            var kpis = Ui.Row("wrap");
            kpis.Add(Kpi("COMPANY CASH", out _kCash, out _kCashSub));
            kpis.Add(Kpi("REVENUE THIS MONTH", out _kRev, out _kRevSub));
            kpis.Add(Kpi("NET PROFIT THIS MONTH", out _kProfit, out _kProfitSub));
            kpis.Add(Kpi("VALUATION", out _kVal, out _kValSub));
            kpis.Add(Kpi("CUSTOMER SATISFACTION", out _kSat, out _kSatSub));
            kpis.Add(Kpi("BRAND REPUTATION", out _kRep, out _kRepSub));
            Body.Add(kpis);

            var hq = Ui.Card("Your HQ", "hq-card");
            var hqHead = Ui.Row("hq-head");
            _hqSub = Ui.Text("", "small", "dim", "grow");
            hqHead.Add(_hqSub);
            hqHead.Add(Ui.Btn("Warehouses & staff", () => S.Navigate(S.Operations), "btn-ghost", "btn-sm"));
            _hq = new HqScene { OpenOperations = () => S.Navigate(S.Operations) };
            hq.Kids(hqHead, _hq);
            Body.Add(hq);

            var journey = Ui.Card("Company journey");
            _stage = Ui.Text("", "h2");
            _stageBar = Ui.Bar(0, Ui.Gold);
            _stageBar.style.marginTop = 8;
            _stageBar.style.height = 10;
            _stageNext = Ui.Text("", "small", "dim", "mt8", "para");
            _rank = Ui.Text("", "small", "mt8");
            var ambBox = Ui.Div("inset", "card-secret", "mt12");
            ambBox.Add(Ui.Text("YOUR SECRET AMBITION", "field-label", "secret"));
            _ambition = Ui.Text("", "small", "para");
            _ambitionBar = Ui.Bar(0, Ui.Secret);
            _ambitionBar.style.marginTop = 6;
            ambBox.Kids(_ambition, _ambitionBar);
            ambBox.RegisterCallback<ClickEvent>(_ => S.Navigate(S.Career));
            var journeyLinks = Ui.Row("mt12");
            journeyLinks.Add(Ui.Btn("Upgrades", () => S.Navigate(S.Growth), "btn-ghost", "btn-sm"));
            journeyLinks.Add(Ui.Btn("Competition", () => S.Navigate(S.Rivals), "btn-ghost", "btn-sm"));
            journey.Kids(_stage, _stageBar, _stageNext, _rank, ambBox, journeyLinks);

            var next = Ui.Card("Next steps");
            var nlist = Ui.Col();
            next.Add(nlist);
            _steps = new KeyedList<NextStep>(nlist, x => x.Text, CreateStepRow, "Everything is under control. Look for growth: research, upgrades, rivals.");
            Body.Add(Cols(journey, next));
            next.style.flexGrow = 1.6f;

            var c1 = Ui.Card("Daily revenue & profit (7-day average)");
            _revChart = new LineChart();
            c1.Add(_revChart);
            var c2 = Ui.Card("Company cash");
            _cashChart = new LineChart();
            c2.Add(_cashChart);
            _forecast = Ui.Text("", "small", "dim", "mt8");
            c2.Add(_forecast);
            var c3 = Ui.Card("Valuation");
            _valChart = new LineChart();
            c3.Add(_valChart);
            Body.Add(Cols(c1, c2, c3));

            var partners = Ui.Card("Partners");
            var plist = Ui.Col();
            partners.Add(plist);
            _partners = new KeyedList<Player>(plist, p => p.Id.ToString(), CreatePartnerRow);
            var products = Ui.Card("Top products");
            var prodList = Ui.Col();
            products.Add(prodList);
            _products = new KeyedList<CompanyProduct>(prodList, p => p.Id, CreateProductRow, "No products yet. Launch one from your research notebook (Market & Research).");
            var attention = Ui.Card("Needs attention");
            var alist = Ui.Col();
            attention.Add(alist);
            attention.Add(Ui.Btn("Open situations", () => S.Navigate(S.Situations), "btn-ghost", "btn-sm", "mt8"));
            _attention = new KeyedList<GameEvent>(alist, e => e.Id, CreateAttentionRow, "All quiet. For now.");
            Body.Add(Cols(partners, products, attention));
        }

        ListRow<NextStep> CreateStepRow()
        {
            var row = new ListRow<NextStep>();
            var r = Ui.Row("next-step");
            var dot = Ui.Div("dot");
            var text = Ui.Text("", "small", "para", "grow");
            text.style.marginLeft = 8;
            text.style.marginRight = 8;
            var btn = Ui.Btn("", () => row.Item?.Act?.Invoke(), "btn-sm");
            r.Kids(dot, text, btn);
            row.Root = r;
            row.Bind = st =>
            {
                dot.style.backgroundColor = st.Urgent ? Ui.Bad : st.Priority >= 60 ? Ui.Warn : Ui.Accent;
                text.SetText(st.Text);
                btn.text = st.Button;
                btn.EnableInClassList("btn-warn", st.Urgent);
            };
            return row;
        }

        ListRow<Player> CreatePartnerRow()
        {
            var row = new ListRow<Player>();
            var r = Ui.Row("mb8");
            var av = Ui.Avatar(null);
            var col = Ui.Col("grow");
            col.style.marginLeft = 8;
            var name = Ui.Text("", "b");
            var sub = Ui.Text("", "tiny", "dim");
            col.Add(name);
            col.Add(sub);
            var own = Ui.Text("", "b", "right");
            r.Add(av);
            r.Add(col);
            r.Add(own);
            row.Root = r;
            row.Bind = p =>
            {
                Ui.UpdateAvatar(av, p);
                name.SetText(p.Name + (p.Id == Me ? " (you)" : "") + (!p.Connected ? " · offline" : "") + (p.AwayUntilDay > V.Day ? " · away" : ""));
                sub.SetText($"{(p.IsAI ? p.Personality + " AI · " : "")}Lv {p.Level} · perf {Fmt.Int(p.Performance)} · {Fmt.Money(p.Salary)}/mo");
                own.SetText(Fmt.Pct(p.Shares / (double)V.Company.TotalShares, 1));
            };
            return row;
        }

        ListRow<CompanyProduct> CreateProductRow()
        {
            var row = new ListRow<CompanyProduct>();
            var r = Ui.Row("mb8");
            var col = Ui.Col("grow");
            var name = Ui.Text("", "b");
            var sub = Ui.Text("", "tiny", "dim");
            col.Add(name);
            col.Add(sub);
            var profit = Ui.Text("", "b", "right");
            r.Add(col);
            r.Add(profit);
            row.Root = r;
            row.Bind = cp =>
            {
                name.SetText(cp.Name);
                double days = cp.Sales7 > 0.2 ? cp.Stock / cp.Sales7 : 999;
                sub.SetText($"{Fmt.Num(cp.Sales7)}/day · {(days >= 999 ? "no recent sales" : Fmt.Int(days) + " days of stock")} · {(cp.ManagerId >= 0 ? "managed by " + Name(cp.ManagerId) : "nobody manages it yet")}");
                profit.SetText(Fmt.Money(cp.ProfitQuarter));
                profit.style.color = cp.ProfitQuarter >= 0 ? Ui.Good : Ui.Bad;
            };
            return row;
        }

        ListRow<GameEvent> CreateAttentionRow()
        {
            var row = new ListRow<GameEvent>();
            var r = Ui.Row("mb8");
            var dot = Ui.Div("dot");
            var col = Ui.Col("grow");
            col.style.marginLeft = 8;
            var title = Ui.Text("", "b", "ellipsis");
            var sub = Ui.Text("", "tiny", "dim");
            col.Add(title);
            col.Add(sub);
            r.Add(dot);
            r.Add(col);
            row.Root = r;
            row.Bind = e =>
            {
                dot.style.backgroundColor = Ui.CategoryColor(e.Category);
                title.SetText(e.Title);
                sub.SetText($"{e.Category} · {DaysLeft(e.DeadlineDay, V.Day)} left" + (e.ClaimedBy >= 0 ? $" · {Name(e.ClaimedBy)} is on it" : ""));
            };
            return row;
        }

        public override void Refresh()
        {
            var v = V;
            var c = v.Company;
            Subtitle.SetText($"{c.Name} · {Fmt.Date(v.Day)} · {c.Products.Count(p => p.Active)} products · {c.Employees.Count} employees");

            _banners.Clear();
            if (v.Phase == GamePhase.BoardMeeting)
            {
                var b = Ui.Div("banner", "banner-meeting");
                b.Add(Ui.Text("The board meeting is in session. The clock is paused until everyone is done voting.", "grow", "b"));
                b.Add(Ui.Btn("Go to the board", () => S.Navigate(S.Board), "btn-secret"));
                _banners.Add(b);
            }
            if (c.Cash < 0)
            {
                var b = Ui.Div("banner", "card-danger");
                b.Add(Ui.Text($"The company is overdrawn by {Fmt.Money(-c.Cash)}. Overdraft limit {Fmt.Money(Math.Max(20000, Math.Max(c.LastMonth.Revenue, c.Month.Revenue) * 0.5))}.", "grow", "b", "bad"));
                _banners.Add(b);
            }

            _kCash.SetText(Fmt.Money(c.Cash));
            _kCash.style.color = c.Cash < 0 ? Ui.Bad : Ui.TextColor;
            _kCashSub.SetText(c.Debt > 0 ? $"Debt {Fmt.Money(c.Debt)}" : "No debt");
            _kRev.SetText(Fmt.Money(c.Month.Revenue));
            _kRevSub.SetText($"Last month {Fmt.Money(c.LastMonth.Revenue)}");
            _kProfit.SetText(Fmt.Money(c.Month.NetProfit));
            _kProfit.style.color = c.Month.NetProfit >= 0 ? Ui.Good : Ui.Bad;
            _kProfitSub.SetText($"Last month {Fmt.Money(c.LastMonth.NetProfit)}");
            _kVal.SetText(Fmt.Money(c.Valuation));
            double lastVal = v.Board.LastValuation > 0 ? v.Board.LastValuation : v.Settings.StartingCash;
            _kValSub.SetText($"{Fmt.SignedPct(c.Valuation / Math.Max(1, lastVal) - 1)} since last board meeting");
            _kSat.SetText(Fmt.Int(c.Satisfaction) + "%");
            _kSat.style.color = c.Satisfaction >= 70 ? Ui.Good : c.Satisfaction >= 50 ? Ui.TextColor : Ui.Bad;
            _kSatSub.SetText($"{Fmt.Int(c.OrdersToday)} orders today · capacity {Fmt.Int(c.CapacityPerDay)}/day");
            _kRep.SetText(Fmt.Int(c.Reputation) + " / 100");
            _kRepSub.SetText(c.Reputation >= 70 ? "A household name" : c.Reputation >= 45 ? "Growing brand" : "Mostly unknown");
            _hq.SetState(HqScene.From(v));
            _hqSub.SetText(_hq.Summary);

            var q = S.Q;
            var st = Stages.Get(c.Stage);
            _stage.SetText($"{st.Name}  ·  stage {c.Stage + 1} of {Stages.All.Length}");
            if (c.Stage < Stages.Max)
            {
                var nx = Stages.Get(c.Stage + 1);
                Ui.SetBar(_stageBar, (float)((c.Valuation - st.Valuation) / Math.Max(1, nx.Valuation - st.Valuation)), Ui.Gold);
                _stageNext.SetText($"{Fmt.Money(c.Valuation)} of {Fmt.Money(nx.Valuation)} to become {Stages.InSentence(c.Stage + 1)}. Unlocks: {nx.Unlocks}.");
            }
            else
            {
                Ui.SetBar(_stageBar, 1, Ui.Gold);
                _stageNext.SetText(c.WentPublic ? "Public company." : c.IpoPending ? $"IPO on {Fmt.Date(c.IpoDay)}." : "Ready to go public. Propose the IPO on the Competition page.");
            }
            int rank = q.CompanyRank();
            var above = v.Rivals.Where(r => !r.Acquired && r.Valuation > c.Valuation).OrderBy(r => r.Valuation).FirstOrDefault();
            _rank.SetText(rank == 1 ? $"#1 of {q.CompanyCount()} in the industry." : $"#{rank} of {q.CompanyCount()} in the industry · next: {above?.Name} ({Fmt.Money(above?.Valuation ?? 0)})");
            _rank.style.color = rank == 1 ? Ui.Gold : Ui.TextColor;
            var pr = MePriv;
            if (pr != null && !string.IsNullOrEmpty(pr.Ambition))
            {
                var (status, fraction, done) = q.AmbitionProgress(Me);
                _ambition.SetText($"{Ambitions.Get(pr.Ambition)?.Name}: {status}" + (done ? " (on track!)" : ""));
                Ui.SetBar(_ambitionBar, (float)fraction, done ? Ui.Good : Ui.Secret);
                _ambitionBar.Show(true);
            }
            else
            {
                _ambition.SetText("Not chosen yet. Click to pick one.");
                _ambitionBar.Show(false);
            }
            _steps.Sync(Advisor.For(S));

            var h = v.History;
            _revChart.SetData(
                new LineChart.Series { Values = LineChart.Smooth(h.Revenue, 7, 180), Color = Ui.Accent, Name = "Revenue", Fill = true },
                new LineChart.Series { Values = LineChart.Smooth(h.Profit, 7, 180), Color = Ui.Good, Name = "Profit" });
            _cashChart.SetData(new LineChart.Series { Values = LineChart.Tail(h.Cash, 180), Color = Ui.Gold, Fill = true });
            _valChart.SetData(new LineChart.Series { Values = LineChart.Tail(h.Valuation, 360), Color = Ui.Secret, Fill = true });

            var me = MeP;
            if (me != null && (me.HasSkill(Skills.CashFlowExpert) || me.HasSkill(Skills.Accountant)))
            {
                double obligations = c.Employees.Sum(e => e.Salary) + v.Players.Sum(p => p.Salary) + c.Warehouses.Sum(w => w.MonthlyCost) + c.Loans.Sum(l => l.MonthlyPayment);
                double monthlyNet = c.LastMonth.NetProfit;
                string runway = monthlyNet >= 0 ? "cash-flow positive" : $"runway about {Fmt.Fixed(Math.Max(0, c.Cash) / Math.Max(1, -monthlyNet), 1)} months";
                _forecast.SetText($"Forecast (your finance skills): monthly fixed costs {Fmt.Money(obligations)} · {runway}.");
            }
            else _forecast.SetText("Learn Cash Flow Expert or Accountant to see forecasts.");

            _partners.Sync(v.Players);
            _products.Sync(c.Products.Where(p => p.Active).OrderByDescending(p => p.ProfitQuarter).Take(6).ToList());
            _attention.Sync(v.Events.OrderByDescending(e => e.Severity).ThenBy(e => e.DeadlineDay).Take(5).ToList());
        }
    }

    // ====================================================================== Situations
    public class SituationsPage : Page
    {
        public override string Title => "Situations";
        KeyedList<GameEvent> _list;
        KeyedList<GameEvent> _done;
        int _filter;

        public SituationsPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            HeadRight.Add(Ui.Segmented(new[] { "All", "Unclaimed", "Mine" }, 0, i => { _filter = i; Refresh(); }));
            Body.Add(Ui.Text("Problems and opportunities appear constantly. Anyone can claim one and decide. Whoever handles it earns the XP. Unhandled situations resolve themselves, usually badly.", "para", "dim", "small", "mb12"));
            var list = Ui.Col();
            Body.Add(list);
            _list = new KeyedList<GameEvent>(list, e => e.Id, CreateEventCard, "No open situations right now.");
            Body.Add(Ui.Text("RECENTLY RESOLVED", "h3", "mt12", "mb8"));
            var done = Ui.Col();
            Body.Add(done);
            _done = new KeyedList<GameEvent>(done, e => e.Id, CreateDoneRow, "Nothing yet.");
        }

        ListRow<GameEvent> CreateEventCard()
        {
            var row = new ListRow<GameEvent>();
            var card = Ui.Div("event-card");
            var stripe = Ui.Div("event-stripe");
            var main = Ui.Div("event-main");
            var head = Ui.Row();
            var title = Ui.Text("", "event-title", "grow");
            var sev = Ui.Badge("");
            var cat = Ui.Badge("");
            var due = Ui.Badge("");
            head.Add(title);
            head.Add(cat);
            head.Add(sev);
            head.Add(due);
            var text = Ui.Text("", "event-text");
            var claimRow = Ui.Row("mt8");
            var claimed = Ui.Text("", "small", "dim", "grow");
            var claimBtn = Ui.Btn("Claim", () => Send(new Command { Type = CommandType.ClaimEvent, A = row.Item.Id }), "btn-ghost", "btn-sm");
            claimRow.Add(claimed);
            claimRow.Add(claimBtn);
            var choices = Ui.Row("wrap");
            choices.style.alignItems = Align.Stretch;
            main.Add(head);
            main.Add(text);
            main.Add(choices);
            main.Add(claimRow);
            card.Add(stripe);
            card.Add(main);
            row.Root = card;
            var choiceEls = new List<(VisualElement el, Label cost)>();
            string builtFor = null;
            row.Bind = e =>
            {
                stripe.style.backgroundColor = Ui.CategoryColor(e.Category);
                title.SetText(e.Title);
                cat.SetText(e.Category.ToString());
                sev.SetText(e.Severity >= 3 ? "CRITICAL" : e.Severity == 2 ? "Important" : "Minor");
                sev.EnableInClassList("badge-bad", e.Severity >= 3);
                sev.EnableInClassList("badge-warn", e.Severity == 2);
                bool early = e.PublicDay > V.Day;
                due.SetText(early ? "Early signal: only you see this" : DaysLeft(e.DeadlineDay, V.Day) + " left");
                due.EnableInClassList("badge-secret", early);
                due.EnableInClassList("badge-warn", !early && e.DeadlineDay - V.Day <= 1);
                text.SetText(e.Text);
                bool mine = e.ClaimedBy == Me;
                bool othersClaim = e.ClaimedBy >= 0 && e.ClaimedBy != Me && e.ClaimedUntil >= V.Day;
                claimed.SetText(e.PendingApproval ? "A decision is waiting for partner approval." : othersClaim ? $"{Name(e.ClaimedBy)} is handling this." : mine ? "You claimed this." : "Unclaimed.");
                claimBtn.Show(!mine && !othersClaim && !e.PendingApproval);
                if (builtFor != e.Id)
                {
                    builtFor = e.Id;
                    choices.Clear();
                    choiceEls.Clear();
                    for (int i = 0; i < e.Choices.Count; i++)
                    {
                        int idx = i;
                        var ch = e.Choices[i];
                        var box = Ui.Div("choice");
                        box.Add(Ui.Text(ch.Label, "choice-label"));
                        if (!string.IsNullOrEmpty(ch.Detail)) box.Add(Ui.Text(ch.Detail, "choice-detail"));
                        var cost = Ui.Text("", "choice-cost");
                        box.Add(cost);
                        box.RegisterCallback<ClickEvent>(_ => Send(new Command { Type = CommandType.ResolveEvent, A = row.Item.Id, I = idx }));
                        choices.Add(box);
                        choiceEls.Add((box, cost));
                    }
                }
                var me = MeP;
                for (int i = 0; i < e.Choices.Count && i < choiceEls.Count; i++)
                {
                    var ch = e.Choices[i];
                    var parts = new List<string>();
                    if (ch.Cost > 0) parts.Add("Costs " + Fmt.Money(ch.Cost));
                    if (ch.BusyDays > 0) parts.Add($"Takes {Fmt.Days(ch.BusyDays)} of your time");
                    bool locked = false;
                    if (!string.IsNullOrEmpty(ch.RequiresSkill))
                    {
                        string skillName = Skills.Get(ch.RequiresSkill)?.Name + (ch.RequiresSkillAlt != null ? " or " + Skills.Get(ch.RequiresSkillAlt)?.Name : "");
                        parts.Add("Needs " + skillName);
                        locked = me == null || (!me.HasSkill(ch.RequiresSkill) && (ch.RequiresSkillAlt == null || !me.HasSkill(ch.RequiresSkillAlt)));
                    }
                    if (ch.Cost > 0 && me != null && ch.Cost > me.SpendingLimit && V.Players.Count > 1) parts.Add("needs approval");
                    choiceEls[i].cost.SetText(string.Join(" · ", parts));
                    choiceEls[i].el.EnableInClassList("choice-locked", locked || othersClaim || e.PendingApproval);
                }
            };
            return row;
        }

        ListRow<GameEvent> CreateDoneRow()
        {
            var row = new ListRow<GameEvent>();
            var r = Ui.Div("feed-item");
            var t = Ui.Text("", "small", "b");
            var o = Ui.Text("", "small", "dim", "para");
            r.Add(t);
            r.Add(o);
            row.Root = r;
            row.Bind = e =>
            {
                t.SetText($"{Fmt.ShortDate(e.ResolvedDay)} · {e.Title}");
                string who = e.ResolvedBy >= 0 ? Name(e.ResolvedBy) + ": " + (e.ChosenIndex >= 0 && e.ChosenIndex < e.Choices.Count ? e.Choices[e.ChosenIndex].Label : "") : "Nobody handled it.";
                o.SetText(who + " " + e.Outcome);
            };
            return row;
        }

        public override void Refresh()
        {
            var list = V.Events.Where(e =>
                _filter == 0 ||
                (_filter == 1 && (e.ClaimedBy < 0 || e.ClaimedUntil < V.Day)) ||
                (_filter == 2 && e.ClaimedBy == Me)).OrderByDescending(e => e.Severity).ThenBy(e => e.DeadlineDay).ToList();
            Subtitle.SetText($"{V.Events.Count} open · {V.Events.Count(e => e.Severity >= 3)} critical");
            _list.Sync(list);
            var done = new List<GameEvent>();
            for (int i = V.ResolvedEvents.Count - 1; i >= 0 && done.Count < 10; i--) done.Add(V.ResolvedEvents[i]);
            _done.Sync(done);
        }
    }

    // ====================================================================== Products
    public class ProductsPage : Page
    {
        public override string Title => "Products";
        KeyedList<CompanyProduct> _rows;
        KeyedList<Contract> _contracts;
        string _selected;
        VisualElement _detail;
        LineChart _salesChart;
        Label _detailTitle, _detailText;

        public ProductsPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            HeadRight.Add(Ui.Btn("Find products to launch", () => S.Navigate(S.Market), "btn-ghost"));
            var card = Ui.Card();
            card.Add(Header(("Product", 0), ("Manager", 110), ("Price", 96), ("Stock", 60), ("Inbound", 68), ("Days", 48), ("Sold/d", 58), ("Demand", 66), ("Margin", 60), ("Profit Q", 80), ("Rating", 56), ("", 196)));
            var list = Ui.Col();
            card.Add(list);
            _rows = new KeyedList<CompanyProduct>(list, p => p.Id, CreateRow, "No products yet. Research the market, then launch something from your notebook.");
            Body.Add(card);

            var contracts = Ui.Card("B2B contracts");
            contracts.Add(Ui.Text("Big buyers offer bulk contracts in Situations. Deliveries go out automatically as soon as we have enough stock. Miss the deadline and we pay a penalty.", "para", "dim", "small", "mb8"));
            contracts.Add(Header(("Client", 0), ("Product", 200), ("Quantity", 80), ("Unit price", 80), ("Value", 90), ("Due", 90), ("Status", 150), ("Signed by", 110)));
            var klist = Ui.Col();
            contracts.Add(klist);
            _contracts = new KeyedList<Contract>(klist, k => k.Id, CreateContractRow, "No contracts yet.");
            Body.Add(contracts);
            _detail = Ui.Card();
            _detailTitle = Ui.Text("", "h2");
            _detailText = Ui.Text("", "small", "dim", "para", "mt8");
            _salesChart = new LineChart(140) { Format = v => Fmt.Int(v) + "/day" };
            _detail.Add(_detailTitle);
            _detail.Add(_salesChart);
            _detail.Add(_detailText);
            _detail.Show(false);
            Body.Add(_detail);
        }

        ListRow<CompanyProduct> CreateRow()
        {
            var row = new ListRow<CompanyProduct>();
            var tr = Ui.Div("tr");
            var nameBox = CellBox(tr, 0);
            var nameCol = Ui.Col();
            var name = Ui.Text("", "b");
            var catRow = Ui.Row();
            var cat = Ui.Text("", "tiny", "dim");
            var tags = Ui.Text("", "tiny", "b");
            tags.style.marginLeft = 6;
            catRow.Kids(cat, tags);
            nameCol.Add(name);
            nameCol.Add(catRow);
            nameBox.Add(nameCol);
            var mgrBox = CellBox(tr, 110);
            var av = Ui.Avatar(null, "avatar-sm");
            var mgr = Ui.Text("", "small", "ellipsis");
            mgr.style.marginLeft = 6;
            mgr.style.maxWidth = 80;
            var claim = Ui.Btn("Claim", () => Send(new Command { Type = CommandType.ClaimProduct, A = row.Item.Id }), "btn-good", "btn-sm");
            mgrBox.Add(av);
            mgrBox.Add(mgr);
            mgrBox.Add(claim);
            var priceBox = CellBox(tr, 96);
            var priceCol = Ui.Col();
            var price = Ui.Text("", "b");
            var comp = Ui.Text("", "tiny", "dim");
            priceCol.Add(price);
            priceCol.Add(comp);
            priceBox.Add(priceCol);
            var stock = Cell(tr, 60);
            var incoming = Cell(tr, 68, "dim");
            var days = Cell(tr, 48);
            var sales = Cell(tr, 58);
            var demand = Cell(tr, 66, "dim");
            var margin = Cell(tr, 60);
            var profit = Cell(tr, 80, "b");
            var rating = Cell(tr, 56);
            var actions = CellBox(tr, 196);
            actions.Add(Ui.Btn("Order", () => Dialogs.Order(S, row.Item), "btn-sm"));
            actions.Add(Ui.Btn("Price", () => Dialogs.Price(S, row.Item), "btn-ghost", "btn-sm"));
            actions.Add(Ui.Btn("Ads", () => Dialogs.Campaign(S, row.Item), "btn-ghost", "btn-sm"));
            actions.Add(Ui.Btn("More", () => ProductMenu(row.Item), "btn-ghost", "btn-sm"));
            tr.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) { _selected = row.Item.Id; Refresh(); } });
            row.Root = tr;
            row.Bind = cp =>
            {
                var mp = V.GetMarketProduct(cp.ProductId);
                name.SetText(cp.Name);
                cat.SetText(cp.Category);
                var tagList = new List<string>();
                Color tagColor = Ui.Dim;
                if (cp.PromoActive(V.Day)) { tagList.Add((cp.Promo == "flash" ? "FLASH SALE " : "FREE SHIPPING ") + DaysLeft(cp.PromoUntil, V.Day)); tagColor = Ui.Gold; }
                if (cp.UpgradeDoneDay > 0) { tagList.Add($"R&D {(cp.Version == 1 ? "Plus" : "Pro")} in {DaysLeft(cp.UpgradeDoneDay, V.Day)}"); if (tagColor == Ui.Dim) tagColor = Ui.Accent; }
                if (cp.AutoRestock) tagList.Add("auto-restock");
                int owed = V.Company.Contracts.Where(k => k.Status == 0 && k.CompanyProductId == cp.Id).Sum(k => k.Quantity);
                if (owed > 0) { tagList.Add($"{Fmt.Int(owed)} owed on contracts"); if (tagColor == Ui.Dim) tagColor = Ui.Warn; }
                tags.SetText(string.Join(" · ", tagList));
                tags.style.color = tagColor;
                var mgrP = V.GetPlayer(cp.ManagerId);
                Ui.UpdateAvatar(av, mgrP);
                mgr.SetText(mgrP != null ? mgrP.Name : "-");
                av.Show(mgrP != null);
                mgr.Show(mgrP != null);
                claim.Show(mgrP == null);
                price.SetText(Fmt.Price(cp.Price));
                double compPrice = mp != null ? mp.CompetitorPrice * (1 - (V.Day <= mp.CompetitorCutUntil ? mp.CompetitorCut : 0)) : 0;
                comp.SetText(compPrice > 0 ? "rival " + Fmt.Price(compPrice) : "");
                comp.style.color = compPrice > 0 && compPrice < cp.Price * 0.93 ? Ui.Warn : Ui.Dim;
                stock.SetText(Fmt.Int(cp.Stock));
                int inc = V.Company.Shipments.Where(s => s.CompanyProductId == cp.Id).Sum(s => s.Quantity);
                incoming.SetText(inc > 0 ? "+" + Fmt.Int(inc) : "-");
                double d = cp.Sales7 > 0.2 ? cp.Stock / cp.Sales7 : 999;
                days.SetText(d >= 999 ? "-" : Fmt.Int(d));
                days.style.color = d < 7 ? Ui.Bad : d < 15 ? Ui.Warn : Ui.TextColor;
                sales.SetText(Fmt.Num(cp.Sales7));
                demand.SetText(Fmt.Num(cp.Demand7));
                double fee = cp.Price * S.Q.FeeRate() + S.Q.ShippingCost(cp);
                double m = cp.Price > 0 ? (cp.Price - cp.AvgCost - fee) / cp.Price : 0;
                margin.SetText(cp.AvgCost > 0 ? Fmt.Pct(m) : "-");
                margin.style.color = m < 0.15 ? Ui.Bad : Ui.TextColor;
                profit.SetText(Fmt.Money(cp.ProfitQuarter));
                profit.style.color = cp.ProfitQuarter >= 0 ? Ui.Good : Ui.Bad;
                rating.SetText(Fmt.Fixed(cp.Rating, 1));
                rating.style.color = cp.Rating >= 4.2 ? Ui.Good : cp.Rating < 3.3 ? Ui.Bad : Ui.TextColor;
                tr.EnableInClassList("tr-sel", cp.Id == _selected);
            };
            return row;
        }

        ListRow<Contract> CreateContractRow()
        {
            var row = new ListRow<Contract>();
            var tr = Ui.Div("tr");
            var client = Cell(tr, 0, "b");
            var product = Cell(tr, 200);
            var qty = Cell(tr, 80);
            var unit = Cell(tr, 80);
            var value = Cell(tr, 90, "b");
            var due = Cell(tr, 90);
            var status = Cell(tr, 150);
            var by = Cell(tr, 110, "dim");
            row.Root = tr;
            row.Bind = k =>
            {
                var v = V;
                var cp = v.Company.Products.FirstOrDefault(p => p.Id == k.CompanyProductId);
                client.SetText(k.Client);
                product.SetText(cp?.Name ?? k.ProductName);
                qty.SetText(Fmt.Int(k.Quantity));
                unit.SetText(Fmt.Price(k.UnitPrice));
                value.SetText(Fmt.Money(k.Quantity * k.UnitPrice));
                due.SetText(k.Status == 0 ? DaysLeft(k.DueDay, v.Day) : Fmt.ShortDate(k.CompletedDay));
                if (k.Status == 1) { status.SetText("Delivered"); status.style.color = Ui.Good; }
                else if (k.Status == 2) { status.SetText($"Missed: {Fmt.Money(k.Penalty)} penalty"); status.style.color = Ui.Bad; }
                else
                {
                    int have = cp?.Stock ?? 0;
                    int inbound = v.Company.Shipments.Where(s => s.CompanyProductId == k.CompanyProductId).Sum(s => s.Quantity);
                    if (have >= k.Quantity) { status.SetText("Ready to ship"); status.style.color = Ui.Good; }
                    else { status.SetText($"Short {Fmt.Int(k.Quantity - have)}" + (inbound > 0 ? $" ({Fmt.Int(inbound)} inbound)" : "")); status.style.color = k.DueDay - v.Day <= 5 ? Ui.Bad : Ui.Warn; }
                }
                by.SetText(Name(k.AcceptedBy));
            };
            return row;
        }

        public void ProductMenu(CompanyProduct cp)
        {
            var v = V;
            var c = v.Company;
            var q = S.Q;
            var m = Overlay.ShowModal(cp.Name);

            var promo = Ui.Div("inset", "mb12");
            promo.Add(Ui.Text("PROMOTIONS", "field-label"));
            bool running = cp.PromoActive(v.Day);
            bool cooling = !running && v.Day < cp.PromoCooldownUntil;
            promo.Add(Ui.Text(running ? $"{(cp.Promo == "flash" ? "Flash sale" : "Free shipping")} running: {DaysLeft(cp.PromoUntil, v.Day)} left. {Fmt.Int(cp.PromoUnits)} units sold so far."
                : cooling ? $"Customers need a break from deals. Next promotion possible on {Fmt.ShortDate(cp.PromoCooldownUntil)}."
                : cp.Stock < 20 ? "Not enough stock for a promotion." : "Ready.", "small", "dim", "mb8", "para"));
            bool canPromo = !running && !cooling && cp.Stock >= 20;
            var prow = Ui.Row("wrap");
            var flash = Ui.Btn("Flash sale: 20% off, 3 days", () => { Send(new Command { Type = CommandType.StartPromotion, A = cp.Id, I = 0 }); m.Close(); }, "btn-sm");
            var ship = Ui.Btn("Free shipping, 7 days", () => { Send(new Command { Type = CommandType.StartPromotion, A = cp.Id, I = 1 }); m.Close(); }, "btn-ghost", "btn-sm");
            flash.SetEnabled(canPromo);
            ship.SetEnabled(canPromo);
            prow.Kids(flash, ship);
            promo.Add(prow);
            promo.Add(Ui.Text("A flash sale sells about 60% more for three days at a 20% discount. Free shipping sells about 30% more for a week and we pay the delivery. Sales stay a little higher for 10 days after either one.", "tiny", "faint", "para", "mt8"));
            m.Body.Add(promo);

            var rnd = Ui.Div("inset", "mb12");
            rnd.Add(Ui.Text("PRODUCT R&D", "field-label"));
            if (cp.Version >= 3) rnd.Add(Ui.Text("This is already the Pro version: the best it gets.", "small", "dim"));
            else if (cp.UpgradeDoneDay > 0) rnd.Add(Ui.Text($"R&D is working on the {(cp.Version == 1 ? "Plus" : "Pro")} version. It launches in {DaysLeft(cp.UpgradeDoneDay, v.Day)}.", "small", "dim"));
            else
            {
                string next = cp.Version == 1 ? "Plus" : "Pro";
                rnd.Add(Ui.Text($"Develop {cp.BaseName ?? cp.Name} {next}: +15% demand, customers accept about 10% higher prices, better quality and ratings.", "small", "para"));
                bool ok = q.CanUpgradeProduct(cp, out string why);
                double cost = q.ProductUpgradeCost(cp);
                var b = Ui.Btn($"Start R&D · {Fmt.Money(cost)} · {Fmt.Days(q.ProductUpgradeDays(cp))}", () => { Send(new Command { Type = CommandType.UpgradeProduct, A = cp.Id }); m.Close(); }, "btn-sm", "mt8");
                b.SetEnabled(ok && c.Cash >= cost);
                rnd.Add(b);
                if (!ok) rnd.Add(Ui.Text(why, "tiny", "warn", "mt8", "para"));
                else if (c.Cash < cost) rnd.Add(Ui.Text("The company can't afford it right now.", "tiny", "warn", "mt8"));
            }
            m.Body.Add(rnd);

            var auto = Ui.Div("inset", "mb12");
            auto.Add(Ui.Text("AUTO-RESTOCK", "field-label"));
            if (!c.Has("dat2")) auto.Add(Ui.Text("Build Demand Forecasting (Data, tier 2) and products can reorder themselves from their preferred supplier.", "small", "dim", "para"));
            else
            {
                var sup = v.GetSupplier(cp.SupplierId);
                auto.Add(Ui.Text(cp.AutoRestock ? $"On: keeps about {cp.AutoRestockDays} days of stock, ordering from {sup?.Name ?? "the preferred supplier"}." : "Off: someone has to reorder by hand.", "small", "dim", "mb8", "para"));
                int days = cp.AutoRestockDays > 0 ? cp.AutoRestockDays : 30;
                var opts = new[] { 20, 30, 45, 60 };
                int idx = Math.Max(0, Array.IndexOf(opts, days));
                auto.Add(Ui.Field("Days of stock to keep", Ui.Segmented(opts.Select(o => o + " days").ToList(), idx, i => days = opts[i])));
                var arow = Ui.Row();
                arow.Add(Ui.Btn(cp.AutoRestock ? "Update" : "Turn on", () => { Send(new Command { Type = CommandType.SetAutoRestock, A = cp.Id, Flag = true, I = days }); m.Close(); }, "btn-sm"));
                if (cp.AutoRestock) arow.Add(Ui.Btn("Turn off", () => { Send(new Command { Type = CommandType.SetAutoRestock, A = cp.Id, Flag = false, I = days }); m.Close(); }, "btn-ghost", "btn-sm"));
                auto.Add(arow);
            }
            m.Body.Add(auto);

            if (cp.ManagerId < 0)
            {
                var own = Ui.Div("inset", "mb12");
                own.Add(Ui.Text("MANAGEMENT", "field-label"));
                own.Add(Ui.Text("Nobody manages this product. Its manager gets the credit (and XP) for its profits.", "small", "dim", "para"));
                own.Add(Ui.Btn("Take charge", () => { Send(new Command { Type = CommandType.ClaimProduct, A = cp.Id }); m.Close(); }, "btn-good", "btn-sm", "mt8"));
                m.Body.Add(own);
            }

            var danger = Ui.Div("inset", "card-danger");
            danger.Add(Ui.Text("DISCONTINUE", "field-label"));
            danger.Add(Ui.Text($"Sell the remaining {Fmt.Int(cp.Stock)} units ({Fmt.Money(cp.Stock * cp.AvgCost)} at cost) to a liquidator at 35% of cost. Pending shipments are cancelled for a 50% refund.", "small", "para"));
            danger.Add(Ui.Btn("Discontinue & liquidate", () => Overlay.Confirm($"Discontinue {cp.Name}?", "This can't be undone.", "Discontinue",
                () => { Send(new Command { Type = CommandType.Liquidate, A = cp.Id }); m.Close(); }, "btn-bad"), "btn-bad", "btn-sm", "mt8"));
            m.Body.Add(danger);
            m.AddButton("Close", m.Close, "btn-ghost");
        }

        public override void Refresh()
        {
            var active = V.Company.Products.Where(p => p.Active).OrderByDescending(p => p.Revenue).ToList();
            double q = active.Sum(p => p.ProfitQuarter);
            Subtitle.SetText($"{active.Count} active products · quarter profit {Fmt.Money(q)} · storage {Fmt.Int(V.Company.StorageUsed)}/{Fmt.Int(V.Company.StorageCapacity)} units");
            _rows.Sync(active);
            _contracts.Sync(V.Company.Contracts.OrderBy(k => k.Status == 0 ? 0 : 1).ThenByDescending(k => k.Status == 0 ? -k.DueDay : k.CompletedDay).Take(12).ToList());
            var sel = active.FirstOrDefault(p => p.Id == _selected);
            _detail.Show(sel != null);
            if (sel != null)
            {
                var mp = V.GetMarketProduct(sel.ProductId);
                var sup = V.GetSupplier(sel.SupplierId);
                _detailTitle.SetText(sel.Name + " · sales history");
                _salesChart.SetData(new LineChart.Series { Values = new List<float>(sel.SalesHistory), Color = Ui.Good, Fill = true });
                string trend = mp != null && mp.TrendVisible ? $" Trend: {TrendText(mp.Phase)}." : "";
                _detailText.SetText($"Preferred supplier: {sup?.Name ?? "-"} · average unit cost {Fmt.Price(sel.AvgCost)} · stock quality {Fmt.Pct(sel.Quality)} · lifetime revenue {Fmt.Money(sel.Revenue)} · lifetime profit {Fmt.Money(sel.Profit)} · " +
                                    $"launched {Fmt.Date(sel.LaunchedDay)} · discovered by {Name(sel.DiscovererId)}. Missed sales today: {Fmt.Int(sel.LostToday)}.{trend}");
            }
        }
    }

    // ====================================================================== Market & Research
    public class MarketPage : Page
    {
        public override string Title => "Market & Research";
        Select _category;
        int _depth;
        Label _researchCost;
        KeyedList<PlayerTask> _tasks;
        KeyedList<Discovery> _notebook, _board;
        VisualElement _radarCard;
        KeyedList<MarketProduct> _radar;

        public MarketPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var research = Ui.Card("Research the market");
            research.Add(Ui.Text("Research reveals products and an estimate of their demand. Discoveries go into your private notebook: launch them yourself (and get the credit), share them with partners, or keep them to yourself.", "para", "dim", "small", "mb8"));
            var row = Ui.Row("wrap");
            var cats = new List<string> { "Any category" };
            cats.AddRange(Catalog.Categories);
            _category = new Select(cats, 0);
            _category.style.width = 200;
            _category.style.marginRight = 8;
            row.Add(_category);
            row.Add(Ui.Segmented(new[] { "Quick", "Deep" }, 0, i => { _depth = i; Refresh(); }));
            row.Add(Ui.Spacer(8));
            _researchCost = Ui.Text("", "small", "dim");
            _researchCost.style.marginRight = 8;
            row.Add(_researchCost);
            row.Add(Ui.Btn("Start research", StartResearch, "btn-good"));
            research.Add(row);
            var taskList = Ui.Col("mt8");
            research.Add(taskList);
            _tasks = new KeyedList<PlayerTask>(taskList, t => t.Id, CreateTaskRow);
            Body.Add(research);

            var nb = Ui.Card("My research notebook (private)", "card-secret");
            nb.Add(Header(("Product", 0), ("Demand/day", 100), ("Price", 70), ("Unit cost", 74), ("Margin", 64), ("Trend", 90), ("Found", 90), ("", 170)));
            var nbList = Ui.Col();
            nb.Add(nbList);
            _notebook = new KeyedList<Discovery>(nbList, d => "n" + d.ProductId, () => CreateDiscoveryRow(true), "Your notebook is empty. Start some research.");
            Body.Add(nb);

            var board = Ui.Card("Shared by partners");
            board.Add(Header(("Product", 0), ("Demand/day", 100), ("Price", 70), ("Unit cost", 74), ("Margin", 64), ("Trend", 90), ("Found by", 90), ("", 170)));
            var bList = Ui.Col();
            board.Add(bList);
            _board = new KeyedList<Discovery>(bList, d => "b" + d.ProductId, () => CreateDiscoveryRow(false), "Nobody has shared research yet.");
            Body.Add(board);

            _radarCard = Ui.Card("Trend radar (Trend Hunter)");
            _radarCard.Add(Ui.Text("Your Trend Hunter skill shows where every product is in its hype cycle. Emerging and rising products are the ones to catch early.", "para", "dim", "small", "mb8"));
            var radar = Ui.Row("wrap");
            _radarCard.Add(radar);
            _radar = new KeyedList<MarketProduct>(radar, m => m.Id, CreateRadarChip);
            Body.Add(_radarCard);
        }

        void StartResearch()
        {
            string cat = _category.Index <= 0 ? "Any" : Catalog.Categories[_category.Index - 1];
            Send(new Command { Type = CommandType.StartResearch, A = cat, I = _depth });
        }

        ListRow<PlayerTask> CreateTaskRow()
        {
            var row = new ListRow<PlayerTask>();
            var r = Ui.Row("mb4");
            var label = Ui.Text("", "small");
            label.style.width = 260;
            var bar = Ui.Bar(0, Ui.Accent);
            var left = Ui.Text("", "small", "dim");
            left.style.marginLeft = 8;
            r.Add(label);
            r.Add(bar);
            r.Add(left);
            row.Root = r;
            row.Bind = t =>
            {
                label.SetText(t.Label);
                float total = Math.Max(1, t.EndDay - t.StartDay);
                Ui.SetBar(bar, (V.Day - t.StartDay) / total);
                left.SetText(DaysLeft(t.EndDay, V.Day));
            };
            return row;
        }

        ListRow<Discovery> CreateDiscoveryRow(bool mine)
        {
            var row = new ListRow<Discovery>();
            var tr = Ui.Div("tr");
            var nameBox = CellBox(tr, 0);
            var nameCol = Ui.Col();
            var name = Ui.Text("", "b");
            var sub = Ui.Row();
            var cat = Ui.Text("", "tiny", "dim");
            var flags = Ui.Text("", "tiny", "secret");
            flags.style.marginLeft = 6;
            sub.Add(cat);
            sub.Add(flags);
            nameCol.Add(name);
            nameCol.Add(sub);
            nameBox.Add(nameCol);
            var demand = Cell(tr, 100);
            var price = Cell(tr, 70);
            var cost = Cell(tr, 74);
            var margin = Cell(tr, 64);
            var trend = Cell(tr, 90);
            var found = Cell(tr, 90, "dim");
            var actions = CellBox(tr, 170);
            var launch = Ui.Btn("Launch", () => Dialogs.Launch(S, row.Item), "btn-good", "btn-sm");
            actions.Add(launch);
            Button share = null;
            if (mine)
            {
                share = Ui.Btn("Share", () => Send(new Command { Type = CommandType.ShareDiscovery, A = row.Item.ProductId }), "btn-ghost", "btn-sm");
                actions.Add(share);
            }
            row.Root = tr;
            row.Bind = d =>
            {
                name.SetText(d.Name);
                cat.SetText(d.Category);
                var tags = new List<string>();
                if (d.Exclusive) tags.Add("EXCLUSIVE");
                if (d.Shared && mine) tags.Add("shared");
                flags.SetText(string.Join(" · ", tags));
                demand.SetText($"{Fmt.Int(d.DemandLow)} - {Fmt.Int(d.DemandHigh)}");
                price.SetText(Fmt.Price(d.RefPrice));
                cost.SetText(Fmt.Price(d.EstCost));
                double m = d.RefPrice > 0 ? (d.RefPrice * 0.88 - d.EstCost - GameSimulation.ShippingPerUnit) / d.RefPrice : 0;
                margin.SetText(Fmt.Pct(m));
                margin.style.color = m >= 0.45 ? Ui.Good : m < 0.25 ? Ui.Bad : Ui.TextColor;
                trend.SetText(d.PhaseKnown ? TrendText(d.Phase) : "Unknown");
                trend.style.color = d.PhaseKnown ? TrendColor(d.Phase) : Ui.Faint;
                found.SetText(mine ? Fmt.ShortDate(d.Day) : Name(d.FoundBy));
                bool live = V.GetCompanyProductByMarketId(d.ProductId) != null;
                launch.SetEnabled(!live);
                launch.text = live ? "Selling" : "Launch";
                if (share != null) share.Show(!d.Shared && !live);
                if (d.DetailsKnown) cat.SetText($"{d.Category} · price sensitivity {Fmt.Fixed(d.Elasticity, 1)} · competition {Fmt.Pct(d.Competition)} · viral {Fmt.Pct(d.Viral)}");
            };
            return row;
        }

        ListRow<MarketProduct> CreateRadarChip()
        {
            var row = new ListRow<MarketProduct>();
            var chip = Ui.Div("inset");
            chip.style.marginRight = 8;
            chip.style.marginBottom = 8;
            chip.style.width = 190;
            var name = Ui.Text("", "small", "b", "ellipsis");
            var phase = Ui.Text("", "tiny");
            chip.Add(name);
            chip.Add(phase);
            row.Root = chip;
            row.Bind = m =>
            {
                name.SetText(m.Name);
                phase.SetText($"{TrendText(m.Phase)} · demand x{Fmt.Fixed(m.Trend, 1)}");
                phase.style.color = TrendColor(m.Phase);
            };
            return row;
        }

        public override void Refresh()
        {
            var me = MeP;
            var q = S.Q;
            double cost = q.ResearchCost(_depth);
            int extra = V.Company.Has("dat1") ? 1 : 0;
            _researchCost.SetText($"{Fmt.Money(cost)} · {(_depth == 1 ? $"{3 + extra} products, better estimates, ~{q.ResearchDays(1)} days" : $"{2 + extra} products, ~{q.ResearchDays(0)} days")}");
            var myTasks = V.Tasks.Where(t => t.PlayerId == Me).ToList();
            _tasks.Sync(myTasks);
            Subtitle.SetText($"Tasks {myTasks.Count}/{(me != null ? q.MaxTasks(me) : 2)} · {MePriv?.Notebook.Count ?? 0} products in your notebook");
            var nb = MePriv != null ? MePriv.Notebook.OrderByDescending(d => d.Day).ToList() : new List<Discovery>();
            _notebook.Sync(nb);
            _board.Sync(V.Company.ResearchBoard.OrderByDescending(d => d.Day).ToList());
            bool hunter = me != null && me.HasSkill(Skills.TrendHunter);
            _radarCard.Show(hunter);
            if (hunter)
                _radar.Sync(V.Market.Products.Where(m => m.Phase == TrendPhase.Emerging || m.Phase == TrendPhase.Rising || m.Phase == TrendPhase.Peak)
                    .OrderBy(m => m.Phase).ThenByDescending(m => m.Trend).ToList());
        }
    }
}
