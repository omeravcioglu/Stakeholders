using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    public partial class GameSimulation
    {
        static readonly HashSet<CommandType> WorkCommands = new HashSet<CommandType>
        {
            CommandType.StartResearch, CommandType.LaunchProduct, CommandType.OrderInventory, CommandType.StartNegotiation,
            CommandType.NegotiationOffer, CommandType.StartCampaign, CommandType.Recruit, CommandType.Hire,
            CommandType.ResolveEvent, CommandType.ClaimEvent, CommandType.StartInvestigation, CommandType.BuyWarehouse,
            CommandType.CompanyInvest, CommandType.SetPrice, CommandType.Liquidate, CommandType.BuyUpgrade, CommandType.StartPromotion,
            CommandType.UpgradeProduct, CommandType.ClaimProduct
        };

        /// <summary>Validate and apply one player command. Safe to call with untrusted input.</summary>
        public CmdResult Execute(Command c)
        {
            if (c == null) return CmdResult.Fail("No command.");
            var p = P(c.PlayerId);
            if (p == null) return CmdResult.Fail("Unknown player.");
            if (State.Phase == GamePhase.GameOver && c.Type != CommandType.Chat) return CmdResult.Fail("The game is over.");
            if (State.Phase == GamePhase.Lobby) return CmdResult.Fail("The game has not started.");

            CmdResult r;
            if (IsAway(p) && WorkCommands.Contains(c.Type))
                r = CmdResult.Fail($"You are away ({p.Status}) until {Fmt.ShortDate(p.AwayUntilDay)}.");
            else
            {
                try { r = Dispatch(c, p); }
                catch (Exception ex) { r = CmdResult.Fail("Something went wrong: " + ex.Message); }
            }
            if (!string.IsNullOrEmpty(r.Message)) Notify(p.Id, r.Ok ? NoticeKind.Info : NoticeKind.Warning, r.Message);
            State.Revision++;
            return r;
        }

        CmdResult Dispatch(Command c, Player p)
        {
            switch (c.Type)
            {
                case CommandType.SetSpeed:
                    State.Speed = Clamp(c.I, 1, 8);
                    State.Paused = false;
                    return CmdResult.Success();
                case CommandType.TogglePause:
                    State.Paused = !State.Paused;
                    if (HumanCount > 1) Log(LogKind.Info, p.Id, $"{p.Name} {(State.Paused ? "paused" : "resumed")} the game.");
                    return CmdResult.Success();
                case CommandType.Chat:
                    if (c.I == p.Id) c.I = -1;
                    if (c.I >= 0 && P(c.I) == null) return CmdResult.Fail("No such partner.");
                    Say(p.Id, c.Text, c.I);
                    if (c.I >= 0 && P(c.I).IsAI) AIReplyToChat(P(c.I), p, c.Text);
                    return CmdResult.Success();
                case CommandType.SetReady:
                    p.Ready = c.Flag;
                    return CmdResult.Success();
                case CommandType.StartResearch: return CmdStartResearch(c, p);
                case CommandType.ShareDiscovery: return CmdShareDiscovery(c, p);
                case CommandType.LaunchProduct: return CmdLaunchProduct(c, p);
                case CommandType.SetPrice: return CmdSetPrice(c, p);
                case CommandType.OrderInventory: return CmdOrderInventory(c, p);
                case CommandType.Liquidate: return CmdLiquidate(c, p);
                case CommandType.SetPreferredSupplier:
                {
                    var cp = State.GetCompanyProduct(c.A);
                    var s = State.GetSupplier(c.B);
                    if (cp == null || s == null) return CmdResult.Fail("Unknown product or supplier.");
                    cp.SupplierId = s.Id;
                    return CmdResult.Success();
                }
                case CommandType.StartNegotiation: return CmdStartNegotiation(c, p);
                case CommandType.NegotiationOffer: return CmdNegotiationOffer(c, p);
                case CommandType.NegotiationWalkAway:
                {
                    var n = State.Negotiations.Find(x => x.Id == c.A && x.PlayerId == p.Id);
                    if (n == null) return CmdResult.Fail("No such negotiation.");
                    if (n.Status == NegotiationStatus.Active)
                    {
                        n.Status = NegotiationStatus.WalkedAway;
                        n.Transcript.Add("You walked away from the table.");
                    }
                    State.Negotiations.Remove(n);
                    return CmdResult.Success();
                }
                case CommandType.StartCampaign: return CmdStartCampaign(c, p);
                case CommandType.StopCampaign:
                {
                    var cam = C.Campaigns.Find(x => x.Id == c.A && x.Active);
                    if (cam == null) return CmdResult.Fail("That campaign is not running.");
                    EndCampaign(cam, $"stopped by {p.Name}");
                    return CmdResult.Success();
                }
                case CommandType.Recruit: return CmdRecruit(c, p);
                case CommandType.Hire: return CmdHire(c, p);
                case CommandType.Fire: return CmdFire(c, p);
                case CommandType.BuyWarehouse: return CmdBuyWarehouse(c, p);
                case CommandType.TakeLoan: return CmdTakeLoan(c, p);
                case CommandType.RepayLoan: return CmdRepayLoan(c, p);
                case CommandType.CompanyInvest:
                case CommandType.BuyUpgrade: return CmdBuyUpgrade(c, p);
                case CommandType.StartPromotion: return CmdStartPromotion(c, p);
                case CommandType.UpgradeProduct: return CmdUpgradeProduct(c, p);
                case CommandType.SetAutoRestock: return CmdSetAutoRestock(c, p);
                case CommandType.ClaimProduct: return CmdClaimProduct(c, p);
                case CommandType.SetRoles: return CmdSetRoles(c, p);
                case CommandType.ChooseAmbition: return CmdChooseAmbition(c, p);
                case CommandType.AcceptContract: return CmdResult.Fail("Contracts are signed from their offer in Situations.");
                case CommandType.SetOption:
                    if (State.Settings.Multiplayer) return CmdResult.Fail("Game options can't be changed in multiplayer.");
                    if (c.A == "autopause") { State.Settings.AutoPause = c.Flag; return CmdResult.Success(); }
                    return CmdResult.Fail("Unknown option.");
                case CommandType.SetPolicy: return CmdSetPolicy(c, p);
                case CommandType.ClaimEvent: return CmdClaimEvent(c, p);
                case CommandType.ResolveEvent: return CmdResolveEvent(c, p);
                case CommandType.RespondOffer: return CmdRespondOffer(c, p);
                case CommandType.ExposeEvidence: return CmdExposeEvidence(c, p);
                case CommandType.BlackmailEvidence: return CmdBlackmailEvidence(c, p);
                case CommandType.IgnoreEvidence:
                {
                    var e = State.Evidence.Find(x => x.Id == c.A);
                    if (e == null || !e.DiscoveredBy.Contains(p.Id)) return CmdResult.Fail("Unknown evidence.");
                    if (!e.Silenced.Contains(p.Id)) e.Silenced.Add(p.Id);
                    return CmdResult.Success("You decided to keep quiet. For now.");
                }
                case CommandType.StartInvestigation: return CmdStartInvestigation(c, p);
                case CommandType.CreateProposal: return CmdCreateProposal(c, p);
                case CommandType.CastVote: return CmdCastVote(c, p);
                case CommandType.WithdrawProposal: return CmdWithdrawProposal(c, p);
                case CommandType.VoteApproval: return CmdVoteApproval(c, p);
                case CommandType.CreateShareOffer: return CmdCreateShareOffer(c, p);
                case CommandType.RespondShareOffer: return CmdRespondShareOffer(c, p);
                case CommandType.CancelShareOffer:
                {
                    var o = State.ShareOffers.Find(x => x.Id == c.A && x.FromId == p.Id && x.Status == OfferStatus.Pending);
                    if (o == null) return CmdResult.Fail("No such offer.");
                    o.Status = OfferStatus.Rejected;
                    return CmdResult.Success("Offer withdrawn.");
                }
                case CommandType.UnlockSkill: return CmdUnlockSkill(c, p);
                case CommandType.PersonalInvest: return CmdPersonalInvest(c, p);
                case CommandType.PersonalSell: return CmdPersonalSell(c, p);
                case CommandType.AdvanceDays:
                {
                    if (State.Settings.Multiplayer) return CmdResult.Fail("Not available in multiplayer.");
                    int days = Clamp(c.I, 1, 400);
                    for (int i = 0; i < days && State.Phase == GamePhase.Running; i++) StepDay();
                    return CmdResult.Success();
                }
            }
            return CmdResult.Fail("Unsupported action.");
        }

        // ------------------------------------------------------------------ spending approval

        /// <summary>
        /// Spending above a partner's limit becomes an approval request. Returns true when the
        /// command was deferred (result holds the message to show).
        /// </summary>
        bool NeedsApproval(Player p, Command c, double cost, string summary, out CmdResult result)
        {
            result = default;
            if (c.Approved || cost <= p.SpendingLimit) return false;
            if (VotingWeight(p) > 0.5) return false; // majority owner: the vote would pass anyway
            foreach (var a in State.Approvals)
                if (a.Status == ProposalStatus.Pending && a.RequesterId == p.Id && a.Command.Type == c.Type && a.Command.A == c.A && a.Command.B == c.B)
                {
                    result = CmdResult.Fail("An identical request is already waiting for approval.");
                    return true;
                }
            var req = new ApprovalRequest
            {
                Id = NewId("a"), RequesterId = p.Id, Command = c.Clone(), Summary = summary, Amount = cost,
                CreatedDay = State.Day, ExpiresDay = State.Day + 5, Status = ProposalStatus.Pending
            };
            req.Votes.Add(new VoteRecord { PlayerId = p.Id, Choice = VoteChoice.Yes, Weight = VotingWeight(p) });
            State.Approvals.Add(req);
            Log(LogKind.Board, p.Id, $"{p.Name} asks the partners to approve: {summary} ({Fmt.Money(cost)}).");
            foreach (var other in State.Players)
                if (other.Id != p.Id) Notify(other.Id, NoticeKind.Warning, $"{p.Name} needs your approval: {summary} ({Fmt.Money(cost)}).");
            result = CmdResult.Success($"That is over your {Fmt.Money(p.SpendingLimit)} limit, so it went to your partners for approval.");
            return true;
        }

        // ------------------------------------------------------------------ research & products

        CmdResult CmdStartResearch(Command c, Player p)
        {
            if (ActiveTaskCount(p.Id) >= MaxTasks(p)) return CmdResult.Fail("You are already busy with as many tasks as you can handle.");
            int depth = c.I == 1 ? 1 : 0;
            string category = string.IsNullOrEmpty(c.A) ? "Any" : c.A;
            if (category != "Any" && Array.IndexOf(Catalog.Categories, category) < 0) return CmdResult.Fail("Unknown category.");
            double cost = ResearchCost(depth);
            if (C.Cash < cost) return CmdResult.Fail("The company cannot afford that research.");
            if (NeedsApproval(p, c, cost, $"{(depth == 1 ? "Deep" : "Quick")} research: {category}", out var ar)) return ar;
            Spend(cost, TxType.Research, p.Id, $"Market research ({category}, {(depth == 1 ? "deep" : "quick")})");
            int days = ResearchDays(depth);
            StartTask(p.Id, TaskKind.Research, $"Researching {(category == "Any" ? "the market" : category)}", days, category, depth);
            Log(LogKind.Market, p.Id, $"{p.Name} started {(depth == 1 ? "deep" : "quick")} research on {(category == "Any" ? "the whole market" : category)}.");
            return CmdResult.Success($"Research started. Results in {Fmt.Days(days)}.");
        }

        CmdResult CmdShareDiscovery(Command c, Player p)
        {
            var pr = Priv(p.Id);
            var d = pr.Notebook.Find(x => x.ProductId == c.A);
            if (d == null) return CmdResult.Fail("That product is not in your notebook.");
            if (d.Shared) return CmdResult.Fail("Already shared.");
            if (C.ResearchBoard.Exists(x => x.ProductId == d.ProductId)) { d.Shared = true; return CmdResult.Fail("A partner already shared that product."); }
            d.Shared = true;
            var copy = d.Copy();
            copy.Shared = true;
            C.ResearchBoard.Add(copy);
            if (C.ResearchBoard.Count > 40) C.ResearchBoard.RemoveAt(0);
            Award(p.Id, 10, $"Shared research on {d.Name}");
            Log(LogKind.Market, p.Id, $"{p.Name} shared research on {d.Name} with the partners.");
            return CmdResult.Success();
        }

        CmdResult CmdLaunchProduct(Command c, Player p)
        {
            var mp = State.GetMarketProduct(c.A);
            if (mp == null) return CmdResult.Fail("Unknown product.");
            var disc = FindDiscovery(p.Id, mp.Id);
            if (disc == null) return CmdResult.Fail("You need research on this product before launching it.");
            if (State.GetCompanyProductByMarketId(mp.Id) != null) return CmdResult.Fail("We already sell that product.");
            var s = State.GetSupplier(c.B);
            if (s == null || !s.Serves(mp.Category)) return CmdResult.Fail("That supplier does not make this product.");
            if (!CanUseSupplier(s, p.Id)) return CmdResult.Fail($"{s.Name} is not available to you.");
            if (State.Day < s.BlockedUntil) return CmdResult.Fail($"{s.Name} refuses to deal with us for now.");
            int qty = c.I;
            if (qty < s.Moq) return CmdResult.Fail($"{s.Name} has a minimum order of {Fmt.Int(s.Moq)} units.");
            double price = c.X > 0 ? c.X : mp.RefPrice;
            price = Math.Round(Clamp(price, mp.RefPrice * 0.3, mp.RefPrice * 3), 2);
            if (StockOnHand() + IncomingUnits() + qty > StorageCapacity() * 1.1) return CmdResult.Fail("Not enough warehouse space for that order.");
            double unit = UnitPrice(s, mp, qty, p.Id);
            double total = unit * qty;
            if (C.Cash < total) return CmdResult.Fail($"The company only has {Fmt.Money(C.Cash)}; this launch costs {Fmt.Money(total)}.");
            if (NeedsApproval(p, c, total, $"Launch {mp.Name} with {Fmt.Int(qty)} units from {s.Name}", out var ar)) return ar;

            var existing = C.Products.Find(x => x.ProductId == mp.Id);
            CompanyProduct cp;
            if (existing != null)
            {
                cp = existing;
                cp.Active = true;
                cp.ManagerId = p.Id;
                cp.LaunchedDay = State.Day;
                cp.Price = price;
                cp.SupplierId = s.Id;
            }
            else
            {
                cp = new CompanyProduct
                {
                    Id = NewId("cp"), ProductId = mp.Id, Name = mp.Name, BaseName = mp.Name, Category = mp.Category, ManagerId = p.Id,
                    DiscovererId = disc.FoundBy, LaunchedDay = State.Day, Price = price, SupplierId = s.Id, Quality = s.Quality,
                    Rating = 3.6 + s.Quality * 0.8
                };
                C.Products.Add(cp);
            }
            disc.Launched = true;
            PlaceOrder(p, cp, mp, s, qty, false);
            p.ProductsLaunched++;
            Award(p.Id, 20, $"Launched {mp.Name}");
            Log(LogKind.Company, p.Id, $"{p.Name} launched {mp.Name} at {Fmt.Price(price)} ({Fmt.Int(qty)} units from {s.Name}).");
            return CmdResult.Success($"{mp.Name} launched! Stock arrives in about {s.LeadTime} days.");
        }

        CmdResult CmdSetPrice(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            var mp = State.GetMarketProduct(cp.ProductId);
            double price = Math.Round(Clamp(c.X, mp.RefPrice * 0.3, mp.RefPrice * 3), 2);
            double old = cp.Price;
            cp.Price = price;
            if (Math.Abs(price - old) / Math.Max(0.01, old) >= 0.1)
                Log(LogKind.Company, p.Id, $"{p.Name} changed the price of {cp.Name}: {Fmt.Price(old)} -> {Fmt.Price(price)}.");
            return CmdResult.Success();
        }

        CmdResult CmdOrderInventory(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null) return CmdResult.Fail("Unknown product.");
            var mp = State.GetMarketProduct(cp.ProductId);
            var s = State.GetSupplier(c.B);
            if (s == null || !s.Serves(mp.Category)) return CmdResult.Fail("That supplier does not make this product.");
            if (!CanUseSupplier(s, p.Id)) return CmdResult.Fail($"{s.Name} is not available to you.");
            if (State.Day < s.BlockedUntil) return CmdResult.Fail($"{s.Name} refuses to deal with us until {Fmt.ShortDate(s.BlockedUntil)}.");
            int qty = c.I;
            if (qty < s.Moq) return CmdResult.Fail($"{s.Name} has a minimum order of {Fmt.Int(s.Moq)} units.");
            if (qty > 200000) return CmdResult.Fail("That order is unrealistically large.");
            if (StockOnHand() + IncomingUnits() + qty > StorageCapacity() * 1.1)
                return CmdResult.Fail($"Not enough warehouse space. Free space: {Fmt.Int(Math.Max(0, StorageCapacity() - StockOnHand() - IncomingUnits()))} units.");
            bool rush = c.Flag;
            double unit = UnitPrice(s, mp, qty, p.Id) * (rush ? 1.35 : 1);
            double total = unit * qty;
            if (C.Cash < total) return CmdResult.Fail($"The company only has {Fmt.Money(C.Cash)}; this order costs {Fmt.Money(total)}.");
            if (NeedsApproval(p, c, total, $"Order {Fmt.Int(qty)} x {cp.Name} from {s.Name}", out var ar)) return ar;
            PlaceOrder(p, cp, mp, s, qty, rush);
            return CmdResult.Success($"Ordered {Fmt.Int(qty)} x {cp.Name} for {Fmt.Money(total)}.");
        }

        /// <summary>Creates the shipment, pays for it, flags anomalies and settles kickback deals.</summary>
        public Shipment PlaceOrder(Player p, CompanyProduct cp, MarketProduct mp, Supplier s, int qty, bool rush)
        {
            double unit = UnitPrice(s, mp, qty, p.Id) * (rush ? 1.35 : 1);
            double total = unit * qty;
            double cheapest = CheapestUnitPrice(mp, qty, -1, out var cs) * (rush ? 1.35 : 1);
            double premium = cheapest > 0 ? unit / cheapest - 1 : 0;
            string flag = null;
            int level = 0;
            if (cs != null && cs != s && premium >= 0.08)
            {
                flag = $"{Fmt.Pct(premium)} above the cheapest supplier ({cs.Name})";
                level = premium >= 0.15 ? 2 : 1;
            }
            Spend(total, TxType.Inventory, p.Id, $"{Fmt.Int(qty)} x {cp.Name} from {s.Name} @ {Fmt.Price(unit)}{(rush ? " (air freight)" : "")}", flag, level);
            int lead = rush ? 3 : s.LeadTime;
            if (!rush && EmployeePower(EmployeeRole.Buyer) >= 1 && lead > 5) lead -= 1;
            if (!rush && C.Has("src4")) lead = Math.Max(2, (int)Math.Ceiling(lead * 0.7));
            double quality = Math.Min(1, s.Quality + (C.Has("src1") ? 0.05 : 0));
            var sh = new Shipment
            {
                Id = NewId("s"), CompanyProductId = cp.Id, SupplierId = s.Id, Quantity = qty, UnitCost = unit, Quality = quality,
                OrderedDay = State.Day, EtaDay = State.Day + lead, OrderedBy = p.Id, Rush = rush
            };
            C.Shipments.Add(sh);
            s.TotalPurchased += total;
            s.Relationship = Math.Min(100, s.Relationship + 1);
            cp.LastOrderedBy = p.Id;
            if (string.IsNullOrEmpty(cp.SupplierId)) cp.SupplierId = s.Id;
            SettleKickback(p, s, cp, qty, total);
            if (flag != null) AIReactToFlaggedOrder(p, s, cs, premium, cp);
            return sh;
        }

        CmdResult CmdLiquidate(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            double stockValue = cp.Stock * cp.AvgCost;
            double proceeds = stockValue * 0.35;
            int units = cp.Stock;
            if (proceeds > 0)
            {
                C.Cash += proceeds;
                C.Today.Revenue += proceeds;
                C.Today.Cogs += stockValue;
                AddLedger(TxType.Liquidation, proceeds, p.Id, $"Liquidated {Fmt.Int(units)} x {cp.Name} (35% of cost)", null, 0);
            }
            double refunds = 0;
            for (int i = C.Shipments.Count - 1; i >= 0; i--)
            {
                var sh = C.Shipments[i];
                if (sh.CompanyProductId != cp.Id) continue;
                refunds += sh.Quantity * sh.UnitCost * 0.5;
                C.Shipments.RemoveAt(i);
            }
            if (refunds > 0) Receive(refunds, TxType.Inventory, p.Id, $"Cancelled {cp.Name} shipments (50% refund)");
            foreach (var cam in C.Campaigns) if (cam.Active && cam.CompanyProductId == cp.Id) EndCampaign(cam, "product discontinued");
            cp.Stock = 0;
            cp.Active = false;
            cp.Sales7 = 0;
            cp.Demand7 = 0;
            double loss = stockValue - proceeds;
            if (loss > 5000 && cp.LastOrderedBy >= 0) Penalize(cp.LastOrderedBy, 50, $"Bad inventory purchase: {cp.Name} was liquidated at a {Fmt.Money(loss)} loss");
            Log(LogKind.Company, p.Id, $"{p.Name} discontinued {cp.Name} and liquidated {Fmt.Int(units)} units for {Fmt.Money(proceeds)}.");
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ negotiation

        CmdResult CmdStartNegotiation(Command c, Player p)
        {
            var s = State.GetSupplier(c.A);
            if (s == null || !CanUseSupplier(s, p.Id)) return CmdResult.Fail("That supplier is not available.");
            if (State.Day < s.BlockedUntil) return CmdResult.Fail($"{s.Name} won't take your calls until {Fmt.ShortDate(s.BlockedUntil)}.");
            if (State.Negotiations.Exists(n => n.PlayerId == p.Id && n.Status == NegotiationStatus.Active))
                return CmdResult.Fail("Finish your current negotiation first.");
            if (s.Discount > 0 && s.DiscountUntil - State.Day > 60) return CmdResult.Fail($"We already have a fresh deal with {s.Name}.");
            double max = 0.03 + (s.CostFactor - 0.7) * 0.12 + s.Relationship / 1000.0 + Rng.Range(0, 0.04);
            if (p.HasSkill(Skills.Negotiator)) max += 0.04;
            if (s.TotalPurchased > 50000) max += 0.02;
            if (s.TotalPurchased > 250000) max += 0.02;
            max = Clamp(max, 0.03, 0.22);
            var neg = new Negotiation
            {
                Id = NewId("n"), PlayerId = p.Id, SupplierId = s.Id, MaxDiscount = max, RoundsLeft = p.HasSkill(Skills.FastTalker) ? 4 : 3,
                Status = NegotiationStatus.Active, StartedDay = State.Day
            };
            if (p.HasSkill(Skills.Negotiator))
            {
                neg.HintKnown = true;
                neg.HintLow = Math.Max(0, Math.Round((max - Rng.Range(0.01, 0.04)) * 100) / 100);
                neg.HintHigh = Math.Round((max + Rng.Range(0.01, 0.04)) * 100) / 100;
            }
            neg.Transcript.Add($"{s.Name}: \"We value our relationship. What discount did you have in mind?\"");
            State.Negotiations.Add(neg);
            return CmdResult.Success();
        }

        CmdResult CmdNegotiationOffer(Command c, Player p)
        {
            var n = State.Negotiations.Find(x => x.Id == c.A && x.PlayerId == p.Id);
            if (n == null || n.Status != NegotiationStatus.Active) return CmdResult.Fail("No active negotiation.");
            var s = State.GetSupplier(n.SupplierId);
            double ask = Clamp(c.X, 0.005, 0.5);
            bool fast = p.HasSkill(Skills.FastTalker);
            n.Transcript.Add($"You: \"We need {Fmt.Pct(ask, 1)} off.\"");
            if (ask <= n.MaxDiscount + 1e-9)
            {
                n.Status = NegotiationStatus.Accepted;
                n.AgreedDiscount = ask;
                if (ask > s.Discount || State.Day > s.DiscountUntil) s.Discount = ask;
                s.DiscountUntil = State.Day + 150;
                s.Relationship = Math.Min(100, s.Relationship + 3);
                n.Transcript.Add($"{s.Name}: \"Deal. {Fmt.Pct(ask, 1)} off for the next five months.\"");
                p.NegotiationsWon++;
                Award(p.Id, (int)Clamp(30 + ask * 400, 30, 90), $"Negotiated {Fmt.Pct(ask, 1)} off with {s.Name}");
                Log(LogKind.Company, p.Id, $"{p.Name} negotiated a {Fmt.Pct(ask, 1)} discount with {s.Name}.");
                return CmdResult.Success();
            }
            n.RoundsLeft--;
            if (ask <= n.MaxDiscount + (fast ? 0.07 : 0.05))
            {
                n.LastCounter = Math.Max(0.01, Math.Round((n.MaxDiscount - Rng.Range(0, 0.01)) * 1000) / 1000);
                n.Transcript.Add($"{s.Name}: \"Too steep. We could do {Fmt.Pct(n.LastCounter, 1)}.\"");
            }
            else
            {
                s.Relationship = Math.Max(0, s.Relationship - (fast ? 2 : 5));
                if (!fast && ask > n.MaxDiscount + 0.15) n.RoundsLeft--;
                n.Transcript.Add($"{s.Name}: \"That's frankly insulting.\"");
            }
            if (n.RoundsLeft <= 0)
            {
                n.Status = NegotiationStatus.Failed;
                n.Transcript.Add($"{s.Name} ended the call.");
                if (s.Relationship < 30) s.BlockedUntil = State.Day + 10;
                Log(LogKind.Company, p.Id, $"{p.Name}'s negotiation with {s.Name} failed.");
            }
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ marketing

        CmdResult CmdStartCampaign(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            var channel = (AdChannel)Clamp(c.I, 0, 2);
            double budget = Math.Round(Clamp(c.X, 20, 50000));
            int days = Clamp(c.J, 7, 90);
            if (C.Campaigns.Exists(x => x.Active && x.CompanyProductId == cp.Id && x.Channel == channel))
                return CmdResult.Fail($"There is already a {channel} campaign for {cp.Name}.");
            double total = budget * days;
            if (C.Cash < budget * 3) return CmdResult.Fail("The company does not have enough cash to start advertising.");
            if (NeedsApproval(p, c, total, $"{channel} campaign for {cp.Name}: {Fmt.Money(budget)}/day for {days} days", out var ar)) return ar;
            var cam = new Campaign
            {
                Id = NewId("m"), CompanyProductId = cp.Id, Channel = channel, DailyBudget = budget, StartDay = State.Day + 1,
                EndDay = State.Day + days, CreatedBy = p.Id
            };
            C.Campaigns.Add(cam);
            C.Campaigns.RemoveAll(x => !x.Active && State.Day - x.EndDay > 120);
            Log(LogKind.Company, p.Id, $"{p.Name} launched a {channel} campaign for {cp.Name} ({Fmt.Money(budget)}/day, {days} days).");
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ operations

        CmdResult CmdRecruit(Command c, Player p)
        {
            if (!Enum.TryParse(c.A, out EmployeeRole role)) return CmdResult.Fail("Unknown role.");
            if (ActiveTaskCount(p.Id) >= MaxTasks(p)) return CmdResult.Fail("You are already busy with as many tasks as you can handle.");
            if (State.Tasks.Exists(t => t.Kind == TaskKind.Recruit && t.Param == c.A)) return CmdResult.Fail("Someone is already recruiting for that role.");
            double cost = 500;
            if (C.Cash < cost) return CmdResult.Fail("The company cannot afford job ads.");
            Spend(cost, TxType.Hiring, p.Id, $"Job ads: {Catalog.RoleName(role)}");
            int days = p.HasSkill(Skills.Recruiter) ? 1 : 2;
            StartTask(p.Id, TaskKind.Recruit, $"Recruiting a {Catalog.RoleName(role)}", days, c.A);
            return CmdResult.Success($"Recruiting started. Candidates in {Fmt.Days(days)}.");
        }

        CmdResult CmdHire(Command c, Player p)
        {
            var cand = State.Candidates.Find(x => x.Id == c.A);
            if (cand == null) return CmdResult.Fail("That candidate is no longer available.");
            double signing = cand.Salary;
            if (C.Cash < signing) return CmdResult.Fail("The company cannot afford the signing bonus.");
            if (NeedsApproval(p, c, signing * 3, $"Hire {cand.Name} ({Catalog.RoleName(cand.Role)}, {Fmt.Money(cand.Salary)}/mo)", out var ar)) return ar;
            State.Candidates.Remove(cand);
            Spend(signing, TxType.Hiring, p.Id, $"Signing bonus: {cand.Name}");
            var e = new Employee
            {
                Id = NewId("e"), Name = cand.Name, Role = cand.Role, Skill = cand.Skill, Salary = cand.Salary, Morale = 72,
                ManagerId = p.Id, HiredDay = State.Day, LastRaiseDay = State.Day
            };
            C.Employees.Add(e);
            p.Hires++;
            Award(p.Id, cand.Skill >= 4 ? 25 : 10, $"Hired {cand.Name}");
            Log(LogKind.Team, p.Id, $"{p.Name} hired {cand.Name} as {Catalog.RoleName(cand.Role)} ({Fmt.Money(cand.Salary)}/mo).");
            return CmdResult.Success();
        }

        CmdResult CmdFire(Command c, Player p)
        {
            var e = C.Employees.Find(x => x.Id == c.A);
            if (e == null) return CmdResult.Fail("Unknown employee.");
            C.Employees.Remove(e);
            Spend(e.Salary, TxType.Severance, p.Id, $"Severance: {e.Name}");
            foreach (var other in C.Employees) other.Morale = Math.Max(0, other.Morale - 4);
            Log(LogKind.Team, p.Id, $"{p.Name} let {e.Name} go.");
            return CmdResult.Success();
        }

        CmdResult CmdBuyWarehouse(Command c, Player p)
        {
            var def = Catalog.GetWarehouse(c.A);
            if (def == null || def.Id == "garage") return CmdResult.Fail("Unknown warehouse type.");
            if (C.Cash < def.SetupCost) return CmdResult.Fail($"Setting up a {def.Name} costs {Fmt.Money(def.SetupCost)}.");
            if (NeedsApproval(p, c, def.SetupCost, $"Open a {def.Name}", out var ar)) return ar;
            OpenWarehouse(def, p.Id);
            return CmdResult.Success();
        }

        public void OpenWarehouse(Catalog.WarehouseDef def, int actor)
        {
            if (def.SetupCost > 0) Spend(def.SetupCost, TxType.Warehouse, actor, $"Opened {def.Name}");
            C.Warehouses.Add(new Warehouse
            {
                Id = NewId("w"), Tier = def.Id, Name = def.Name, Capacity = def.Capacity, MonthlyCost = def.MonthlyCost,
                FulfillmentBonus = def.FulfillmentBonus, OpenedDay = State.Day, SetupCost = def.SetupCost
            });
            if (actor >= 0) Log(LogKind.Company, actor, $"{State.PlayerName(actor)} opened a {def.Name} (+{Fmt.Int(def.Capacity)} units of storage).");
        }

        // ------------------------------------------------------------------ finance

        public double LoanOfferAmount(string id, out string unavailableReason)
        {
            unavailableReason = null;
            double monthlyRevenue = C.LastMonth.Revenue > 0 ? C.LastMonth.Revenue : C.Month.Revenue;
            switch (id)
            {
                case "credit":
                    return Math.Round(Clamp(monthlyRevenue * 1.5, 25000, 500000) / 1000) * 1000;
                case "growth":
                    if (State.Day < 90) { unavailableReason = "Needs 90 days of operating history."; return 0; }
                    return Math.Round(Clamp(C.Valuation * 0.12, 50000, 2000000) / 1000) * 1000;
                case "venture":
                    if (C.Valuation < 2000000) { unavailableReason = "Needs a valuation above $2M."; return 0; }
                    return Math.Round(Clamp(C.Valuation * 0.25, 250000, 10000000) / 1000) * 1000;
                case "mca":
                    return Math.Round(Clamp(monthlyRevenue * 1.0, 20000, 1000000) / 1000) * 1000;
            }
            unavailableReason = "Unknown loan.";
            return 0;
        }

        public static double MonthlyPayment(double principal, double annualRate, int months)
        {
            double r = annualRate / 12.0;
            if (r <= 0) return principal / months;
            return principal * r / (1 - Math.Pow(1 + r, -months));
        }

        CmdResult CmdTakeLoan(Command c, Player p)
        {
            Catalog.LoanDef def = default;
            bool found = false;
            foreach (var l in Catalog.Loans) if (l.Id == c.A) { def = l; found = true; }
            if (!found) return CmdResult.Fail("Unknown loan.");
            if (C.Loans.Exists(l => l.Lender == def.Lender)) return CmdResult.Fail("We already have that loan.");
            double amount = LoanOfferAmount(def.Id, out string why);
            if (amount <= 0) return CmdResult.Fail(why ?? "Not available.");
            // Taking on debt always needs the partners unless you control the company.
            if (NeedsApproval(p, c, amount, $"Take a {Fmt.Money(amount)} {def.Lender} at {Fmt.Pct(def.Rate)}", out var ar)) return ar;
            TakeLoan(def, amount, p.Id);
            return CmdResult.Success();
        }

        public void TakeLoan(Catalog.LoanDef def, double amount, int actor)
        {
            var loan = new Loan
            {
                Id = NewId("l"), Lender = def.Lender, Principal = amount, Balance = amount, AnnualRate = def.Rate,
                MonthlyPayment = MonthlyPayment(amount, def.Rate, def.Months), MonthsLeft = def.Months, TakenDay = State.Day, TakenBy = actor
            };
            C.Loans.Add(loan);
            C.Cash += amount;
            AddLedger(TxType.LoanIn, amount, actor, $"Loan received: {def.Lender} ({Fmt.Pct(def.Rate)}, {def.Months} months)", null, 0);
            Log(LogKind.Finance, actor, $"{State.PlayerName(actor)} took a {Fmt.Money(amount)} {def.Lender}.");
        }

        CmdResult CmdRepayLoan(Command c, Player p)
        {
            var l = C.Loans.Find(x => x.Id == c.A);
            if (l == null) return CmdResult.Fail("Unknown loan.");
            if (C.Cash < l.Balance) return CmdResult.Fail("Not enough cash to repay it.");
            if (NeedsApproval(p, c, l.Balance, $"Repay the {l.Lender} early", out var ar)) return ar;
            C.Cash -= l.Balance;
            AddLedger(TxType.LoanPayment, -l.Balance, p.Id, $"Repaid {l.Lender} early", null, 0);
            C.Loans.Remove(l);
            Log(LogKind.Finance, p.Id, $"{p.Name} repaid the {l.Lender} early.");
            return CmdResult.Success();
        }

        CmdResult CmdSetPolicy(Command c, Player p)
        {
            if (VotingWeight(p) <= 0.5 && State.Players.Count > 1) return CmdResult.Fail("Company policies change through board proposals.");
            ApplyPolicy(c.A, c.I);
            return CmdResult.Success();
        }

        public string ApplyPolicy(string policy, int value)
        {
            var pol = C.Policies;
            switch (policy)
            {
                case "returns": pol.ReturnPolicy = Clamp(value, 0, 2); return "Return policy: " + (value == 0 ? "strict" : value == 2 ? "generous" : "standard");
                case "audits": pol.SupplierAudits = value != 0; return "Supplier audits " + (value != 0 ? "on" : "off");
                case "openbooks": pol.OpenBooks = value != 0; return "Open books " + (value != 0 ? "on" : "off");
                case "ballot": pol.SecretBallot = value != 0; return "Secret ballot " + (value != 0 ? "on" : "off");
                case "limit":
                    pol.DefaultSpendingLimit = Clamp(value, 5000, 1000000);
                    foreach (var pl in State.Players) pl.SpendingLimit = Math.Max(pl.SpendingLimit, pol.DefaultSpendingLimit + (pl.HasSkill(Skills.Executive) ? 40000 : 0));
                    return "Default spending limit " + Fmt.Money(value);
            }
            return "Unknown policy";
        }

        // ------------------------------------------------------------------ events

        CmdResult CmdClaimEvent(Command c, Player p)
        {
            var e = State.GetEvent(c.A);
            if (e == null || e.Resolved) return CmdResult.Fail("That situation is already handled.");
            if (!EventVisibleTo(e, p.Id)) return CmdResult.Fail("Unknown situation.");
            if (e.ClaimedBy >= 0 && e.ClaimedBy != p.Id && e.ClaimedUntil >= State.Day)
                return CmdResult.Fail($"{State.PlayerName(e.ClaimedBy)} is already handling this.");
            e.ClaimedBy = p.Id;
            e.ClaimedUntil = State.Day + 2;
            return CmdResult.Success();
        }

        public bool EventVisibleTo(GameEvent e, int playerId) => State.Day >= e.PublicDay || e.EarlyAccess.Contains(playerId);

        CmdResult CmdResolveEvent(Command c, Player p)
        {
            var e = State.GetEvent(c.A);
            if (e == null || e.Resolved) return CmdResult.Fail("That situation is already handled.");
            if (!EventVisibleTo(e, p.Id)) return CmdResult.Fail("Unknown situation.");
            if (e.PendingApproval && !c.Approved) return CmdResult.Fail("A decision on this is waiting for partner approval.");
            if (e.ClaimedBy >= 0 && e.ClaimedBy != p.Id && e.ClaimedUntil >= State.Day && !c.Approved)
                return CmdResult.Fail($"{State.PlayerName(e.ClaimedBy)} is handling this one.");
            if (c.I < 0 || c.I >= e.Choices.Count) return CmdResult.Fail("Invalid choice.");
            var ch = e.Choices[c.I];
            if (!string.IsNullOrEmpty(ch.RequiresSkill) && !p.HasSkill(ch.RequiresSkill) && (string.IsNullOrEmpty(ch.RequiresSkillAlt) || !p.HasSkill(ch.RequiresSkillAlt)))
                return CmdResult.Fail($"Requires the {Skills.Get(ch.RequiresSkill)?.Name ?? ch.RequiresSkill} skill.");
            if (ch.Cost > 0 && C.Cash < ch.Cost) return CmdResult.Fail("The company cannot afford that option.");
            if (ch.BusyDays > 0 && ActiveTaskCount(p.Id) >= MaxTasks(p)) return CmdResult.Fail("That option needs your personal time, and you are fully booked.");
            if (ch.Cost > 0 && NeedsApproval(p, c, ch.Cost, $"{e.Title}: {ch.Label}", out var ar))
            {
                if (ar.Ok)
                {
                    e.PendingApproval = true;
                    e.DeadlineDay = Math.Max(e.DeadlineDay, State.Day + 6);
                }
                return ar;
            }
            e.PendingApproval = false;
            ResolveEventChoice(e, c.I, p);
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ career & personal money

        CmdResult CmdUnlockSkill(Command c, Player p)
        {
            if (!CanUnlock(p, c.A, out string why)) return CmdResult.Fail(why);
            var def = Skills.Get(c.A);
            p.Skills.Add(def.Id);
            p.SkillPoints--;
            if (def.Id == Skills.Executive) p.SpendingLimit += 40000;
            if (!def.Hidden) Log(LogKind.Personal, p.Id, $"{p.Name} learned {def.Name}.");
            return CmdResult.Success($"You learned {def.Name}.");
        }

        CmdResult CmdPersonalInvest(Command c, Player p)
        {
            var a = Catalog.GetAsset(c.A);
            if (a == null) return CmdResult.Fail("Unknown investment.");
            if (a.InvestorOnly && !p.HasSkill(Skills.Investor)) return CmdResult.Fail("Requires the Investor skill.");
            var pr = Priv(p.Id);
            double amount = Math.Round(c.X);
            if (amount < 500) return CmdResult.Fail("Minimum investment is $500.");
            if (amount > pr.Cash) return CmdResult.Fail("You don't have that much personal cash.");
            pr.Cash -= amount;
            var h = pr.Holdings.Find(x => x.AssetId == a.Id);
            if (h == null) { h = new Holding { AssetId = a.Id, BoughtDay = State.Day }; pr.Holdings.Add(h); }
            h.Value += amount;
            h.Invested += amount;
            return CmdResult.Success($"Invested {Fmt.Money(amount)} in {a.Name}.");
        }

        CmdResult CmdPersonalSell(Command c, Player p)
        {
            var a = Catalog.GetAsset(c.A);
            var pr = Priv(p.Id);
            var h = pr.Holdings.Find(x => x.AssetId == c.A);
            if (a == null || h == null || h.Value <= 0) return CmdResult.Fail("You don't own that.");
            double amount = c.X <= 0 || c.X >= h.Value * 0.999 ? h.Value : c.X;
            double fraction = amount / h.Value;
            double proceeds = amount * (1 - a.SellFee);
            h.Invested -= h.Invested * fraction;
            h.Value -= amount;
            pr.Cash += proceeds;
            pr.Holdings.RemoveAll(x => x.Value < 1);
            return CmdResult.Success($"Sold {a.Name} for {Fmt.Money(proceeds)}.");
        }

        CmdResult CmdStartInvestigation(Command c, Player p)
        {
            if (ActiveTaskCount(p.Id) >= MaxTasks(p)) return CmdResult.Fail("You are already busy with as many tasks as you can handle.");
            if (State.Tasks.Exists(t => t.PlayerId == p.Id && t.Kind == TaskKind.Investigate)) return CmdResult.Fail("You are already investigating.");
            StartTask(p.Id, TaskKind.Investigate, "Quietly reviewing the books", 3, null, 0, true);
            return CmdResult.Success("You start going through the books. Quietly.");
        }
    }
}
