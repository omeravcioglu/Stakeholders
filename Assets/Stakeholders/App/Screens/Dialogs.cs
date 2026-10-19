using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    /// <summary>All modal dialogs. Each one sends a single Command when confirmed.</summary>
    public static class Dialogs
    {
        /// <summary>Read-only calculator over the current view (prices mirror the simulation's rules).</summary>
        static GameSimulation Calc(GameScreen s) => s.Q;

        static List<Supplier> UsableSuppliers(GameScreen s, string category)
        {
            var me = s.MePlayer;
            return s.View.Suppliers.Where(x => x.Serves(category) && !x.Bankrupt && !x.Blacklisted && (!x.Exclusive || (me != null && me.HasSkill(Skills.SupplierNetwork))))
                .OrderBy(x => x.CostFactor).ToList();
        }

        static VisualElement QuickButtons(TextField field, params (string label, double value)[] items)
        {
            var row = Ui.Row("wrap", "mt8");
            foreach (var (label, value) in items)
            {
                double v = value;
                row.Add(Ui.Btn(label, () => field.value = Fmt.Fixed(v, v % 1 == 0 ? 0 : 2), "btn-ghost", "btn-sm"));
            }
            return row;
        }

        static string ApprovalNote(GameScreen s, double cost)
        {
            var me = s.MePlayer;
            var v = s.View;
            if (me == null) return "";
            long total = v.TotalPlayerShares;
            bool majority = total > 0 && me.Shares / (double)total > 0.5;
            if (majority || cost <= me.SpendingLimit) return "";
            return $"This is above your {Fmt.Money(me.SpendingLimit)} spending authority, so your partners must approve it first.";
        }

        // ------------------------------------------------------------------ order stock

        public static void Order(GameScreen s, CompanyProduct cp)
        {
            var v = s.View;
            var mp = v.GetMarketProduct(cp.ProductId);
            var suppliers = UsableSuppliers(s, cp.Category);
            if (suppliers.Count == 0) { s.UiRoot.Toast("No supplier can make this product right now.", NoticeKind.Warning); return; }
            var m = s.UiRoot.ShowModal("Order stock: " + cp.Name);
            int incoming = v.Company.Shipments.Where(x => x.CompanyProductId == cp.Id).Sum(x => x.Quantity);
            double days = cp.Sales7 > 0.2 ? cp.Stock / cp.Sales7 : 0;
            m.Body.Add(Ui.Text($"In stock {Fmt.Int(cp.Stock)} · incoming {Fmt.Int(incoming)} · selling {Fmt.Num(cp.Sales7)}/day" + (days > 0 ? $" (~{Fmt.Int(days)} days)" : ""), "dim", "mb12"));
            int prefIdx = Math.Max(0, suppliers.FindIndex(x => x.Id == cp.SupplierId));
            var sel = new Select(suppliers.Select(x => $"{x.Name} ({x.Country}) · quality {Fmt.Pct(x.Quality)} · {x.LeadTime}d · MOQ {Fmt.Int(x.Moq)}"), prefIdx);
            m.Body.Add(Ui.Field("Supplier", sel));
            var sup0 = suppliers[prefIdx];
            int suggested = (int)(Math.Ceiling(Math.Max(sup0.Moq, cp.Sales7 * (sup0.LeadTime + 30) - cp.Stock - incoming) / 50.0) * 50);
            var qty = Ui.Input(suggested.ToString(), null, "input-num");
            var qtyField = Ui.Field("Quantity", qty);
            qtyField.Add(QuickButtons(qty, ("500", 500), ("1,000", 1000), ("2,500", 2500), ("5,000", 5000), ("10,000", 10000)));
            m.Body.Add(qtyField);
            bool rush = false;
            Action refresh = null;
            m.Body.Add(Ui.Field("Shipping", Ui.Segmented(new[] { "Sea freight (normal)", "Air freight (+35%, 3 days)" }, 0, i => { rush = i == 1; refresh?.Invoke(); })));
            var preview = Ui.Text("", "para", "inset");
            m.Body.Add(preview);
            var warn = Ui.Text("", "small", "warn", "para", "mt8");
            m.Body.Add(warn);

            void Update()
            {
                var sup = suppliers[sel.Index];
                int q = (int)Ui.ParseNumber(qty.value);
                var calc = Calc(s);
                double unit = calc.UnitPrice(sup, mp, Math.Max(1, q), s.Me) * (rush ? 1.35 : 1);
                double cheapest = calc.CheapestUnitPrice(mp, Math.Max(1, q), s.Me, out var cs) * (rush ? 1.35 : 1);
                double total = unit * q;
                double bulk = GameSimulation.BulkDiscount(q, s.MePlayer != null && s.MePlayer.HasSkill(Skills.BulkSpecialist));
                int space = (int)(v.Company.StorageCapacity - v.Company.StorageUsed - v.Company.Shipments.Sum(x => x.Quantity));
                preview.SetText($"Unit price {Fmt.Price(unit)}" + (bulk > 0 ? $" (bulk -{Fmt.Pct(bulk)})" : "") +
                                (sup.Discount > 0 && v.Day <= sup.DiscountUntil ? $" (negotiated -{Fmt.Pct(sup.Discount, 1)})" : "") +
                                $"\nTotal {Fmt.MoneyFull(total)} · arrives in ~{(rush ? 3 : sup.LeadTime)} days · quality {Fmt.Pct(sup.Quality)}\n" +
                                (cs != null && cs != sup && unit > cheapest * 1.01 ? $"{cs.Name} would charge {Fmt.Price(cheapest)} per unit ({Fmt.Pct(unit / cheapest - 1)} less).\n" : "") +
                                $"Free storage: {Fmt.Int(Math.Max(0, space))} units · company cash {Fmt.Money(v.Company.Cash)}");
                var notes = new List<string>();
                if (q < sup.Moq) notes.Add($"Minimum order is {Fmt.Int(sup.Moq)} units.");
                if (total > v.Company.Cash) notes.Add("The company can't afford this.");
                if (q > space * 1.1) notes.Add("Not enough warehouse space.");
                string approval = ApprovalNote(s, total);
                if (approval != "") notes.Add(approval);
                warn.SetText(string.Join(" ", notes));
            }
            sel.Changed += _ => Update();
            qty.RegisterValueChangedCallback(_ => Update());
            refresh = Update;
            Update();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Place order", () =>
            {
                var sup = suppliers[sel.Index];
                if (sup.Id != cp.SupplierId) s.Send(new Command { Type = CommandType.SetPreferredSupplier, A = cp.Id, B = sup.Id });
                s.Send(new Command { Type = CommandType.OrderInventory, A = cp.Id, B = sup.Id, I = (int)Ui.ParseNumber(qty.value), Flag = rush });
                m.Close();
            }, "btn-good");
        }

        // ------------------------------------------------------------------ price

        public static void Price(GameScreen s, CompanyProduct cp)
        {
            var v = s.View;
            var mp = v.GetMarketProduct(cp.ProductId);
            var m = s.UiRoot.ShowModal("Set price: " + cp.Name);
            double comp = mp.CompetitorPrice * (1 - (v.Day <= mp.CompetitorCutUntil ? mp.CompetitorCut : 0));
            m.Body.Add(Ui.Text($"Current {Fmt.Price(cp.Price)} · typical market price {Fmt.Price(mp.RefPrice)} · main competitor {Fmt.Price(comp)} · your unit cost {Fmt.Price(cp.AvgCost)}", "dim", "para", "mb12"));
            var input = Ui.Input(Fmt.Fixed(cp.Price, 2), null, "input-num");
            var f = Ui.Field("New price ($)", input);
            f.Add(QuickButtons(input, ("-10%", Math.Round(cp.Price * 0.9, 2)), ("-5%", Math.Round(cp.Price * 0.95, 2)), ("Market", mp.RefPrice), ("Match rival", Math.Round(comp, 2)), ("+5%", Math.Round(cp.Price * 1.05, 2)), ("+10%", Math.Round(cp.Price * 1.1, 2))));
            m.Body.Add(f);
            var hint = Ui.Text("", "small", "dim", "para");
            m.Body.Add(hint);
            void Update()
            {
                double p = Ui.ParseNumber(input.value, cp.Price);
                double fee = p * Calc(s).FeeRate() + Calc(s).ShippingCost(cp);
                double margin = p > 0 ? (p - cp.AvgCost - fee) / p : 0;
                hint.SetText($"Margin per unit after fees and shipping: {Fmt.Price(p - cp.AvgCost - fee)} ({Fmt.Pct(margin)}). Higher prices sell fewer units; how many fewer depends on the product. Allowed range {Fmt.Price(mp.RefPrice * 0.3)} to {Fmt.Price(mp.RefPrice * 3)}.");
            }
            input.RegisterValueChangedCallback(_ => Update());
            Update();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Set price", () => { s.Send(new Command { Type = CommandType.SetPrice, A = cp.Id, X = Ui.ParseNumber(input.value, cp.Price) }); m.Close(); });
        }

        // ------------------------------------------------------------------ campaign

        public static void Campaign(GameScreen s, CompanyProduct cp)
        {
            var v = s.View;
            var products = v.Company.Products.Where(p => p.Active).ToList();
            if (products.Count == 0) { s.UiRoot.Toast("Launch a product before advertising.", NoticeKind.Warning); return; }
            var m = s.UiRoot.ShowModal("New ad campaign");
            int idx = cp != null ? Math.Max(0, products.FindIndex(p => p.Id == cp.Id)) : 0;
            var sel = new Select(products.Select(p => $"{p.Name} · {Fmt.Price(p.Price)} · {Fmt.Int(p.Stock)} in stock"), idx);
            m.Body.Add(Ui.Field("Product", sel));
            int channel = 0;
            Action refresh = null;
            var channelHelp = Ui.Text("", "small", "dim", "para", "mt8");
            var chField = Ui.Field("Channel", Ui.Segmented(new[] { "Search", "Social", "Email" }, 0, i => { channel = i; refresh?.Invoke(); }));
            chField.Add(channelHelp);
            m.Body.Add(chField);
            var budget = Ui.Input("100", null, "input-num");
            var bf = Ui.Field("Budget per day ($)", budget);
            bf.Add(QuickButtons(budget, ("$50", 50), ("$100", 100), ("$250", 250), ("$500", 500), ("$1,000", 1000)));
            m.Body.Add(bf);
            int days = 30;
            m.Body.Add(Ui.Field("Duration", Ui.Segmented(new[] { "7 days", "14 days", "30 days", "60 days" }, 2, i => { days = new[] { 7, 14, 30, 60 }[i]; refresh?.Invoke(); })));
            var preview = Ui.Text("", "para", "inset");
            m.Body.Add(preview);
            var warn = Ui.Text("", "small", "warn", "para", "mt8");
            m.Body.Add(warn);
            void Update()
            {
                channelHelp.SetText(channel == 0 ? "Search: customers already looking for the product. Most reliable." : channel == 1 ? "Social: more expensive per sale, but can make a product go viral." : "Email: cheapest per sale, but only reaches past customers.");
                double b = Ui.ParseNumber(budget.value, 100);
                double total = b * days;
                var me = s.MePlayer;
                preview.SetText($"Total budget {Fmt.MoneyFull(total)} over {days} days." + (me != null && me.HasSkill(Skills.AdSpecialist) ? " Your Ad Specialist skill makes it 25% more efficient." : "") +
                                (me != null && me.HasSkill(Skills.ViralMarketing) && channel == 1 ? " Viral Marketing boosts your odds of a viral hit." : ""));
                warn.SetText(ApprovalNote(s, total));
            }
            budget.RegisterValueChangedCallback(_ => Update());
            refresh = Update;
            Update();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Launch campaign", () =>
            {
                s.Send(new Command { Type = CommandType.StartCampaign, A = products[sel.Index].Id, I = channel, X = Ui.ParseNumber(budget.value, 100), J = days });
                m.Close();
            }, "btn-good");
        }

        // ------------------------------------------------------------------ launch

        public static void Launch(GameScreen s, Discovery d)
        {
            var v = s.View;
            var mp = v.GetMarketProduct(d.ProductId);
            var suppliers = UsableSuppliers(s, d.Category);
            if (mp == null || suppliers.Count == 0) { s.UiRoot.Toast("No supplier can make this product.", NoticeKind.Warning); return; }
            var m = s.UiRoot.ShowModal("Launch " + d.Name);
            m.Body.Add(Ui.Text($"Estimated demand {Fmt.Int(d.DemandLow)}-{Fmt.Int(d.DemandHigh)} units/day at around {Fmt.Price(d.RefPrice)} (estimate by {s.View.PlayerName(d.FoundBy)}). New listings take about two weeks to reach full visibility.", "para", "dim", "mb12"));
            var calc = Calc(s);
            int best = 0;
            double bestScore = double.MaxValue;
            for (int i = 0; i < suppliers.Count; i++)
            {
                double sc = calc.UnitPrice(suppliers[i], mp, Math.Max(500, suppliers[i].Moq), s.Me) * (1.25 - suppliers[i].Quality * 0.4);
                if (sc < bestScore) { bestScore = sc; best = i; }
            }
            var sel = new Select(suppliers.Select(x => $"{x.Name} ({x.Country}) · quality {Fmt.Pct(x.Quality)} · {x.LeadTime}d · MOQ {Fmt.Int(x.Moq)}"), best);
            m.Body.Add(Ui.Field("Supplier", sel));
            var sup0 = suppliers[best];
            int suggested = (int)(Math.Ceiling(Math.Max(sup0.Moq, d.DemandMid * 0.5 * (sup0.LeadTime + 25)) / 50.0) * 50);
            var qty = Ui.Input(suggested.ToString(), null, "input-num");
            var qf = Ui.Field("First order (units)", qty);
            qf.Add(QuickButtons(qty, ("Test: 100", 100), ("500", 500), ("1,000", 1000), ("2,500", 2500)));
            m.Body.Add(qf);
            var price = Ui.Input(Fmt.Fixed(d.RefPrice, 2), null, "input-num");
            m.Body.Add(Ui.Field("Selling price ($)", price));
            var preview = Ui.Text("", "para", "inset");
            m.Body.Add(preview);
            var warn = Ui.Text("", "small", "warn", "para", "mt8");
            m.Body.Add(warn);
            void Update()
            {
                var sup = suppliers[sel.Index];
                int q = (int)Ui.ParseNumber(qty.value);
                double unit = Calc(s).UnitPrice(sup, mp, Math.Max(1, q), s.Me);
                double p = Ui.ParseNumber(price.value, d.RefPrice);
                double margin = p > 0 ? (p - unit - p * Calc(s).FeeRate() - Calc(s).ShippingCost(null)) / p : 0;
                preview.SetText($"Unit cost {Fmt.Price(unit)} · total {Fmt.MoneyFull(unit * q)} · margin per sale {Fmt.Pct(margin)} · arrives in ~{sup.LeadTime} days\nYou'll be the product manager: its profits count toward your performance.");
                var notes = new List<string>();
                if (q < sup.Moq) notes.Add($"Minimum order is {Fmt.Int(sup.Moq)} units.");
                if (unit * q > v.Company.Cash) notes.Add("The company can't afford this.");
                string approval = ApprovalNote(s, unit * q);
                if (approval != "") notes.Add(approval);
                warn.SetText(string.Join(" ", notes));
            }
            sel.Changed += _ => Update();
            qty.RegisterValueChangedCallback(_ => Update());
            price.RegisterValueChangedCallback(_ => Update());
            Update();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Launch product", () =>
            {
                s.Send(new Command { Type = CommandType.LaunchProduct, A = d.ProductId, B = suppliers[sel.Index].Id, I = (int)Ui.ParseNumber(qty.value), X = Ui.ParseNumber(price.value, d.RefPrice) });
                m.Close();
            }, "btn-good");
        }

        // ------------------------------------------------------------------ negotiation

        public static void Negotiation(GameScreen s, string supplierId, bool start)
        {
            var sup = s.View.GetSupplier(supplierId);
            if (sup == null) return;
            if (start && !s.View.Negotiations.Any(n => n.PlayerId == s.Me && n.SupplierId == supplierId && n.Status == NegotiationStatus.Active))
                s.Send(new Command { Type = CommandType.StartNegotiation, A = supplierId });
            var m = s.UiRoot.ShowModal("Negotiate with " + sup.Name);
            m.Body.Add(Ui.Text("Ask for a discount on all future orders. Ask too little and you leave money on the table; ask too much and they get offended. You only get a few rounds.", "para", "dim", "small", "mb8"));
            var transcript = Ui.Div("transcript", "col");
            m.Body.Add(transcript);
            var info = Ui.Text("", "small", "accent", "mt8", "para");
            m.Body.Add(info);
            var ask = Ui.Input("10", null, "input-num");
            var askField = Ui.Field("Your ask (% discount)", ask);
            askField.Add(QuickButtons(ask, ("5%", 5), ("8%", 8), ("10%", 10), ("12%", 12), ("15%", 15), ("20%", 20)));
            askField.style.marginTop = 10;
            m.Body.Add(askField);
            var offerBtn = m.AddButton("Make offer", () =>
            {
                var n = Current(s, supplierId);
                if (n != null) s.Send(new Command { Type = CommandType.NegotiationOffer, A = n.Id, X = Ui.ParseNumber(ask.value, 10) / 100.0 });
            }, "btn-good");
            var counterBtn = m.AddButton("Accept counter", () =>
            {
                var n = Current(s, supplierId);
                if (n != null) s.Send(new Command { Type = CommandType.NegotiationOffer, A = n.Id, X = n.LastCounter });
            });
            var walkBtn = m.AddButton("Walk away", () =>
            {
                var n = Current(s, supplierId);
                if (n != null) s.Send(new Command { Type = CommandType.NegotiationWalkAway, A = n.Id });
                m.Close();
            }, "btn-ghost");
            int lines = -1;
            m.Refresh = () =>
            {
                var n = s.View.Negotiations.LastOrDefault(x => x.PlayerId == s.Me && x.SupplierId == supplierId);
                if (n == null)
                {
                    if (lines != 0) { transcript.Clear(); transcript.Add(Ui.Text("Calling the supplier...", "dim")); lines = 0; }
                    offerBtn.SetEnabled(false); counterBtn.Show(false);
                    return;
                }
                if (n.Transcript.Count != lines)
                {
                    lines = n.Transcript.Count;
                    transcript.Clear();
                    foreach (var line in n.Transcript) transcript.Add(Ui.Text(line, line.StartsWith("You") ? "accent" : null));
                }
                bool active = n.Status == NegotiationStatus.Active;
                string hint = n.HintKnown ? $"Your negotiation instinct: they'd probably go {Fmt.Pct(n.HintLow)} to {Fmt.Pct(n.HintHigh)}. " : "";
                info.SetText(active ? $"{hint}Rounds left: {n.RoundsLeft}." : n.Status == NegotiationStatus.Accepted ? $"Deal! {Fmt.Pct(n.AgreedDiscount, 1)} off all orders from {sup.Name}." : n.Status == NegotiationStatus.Failed ? "No deal this time." : "You walked away.");
                info.style.color = n.Status == NegotiationStatus.Accepted ? Ui.Good : n.Status == NegotiationStatus.Failed ? Ui.Bad : Ui.Accent;
                offerBtn.SetEnabled(active);
                askField.Show(active);
                counterBtn.Show(active && n.LastCounter > 0);
                counterBtn.text = $"Accept {Fmt.Pct(n.LastCounter, 1)}";
                walkBtn.text = active ? "Walk away" : "Close";
            };
            m.Refresh();
        }

        static Negotiation Current(GameScreen s, string supplierId) =>
            s.View.Negotiations.FirstOrDefault(x => x.PlayerId == s.Me && x.SupplierId == supplierId && x.Status == NegotiationStatus.Active);

        // ------------------------------------------------------------------ proposals

        static readonly ProposalType[] ProposalTypes =
        {
            ProposalType.SalaryChange, ProposalType.Bonus, ProposalType.Dividend, ProposalType.Loan, ProposalType.Warehouse, ProposalType.Investment,
            ProposalType.Positioning, ProposalType.AcquireRival, ProposalType.GoPublic,
            ProposalType.RaiseCapital, ProposalType.Policy, ProposalType.ExecutiveLimit, ProposalType.Discipline, ProposalType.TransferProduct
        };

        public static void Proposal(GameScreen s, ProposalType initial = ProposalType.SalaryChange)
        {
            var v = s.View;
            var m = s.UiRoot.ShowModal("New board proposal");
            m.Body.Add(Ui.Text("Proposals are voted on at the next board meeting (or right away if one is in session). Chat is where the real negotiating happens.", "para", "dim", "small", "mb8"));
            var typeSel = new Select(ProposalTypes.Select(GameSimulation.ProposalTitle), Math.Max(0, Array.IndexOf(ProposalTypes, initial)));
            m.Body.Add(Ui.Field("Type", typeSel));
            var fields = Ui.Col();
            m.Body.Add(fields);
            var note = Ui.Input("");
            m.Body.Add(Ui.Field("Your argument (optional)", note));
            Func<Command> build = null;

            var players = v.Players.ToList();
            var playerNames = players.Select(p => p.Name + (p.Id == s.Me ? " (you)" : "")).ToList();
            int meIndex = Math.Max(0, players.FindIndex(p => p.Id == s.Me));

            void Rebuild()
            {
                fields.Clear();
                var type = ProposalTypes[typeSel.Index];
                switch (type)
                {
                    case ProposalType.SalaryChange:
                    case ProposalType.ExecutiveLimit:
                    {
                        var who = new Select(playerNames, meIndex);
                        var amount = Ui.Input("", null, "input-num");
                        void Default() { var p = players[who.Index]; amount.value = Fmt.Fixed(type == ProposalType.SalaryChange ? Math.Round(p.Salary * 1.25 / 100) * 100 : p.SpendingLimit + 25000, 0); }
                        who.Changed += _ => Default();
                        Default();
                        fields.Add(Ui.Field("Partner", who));
                        fields.Add(Ui.Field(type == ProposalType.SalaryChange ? "New monthly salary ($)" : "New spending authority ($)", amount));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = players[who.Index].Id, X = Ui.ParseNumber(amount.value) };
                        break;
                    }
                    case ProposalType.Bonus:
                    case ProposalType.Discipline:
                    {
                        var who = new Select(playerNames, type == ProposalType.Discipline ? (meIndex == 0 && players.Count > 1 ? 1 : 0) : meIndex);
                        var amount = Ui.Input(type == ProposalType.Bonus ? "5000" : "10000", null, "input-num");
                        fields.Add(Ui.Field("Partner", who));
                        fields.Add(Ui.Field(type == ProposalType.Bonus ? "Bonus ($)" : "Fine ($, paid to the company)", amount));
                        if (type == ProposalType.Discipline) fields.Add(Ui.Text("Discipline also cuts their salary by 30% and revokes their spending authority.", "small", "dim"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = players[who.Index].Id, X = Ui.ParseNumber(amount.value) };
                        break;
                    }
                    case ProposalType.Dividend:
                    {
                        var amount = Ui.Input(Fmt.Fixed(Math.Max(1000, Math.Round(v.Company.Cash * 0.2 / 1000) * 1000), 0), null, "input-num");
                        var hint = Ui.Text("", "small", "dim");
                        void H() { var me = s.MePlayer; double a = Ui.ParseNumber(amount.value); hint.SetText($"Company cash {Fmt.Money(v.Company.Cash)} · you'd receive {Fmt.Money(me != null ? a * me.Shares / v.Company.TotalShares : 0)}"); }
                        amount.RegisterValueChangedCallback(_ => H());
                        H();
                        fields.Add(Ui.Field("Total dividend ($)", amount));
                        fields.Add(hint);
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, X = Ui.ParseNumber(amount.value) };
                        break;
                    }
                    case ProposalType.Loan:
                    {
                        var loans = Catalog.Loans.ToList();
                        var sel = new Select(loans.Select(l => $"{l.Lender} · {Fmt.Pct(l.Rate)} · {l.Months} months"), 0);
                        fields.Add(Ui.Field("Loan", sel));
                        fields.Add(Ui.Text("The amount is set by the lender when the vote passes (based on revenue and valuation).", "small", "dim"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = loans[sel.Index].Id };
                        break;
                    }
                    case ProposalType.Warehouse:
                    {
                        var defs = Catalog.Warehouses.Where(w => w.Id != "garage").ToList();
                        var sel = new Select(defs.Select(w => $"{w.Name} · {Fmt.Money(w.SetupCost)} + {Fmt.Money(w.MonthlyCost)}/mo · {Fmt.Int(w.Capacity)} units"), 0);
                        fields.Add(Ui.Field("Warehouse", sel));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = defs[sel.Index].Id };
                        break;
                    }
                    case ProposalType.Investment:
                    {
                        var me = s.MePlayer;
                        var ups = Catalog.Upgrades.Where(u => !v.Company.Has(u.Id) && !v.Company.Building.Exists(b => b.Id == u.Id) && s.Q.UpgradeUnlocked(u, me, out _)).ToList();
                        if (ups.Count == 0) { fields.Add(Ui.Text("No upgrade is available right now. Grow the company to unlock the next tier.", "dim")); build = null; break; }
                        var sel = new Select(ups.Select(u => $"{u.Branch}: {u.Name} · {Fmt.Money(u.Cost)}"), 0);
                        var desc = Ui.Text(ups[0].Description, "small", "dim", "para");
                        sel.Changed += i => desc.SetText(ups[i].Description);
                        fields.Add(Ui.Field("Upgrade", sel));
                        fields.Add(desc);
                        fields.Add(Ui.Text("You can also build upgrades directly from the Growth page. A vote is useful when it's above your spending authority.", "small", "faint", "para", "mt8"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = ups[sel.Index].Id };
                        break;
                    }
                    case ProposalType.Positioning:
                    {
                        var opts = Positionings.All.Where(p => p.Id != v.Company.Positioning).ToList();
                        if (v.Company.Stage < 1) { fields.Add(Ui.Text($"A company strategy unlocks at the {Stages.Get(1).Name} stage.", "dim")); build = null; break; }
                        var sel = new Select(opts.Select(p => p.Name), 0);
                        var desc = Ui.Text(opts[0].Description, "small", "dim", "para");
                        sel.Changed += i => desc.SetText(opts[i].Description);
                        fields.Add(Ui.Field("Strategy", sel));
                        fields.Add(desc);
                        if (!string.IsNullOrEmpty(v.Company.Positioning)) fields.Add(Ui.Text("Changing strategy costs $50K in rebranding.", "small", "warn", "mt8"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = opts[sel.Index].Id };
                        break;
                    }
                    case ProposalType.AcquireRival:
                    {
                        var targets = v.Rivals.Where(r => s.Q.CanAcquire(r, out _)).ToList();
                        if (targets.Count == 0)
                        {
                            fields.Add(Ui.Text(v.Company.Stage < 3 ? $"Acquisitions unlock at the {Stages.Get(3).Name} stage." : "No rival is small enough yet: we must be worth 1.5x their valuation.", "dim", "para"));
                            build = null;
                            break;
                        }
                        var sel = new Select(targets.Select(r => $"{r.Name} · {Fmt.Money(s.Q.AcquisitionPrice(r))}"), 0);
                        var desc = Ui.Text("", "small", "dim", "para");
                        void D(int i) => desc.SetText($"{targets[i].Style} · strong in {string.Join(", ", targets[i].Focus)}. Their customers become ours: +15% demand in those categories.");
                        sel.Changed += D;
                        D(0);
                        fields.Add(Ui.Field("Rival", sel));
                        fields.Add(desc);
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = targets[sel.Index].Id };
                        break;
                    }
                    case ProposalType.GoPublic:
                    {
                        if (v.Company.Stage < Stages.Max) { fields.Add(Ui.Text($"The company must reach {Fmt.Money(Stages.Get(Stages.Max).Valuation)} ({Stages.Get(Stages.Max).Name}) before it can go public.", "dim", "para")); build = null; break; }
                        fields.Add(Ui.Text("The IPO values the company at a 50% premium and ends the game 30 days after the vote: every partner cashes in their shares. Needs two thirds of the votes.", "para"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1 };
                        break;
                    }
                    case ProposalType.RaiseCapital:
                    {
                        var sources = new List<string> { "Outside investors (15% discount)" };
                        sources.AddRange(players.Select(p => p.Name + (p.Id == s.Me ? " (you)" : "") + " pays personally"));
                        var sel = new Select(sources, 0);
                        var amount = Ui.Input("100000", null, "input-num");
                        fields.Add(Ui.Field("Who buys the new shares", sel));
                        fields.Add(Ui.Field("Amount to raise ($)", amount));
                        fields.Add(Ui.Text("New shares dilute everyone else's ownership.", "small", "dim"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = sel.Index == 0 ? -1 : players[sel.Index - 1].Id, X = Ui.ParseNumber(amount.value) };
                        break;
                    }
                    case ProposalType.Policy:
                    {
                        var keys = new[] { "returns", "audits", "openbooks", "ballot", "limit" };
                        var names = new[] { "Return policy", "Supplier audits", "Open books", "Secret ballot", "Default spending authority" };
                        var sel = new Select(names, 0);
                        var valueHost = Ui.Col();
                        Func<int> getValue = () => 1;
                        void Values()
                        {
                            valueHost.Clear();
                            string k = keys[sel.Index];
                            if (k == "returns")
                            {
                                int val = 1;
                                valueHost.Add(Ui.Field("Setting", Ui.Segmented(new[] { "Strict", "Standard", "Generous" }, 1, i => val = i)));
                                getValue = () => val;
                            }
                            else if (k == "limit")
                            {
                                var amount = Ui.Input("50000", null, "input-num");
                                valueHost.Add(Ui.Field("Amount ($)", amount));
                                getValue = () => (int)Ui.ParseNumber(amount.value, 50000);
                            }
                            else
                            {
                                int val = 1;
                                valueHost.Add(Ui.Field("Setting", Ui.Segmented(new[] { "Off", "On" }, 1, i => val = i)));
                                getValue = () => val;
                            }
                        }
                        sel.Changed += _ => Values();
                        Values();
                        fields.Add(Ui.Field("Policy", sel));
                        fields.Add(valueHost);
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = -1, A = keys[sel.Index], X = getValue() };
                        break;
                    }
                    case ProposalType.TransferProduct:
                    {
                        var prods = v.Company.Products.Where(p => p.Active).ToList();
                        if (prods.Count == 0) { fields.Add(Ui.Text("No products yet.", "dim")); build = null; break; }
                        var prod = new Select(prods.Select(p => $"{p.Name} (managed by {v.PlayerName(p.ManagerId)})"), 0);
                        var who = new Select(playerNames, meIndex);
                        fields.Add(Ui.Field("Product", prod));
                        fields.Add(Ui.Field("New manager", who));
                        fields.Add(Ui.Text("The manager gets the credit (and XP) for the product's profits.", "small", "dim"));
                        build = () => new Command { Type = CommandType.CreateProposal, I = (int)type, J = players[who.Index].Id, A = prods[prod.Index].Id };
                        break;
                    }
                }
            }
            typeSel.Changed += _ => Rebuild();
            Rebuild();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Submit proposal", () =>
            {
                if (build == null) return;
                var cmd = build();
                cmd.Text = note.value;
                s.Send(cmd);
                m.Close();
            }, "btn-good");
        }

        // ------------------------------------------------------------------ shares

        public static void ShareOffer(GameScreen s, Player other, bool buying)
        {
            var v = s.View;
            var me = s.MePlayer;
            var m = s.UiRoot.ShowModal(buying ? $"Buy shares from {other.Name}" : $"Sell shares to {other.Name}");
            double theirs = other.Shares / (double)v.Company.TotalShares, mine = me.Shares / (double)v.Company.TotalShares;
            m.Body.Add(Ui.Text($"{other.Name} owns {Fmt.Pct(theirs, 1)} · you own {Fmt.Pct(mine, 1)} · valuation {Fmt.Money(v.Company.Valuation)} · your cash {Fmt.Money(s.MePriv?.Cash ?? 0)}", "dim", "para", "mb12"));
            var pct = Ui.Input("2", null, "input-num");
            var pf = Ui.Field("Percent of the company", pct);
            pf.Add(QuickButtons(pct, ("1%", 1), ("2%", 2), ("5%", 5), ("10%", 10)));
            m.Body.Add(pf);
            var price = Ui.Input("", null, "input-num");
            m.Body.Add(Ui.Field("Price ($)", price));
            var hint = Ui.Text("", "small", "dim", "para");
            m.Body.Add(hint);
            var note = Ui.Input("");
            m.Body.Add(Ui.Field("Message (optional)", note));
            bool touched = false;
            price.RegisterValueChangedCallback(_ => touched = true);
            void Update()
            {
                double p = Ui.ParseNumber(pct.value, 2) / 100.0;
                double fair = v.Company.Valuation * p;
                if (!touched) { price.SetValueWithoutNotify(Fmt.Fixed(Math.Round(fair / 100) * 100, 0)); }
                hint.SetText($"Fair value of {Fmt.Pct(p, 1)} at the current valuation: {Fmt.Money(fair)}. Nothing forces them to accept, and nothing stops you from negotiating in chat first.");
            }
            pct.RegisterValueChangedCallback(_ => Update());
            Update();
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Send offer", () =>
            {
                s.Send(new Command { Type = CommandType.CreateShareOffer, I = other.Id, Flag = buying, X = Ui.ParseNumber(pct.value, 2) / 100.0, Y = Ui.ParseNumber(price.value), Text = note.value });
                m.Close();
            }, "btn-good");
        }

        // ------------------------------------------------------------------ blackmail

        public static void Blackmail(GameScreen s, Evidence e)
        {
            var m = s.UiRoot.ShowModal("Demand payment");
            m.Body.Add(Ui.Text($"You know this: {e.Summary}", "para", "mb8"));
            m.Body.Add(Ui.Text($"Send {s.View.PlayerName(e.ActorId)} a private message: pay you, and you stay quiet. They might pay. They might call your bluff. Nothing makes either of you keep your word.", "para", "dim", "small", "mb12"));
            var amount = Ui.Input(Fmt.Fixed(Math.Max(3000, Math.Round(e.Amount * 1.2 / 500) * 500), 0), null, "input-num");
            m.Body.Add(Ui.Field("Amount ($)", amount));
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton("Send demand", () =>
            {
                s.Send(new Command { Type = CommandType.BlackmailEvidence, A = e.Id, X = Ui.ParseNumber(amount.value, 5000) });
                m.Close();
            }, "btn-secret");
        }
    }
}
