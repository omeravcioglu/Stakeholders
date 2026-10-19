using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    // ====================================================================== Partners
    public class PartnersPage : Page
    {
        public override string Title => "Partners";
        PieChart _pie;
        VisualElement _legend;
        KeyedList<Player> _cards;
        KeyedList<ShareOffer> _offers;
        readonly Dictionary<string, Label> _work = new Dictionary<string, Label>();

        public PartnersPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var work = Ui.Card("Who does what");
            work.Add(Ui.Text("AI partners only work in the areas you give them (they still step in when something is badly neglected). Everything else is up to the human partners. Click an area on an AI partner's card below to hand it over or take it back.", "para", "dim", "small", "mb8"));
            var workGrid = Ui.Row("wrap");
            foreach (var role in Roles.All)
            {
                var box = Ui.Div("inset", "work-box");
                box.Add(Ui.Text(Roles.Name(role), "b"));
                var who = Ui.Text("", "small", "mt8");
                box.Add(who);
                box.Add(Ui.Text(Roles.Description(role), "tiny", "faint", "para", "mt8"));
                workGrid.Add(box);
                _work[role] = who;
            }
            work.Add(workGrid);
            Body.Add(work);

            var cap = Ui.Card("Ownership");
            var row = Ui.Row();
            _pie = new PieChart(170);
            row.Add(_pie);
            row.Add(Ui.Spacer(16));
            _legend = Ui.Col("grow");
            row.Add(_legend);
            cap.Add(row);
            cap.Add(Ui.Text("Ownership is power: it decides votes and dividends. Performance is contribution: it earns salary arguments and (if enabled) new shares every quarter. Personal wealth is how you win.", "para", "dim", "small", "mt8"));
            var offers = Ui.Card("Share offers");
            var olist = Ui.Col();
            offers.Add(olist);
            _offers = new KeyedList<ShareOffer>(olist, o => o.Id, CreateOfferRow, "No open share offers. Make one from a partner card below.");
            Body.Add(Cols(cap, offers));

            var grid = Ui.Row("wrap");
            grid.style.alignItems = Align.FlexStart;
            Body.Add(grid);
            _cards = new KeyedList<Player>(grid, p => p.Id.ToString(), CreateCard);
        }

        ListRow<Player> CreateCard()
        {
            var row = new ListRow<Player>();
            var card = Ui.Div("card");
            card.style.width = 330;
            var head = Ui.Row();
            var av = Ui.Avatar(null, "avatar-lg");
            var col = Ui.Col("grow");
            col.style.marginLeft = 10;
            var name = Ui.Text("", "h2");
            var type = Ui.Text("", "small", "dim");
            col.Add(name);
            col.Add(type);
            head.Add(av);
            head.Add(col);
            card.Add(head);
            var stats = Ui.Text("", "small", "para", "mt8");
            card.Add(stats);
            var trust = Ui.Text("", "small", "mt8");
            card.Add(trust);
            var skills = Ui.Row("wrap", "mt8");
            card.Add(skills);
            var roleBox = Ui.Col("mt8");
            roleBox.Add(Ui.Text("RESPONSIBLE FOR", "field-label"));
            var roleRow = Ui.Row("wrap");
            var roleButtons = new Dictionary<string, Button>();
            foreach (var r in Roles.All)
            {
                var role = r;
                var b = new Button(() => ToggleRole(row.Item, role)) { text = Roles.Name(r), tooltip = Roles.Description(r) };
                b.AddToClassList("role-chip");
                roleButtons[r] = b;
                roleRow.Add(b);
            }
            roleBox.Add(roleRow);
            card.Add(roleBox);
            var actions = Ui.Row("wrap", "mt8");
            var buy = Ui.Btn("Offer to buy shares", () => Dialogs.ShareOffer(S, row.Item, true), "btn-sm");
            var sell = Ui.Btn("Offer to sell", () => Dialogs.ShareOffer(S, row.Item, false), "btn-ghost", "btn-sm");
            var dm = Ui.Btn("Message", () => S.OpenDm(row.Item.Id), "btn-ghost", "btn-sm");
            actions.Add(buy);
            actions.Add(sell);
            actions.Add(dm);
            card.Add(actions);
            row.Root = card;
            string skillKey = null;
            row.Bind = p =>
            {
                var v = V;
                Ui.UpdateAvatar(av, p);
                name.SetText(p.Name + (p.Id == Me ? " (you)" : ""));
                name.style.color = Ui.PlayerInk(p.Color);
                string kind = p.IsAI ? $"AI partner · {p.Personality}" : "Human partner";
                if (!p.Connected) kind += " · offline";
                if (p.AwayUntilDay > v.Day) kind += " · " + p.Status;
                type.SetText(kind);
                double own = p.Shares / (double)v.Company.TotalShares;
                stats.SetText($"Ownership {Fmt.Pct(own, 1)} (worth {Fmt.Money(own * v.Company.Valuation)})\nSalary {Fmt.Money(p.Salary)}/mo · spending authority {Fmt.Money(p.SpendingLimit)}\n" +
                              $"Level {p.Level} · performance {Fmt.Int(p.Performance)} · reputation {Fmt.Int(p.Reputation)}\n" +
                              $"Profit generated {Fmt.Money(p.ProfitGenerated)} (this quarter {Fmt.Money(p.QuarterProfit)})\n" +
                              $"Products launched {p.ProductsLaunched} · situations handled {p.EventsResolved} · deals won {p.NegotiationsWon}" +
                              (p.ScandalCount > 0 ? $"\nScandals: {p.ScandalCount}" : ""));
                trust.Show(p.IsAI && p.TrustToViewer >= 0);
                if (p.IsAI && p.TrustToViewer >= 0)
                {
                    trust.SetText($"Trusts you: {Fmt.Int(p.TrustToViewer)}/100");
                    trust.style.color = p.TrustToViewer >= 60 ? Ui.Good : p.TrustToViewer < 35 ? Ui.Bad : Ui.Dim;
                }
                string key = string.Join(",", p.Skills);
                if (key != skillKey)
                {
                    skillKey = key;
                    skills.Clear();
                    foreach (var sk in p.Skills)
                    {
                        var def = Skills.Get(sk);
                        if (def != null) skills.Add(Ui.Text(def.Name, "chip"));
                    }
                    if (p.Skills.Count == 0) skills.Add(Ui.Text("No skills yet", "chip"));
                }
                roleBox.Show(p.IsAI);
                bool canDelegate = MeP != null && !MeP.IsAI;
                foreach (var kv in roleButtons)
                {
                    kv.Value.EnableInClassList("on", p.Roles.Contains(kv.Key));
                    kv.Value.SetEnabled(canDelegate);
                }
                actions.Show(p.Id != Me);
            };
            return row;
        }

        void ToggleRole(Player ai, string role)
        {
            if (ai == null || !ai.IsAI) return;
            var roles = new List<string>(ai.Roles);
            if (!roles.Remove(role)) roles.Add(role);
            Send(new Command { Type = CommandType.SetRoles, I = ai.Id, Text = string.Join(",", roles) });
        }

        ListRow<ShareOffer> CreateOfferRow()
        {
            var row = new ListRow<ShareOffer>();
            var r = Ui.Div("inset", "mb8");
            var text = Ui.Text("", "para");
            var note = Ui.Text("", "small", "dim", "para");
            var btns = Ui.Row("mt8");
            var accept = Ui.Btn("Accept", () => Send(new Command { Type = CommandType.RespondShareOffer, A = row.Item.Id, Flag = true }), "btn-good", "btn-sm");
            var decline = Ui.Btn("Decline", () => Send(new Command { Type = CommandType.RespondShareOffer, A = row.Item.Id, Flag = false }), "btn-ghost", "btn-sm");
            var cancel = Ui.Btn("Withdraw", () => Send(new Command { Type = CommandType.CancelShareOffer, A = row.Item.Id }), "btn-ghost", "btn-sm");
            btns.Add(accept);
            btns.Add(decline);
            btns.Add(cancel);
            r.Add(text);
            r.Add(note);
            r.Add(btns);
            row.Root = r;
            row.Bind = o =>
            {
                var v = V;
                string pct = Fmt.Shares(o.Shares, v.Company.TotalShares);
                double fair = v.Company.Valuation * o.Shares / v.Company.TotalShares;
                string fairTxt = $" (fair value about {Fmt.Money(fair)})";
                bool incoming = o.ToId == Me;
                if (incoming)
                    text.SetText(o.FromBuys ? $"{Name(o.FromId)} offers {Fmt.Money(o.Price)} for {pct} of YOUR shares{fairTxt}." : $"{Name(o.FromId)} offers to SELL you {pct} for {Fmt.Money(o.Price)}{fairTxt}.");
                else
                    text.SetText(o.FromBuys ? $"You offered {Name(o.ToId)} {Fmt.Money(o.Price)} for {pct} of their shares{fairTxt}." : $"You offered to sell {Name(o.ToId)} {pct} for {Fmt.Money(o.Price)}{fairTxt}.");
                note.SetText((string.IsNullOrEmpty(o.Note) ? "" : "\"" + o.Note + "\" · ") + "expires " + DaysLeft(o.ExpiresDay, v.Day));
                accept.Show(incoming);
                decline.Show(incoming);
                cancel.Show(!incoming);
            };
            return row;
        }

        public override void Refresh()
        {
            var v = V;
            var c = v.Company;
            Subtitle.SetText($"{v.Players.Count} partners · {Fmt.Int(c.TotalShares)} shares");
            var slices = new List<(float, Color)>();
            _legend.Clear();
            foreach (var p in v.Players.OrderByDescending(p => p.Shares))
            {
                slices.Add((p.Shares, Ui.PlayerColor(p.Color)));
                var r = Ui.Row("mb4");
                r.Add(Ui.Dot(Ui.PlayerColor(p.Color)));
                r.Add(Ui.Text($"  {p.Name}", "grow"));
                double own = p.Shares / (double)c.TotalShares;
                r.Add(Ui.Text($"{Fmt.Pct(own, 1)} · {Fmt.Money(own * c.Valuation)}", "b"));
                _legend.Add(r);
            }
            if (c.InvestorShares > 0)
            {
                slices.Add((c.InvestorShares, Ui.Faint));
                var r = Ui.Row("mb4");
                r.Add(Ui.Dot(Ui.Faint));
                r.Add(Ui.Text("  Outside investors", "grow", "dim"));
                r.Add(Ui.Text(Fmt.Pct(c.InvestorShares / (double)c.TotalShares, 1), "b", "dim"));
                _legend.Add(r);
            }
            _pie.SetSlices(slices);
            foreach (var role in Roles.All)
            {
                var ais = v.Players.Where(p => p.IsAI && p.Roles.Contains(role)).Select(p => p.Name).ToList();
                var label = _work[role];
                label.SetText(ais.Count > 0 ? string.Join(", ", ais) + " (AI)" : v.Players.Count(p => !p.IsAI) > 1 ? "The human partners" : "You");
                label.style.color = ais.Count > 0 ? Ui.Accent : Ui.Gold;
            }
            _offers.Sync(v.ShareOffers.Where(o => o.Status == OfferStatus.Pending).ToList());
            _cards.Sync(v.Players.OrderBy(p => p.Id == Me ? 0 : 1).ThenBy(p => p.Id).ToList());
        }
    }

    // ====================================================================== Board
    public class BoardPage : Page
    {
        public override string Title => "Board";
        VisualElement _banner;
        Label _bannerText, _bannerSub;
        Button _readyBtn;
        Label _report;
        VisualElement _reportPlayers;
        KeyedList<Proposal> _agenda;
        KeyedList<ApprovalRequest> _approvals;
        KeyedList<Proposal> _history;

        public BoardPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            HeadRight.Add(Ui.Btn("New proposal", () => Dialogs.Proposal(S), "btn-good"));
            _banner = Ui.Div("banner");
            var bcol = Ui.Col("grow");
            _bannerText = Ui.Text("", "b", "h2");
            _bannerSub = Ui.Text("", "small", "dim", "para");
            bcol.Add(_bannerText);
            bcol.Add(_bannerSub);
            _banner.Add(bcol);
            _readyBtn = Ui.Btn("I'm done: adjourn", () => Send(new Command { Type = CommandType.SetReady, Flag = !(MeP?.Ready ?? false) }), "btn-secret");
            _banner.Add(_readyBtn);
            Body.Add(_banner);

            var agenda = Ui.Card("Agenda");
            agenda.Add(Ui.Text("Proposals are voted on at board meetings (every quarter). Votes are weighted by ownership; more than 50% yes passes. You can vote early. Promises made in chat are not enforced by anyone.", "para", "dim", "small", "mb8"));
            var alist = Ui.Col();
            agenda.Add(alist);
            _agenda = new KeyedList<Proposal>(alist, p => p.Id, CreateProposalCard, "The agenda is empty. Propose something: raises, dividends, loans, warehouses, policies...");
            Body.Add(agenda);

            var approvals = Ui.Card("Spending approvals");
            approvals.Add(Ui.Text("When a partner wants to spend more than their spending authority, the others approve or reject it right away.", "para", "dim", "small", "mb8"));
            var aplist = Ui.Col();
            approvals.Add(aplist);
            _approvals = new KeyedList<ApprovalRequest>(aplist, a => a.Id, CreateApprovalRow, "No pending requests.");

            var report = Ui.Card("Last quarter");
            _report = Ui.Text("", "para");
            report.Add(_report);
            _reportPlayers = Ui.Col("mt8");
            report.Add(_reportPlayers);
            Body.Add(Cols(approvals, report));

            var hist = Ui.Card("Decisions");
            var hlist = Ui.Col();
            hist.Add(hlist);
            _history = new KeyedList<Proposal>(hlist, p => p.Id, CreateHistoryRow, "No decisions yet.");
            Body.Add(hist);
        }

        ListRow<Proposal> CreateProposalCard()
        {
            var row = new ListRow<Proposal>();
            var card = Ui.Div("inset", "mb8");
            var head = Ui.Row();
            var type = Ui.Badge("", "accent");
            var by = Ui.Text("", "small", "dim", "grow");
            head.Add(type);
            head.Add(by);
            var text = Ui.Text("", "b", "para", "mt8");
            var context = Ui.Text("", "small", "dim", "para");
            var tally = Ui.Row("mt8");
            var yesBar = Ui.Bar(0, Ui.Good);
            yesBar.style.marginRight = 8;
            var tallyText = Ui.Text("", "small");
            tallyText.style.width = 190;
            tally.Add(yesBar);
            tally.Add(tallyText);
            var votes = Ui.Row("wrap", "mt8");
            var btns = Ui.Row("mt8");
            var yes = Ui.Btn("Yes", () => Vote(row.Item, VoteChoice.Yes), "btn-good", "btn-sm");
            var no = Ui.Btn("No", () => Vote(row.Item, VoteChoice.No), "btn-bad", "btn-sm");
            var abstain = Ui.Btn("Abstain", () => Vote(row.Item, VoteChoice.Abstain), "btn-ghost", "btn-sm");
            var withdraw = Ui.Btn("Withdraw", () => Send(new Command { Type = CommandType.WithdrawProposal, A = row.Item.Id }), "btn-ghost", "btn-sm");
            var myVote = Ui.Text("", "small", "dim");
            myVote.style.marginLeft = 8;
            btns.Add(yes); btns.Add(no); btns.Add(abstain); btns.Add(withdraw); btns.Add(myVote);
            card.Add(head); card.Add(text); card.Add(context); card.Add(tally); card.Add(votes); card.Add(btns);
            row.Root = card;
            row.Bind = p =>
            {
                var v = V;
                type.SetText(GameSimulation.ProposalTitle(p.Type));
                by.SetText($"  proposed by {Name(p.ProposerId)} on {Fmt.ShortDate(p.CreatedDay)}");
                text.SetText(p.Text);
                context.SetText(ContextText(p));
                // Tally with current ownership
                double eligible = 0, yesW = 0, noW = 0;
                foreach (var pl in v.Players)
                    if (pl.Shares > 0 && !(p.Type == ProposalType.Discipline && p.TargetId == pl.Id)) eligible += pl.Shares;
                foreach (var vote in p.Votes)
                {
                    var pl = v.GetPlayer(vote.PlayerId);
                    if (pl == null || eligible <= 0) continue;
                    double w = pl.Shares / eligible;
                    if (vote.Choice == VoteChoice.Yes) yesW += w + (pl.Id == p.ProposerId && pl.HasSkill(Skills.BoardInfluence) ? 0.1 : 0);
                    else if (vote.Choice == VoteChoice.No) noW += w;
                }
                double need = GameSimulation.RequiredMajority(p);
                Ui.SetBar(yesBar, (float)Math.Min(1, yesW));
                tallyText.SetText($"Yes {Fmt.Pct(yesW)} · No {Fmt.Pct(noW)} · needs >{Fmt.Pct(need)}");
                votes.Clear();
                foreach (var pl in v.Players)
                {
                    if (pl.Shares <= 0) continue;
                    var vr = p.Votes.FirstOrDefault(x => x.PlayerId == pl.Id);
                    string label = vr == null ? "not voted" : vr.Choice == VoteChoice.None ? "voted (secret)" : vr.Choice.ToString();
                    var chip = Ui.Text($"{pl.Name}: {label}", "chip");
                    if (vr != null && vr.Choice == VoteChoice.Yes) chip.style.color = Ui.Good;
                    if (vr != null && vr.Choice == VoteChoice.No) chip.style.color = Ui.Bad;
                    votes.Add(chip);
                }
                var mine = p.Votes.FirstOrDefault(x => x.PlayerId == Me);
                bool canVote = !(p.Type == ProposalType.Discipline && p.TargetId == Me) && (MeP?.Shares ?? 0) > 0;
                yes.Show(canVote); no.Show(canVote); abstain.Show(canVote);
                withdraw.Show(p.ProposerId == Me);
                myVote.SetText(!canVote ? "You can't vote on this." : mine == null ? (v.Phase == GamePhase.BoardMeeting ? "Vote now." : "Voting closes at the next board meeting.") : "Your vote: " + mine.Choice);
            };
            return row;
        }

        string ContextText(Proposal p)
        {
            var v = V;
            var target = v.GetPlayer(p.TargetId);
            switch (p.Type)
            {
                case ProposalType.SalaryChange:
                    if (target == null) return "";
                    var q = v.Board.LastReport?.Players.FirstOrDefault(x => x.PlayerId == target.Id);
                    return $"Current salary {Fmt.Money(target.Salary)} · requested {Fmt.Money(p.Amount)} · profit generated last quarter {Fmt.Money(q?.ProfitGenerated ?? target.QuarterProfit)} · performance {Fmt.Int(target.Performance)} · extra cost {Fmt.Money((p.Amount - target.Salary) * 12)}/year";
                case ProposalType.Dividend:
                    var me = MeP;
                    double myShare = me != null ? p.Amount * me.Shares / v.Company.TotalShares : 0;
                    return $"Company cash {Fmt.Money(v.Company.Cash)} · your share {Fmt.Money(myShare)}";
                case ProposalType.Bonus:
                    return target != null ? $"{target.Name} generated {Fmt.Money(target.QuarterProfit)} this quarter · performance {Fmt.Int(target.Performance)}" : "";
                case ProposalType.RaiseCapital:
                    return $"Current valuation {Fmt.Money(v.Company.Valuation)} · dilutes everyone who isn't buying";
                case ProposalType.SellCompany:
                    var me2 = MeP;
                    return $"Valuation {Fmt.Money(v.Company.Valuation)} · your payout {Fmt.Money(me2 != null ? p.Amount * me2.Shares / v.Company.TotalShares : 0)} · ends the game";
                case ProposalType.Discipline:
                    return "The accused partner cannot vote on this.";
            }
            return p.Amount > 0 ? $"Cost {Fmt.Money(p.Amount)} · company cash {Fmt.Money(v.Company.Cash)}" : "";
        }

        void Vote(Proposal p, VoteChoice c) => Send(new Command { Type = CommandType.CastVote, A = p.Id, I = (int)c });

        ListRow<ApprovalRequest> CreateApprovalRow()
        {
            var row = new ListRow<ApprovalRequest>();
            var r = Ui.Div("inset", "mb8");
            var text = Ui.Text("", "b", "para");
            var sub = Ui.Text("", "small", "dim", "para");
            var btns = Ui.Row("mt8");
            var ok = Ui.Btn("Approve", () => Send(new Command { Type = CommandType.VoteApproval, A = row.Item.Id, I = (int)VoteChoice.Yes }), "btn-good", "btn-sm");
            var no = Ui.Btn("Reject", () => Send(new Command { Type = CommandType.VoteApproval, A = row.Item.Id, I = (int)VoteChoice.No }), "btn-bad", "btn-sm");
            btns.Add(ok); btns.Add(no);
            r.Add(text); r.Add(sub); r.Add(btns);
            row.Root = r;
            row.Bind = a =>
            {
                text.SetText($"{Name(a.RequesterId)}: {a.Summary}");
                var votes = string.Join(", ", a.Votes.Select(x => $"{Name(x.PlayerId)} {x.Choice}"));
                sub.SetText($"{Fmt.Money(a.Amount)} · expires {DaysLeft(a.ExpiresDay, V.Day)} · {votes}");
                bool mine = a.RequesterId == Me || a.Votes.Any(x => x.PlayerId == Me);
                btns.Show(!mine && a.Status == ProposalStatus.Pending);
            };
            return row;
        }

        ListRow<Proposal> CreateHistoryRow()
        {
            var row = new ListRow<Proposal>();
            var r = Ui.Div("feed-item");
            var t = Ui.Text("", "small", "b", "para");
            var s = Ui.Text("", "tiny", "dim", "para");
            r.Add(t); r.Add(s);
            row.Root = r;
            row.Bind = p =>
            {
                t.SetText(p.Text);
                t.style.color = p.Status == ProposalStatus.Passed ? Ui.Good : p.Status == ProposalStatus.Failed ? Ui.Bad : Ui.Dim;
                s.SetText($"{Fmt.ShortDate(p.ResolvedDay)} · {Name(p.ProposerId)} · {p.Result}");
            };
            return row;
        }

        public override void Refresh()
        {
            var v = V;
            var b = v.Board;
            bool meeting = v.Phase == GamePhase.BoardMeeting;
            _banner.EnableInClassList("banner-meeting", meeting);
            if (meeting)
            {
                _bannerText.SetText(b.Extraordinary ? "EMERGENCY BOARD MEETING" : $"BOARD MEETING #{b.MeetingNumber} IN SESSION");
                int waiting = v.Players.Count(p => !p.IsAI && p.Connected && !p.Ready);
                string timer = b.MeetingTimeLeft > 0 ? $" · {Fmt.Int(b.MeetingTimeLeft)}s left" : "";
                _bannerSub.SetText($"The clock is paused. Vote, argue, make deals. When every human partner clicks adjourn, unvoted proposals count as abstentions.{timer} · waiting for {waiting}");
                bool ready = MeP?.Ready ?? false;
                _readyBtn.text = ready ? "Waiting for others (undo)" : "I'm done: adjourn";
                _readyBtn.Show(true);
            }
            else
            {
                int days = b.NextMeetingDay - v.Day;
                _bannerText.SetText($"Next board meeting in {Fmt.Days(Math.Max(0, days))} ({Fmt.ShortDate(b.NextMeetingDay)})");
                int pendingCount = b.Agenda.Count(p => p.Status == ProposalStatus.Pending);
                _bannerSub.SetText($"{(pendingCount == 0 ? "No" : pendingCount.ToString())} proposal{(pendingCount == 1 ? "" : "s")} on the agenda. Quarterly results, raises and dividends are decided there.");
                _readyBtn.Show(false);
            }
            Subtitle.SetText((b.MeetingNumber == 0 ? "No meetings yet" : $"{b.MeetingNumber} meeting{(b.MeetingNumber == 1 ? "" : "s")} held") + $" · you hold {Fmt.Pct((MeP?.Shares ?? 0) / (double)v.Company.TotalShares, 1)} of the votes");
            _agenda.Sync(b.Agenda.Where(p => p.Status == ProposalStatus.Pending).ToList());
            _approvals.Sync(v.Approvals.Where(a => a.Status == ProposalStatus.Pending).ToList());
            var r = b.LastReport;
            if (r == null)
            {
                _report.SetText("The first quarterly report arrives at the first board meeting.");
                _reportPlayers.Clear();
            }
            else
            {
                _report.SetText($"Q{r.Quarter} Y{r.Year}: revenue {Fmt.Money(r.Revenue)}, net profit {Fmt.Money(r.Profit)}, valuation {Fmt.Money(r.ValuationStart)} -> {Fmt.Money(r.ValuationEnd)}.");
                _reportPlayers.Clear();
                foreach (var pq in r.Players.OrderByDescending(x => x.ProfitGenerated))
                {
                    var row = Ui.Row("mb4");
                    var pl = v.GetPlayer(pq.PlayerId);
                    row.Add(Ui.Dot(pl != null ? Ui.PlayerColor(pl.Color) : Ui.Faint));
                    row.Add(Ui.Text("  " + Name(pq.PlayerId), "grow", "small", "b"));
                    string grant = pq.SharesGranted > 0 ? $" · +{Fmt.Shares(pq.SharesGranted, v.Company.TotalShares)} equity" : "";
                    row.Add(Ui.Text($"profit {Fmt.Money(pq.ProfitGenerated)} · {pq.Xp} XP · {pq.Events} situations{grant}", "small", "dim"));
                    _reportPlayers.Add(row);
                }
            }
            var hist = new List<Proposal>();
            for (int i = b.History.Count - 1; i >= 0 && hist.Count < 15; i--) hist.Add(b.History[i]);
            _history.Sync(hist);
        }
    }

    // ====================================================================== Career
    public class CareerPage : Page
    {
        public override string Title => "Career";
        Label _level, _xp, _stats, _worth, _ambName, _ambText, _ambStatus;
        VisualElement _xpBar, _ambBar;
        Button _ambChoose;
        readonly Dictionary<string, (VisualElement box, Label state, Button btn)> _skills = new Dictionary<string, (VisualElement, Label, Button)>();
        KeyedList<Catalog.AssetDef> _assets;
        KeyedList<PlayerTask> _tasks;

        public CareerPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var profile = Ui.Card("Your career");
            _level = Ui.Text("", "h1", "para");
            _xp = Ui.Text("", "small", "dim");
            _xpBar = Ui.Bar(0, Ui.Gold);
            _xpBar.style.marginTop = 6;
            _stats = Ui.Text("", "para", "mt8");
            profile.Add(_level); profile.Add(_xp); profile.Add(_xpBar); profile.Add(_stats);
            var wealth = Ui.Card("Personal wealth");
            _worth = Ui.Text("", "para");
            wealth.Add(_worth);
            var tasks = Ui.Card("Your tasks");
            var tl = Ui.Col();
            tasks.Add(tl);
            _tasks = new KeyedList<PlayerTask>(tl, t => t.Id, CreateTaskRow, "You're free. Research, recruit or investigate.");
            var ambition = Ui.Card("Secret ambition", "card-secret");
            _ambName = Ui.Text("", "h2", "secret");
            _ambText = Ui.Text("", "para", "mt8");
            _ambBar = Ui.Bar(0, Ui.Secret);
            _ambBar.style.marginTop = 10;
            _ambStatus = Ui.Text("", "small", "dim", "mt8", "para");
            _ambChoose = Ui.Btn("Choose your ambition", () => AmbitionPicker.Show(S), "btn-secret", "mt8");
            ambition.Kids(_ambName, _ambText, _ambBar, _ambStatus, _ambChoose);
            Body.Add(Cols(profile, ambition, wealth, tasks));

            var skillsCard = Ui.Card("Skills");
            skillsCard.Add(Ui.Text("Every level gives a skill point. Second-row skills need level 4 and another skill in the same tree. Intrigue skills are hidden from your partners.", "para", "dim", "small", "mb8"));
            var trees = Ui.Row("row-top");
            foreach (var tree in Skills.Trees)
            {
                var col = Ui.Div("skill-col");
                col.Add(Ui.Text(tree.ToUpperInvariant(), "h3", "mb8"));
                foreach (var def in Skills.All.Where(s => s.Tree == tree))
                {
                    var box = Ui.Div("skill", "col");
                    box.Add(Ui.Text(def.Name, "skill-name"));
                    box.Add(Ui.Text(def.Description, "skill-desc"));
                    var state = Ui.Text("", "tiny", "dim", "mt8");
                    box.Add(state);
                    var d = def;
                    var btn = Ui.Btn("Learn", () => Send(new Command { Type = CommandType.UnlockSkill, A = d.Id }), "btn-sm", "mt8");
                    box.Add(btn);
                    col.Add(box);
                    _skills[def.Id] = (box, state, btn);
                }
                trees.Add(col);
            }
            skillsCard.Add(trees);
            Body.Add(skillsCard);

            var inv = Ui.Card("Personal investments");
            inv.Add(Ui.Text("Your own money, your own risk. Investments grow (or shrink) daily. The Investor skill unlocks better options.", "para", "dim", "small", "mb8"));
            inv.Add(Header(("Asset", 0), ("Value", 110), ("Invested", 110), ("Return", 80), ("", 230)));
            var al = Ui.Col();
            inv.Add(al);
            _assets = new KeyedList<Catalog.AssetDef>(al, a => a.Id, CreateAssetRow);
            Body.Add(inv);
        }

        ListRow<PlayerTask> CreateTaskRow()
        {
            var row = new ListRow<PlayerTask>();
            var r = Ui.Row("mb4");
            var l = Ui.Text("", "small", "grow");
            var d = Ui.Text("", "small", "dim");
            r.Add(l); r.Add(d);
            row.Root = r;
            row.Bind = t => { l.SetText(t.Label); d.SetText(DaysLeft(t.EndDay, V.Day)); };
            return row;
        }

        ListRow<Catalog.AssetDef> CreateAssetRow()
        {
            var row = new ListRow<Catalog.AssetDef>();
            var tr = Ui.Div("tr");
            var nameBox = CellBox(tr, 0);
            var col = Ui.Col();
            var name = Ui.Text("", "b");
            var desc = Ui.Text("", "tiny", "dim");
            col.Add(name); col.Add(desc);
            nameBox.Add(col);
            var value = Cell(tr, 110, "b");
            var invested = Cell(tr, 110, "dim");
            var ret = Cell(tr, 80);
            var act = CellBox(tr, 230);
            var amount = Ui.Input("5000", null, "input-num");
            amount.style.width = 90;
            amount.style.marginRight = 6;
            act.Add(amount);
            act.Add(Ui.Btn("Buy", () => Send(new Command { Type = CommandType.PersonalInvest, A = row.Item.Id, X = Ui.ParseNumber(amount.value) }), "btn-sm"));
            var sell = Ui.Btn("Sell all", () => Send(new Command { Type = CommandType.PersonalSell, A = row.Item.Id, X = 0 }), "btn-ghost", "btn-sm");
            act.Add(sell);
            row.Root = tr;
            row.Bind = a =>
            {
                var h = MePriv?.Holdings.FirstOrDefault(x => x.AssetId == a.Id);
                name.SetText(a.Name);
                desc.SetText(a.Description);
                value.SetText(h != null ? Fmt.Money(h.Value) : "-");
                invested.SetText(h != null ? Fmt.Money(h.Invested) : "-");
                double r = h != null && h.Invested > 0 ? h.Value / h.Invested - 1 : 0;
                ret.SetText(h != null ? Fmt.SignedPct(r, 1) : "-");
                ret.style.color = r >= 0 ? Ui.Good : Ui.Bad;
                sell.Show(h != null);
            };
            return row;
        }

        public override void Refresh()
        {
            var me = MeP;
            var pr = MePriv;
            var v = V;
            if (me == null || pr == null) return;
            int cur = GameSimulation.TotalXpForLevel(me.Level), next = GameSimulation.TotalXpForLevel(me.Level + 1);
            _level.SetText($"Business level {me.Level}" + (me.SkillPoints > 0 ? $"  ·  {me.SkillPoints} skill point{(me.SkillPoints == 1 ? "" : "s")} to spend" : ""));
            _level.style.color = me.SkillPoints > 0 ? Ui.Gold : Ui.TextColor;
            _xp.SetText($"{Fmt.Int(me.Xp - cur)} / {Fmt.Int(next - cur)} XP to level {me.Level + 1} · total XP {Fmt.Int(me.Xp)}");
            Ui.SetBar(_xpBar, (me.Xp - cur) / (float)Math.Max(1, next - cur));
            _stats.SetText($"Performance: {Fmt.Int(me.Performance)} points\nSalary: {Fmt.Money(me.Salary)}/month · spending authority {Fmt.Money(me.SpendingLimit)}\n" +
                           $"Reputation: {Fmt.Int(me.Reputation)} · profit generated {Fmt.Money(me.ProfitGenerated)}\n" +
                           $"Products launched {me.ProductsLaunched} · situations handled {me.EventsResolved} · negotiations won {me.NegotiationsWon} · proposals passed {me.ProposalsPassed}");
            double holdings = pr.Holdings.Sum(h => h.Value);
            double own = me.Shares / (double)v.Company.TotalShares;
            double equity = own * v.Company.Valuation;
            _worth.SetText($"Net worth: {Fmt.MoneyFull(pr.Cash + holdings + equity)}\n\nPersonal cash: {Fmt.MoneyFull(pr.Cash)}\nInvestments: {Fmt.MoneyFull(holdings)}\nCompany stake ({Fmt.Pct(own, 1)}): {Fmt.MoneyFull(equity)}\n\n" +
                           $"Earned so far: salary {Fmt.Money(pr.SalaryReceived)} · dividends {Fmt.Money(pr.DividendsReceived)} · bonuses {Fmt.Money(pr.BonusesReceived)} · share sales {Fmt.Money(pr.ShareTradeIncome)}" +
                           (pr.SecretIncome > 0 ? $"\nOff the books: {Fmt.Money(pr.SecretIncome)}" : ""));
            _tasks.Sync(v.Tasks.Where(t => t.PlayerId == Me).ToList());
            bool chosen = !string.IsNullOrEmpty(pr.Ambition);
            _ambChoose.Show(!chosen && pr.AmbitionChoices.Count > 0);
            _ambBar.Show(chosen);
            if (chosen)
            {
                var (status, fraction, done) = S.Q.AmbitionProgress(Me);
                _ambName.SetText(Ambitions.Get(pr.Ambition)?.Name ?? pr.Ambition);
                _ambText.SetText(S.Q.AmbitionText(pr.Ambition));
                Ui.SetBar(_ambBar, (float)fraction, done ? Ui.Good : Ui.Secret);
                _ambStatus.SetText($"{status}. {(done ? "Currently met: keep it that way until the end." : "Not there yet.")} Completing it adds a bonus of at least $100K (5% of the final valuation) to your net worth. Nobody else knows what it is.");
            }
            else
            {
                _ambName.SetText("Not chosen yet");
                _ambText.SetText("Pick a secret personal goal from three options. It's revealed to everyone at the end.");
                _ambStatus.SetText("");
            }
            foreach (var def in Skills.All)
            {
                var (box, state, btn) = _skills[def.Id];
                bool learned = me.HasSkill(def.Id);
                string reason = null;
                bool can = !learned && CanUnlockLocal(me, def, out reason);
                box.EnableInClassList("skill-learned", learned);
                box.EnableInClassList("skill-available", can);
                box.EnableInClassList("skill-locked", !learned && !can && me.SkillPoints > 0);
                state.SetText(learned ? "Learned" : can ? (def.Hidden ? "Available · hidden from partners" : "Available") : reason);
                btn.Show(!learned);
                btn.SetEnabled(can);
            }
            _assets.Sync(Catalog.PersonalAssets.Where(a => !a.InvestorOnly || me.HasSkill(Skills.Investor) || pr.Holdings.Any(h => h.AssetId == a.Id)).ToList());
        }

        static bool CanUnlockLocal(Player p, SkillDef def, out string reason)
        {
            reason = null;
            if (p.SkillPoints <= 0) { reason = "Needs a skill point"; return false; }
            if (def.Tier >= 2)
            {
                if (p.Level < Skills.Tier2Level) { reason = $"Needs level {Skills.Tier2Level}"; return false; }
                if (!p.Skills.Any(s => Skills.Get(s)?.Tree == def.Tree)) { reason = $"Needs another {def.Tree} skill"; return false; }
            }
            return true;
        }
    }

    /// <summary>Choosing a secret ambition from the three the game offered this player.</summary>
    public static class AmbitionPicker
    {
        public static void Show(GameScreen s)
        {
            var pr = s.MePriv;
            if (pr == null) return;
            if (!string.IsNullOrEmpty(pr.Ambition)) { s.Navigate(s.Career); return; }
            var m = s.UiRoot.ShowModal("Choose your secret ambition", true);
            m.Body.Add(Ui.Text("Pick one personal goal. Nobody else learns it until the final reckoning. If you complete it by the end of the game, a bonus of at least $100K (5% of the final company valuation) is added to your net worth.", "para", "dim", "mb12"));
            var row = Ui.Div("row-top", "wrap");
            foreach (var id in pr.AmbitionChoices)
            {
                var def = Ambitions.Get(id);
                if (def == null) continue;
                var box = Ui.Div("inset", "ambition-box", "col");
                box.Add(Ui.Text(def.Name, "h2", "secret"));
                var text = Ui.Text(s.Q.AmbitionText(id), "para", "mt8");
                text.style.flexGrow = 1;
                box.Add(text);
                var choice = id;
                box.Add(Ui.Btn("Choose", () => { s.Send(new Command { Type = CommandType.ChooseAmbition, A = choice }); m.Close(); }, "btn-secret", "mt12"));
                row.Add(box);
            }
            m.Body.Add(row);
            m.Body.Add(Ui.Text("If you don't choose by day 20, fate picks one for you.", "small", "faint", "mt12"));
            m.AddButton("Decide later", m.Close, "btn-ghost");
        }
    }

    // ====================================================================== Private
    public class PrivatePage : Page
    {
        public override string Title => "Private";
        KeyedList<PrivateOffer> _offers, _deals, _sent;
        KeyedList<Evidence> _evidence;
        Label _investigate;

        public PrivatePage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var intro = Ui.Card(null, "card-secret");
            intro.Add(Ui.Text("Only you can see this page.", "b", "secret"));
            intro.Add(Ui.Text("Suppliers, rivals and even your partners will make you private offers. Taking them makes you richer and may cost the company. Every questionable deal leaves traces that partners might find. Nobody is told to betray anyone. You just get reasons to.", "para", "dim", "small", "mt8"));
            Body.Add(intro);

            var offers = Ui.Card("Private offers", "card-secret");
            var ol = Ui.Col();
            offers.Add(ol);
            _offers = new KeyedList<PrivateOffer>(ol, o => o.Id, CreateOfferCard, "No offers right now. They'll come.");
            Body.Add(offers);

            var ev = Ui.Card("Evidence you've found", "card-secret");
            ev.Add(Ui.Text("Expose it to the whole company, demand payment for your silence, or keep it in your pocket.", "para", "dim", "small", "mb8"));
            var el = Ui.Col();
            ev.Add(el);
            _evidence = new KeyedList<Evidence>(el, e => e.Id, CreateEvidenceCard, "You haven't found anything suspicious. Yet.");
            var inv = Ui.Row("mt8");
            _investigate = Ui.Text("", "small", "dim", "grow", "para");
            inv.Add(_investigate);
            inv.Add(Ui.Btn("Quietly review the books", () => Send(new Command { Type = CommandType.StartInvestigation }), "btn-secret", "btn-sm"));
            ev.Add(inv);
            Body.Add(ev);

            var deals = Ui.Card("Your deals", "card-secret");
            var dl = Ui.Col();
            deals.Add(dl);
            _deals = new KeyedList<PrivateOffer>(dl, o => o.Id, CreateDealRow, "No deals. Clean hands, for now.");
            var sent = Ui.Card("Messages you've sent", "card-secret");
            var sl = Ui.Col();
            sent.Add(sl);
            _sent = new KeyedList<PrivateOffer>(sl, o => o.Id, CreateDealRow, "Nothing sent.");
            Body.Add(Cols(deals, sent));
        }

        ListRow<PrivateOffer> CreateOfferCard()
        {
            var row = new ListRow<PrivateOffer>();
            var card = Ui.Div("inset", "mb8");
            var head = Ui.Row();
            var title = Ui.Text("", "b", "grow");
            var amount = Ui.Badge("", "gold");
            var exp = Ui.Badge("", "secret");
            head.Add(title); head.Add(amount); head.Add(exp);
            var text = Ui.Text("", "para", "mt8");
            var cost = Ui.Text("", "small", "dim", "mt8", "para");
            var btns = Ui.Row("mt8");
            var accept = Ui.Btn("Accept", () => Send(new Command { Type = CommandType.RespondOffer, A = row.Item.Id, I = 0 }), "btn-secret", "btn-sm");
            var reject = Ui.Btn("Reject", () => Send(new Command { Type = CommandType.RespondOffer, A = row.Item.Id, I = 1 }), "btn-ghost", "btn-sm");
            var report = Ui.Btn("Report it", () => Send(new Command { Type = CommandType.RespondOffer, A = row.Item.Id, I = 2 }), "btn-ghost", "btn-sm");
            btns.Add(accept); btns.Add(reject); btns.Add(report);
            card.Add(head); card.Add(text); card.Add(cost); card.Add(btns);
            row.Root = card;
            row.Bind = o =>
            {
                title.SetText(o.Title);
                bool pay = o.Kind == OfferKind.Blackmail || o.Kind == OfferKind.InsiderTip;
                amount.SetText(o.PersonalAmount > 0 ? (pay ? "Costs you " : "You get ") + Fmt.Money(o.PersonalAmount) : "No money");
                exp.SetText("expires " + DaysLeft(o.ExpiresDay, V.Day));
                text.SetText(o.Text);
                cost.SetText(o.CompanyCost > 0 ? $"Likely cost to the company: about {Fmt.Money(o.CompanyCost)}." : "");
                cost.Show(o.CompanyCost > 0);
                accept.text = o.Kind == OfferKind.Blackmail ? "Pay them" : "Accept";
                reject.text = o.Kind == OfferKind.Blackmail ? "Refuse" : "Reject";
                report.Show(o.Reportable);
            };
            return row;
        }

        ListRow<Evidence> CreateEvidenceCard()
        {
            var row = new ListRow<Evidence>();
            var card = Ui.Div("inset", "mb8");
            var text = Ui.Text("", "para");
            var sub = Ui.Text("", "small", "dim", "mt8");
            var btns = Ui.Row("mt8");
            var expose = Ui.Btn("Expose publicly", () => Overlay.Confirm("Expose this?", "Everyone will see it. The partner loses reputation and performance, the company takes a small reputation hit, and a discipline vote goes on the board agenda.", "Expose", () => Send(new Command { Type = CommandType.ExposeEvidence, A = row.Item.Id }), "btn-bad"), "btn-bad", "btn-sm");
            var demand = Ui.Btn("Demand payment", () => Dialogs.Blackmail(S, row.Item), "btn-secret", "btn-sm");
            var quiet = Ui.Btn("Keep quiet", () => Send(new Command { Type = CommandType.IgnoreEvidence, A = row.Item.Id }), "btn-ghost", "btn-sm");
            btns.Add(expose); btns.Add(demand); btns.Add(quiet);
            card.Add(text); card.Add(sub); card.Add(btns);
            row.Root = card;
            row.Bind = e =>
            {
                text.SetText(e.Summary);
                sub.SetText(e.Exposed ? $"Exposed by {Name(e.ExposedBy)} on {Fmt.ShortDate(e.ExposedDay)}" : $"Happened around {Fmt.ShortDate(e.Day)}" + (e.Silenced.Contains(Me) ? " · you decided to keep quiet" : ""));
                btns.Show(!e.Exposed);
                quiet.Show(!e.Silenced.Contains(Me));
            };
            return row;
        }

        ListRow<PrivateOffer> CreateDealRow()
        {
            var row = new ListRow<PrivateOffer>();
            var r = Ui.Div("feed-item");
            var t = Ui.Text("", "small", "b", "para");
            var s = Ui.Text("", "tiny", "dim", "para");
            r.Add(t); r.Add(s);
            row.Root = r;
            row.Bind = o =>
            {
                t.SetText(o.TargetId == Me ? o.Title : $"To {Name(o.TargetId)}: {o.Title}");
                string status = o.Status.ToString();
                if (o.Kind == OfferKind.Kickback && o.Status == OfferStatus.Accepted) status = $"Waiting for your order of {Fmt.Int(o.Quantity)}+ units from {o.FromName} by {Fmt.ShortDate(o.FulfillBy)}";
                s.SetText($"{Fmt.ShortDate(o.CreatedDay)} · {status}" + (o.PersonalAmount > 0 ? " · " + Fmt.Money(o.PersonalAmount) : ""));
            };
            return row;
        }

        public override void Refresh()
        {
            var v = V;
            var pending = v.Offers.Where(o => o.TargetId == Me && o.Status == OfferStatus.Pending).OrderBy(o => o.ExpiresDay).ToList();
            _offers.Sync(pending);
            _evidence.Sync(v.Evidence.Where(e => e.DiscoveredBy.Contains(Me) || e.Exposed).OrderByDescending(e => e.Day).Take(30).ToList());
            var deals = v.Offers.Where(o => o.TargetId == Me && o.Status != OfferStatus.Pending && o.Status != OfferStatus.Expired).OrderByDescending(o => o.CreatedDay).Take(20).ToList();
            _deals.Sync(deals);
            _sent.Sync(v.Offers.Where(o => o.FromId == Me).OrderByDescending(o => o.CreatedDay).Take(15).ToList());
            var me = MeP;
            bool investigating = v.Tasks.Any(t => t.PlayerId == Me && t.Kind == TaskKind.Investigate);
            _investigate.SetText(investigating ? "You're going through the books..." : "Spend 3 days reviewing transactions for questionable deals." + (me != null && me.HasSkill(Skills.Investigator) ? " Your Investigator skill doubles the odds." : ""));
            Subtitle.SetText($"{pending.Count} pending offers · secret income so far {Fmt.Money(MePriv?.SecretIncome ?? 0)}");
        }
    }
}
