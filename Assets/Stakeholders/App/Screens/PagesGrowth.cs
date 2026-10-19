using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    // ====================================================================== Growth & upgrades
    public class GrowthPage : Page
    {
        public override string Title => "Growth";
        Label _stageName, _stageNext, _stageUnlocks, _rank, _strategyNow;
        VisualElement _stageBar;
        readonly List<Label> _stageChips = new List<Label>();
        readonly Dictionary<string, (VisualElement box, Button btn, Label note)> _strategies = new Dictionary<string, (VisualElement, Button, Label)>();
        readonly Dictionary<string, (VisualElement card, Label state, Button btn)> _ups = new Dictionary<string, (VisualElement, Label, Button)>();
        KeyedList<UpgradeBuild> _builds;
        KeyedList<CompanyProduct> _rnd;
        VisualElement _investorCard;

        public GrowthPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var journey = Ui.Card("Company journey");
            _stageName = Ui.Text("", "h1");
            _stageBar = Ui.Bar(0, Ui.Gold);
            _stageBar.style.marginTop = 10;
            _stageBar.style.height = 12;
            _stageNext = Ui.Text("", "small", "dim", "mt8");
            _stageUnlocks = Ui.Text("", "small", "para", "mt8");
            var track = Ui.Row("wrap", "mt12");
            foreach (var st in Stages.All)
            {
                var chip = Ui.Text(st.Name, "stage-chip");
                _stageChips.Add(chip);
                track.Add(chip);
            }
            _rank = Ui.Text("", "small", "mt8");
            journey.Kids(_stageName, _stageBar, _stageNext, _stageUnlocks, track, _rank);

            var strategy = Ui.Card("Company strategy");
            _strategyNow = Ui.Text("", "para", "mb8");
            strategy.Add(_strategyNow);
            var opts = Ui.Div("row-top", "wrap");
            foreach (var pos in Positionings.All)
            {
                var box = Ui.Div("inset", "strategy-box", "col");
                box.Add(Ui.Text(pos.Name, "b"));
                var desc = Ui.Text(pos.Description, "tiny", "dim", "para");
                desc.style.marginTop = 4;
                desc.style.flexGrow = 1;
                box.Add(desc);
                var note = Ui.Text("", "tiny", "faint", "mt8");
                var p = pos;
                var btn = Ui.Btn("Propose to the board", () => ProposeStrategy(p), "btn-sm", "mt8");
                box.Add(note);
                box.Add(btn);
                opts.Add(box);
                _strategies[pos.Id] = (box, btn, note);
            }
            strategy.Add(opts);
            strategy.Add(Ui.Text("A strategy is a company-wide bet that changes how customers see you. It needs a board vote. Changing an existing strategy costs $50K in rebranding.", "tiny", "faint", "para", "mt8"));
            Body.Add(Cols(journey, strategy));

            var progress = Ui.Card("In progress");
            var blist = Ui.Col();
            progress.Add(blist);
            _builds = new KeyedList<UpgradeBuild>(blist, b => b.Id, CreateBuildRow, "Nothing under construction. Pick an upgrade below.");
            var rlist = Ui.Col();
            progress.Add(rlist);
            _rnd = new KeyedList<CompanyProduct>(rlist, p => p.Id, CreateRndRow);
            Body.Add(progress);

            var tree = Ui.Card("Upgrade tree");
            tree.Add(Ui.Text("Upgrades are permanent and take a few days to build. Each tier needs the tier before it and a company stage. Anything above your spending authority goes to your partners for approval.", "para", "dim", "small", "mb8"));
            var cols = Ui.Div("row-top");
            foreach (var branch in Catalog.UpgradeBranches)
            {
                var col = Ui.Div("up-col");
                col.Add(Ui.Text(branch.ToUpperInvariant(), "h3", "mb8"));
                foreach (var u in Catalog.Upgrades.Where(x => x.Branch == branch).OrderBy(x => x.Tier))
                    col.Add(CreateUpgradeCard(u));
                cols.Add(col);
            }
            tree.Add(cols);
            Body.Add(tree);

            _investorCard = Ui.Card("Investor deals");
            _investorCard.Add(Ui.Text("Only a partner with the Investor skill can arrange these.", "para", "dim", "small", "mb8"));
            var inv = Ui.Div("row-top", "wrap");
            foreach (var u in Catalog.Upgrades.Where(x => x.InvestorOnly))
            {
                var card = CreateUpgradeCard(u);
                card.style.width = 260;
                card.style.marginRight = 10;
                inv.Add(card);
            }
            _investorCard.Add(inv);
            Body.Add(_investorCard);
        }

        VisualElement CreateUpgradeCard(Catalog.UpgradeDef u)
        {
            var card = Ui.Div("up-card", "col");
            card.Add(Ui.Text(u.Name, "skill-name"));
            var meta = Ui.Text((u.Tier > 0 ? $"Tier {u.Tier} · " : "") + $"{Fmt.Money(u.Cost)} · {Fmt.Days(u.BuildDays)}", "tiny", "gold");
            meta.style.marginTop = 2;
            card.Add(meta);
            card.Add(Ui.Text(u.Description, "skill-desc"));
            var state = Ui.Text("", "tiny", "dim", "mt8", "para");
            card.Add(state);
            var id = u.Id;
            var btn = Ui.Btn("Build", () => Send(new Command { Type = CommandType.BuyUpgrade, A = id }), "btn-sm", "mt8");
            card.Add(btn);
            _ups[u.Id] = (card, state, btn);
            return card;
        }

        ListRow<UpgradeBuild> CreateBuildRow()
        {
            var row = new ListRow<UpgradeBuild>();
            var r = Ui.Row("mb8");
            var name = Ui.Text("", "small", "b");
            name.style.width = 260;
            var bar = Ui.Bar(0, Ui.Gold);
            var left = Ui.Text("", "small", "dim");
            left.style.marginLeft = 10;
            left.style.width = 190;
            r.Kids(name, bar, left);
            row.Root = r;
            row.Bind = b =>
            {
                name.SetText("Building: " + (Catalog.GetUpgrade(b.Id)?.Name ?? b.Id));
                Ui.SetBar(bar, (V.Day - b.StartDay) / (float)Math.Max(1, b.DoneDay - b.StartDay), Ui.Gold);
                left.SetText($"{DaysLeft(b.DoneDay, V.Day)} left · {Name(b.OrderedBy)}");
            };
            return row;
        }

        ListRow<CompanyProduct> CreateRndRow()
        {
            var row = new ListRow<CompanyProduct>();
            var r = Ui.Row("mb8");
            var name = Ui.Text("", "small", "b");
            name.style.width = 260;
            var bar = Ui.Bar(0, Ui.Accent);
            var left = Ui.Text("", "small", "dim");
            left.style.marginLeft = 10;
            left.style.width = 190;
            r.Kids(name, bar, left);
            row.Root = r;
            row.Bind = cp =>
            {
                int days = S.Q.ProductUpgradeDays(cp);
                name.SetText($"R&D: {cp.BaseName ?? cp.Name} {(cp.Version == 1 ? "Plus" : "Pro")}");
                Ui.SetBar(bar, 1f - (cp.UpgradeDoneDay - V.Day) / (float)Math.Max(1, days));
                left.SetText($"{DaysLeft(cp.UpgradeDoneDay, V.Day)} left · {Name(cp.UpgradedBy)}");
            };
            return row;
        }

        void ProposeStrategy(PositioningDef pos)
        {
            var c = V.Company;
            string cost = string.IsNullOrEmpty(c.Positioning) ? "" : " Rebranding costs $50K if it passes.";
            Overlay.Confirm($"Propose: {pos.Name}", $"{pos.Description}\n\nThe partners vote on it at the next board meeting.{cost}", "Put it to a vote",
                () => Send(new Command { Type = CommandType.CreateProposal, I = (int)ProposalType.Positioning, A = pos.Id, J = -1 }));
        }

        public override void Refresh()
        {
            var v = V;
            var c = v.Company;
            var q = S.Q;
            var me = MeP;
            var st = Stages.Get(c.Stage);
            Subtitle.SetText($"{st.Name} · {c.Upgrades.Count} upgrades owned · {c.Building.Count} under construction");
            _stageName.SetText($"Stage {c.Stage + 1} of {Stages.All.Length}: {st.Name}");
            if (c.Stage < Stages.Max)
            {
                var next = Stages.Get(c.Stage + 1);
                Ui.SetBar(_stageBar, (float)((c.Valuation - st.Valuation) / Math.Max(1, next.Valuation - st.Valuation)), Ui.Gold);
                _stageNext.SetText($"Valuation {Fmt.Money(c.Valuation)} of {Fmt.Money(next.Valuation)} needed for {next.Name}.");
                _stageUnlocks.SetText($"Next unlock: {next.Unlocks}.");
            }
            else
            {
                Ui.SetBar(_stageBar, 1, Ui.Gold);
                _stageNext.SetText(c.WentPublic ? "The company is public." : c.IpoPending ? $"The IPO happens on {Fmt.Date(c.IpoDay)}." : "Every stage reached. The company can go public: propose the IPO on the Competition page.");
                _stageUnlocks.SetText("");
            }
            for (int i = 0; i < _stageChips.Count; i++)
            {
                _stageChips[i].EnableInClassList("stage-done", i < c.Stage);
                _stageChips[i].EnableInClassList("stage-current", i == c.Stage);
            }
            int rank = q.CompanyRank(), count = q.CompanyCount();
            var above = v.Rivals.Where(r => !r.Acquired && r.Valuation > c.Valuation).OrderBy(r => r.Valuation).FirstOrDefault();
            _rank.SetText(rank == 1 ? $"#1 of {count} on the industry leaderboard." : $"#{rank} of {count} on the industry leaderboard. Next to beat: {above?.Name} at {Fmt.Money(above?.Valuation ?? 0)}.");
            _rank.style.color = rank == 1 ? Ui.Gold : Ui.TextColor;

            var cur = Positionings.Get(c.Positioning);
            _strategyNow.SetText(cur != null ? $"Current strategy: {cur.Name}, adopted {Fmt.ShortDate(c.PositioningDay)}."
                : c.Stage >= 1 ? "No strategy yet. Pick one and put it to a board vote." : $"A company strategy unlocks at the {Stages.Get(1).Name} stage ({Fmt.Money(Stages.Get(1).Valuation)} valuation).");
            bool pending = v.Board.Agenda.Exists(x => x.Type == ProposalType.Positioning && x.Status == ProposalStatus.Pending);
            foreach (var pos in Positionings.All)
            {
                var (box, btn, note) = _strategies[pos.Id];
                bool active = c.Positioning == pos.Id;
                box.EnableInClassList("strategy-on", active);
                btn.Show(!active);
                btn.SetEnabled(c.Stage >= 1 && !pending);
                note.SetText(active ? "ACTIVE" : pending ? "A strategy vote is already on the agenda." : c.Stage < 1 ? "Locked" : string.IsNullOrEmpty(c.Positioning) ? "Free to adopt" : "Switching costs $50K");
                note.style.color = active ? Ui.Good : Ui.Faint;
            }

            _builds.Sync(c.Building);
            _rnd.Sync(c.Products.Where(p => p.Active && p.UpgradeDoneDay > 0).ToList());

            foreach (var u in Catalog.Upgrades)
            {
                if (!_ups.TryGetValue(u.Id, out var e)) continue;
                bool owned = c.Has(u.Id);
                var build = c.Building.FirstOrDefault(b => b.Id == u.Id);
                string why = null;
                bool unlocked = !owned && build == null && q.UpgradeUnlocked(u, me, out why);
                bool afford = c.Cash >= u.Cost;
                e.card.EnableInClassList("up-owned", owned);
                e.card.EnableInClassList("up-building", build != null);
                e.card.EnableInClassList("up-available", unlocked && afford);
                e.card.EnableInClassList("up-locked", !owned && build == null && !unlocked);
                string needsVote = me != null && u.Cost > me.SpendingLimit && v.Players.Count > 1 ? " · needs partner approval" : "";
                e.state.SetText(owned ? "Owned" : build != null ? $"Building: {DaysLeft(build.DoneDay, v.Day)} left" : !unlocked ? why : !afford ? $"Needs {Fmt.Money(u.Cost)} in the bank" : "Available" + needsVote);
                e.state.style.color = owned ? Ui.Good : build != null ? Ui.Gold : unlocked && afford ? Ui.Accent : Ui.Dim;
                e.btn.Show(!owned && build == null);
                e.btn.SetEnabled(unlocked && afford);
                e.btn.text = "Build";
            }
            _investorCard.Show((me != null && me.HasSkill(Skills.Investor)) || Catalog.Upgrades.Any(u => u.InvestorOnly && c.Has(u.Id)));
        }
    }

    // ====================================================================== Competition
    public class RivalsPage : Page
    {
        public override string Title => "Competition";
        KeyedList<Rival> _board;
        KeyedList<string> _shares;
        KeyedList<Rival> _moves;
        Label _ipoText;
        Button _ipoBtn;
        VisualElement _ipoCard;
        const string UsId = "__us";

        public RivalsPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var board = Ui.Card("Industry leaderboard");
            board.Add(Header(("#", 34), ("Company", 0), ("Style", 110), ("Strong in", 250), ("Valuation", 100), ("Month", 70), ("Strength", 110), ("", 190)));
            var blist = Ui.Col();
            board.Add(blist);
            _board = new KeyedList<Rival>(blist, r => r.Id, CreateBoardRow);
            board.Add(Ui.Text("Rivals grow every month, faster where we are weak. They start price wars, copy our best sellers and poach our staff. From the Scale-up stage we can buy any rival worth less than two thirds of us: their customers become ours.", "para", "dim", "small", "mt8"));
            Body.Add(board);

            var share = Ui.Card("Market share by category");
            share.Add(Header(("Category", 150), ("Our share", 0), ("", 60), ("Our products", 100), ("Rivals here", 260)));
            var slist = Ui.Col();
            share.Add(slist);
            _shares = new KeyedList<string>(slist, x => x, CreateShareRow);
            share.Add(Ui.Text("40%+ share makes us the go-to brand (+10% demand). Above 55% the category saturates and extra growth gets harder. Our products in the same category also compete with each other, so spreading across categories pays.", "para", "dim", "small", "mt8"));

            var right = Ui.Col();
            _ipoCard = Ui.Card("The IPO");
            _ipoText = Ui.Text("", "para");
            _ipoBtn = Ui.Btn("Propose the IPO", ProposeIpo, "btn-good", "mt8");
            _ipoCard.Kids(_ipoText, _ipoBtn);
            right.Add(_ipoCard);
            var moves = Ui.Card("Latest rival moves");
            var mlist = Ui.Col();
            moves.Add(mlist);
            _moves = new KeyedList<Rival>(mlist, r => r.Id + r.LastMoveDay, CreateMoveRow, "The competition is quiet. For now.");
            right.Add(moves);
            Body.Add(Cols(share, right));
        }

        ListRow<Rival> CreateBoardRow()
        {
            var row = new ListRow<Rival>();
            var tr = Ui.Div("tr");
            var rank = Cell(tr, 34, "b");
            var nameBox = CellBox(tr, 0);
            var nameCol = Ui.Col();
            var name = Ui.Text("", "b");
            var last = Ui.Text("", "tiny", "dim", "ellipsis");
            nameCol.Kids(name, last);
            nameBox.Add(nameCol);
            var style = Cell(tr, 110, "dim");
            var focus = Cell(tr, 250, "small");
            var val = Cell(tr, 100, "b");
            var month = Cell(tr, 70);
            var strBox = CellBox(tr, 110);
            var strength = Ui.Bar(0, Ui.Warn);
            strBox.Add(strength);
            var act = CellBox(tr, 190);
            var buy = Ui.Btn("Acquire", () => Acquire(row.Item), "btn-sm", "btn-warn");
            var why = Ui.Text("", "tiny", "faint", "para");
            act.Kids(buy, why);
            row.Root = tr;
            row.Bind = r =>
            {
                var v = V;
                bool us = r.Id == UsId;
                tr.EnableInClassList("tr-us", us);
                name.SetText(r.Name + (us ? " (us)" : r.Acquired ? " (acquired)" : ""));
                name.style.color = us ? Ui.Gold : Ui.TextColor;
                last.SetText(us ? "" : r.LastMoveDay > 0 ? $"{Fmt.ShortDate(r.LastMoveDay)}: {r.LastMove}" : "");
                last.Show(!us && r.LastMoveDay > 0);
                style.SetText(r.Style);
                focus.SetText(string.Join(", ", r.Focus));
                val.SetText(Fmt.Money(r.Valuation));
                double change = r.LastMonthValuation > 0 ? r.Valuation / r.LastMonthValuation - 1 : 0;
                month.SetText(Fmt.SignedPct(change));
                month.style.color = change >= 0 ? Ui.Good : Ui.Bad;
                strength.Show(!us);
                Ui.SetBar(strength, (float)(r.Strength / 2.0), r.Strength > 1.3 ? Ui.Bad : r.Strength < 0.7 ? Ui.Good : Ui.Warn);
                string reason = null;
                bool can = !us && S.Q.CanAcquire(r, out reason);
                double price = us ? 0 : S.Q.AcquisitionPrice(r);
                bool pending = v.Board.Agenda.Exists(x => x.Type == ProposalType.AcquireRival && x.OptionId == r.Id && x.Status == ProposalStatus.Pending);
                buy.Show(can && !pending);
                buy.text = $"Acquire · {Fmt.Money(price)}";
                buy.SetEnabled(v.Company.Cash >= price);
                why.Show(!us && (!can || pending));
                why.SetText(pending ? "On the board agenda" : r.Acquired ? "" : reason);
                rank.SetText(_ranks.TryGetValue(r.Id, out string n) ? n : "-");
            };
            return row;
        }

        readonly Dictionary<string, string> _ranks = new Dictionary<string, string>();

        ListRow<string> CreateShareRow()
        {
            var row = new ListRow<string>();
            var tr = Ui.Div("tr");
            var cat = Cell(tr, 150, "b");
            var barBox = CellBox(tr, 0);
            var bar = Ui.Bar(0, Ui.Accent);
            barBox.Add(bar);
            var pct = Cell(tr, 60, "b");
            var ours = Cell(tr, 100, "dim");
            var rivals = Cell(tr, 260, "small");
            row.Root = tr;
            row.Bind = category =>
            {
                var c = V.Company;
                double share = c.CategoryShare.TryGetValue(category, out double sv) ? sv : 0;
                bool acquired = c.AcquiredCategories.Contains(category);
                cat.SetText(category + (acquired ? " +" : ""));
                cat.style.color = acquired ? Ui.Gold : Ui.TextColor;
                Ui.SetBar(bar, (float)share, share > 0.55 ? Ui.Warn : share >= 0.4 ? Ui.Good : Ui.Accent);
                pct.SetText(Fmt.Pct(share));
                int n = c.Products.Count(p => p.Active && p.Category == category);
                ours.SetText(n == 0 ? "-" : n == 1 ? "1 product" : n + " products");
                var here = V.Rivals.Where(r => !r.Acquired && r.Focus.Contains(category)).Select(r => r.Name).ToList();
                rivals.SetText(here.Count == 0 ? "Nobody: wide open" : string.Join(", ", here));
                rivals.style.color = here.Count == 0 ? Ui.Good : here.Count >= 2 ? Ui.Warn : Ui.Dim;
            };
            return row;
        }

        ListRow<Rival> CreateMoveRow()
        {
            var row = new ListRow<Rival>();
            var r = Ui.Div("feed-item");
            var t = Ui.Text("", "small", "para");
            r.Add(t);
            row.Root = r;
            row.Bind = x => t.SetText($"{Fmt.ShortDate(x.LastMoveDay)} · {x.Name} {x.LastMove}.");
            return row;
        }

        void Acquire(Rival r)
        {
            if (r == null) return;
            double price = S.Q.AcquisitionPrice(r);
            Overlay.Confirm($"Acquire {r.Name}?", $"Buy {r.Name} for {Fmt.Money(price)} of company cash. Their customers in {string.Join(", ", r.Focus)} become ours: +15% demand there for the rest of the game. The board votes on it at the next meeting.",
                "Put it to a vote", () => Send(new Command { Type = CommandType.CreateProposal, I = (int)ProposalType.AcquireRival, A = r.Id, J = -1 }), "btn-warn");
        }

        void ProposeIpo()
        {
            Overlay.Confirm("Take the company public?", "The IPO values the company at a 50% premium and ends the game 30 days after the vote passes: every partner cashes in their shares. It needs two thirds of the votes.",
                "Put it to a vote", () => Send(new Command { Type = CommandType.CreateProposal, I = (int)ProposalType.GoPublic, J = -1 }), "btn-good");
        }

        public override void Refresh()
        {
            var v = V;
            var c = v.Company;
            var q = S.Q;
            int rank = q.CompanyRank();
            Subtitle.SetText($"We are #{rank} of {q.CompanyCount()} · valuation {Fmt.Money(c.Valuation)}");
            var hv = v.History.Valuation;
            double monthAgo = hv.Count > 30 ? hv[hv.Count - 31] : v.Settings.StartingCash;
            var topCats = c.CategoryShare.OrderByDescending(kv => kv.Value).Where(kv => kv.Value > 0).Take(3).Select(kv => kv.Key).ToList();
            var us = new Rival
            {
                Id = UsId, Name = c.Name, Style = string.IsNullOrEmpty(c.Positioning) ? "Us" : Positionings.Get(c.Positioning)?.Name ?? "Us",
                Valuation = c.Valuation, LastMonthValuation = monthAgo, Strength = 1
            };
            us.Focus.AddRange(topCats.Count > 0 ? topCats : new List<string> { "-" });
            var list = v.Rivals.Where(r => !r.Acquired).ToList();
            list.Add(us);
            list = list.OrderByDescending(r => r.Valuation).ToList();
            _ranks.Clear();
            for (int i = 0; i < list.Count; i++) _ranks[list[i].Id] = "#" + (i + 1);
            list.AddRange(v.Rivals.Where(r => r.Acquired));
            _board.Sync(list);
            _shares.Sync(Catalog.Categories.OrderByDescending(x => c.CategoryShare.TryGetValue(x, out double s) ? s : 0).ToList());
            _moves.Sync(v.Rivals.Where(r => r.LastMoveDay > 0).OrderByDescending(r => r.LastMoveDay).Take(8).ToList());

            bool pending = v.Board.Agenda.Exists(x => x.Type == ProposalType.GoPublic && x.Status == ProposalStatus.Pending);
            if (c.WentPublic) _ipoText.SetText("The company is public.");
            else if (c.IpoPending) _ipoText.SetText($"The IPO is set for {Fmt.Date(c.IpoDay)} ({DaysLeft(c.IpoDay, v.Day)}). The final valuation that day, plus a 50% premium, decides everyone's payout. Make the numbers look good.");
            else if (c.Stage < Stages.Max) _ipoText.SetText($"The endgame: grow to {Fmt.Money(Stages.Get(Stages.Max).Valuation)} ({Stages.Get(Stages.Max).Name}) and the company can go public at a 50% valuation premium. We are at {Fmt.Money(c.Valuation)}.");
            else _ipoText.SetText(pending ? "The IPO vote is on the board agenda." : "We are big enough to go public. The IPO pays a 50% premium on the valuation and ends the game 30 days after the vote.");
            _ipoBtn.Show(c.Stage >= Stages.Max && !c.IpoPending && !c.WentPublic && !pending);
        }
    }
}
