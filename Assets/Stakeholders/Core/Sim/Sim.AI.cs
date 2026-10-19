using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// AI partners. They act through the same Command pipeline as humans, so they obey every rule
    /// a human does (spending limits, approvals, task slots) and can be swapped in for absent players.
    /// </summary>
    public partial class GameSimulation
    {
        // ------------------------------------------------------------------ memory helpers

        AIMemory Mem(Player ai)
        {
            var pr = Priv(ai.Id);
            if (pr.AI == null) pr.AI = new AIMemory();
            return pr.AI;
        }

        public double GetTrust(Player ai, int otherId)
        {
            var pr = Priv(ai.Id);
            if (pr?.AI == null) return 50;
            return pr.AI.Trust.TryGetValue(otherId, out double v) ? v : 50;
        }

        public void AdjustTrust(Player ai, int otherId, double delta)
        {
            if (ai == null || !ai.IsAI || ai.Id == otherId) return;
            var m = Mem(ai);
            m.Trust[otherId] = Clamp(GetTrust(ai, otherId) + delta, 0, 100);
        }

        CmdResult AICmd(Player ai, CommandType type, string a = null, string b = null, int i = 0, int j = 0, double x = 0, double y = 0, bool flag = false, string text = null)
        {
            return Execute(new Command { Type = type, PlayerId = ai.Id, A = a, B = b, I = i, J = j, X = x, Y = y, Flag = flag, Text = text });
        }

        /// <summary>Cash the AI tries to keep untouched for discretionary spending.</summary>
        double CashReserve(Traits t) => MonthlyObligations() * (0.6 + (1 - t.Risk) * 0.6);

        double TotalSales7()
        {
            double s = 0;
            foreach (var cp in C.Products) if (cp.Active) s += cp.Sales7;
            return s;
        }

        bool HiredRecently()
        {
            foreach (var e in C.Employees) if (State.Day - e.HiredDay < 4) return true;
            return false;
        }

        double AvgPerformance()
        {
            if (State.Players.Count == 0) return 1;
            double s = 0;
            foreach (var p in State.Players) s += Math.Max(1, p.Performance);
            return s / State.Players.Count;
        }

        double AvgSalary()
        {
            if (State.Players.Count == 0) return 1;
            double s = 0;
            foreach (var p in State.Players) s += Math.Max(500, p.Salary);
            return s / State.Players.Count;
        }

        static int StableHash(string s)
        {
            int h = 17;
            if (s != null) foreach (char ch in s) h = h * 31 + ch;
            return h & 0x7fffffff;
        }

        // ------------------------------------------------------------------ daily loop

        void AIDaily()
        {
            foreach (var ai in State.Players.ToArray())
            {
                if (!ai.IsAI) continue;
                var t = Traits.For(ai.Personality);
                var mem = Mem(ai);
                AISpendSkillPoints(ai, t);
                AIRespondOffers(ai, t);
                AIRespondShareOffers(ai, t);
                AIHandleEvidence(ai, t, mem);
                if (State.Phase != GamePhase.Running) return;
                if (IsAway(ai)) continue;

                bool urgent = false;
                foreach (var e in State.Events)
                    if (!e.Resolved && e.DeadlineDay <= State.Day + 1 && EventVisibleTo(e, ai.Id)) { urgent = true; break; }
                mem.ActionBudget += t.Diligence * 0.9 + (urgent ? 0.25 : 0);
                int guard = 0;
                while (mem.ActionBudget >= 1 && guard++ < 3)
                {
                    mem.ActionBudget -= 1;
                    if (!AIDoAction(ai, t)) break;
                    if (State.Phase != GamePhase.Running) return;
                }
                if (mem.ActionBudget > 3) mem.ActionBudget = 3;
                AIPersonalFinance(ai, t);
                AIShareInitiative(ai, t, mem);
                AIChatter(ai, t, mem);
            }
        }

        struct AIOption
        {
            public double Score;
            public Func<bool> Run;
        }

        bool AIDoAction(Player ai, Traits t)
        {
            var options = new List<AIOption>();
            double cash = C.Cash;
            double reserve = CashReserve(t);
            double free = cash - reserve;
            // AI partners only work in the areas they are responsible for.
            bool buying = AIHasRole(ai, Roles.Buying), marketing = AIHasRole(ai, Roles.Marketing), research = AIHasRole(ai, Roles.Research);
            bool hiring = AIHasRole(ai, Roles.Hiring), situations = AIHasRole(ai, Roles.Situations), upgrades = AIHasRole(ai, Roles.Upgrades);

            // 1. Situations
            foreach (var e in State.Events.ToArray())
            {
                if (e.Resolved || e.PendingApproval || !EventVisibleTo(e, ai.Id)) continue;
                if (e.ClaimedBy >= 0 && e.ClaimedBy != ai.Id && e.ClaimedUntil >= State.Day) continue;
                int daysLeft = e.DeadlineDay - State.Day;
                // Anyone steps in to stop a crisis on its last day; otherwise only the partner responsible.
                bool lastMinuteCrisis = e.Severity >= 3 && daysLeft <= 0;
                if (!situations && !lastMinuteCrisis) continue;
                // Give human partners the first shot: AI waits for part of the deadline unless it's urgent.
                int age = State.Day - e.CreatedDay;
                int patience = HumanCount == 0 ? 0 : Math.Max(1, (e.DeadlineDay - e.CreatedDay) * (ai.Personality == Personality.Workaholic ? 35 : 55) / 100);
                if (daysLeft > 1 && age < patience && e.ClaimedBy != ai.Id) continue;
                int choice = AIChooseEventChoice(ai, t, e);
                if (choice < 0) continue;
                double score = (daysLeft <= 1 ? 80 : 45 - daysLeft * 2) + e.Severity * 9;
                var ev = e;
                options.Add(new AIOption { Score = score, Run = () => AICmd(ai, CommandType.ResolveEvent, ev.Id, null, choice).Ok });
            }

            // 2. Restock (anyone steps in when a product has been sold out for days and nobody reordered)
            foreach (var cp in C.Products)
            {
                if (!cp.Active || IncomingUnits(cp.Id) > 0 || cp.AutoRestock) continue;
                bool neglected = cp.Stock == 0 && cp.DaysNoSales >= 5 && cp.Demand7 > 1;
                if (!buying && !neglected) continue;
                var s = AIPickSupplier(ai, cp);
                if (s == null) continue;
                double days = cp.Sales7 > 0.3 ? cp.Stock / cp.Sales7 : (cp.Stock == 0 ? 0 : 999);
                int owed = OutstandingContractUnits(cp.Id);
                if (days >= s.LeadTime + 7 && cp.Stock >= owed) continue;
                var prod = cp;
                double score = 70 + (cp.ManagerId == ai.Id ? 15 : 0) - Math.Min(days, 60) + (owed > cp.Stock ? 10 : 0);
                options.Add(new AIOption { Score = score, Run = buying ? () => AIRestock(ai, t, prod, s) : NeglectRun(ai, $"Nobody was restocking {prod.Name}, so I ordered more. Someone should own purchasing.", () => AIRestock(ai, t, prod, s)) });
            }

            // 3. Kickback obligations (quietly; only possible for whoever controls purchasing)
            if (buying)
                foreach (var o in State.Offers)
                {
                    if (o.Kind != OfferKind.Kickback || o.Status != OfferStatus.Accepted || o.TargetId != ai.Id) continue;
                    var cp = State.GetCompanyProductByMarketId(o.ProductId);
                    var s = State.GetSupplier(o.SupplierId);
                    if (cp == null || s == null) continue;
                    var deal = o;
                    options.Add(new AIOption { Score = 58, Run = () => AICmd(ai, CommandType.OrderInventory, cp.Id, s.Id, deal.Quantity).Ok });
                }

            int active = C.Products.FindAll(x => x.Active).Count;
            int desired = Clamp(3 + State.Day / 60, 3, 16);
            int lastLaunch = 0;
            foreach (var cp in C.Products) lastLaunch = Math.Max(lastLaunch, cp.LaunchedDay);
            // A shrinking catalog nobody is growing: step in.
            bool catalogNeglected = !research && active < 3 && State.Day - lastLaunch > 45;
            if (research || catalogNeglected)
            {
                // 4. Launch a product. A tiny catalog is the real danger, so it launches even on thin cash.
                bool smallCatalog = active < 4;
                if (active < desired && (free > 2000 || (smallCatalog && C.Cash > 6000)))
                {
                    var best = AIBestDiscovery(ai, out double est);
                    if (best != null && est > 60)
                        options.Add(new AIOption
                        {
                            Score = 52 + Math.Min(20, est / 50) + (smallCatalog ? 10 : 0),
                            Run = research ? () => AILaunch(ai, t, best) : NeglectRun(ai, "We only sell a couple of products and nobody is launching new ones, so I did. We need someone on research.", () => AILaunch(ai, t, best))
                        });
                }
                // 5. Research (only when the pipeline of ideas is thin)
                if (ActiveTaskCount(ai.Id) < MaxTasks(ai) && !State.Tasks.Exists(x => x.Kind == TaskKind.Research && State.GetPlayer(x.PlayerId).IsAI))
                {
                    var pr = Priv(ai.Id);
                    int fresh = pr.Notebook.FindAll(d => !d.Launched && State.Day - d.Day < 60 && State.GetCompanyProductByMarketId(d.ProductId) == null).Count;
                    fresh += C.ResearchBoard.FindAll(d => !d.Launched && State.Day - d.Day < 60 && State.GetCompanyProductByMarketId(d.ProductId) == null).Count;
                    if (active < desired && fresh < 1 && (free > 8000 || (smallCatalog && C.Cash > ResearchCost(0) * 3)))
                        options.Add(new AIOption { Score = 42 + (smallCatalog ? 10 : 0), Run = () => AICmd(ai, CommandType.StartResearch, AIResearchCategory(), null, C.Cash > 150000 && t.Risk > 0.4 ? 1 : 0).Ok });
                }
            }
            if (research)
            {
                // Product R&D on the best seller
                CompanyProduct hero = null;
                foreach (var cp in C.Products)
                    if (cp.Sales7 >= 8 && CanUpgradeProduct(cp, out _) && (hero == null || cp.Sales7 * cp.Price > hero.Sales7 * hero.Price)) hero = cp;
                if (hero != null && ProductUpgradeCost(hero) < free * 0.4)
                {
                    var h = hero;
                    options.Add(new AIOption { Score = 34 + t.Ambition * 10, Run = () => AICmd(ai, CommandType.UpgradeProduct, h.Id).Ok });
                }
            }
            // Unmanaged products (like the starter stock) get claimed, after giving the humans a fair chance.
            foreach (var cp in C.Products)
            {
                if (!cp.Active || cp.ManagerId >= 0) continue;
                if (HumanCount > 0 && State.Day < (research ? 6 : 12)) continue;
                var prod = cp;
                options.Add(new AIOption { Score = 40 + t.Ambition * 10, Run = () => AICmd(ai, CommandType.ClaimProduct, prod.Id).Ok });
                break;
            }

            if (marketing)
            {
                // 6. Campaigns
                foreach (var cam in C.Campaigns)
                {
                    if (!cam.Active || State.Day - cam.StartDay < 7 || cam.Spent7 <= 0) continue;
                    if (cam.Roas7 < 1.6)
                    {
                        var c1 = cam;
                        options.Add(new AIOption { Score = 55, Run = () => AICmd(ai, CommandType.StopCampaign, c1.Id).Ok });
                    }
                }
                if (free > 15000)
                {
                    foreach (var cp in C.Products)
                    {
                        if (!cp.Active || cp.Sales7 < 3 || C.Campaigns.Exists(x => x.Active && x.CompanyProductId == cp.Id)) continue;
                        double margin = cp.Price - cp.AvgCost - cp.Price * FeeRate() - ShippingPerUnit;
                        if (margin < cp.Price * 0.25) continue;
                        if (cp.Sales7 > 0 && cp.Stock / cp.Sales7 < 20 && IncomingUnits(cp.Id) == 0) continue;
                        var prod = cp;
                        options.Add(new AIOption { Score = 28 + t.Risk * 15, Run = () => AIStartCampaign(ai, t, prod) });
                        break;
                    }
                }
                // Promotions: clear overstock, and push hard in the holiday season.
                bool holidays = State.MonthIndex >= 10;
                foreach (var cp in C.Products)
                {
                    if (!cp.Active || cp.PromoActive(State.Day) || State.Day < cp.PromoCooldownUntil || cp.Stock < 50 || cp.Sales7 < 1) continue;
                    double days = cp.Stock / Math.Max(0.1, cp.Sales7);
                    var prod = cp;
                    if (days > 70 || (holidays && days > 25))
                        options.Add(new AIOption { Score = 30 + (holidays ? 8 : 0) + Math.Min(10, days / 20), Run = () => AICmd(ai, CommandType.StartPromotion, prod.Id, null, 0).Ok });
                    else if (days > 45 && cp.Demand7 < cp.Sales7 * 1.05)
                        options.Add(new AIOption { Score = 22, Run = () => AICmd(ai, CommandType.StartPromotion, prod.Id, null, 1).Ok });
                }
                // 10. Pricing
                foreach (var cp in C.Products)
                {
                    if (!cp.Active || cp.SalesHistory.Count < 14 || cp.PromoActive(State.Day)) continue;
                    var mp = State.GetMarketProduct(cp.ProductId);
                    double days = cp.Sales7 > 0.2 ? cp.Stock / cp.Sales7 : 999;
                    double refP = mp.RefPrice * (1 + 0.1 * (cp.Version - 1));
                    if (days > 120 && cp.Price > refP * 0.8)
                    {
                        var prod = cp;
                        options.Add(new AIOption { Score = 22, Run = () => AICmd(ai, CommandType.SetPrice, prod.Id, null, 0, 0, Math.Round(prod.Price * 0.93, 2)).Ok });
                    }
                    else if (cp.StockoutDays > 3 && cp.Stock < cp.Sales7 * 5 && cp.Price < refP * 1.35)
                    {
                        var prod = cp;
                        options.Add(new AIOption
                        {
                            Score = 20,
                            Run = () => { prod.StockoutDays = 0; return AICmd(ai, CommandType.SetPrice, prod.Id, null, 0, 0, Math.Round(prod.Price * 1.06, 2)).Ok; }
                        });
                    }
                }
            }

            // 7. Hiring: judged on a week of orders against live capacity, one hire at a time.
            double orders = TotalSales7();
            double capacity = FulfillmentCapacity();
            double support = SupportCapacity();
            bool canHire = hiring && !HiredRecently();
            double monthlyRevenue = Math.Max(C.Month.Revenue, C.LastMonth.Revenue);
            // Orders piling up in the warehouse with nobody hiring: step in.
            if (!hiring && !HiredRecently() && C.Backlog > capacity * 1.5)
            {
                var cand = State.Candidates.FindAll(x => x.Role == EmployeeRole.Warehouse);
                if (cand.Count > 0)
                {
                    var hire = cand[0];
                    options.Add(new AIOption { Score = 55, Run = NeglectRun(ai, "Orders are piling up and nobody is hiring, so I brought in warehouse help.", () => AICmd(ai, CommandType.Hire, hire.Id).Ok) });
                }
            }
            if (hiring)
            {
                if (canHire && (orders > capacity * 0.8 || C.Backlog > capacity * 0.5))
                    AddHiringOption(options, ai, EmployeeRole.Warehouse, 60);
                if (canHire && orders > support * 0.9)
                    AddHiringOption(options, ai, EmployeeRole.Support, 50);
                // Overstaffed for a long time: let someone go.
                var warehouseStaff = C.Employees.FindAll(x => x.Role == EmployeeRole.Warehouse);
                if (warehouseStaff.Count >= 2 && orders < (capacity - 90) * 0.45 && State.Day > 60)
                {
                    warehouseStaff.Sort((a, b) => a.Skill.CompareTo(b.Skill));
                    var worst = warehouseStaff[0];
                    if (State.Day - worst.HiredDay > 45)
                        options.Add(new AIOption { Score = t.Loyalty * 20 + (1 - t.Risk) * 15, Run = () => AICmd(ai, CommandType.Fire, worst.Id).Ok });
                }
                var supportStaff = C.Employees.FindAll(x => x.Role == EmployeeRole.Support);
                if (supportStaff.Count >= 2 && orders < (support - 150) * 0.4 && State.Day > 60)
                {
                    supportStaff.Sort((a, b) => a.Skill.CompareTo(b.Skill));
                    var worst = supportStaff[0];
                    if (State.Day - worst.HiredDay > 45)
                        options.Add(new AIOption { Score = t.Loyalty * 18 + (1 - t.Risk) * 12, Run = () => AICmd(ai, CommandType.Fire, worst.Id).Ok });
                }
                // Specialists are a luxury when the company is bleeding.
                if (C.LastQuarter.NetProfit < 0 && C.Cash < MonthlyObligations() && State.Day > 120)
                {
                    var specialist = C.Employees.Find(x => x.Role != EmployeeRole.Warehouse && x.Role != EmployeeRole.Support && State.Day - x.HiredDay > 30);
                    if (specialist != null)
                        options.Add(new AIOption { Score = 30 + (1 - t.Risk) * 15, Run = () => AICmd(ai, CommandType.Fire, specialist.Id).Ok });
                }
                if (canHire && C.Employees.Count >= 4)
                {
                    double adSpend = 0;
                    foreach (var cam in C.Campaigns) if (cam.Active) adSpend += cam.DailyBudget;
                    double purchases30 = Purchases30();
                    if (adSpend > 1500 && C.Employees.FindAll(x => x.Role == EmployeeRole.Marketing).Count < (int)(adSpend / 1500)) AddHiringOption(options, ai, EmployeeRole.Marketing, 30);
                    if (purchases30 > 120000 && C.Employees.FindAll(x => x.Role == EmployeeRole.Buyer).Count < (int)(purchases30 / 120000)) AddHiringOption(options, ai, EmployeeRole.Buyer, 28);
                    if (monthlyRevenue > 500000 && !C.Employees.Exists(x => x.Role == EmployeeRole.Accountant)) AddHiringOption(options, ai, EmployeeRole.Accountant, 24);
                    if (monthlyRevenue > 200000 && active >= 6 && !C.Employees.Exists(x => x.Role == EmployeeRole.Analyst)) AddHiringOption(options, ai, EmployeeRole.Analyst, 20);
                }
            }

            if (upgrades)
            {
                // 8. Warehouse space
                int capNow = StorageCapacity();
                double used = StockOnHand() + IncomingUnits();
                if (used > capNow * 0.78)
                {
                    // The smallest building that solves the problem: fixed costs kill young companies.
                    int smalls = C.Warehouses.FindAll(w => w.Tier == "small").Count;
                    string tier = smalls < 3 && used < 30000 ? "small" : capNow < 150000 ? "regional" : "mega";
                    var def = Catalog.GetWarehouse(tier);
                    if (free > def.SetupCost * 0.8 && !State.Board.Agenda.Exists(x => x.Type == ProposalType.Warehouse) && !State.Approvals.Exists(a => a.Status == ProposalStatus.Pending && a.Command.Type == CommandType.BuyWarehouse))
                        options.Add(new AIOption { Score = 50, Run = () => AICmd(ai, CommandType.BuyWarehouse, def.Id).Ok });
                }
                // 11. Company upgrades: the best-fitting one the business clearly earns enough to pay for.
                // Never while cash is tight or expensive debt is outstanding: stock comes first.
                double monthlyGross = Math.Max(C.Month.GrossProfit, C.LastMonth.GrossProfit);
                bool healthy = State.Day >= 25 && C.Cash > MonthlyObligations() * 2.5 && !C.Loans.Exists(l => l.AnnualRate > 0.2);
                Catalog.UpgradeDef bestUp = null;
                double bestFit = 0;
                foreach (var u in Catalog.Upgrades)
                {
                    if (!healthy) break;
                    if (C.Has(u.Id) || IsBuilding(u.Id) || !UpgradeUnlocked(u, ai, out _)) continue;
                    if (free < u.Cost * (2.2 - t.Risk * 0.6) || u.Cost > monthlyGross * (1.5 + t.Risk)) continue;
                    double fit = AIUpgradeFit(u.Id, t);
                    if (fit > bestFit) { bestFit = fit; bestUp = u; }
                }
                if (bestUp != null && C.Building.Count < 2 && !State.Approvals.Exists(a => a.Status == ProposalStatus.Pending && a.Command.Type == CommandType.BuyUpgrade))
                {
                    var up = bestUp;
                    options.Add(new AIOption { Score = 20 + bestFit * 22, Run = () => AICmd(ai, CommandType.BuyUpgrade, up.Id).Ok });
                }
            }

            // 9. Negotiate
            if (buying && !State.Negotiations.Exists(n => n.PlayerId == ai.Id && n.Status == NegotiationStatus.Active))
            {
                foreach (var s in State.Suppliers)
                {
                    if (s.TotalPurchased < 15000 || (s.Discount > 0 && State.Day <= s.DiscountUntil) || State.Day < s.BlockedUntil || !CanUseSupplier(s, ai.Id)) continue;
                    var sup = s;
                    options.Add(new AIOption { Score = 24 + (ai.HasSkill(Skills.Negotiator) ? 15 : 0), Run = () => AINegotiate(ai, t, sup) });
                    break;
                }
            }

            // 12. Share research the AI won't use
            if (t.Loyalty > 0.6)
            {
                var pr = Priv(ai.Id);
                foreach (var d in pr.Notebook)
                {
                    if (d.Shared || d.Launched || State.Day - d.Day < 5) continue;
                    var disc = d;
                    options.Add(new AIOption { Score = 12, Run = () => AICmd(ai, CommandType.ShareDiscovery, disc.ProductId).Ok });
                    break;
                }
            }

            if (options.Count == 0) return false;
            // Pick the best with some personality noise.
            AIOption pick = options[0];
            double bestScore = double.MinValue;
            foreach (var o in options)
            {
                double s = o.Score + Rng.Range(-8, 8);
                if (s > bestScore) { bestScore = s; pick = o; }
            }
            try { return pick.Run(); }
            catch { return false; }
        }

        /// <summary>Wraps an action an AI takes outside its responsibilities, and tells the humans about it (at most every few weeks).</summary>
        Func<bool> NeglectRun(Player ai, string message, Func<bool> action)
        {
            return () =>
            {
                bool ok = action();
                var mem = Mem(ai);
                if (ok && HumanCount > 0 && State.Day - mem.LastNeglectDay >= 20)
                {
                    mem.LastNeglectDay = State.Day;
                    Say(ai.Id, message);
                }
                return ok;
            };
        }

        void AddHiringOption(List<AIOption> options, Player ai, EmployeeRole role, double score)
        {
            var cand = State.Candidates.FindAll(c => c.Role == role);
            if (cand.Count > 0)
            {
                cand.Sort((a, b) => (b.ShownSkillHigh + b.ShownSkillLow).CompareTo(a.ShownSkillHigh + a.ShownSkillLow));
                var best = cand[0];
                options.Add(new AIOption { Score = score, Run = () => AICmd(ai, CommandType.Hire, best.Id).Ok });
            }
            else if (!State.Tasks.Exists(x => x.Kind == TaskKind.Recruit && x.Param == role.ToString()) && ActiveTaskCount(ai.Id) < MaxTasks(ai))
            {
                options.Add(new AIOption { Score = score - 8, Run = () => AICmd(ai, CommandType.Recruit, role.ToString()).Ok });
            }
        }

        double Purchases30()
        {
            double s = 0;
            foreach (var tx in C.Ledger) if (tx.Type == TxType.Inventory && tx.Amount < 0 && State.Day - tx.Day < 30) s -= tx.Amount;
            return s;
        }

        /// <summary>Units still owed on open contracts for a product.</summary>
        public int OutstandingContractUnits(string companyProductId)
        {
            int n = 0;
            foreach (var k in C.Contracts) if (k.Status == 0 && k.CompanyProductId == companyProductId) n += k.Quantity;
            return n;
        }

        /// <summary>How much an AI partner wants a company upgrade right now (0 = not at all, ~1 = clearly worth it).</summary>
        double AIUpgradeFit(string id, Traits t)
        {
            var u = Catalog.GetUpgrade(id);
            if (u == null) return 0;
            double monthlyRevenue = Math.Max(C.Month.Revenue, C.LastMonth.Revenue);
            // Payback sanity: an upgrade should cost no more than a few months of revenue.
            double affordability = Clamp(monthlyRevenue * 4 / Math.Max(1, u.Cost), 0, 1.5);
            if (affordability < 0.35 && u.Cost > 20000) return 0;
            double load = C.CapacityPerDay > 0 ? TotalSales7() / C.CapacityPerDay : 0;
            double supportLoad = C.SupportPerDay > 0 ? TotalSales7() / C.SupportPerDay : 0;
            double storage = (StockOnHand() + IncomingUnits()) / Math.Max(1.0, StorageCapacity());
            int active = C.Products.FindAll(x => x.Active).Count;
            double adSpend = 0;
            foreach (var cam in C.Campaigns) if (cam.Active) adSpend += cam.DailyBudget;
            double fit;
            switch (id)
            {
                case "log1": fit = storage > 0.6 ? 1.2 : 0.6; break;
                case "log2": case "log4": fit = load > 0.6 ? 1.3 : load > 0.4 ? 0.6 : 0.1; break;
                case "log3": fit = 0.8 + (C.Satisfaction < 70 ? 0.3 : 0); break;
                case "brd1": fit = C.Reputation < 70 ? 1.1 : 0.6; break;
                case "brd2": fit = adSpend > 300 ? 1.2 : 0.3; break;
                case "brd3": fit = 0.9; break;
                case "brd4": fit = 0.8 + t.Risk * 0.3; break;
                case "src1": fit = 0.9; break;
                case "src2": case "src3": case "src4": fit = Purchases30() > u.Cost * 0.5 ? 1.2 : 0.5; break;
                case "chn1": fit = 1.3; break;
                case "chn2": fit = 0.9 + t.Risk * 0.2; break;
                case "chn3": fit = 0.7; break;
                case "chn4": fit = 1.0; break;
                case "dat1": fit = active < 8 ? 0.9 : 0.5; break;
                case "dat2": fit = active >= 5 ? 1.0 : 0.4; break;
                case "dat3": fit = t.Honesty > 0.6 ? 0.9 : 0.2; break;
                case "dat4": fit = 0.9; break;
                case "ppl1": fit = supportLoad > 0.6 ? 1.2 : 0.4; break;
                case "ppl2": fit = C.Employees.Count >= 6 ? 1.0 : 0.3; break;
                case "ppl3": fit = C.Employees.Count >= 10 ? 0.9 : 0.2; break;
                case "ppl4": fit = 0.8 + t.Ambition * 0.2; break;
                case "treasury": fit = C.Cash > 600000 ? 0.8 : 0; break;
                case "supplierstake": fit = monthlyRevenue > 300000 ? 1 : 0; break;
                default: fit = 0.5; break;
            }
            // Conservative partners hesitate on big-ticket items.
            if (u.Cost > C.Cash * 0.3) fit *= 0.5 + t.Risk * 0.6;
            return fit * Math.Min(1, affordability);
        }

        string AIResearchCategory()
        {
            if (Rng.Chance(0.4)) return "Any";
            // Prefer categories we are not yet selling much of.
            var counts = new Dictionary<string, int>();
            foreach (var c in Catalog.Categories) counts[c] = 0;
            foreach (var cp in C.Products) if (cp.Active && counts.ContainsKey(cp.Category)) counts[cp.Category]++;
            return Rng.PickWeighted(Catalog.Categories, c => 1.0 / (1 + counts[c]));
        }

        Supplier AIPickSupplier(Player ai, CompanyProduct cp)
        {
            var mp = State.GetMarketProduct(cp.ProductId);
            foreach (var o in State.Offers)
                if (o.Kind == OfferKind.Kickback && o.Status == OfferStatus.Accepted && o.TargetId == ai.Id && o.ProductId == cp.ProductId)
                {
                    var ks = State.GetSupplier(o.SupplierId);
                    if (ks != null && CanUseSupplier(ks, ai.Id)) return ks;
                }
            Supplier best = null;
            double bestScore = double.MaxValue;
            int qty = Math.Max(300, (int)(cp.Sales7 * 40));
            foreach (var s in State.Suppliers)
            {
                if (!s.Serves(mp.Category) || !CanUseSupplier(s, ai.Id) || State.Day < s.BlockedUntil) continue;
                double urgency = cp.Stock < cp.Sales7 * 4 ? s.LeadTime * 0.02 : 0;
                double score = UnitPrice(s, mp, Math.Max(qty, s.Moq), ai.Id) * (1.25 - s.Quality * 0.4) * (1 + urgency);
                if (score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        bool AIRestock(Player ai, Traits t, CompanyProduct cp, Supplier s)
        {
            var mp = State.GetMarketProduct(cp.ProductId);
            double rate = cp.Sales7 > 0.3 ? cp.Sales7 : Math.Max(3, cp.Demand7 * 0.8);
            double want = rate * (s.LeadTime + 28 + t.Risk * 25) + OutstandingContractUnits(cp.Id) - cp.Stock;
            int qty = (int)(Math.Ceiling(Math.Max(s.Moq, want) / 50.0) * 50);
            int space = StorageCapacity() - StockOnHand() - IncomingUnits();
            if (space < s.Moq) return false;
            if (qty > space) qty = space / 50 * 50;
            double unit = UnitPrice(s, mp, qty, ai.Id);
            // Restocking proven sellers is the company's lifeblood: it may use most of the cash.
            bool proven = cp.Sales7 >= 3 && cp.Profit > 0;
            double keep = MonthlyObligations() * ((proven ? 0.3 : 0.6) + (State.DayOfMonth >= 18 ? 0.5 : 0));
            double budget = Math.Max(0, C.Cash - keep) * (proven ? 0.55 + t.Risk * 0.3 : 0.3 + t.Risk * 0.25);
            if (unit * qty > budget) qty = (int)(budget / unit / 50) * 50;
            if (qty < s.Moq) return false;
            bool rush = cp.Stock == 0 && cp.Sales7 > 20 && t.Risk > 0.6;
            return AICmd(ai, CommandType.OrderInventory, cp.Id, s.Id, qty, 0, 0, 0, rush).Ok;
        }

        Discovery AIBestDiscovery(Player ai, out double estDailyProfit)
        {
            estDailyProfit = 0;
            Discovery best = null;
            var candidates = new List<Discovery>(Priv(ai.Id).Notebook);
            candidates.AddRange(C.ResearchBoard);
            foreach (var d in candidates)
            {
                if (d.Launched || State.GetCompanyProductByMarketId(d.ProductId) != null) continue;
                if (State.Day - d.Day > 90) continue;
                double margin = d.RefPrice * (1 - 0.14) - d.EstCost - ShippingPerUnit;
                double est = d.DemandMid * 0.7 * margin;
                if (d.PhaseKnown && (d.Phase == TrendPhase.Declining || d.Phase == TrendPhase.Dormant)) est *= 0.5;
                if (d.PhaseKnown && (d.Phase == TrendPhase.Emerging || d.Phase == TrendPhase.Rising)) est *= 1.4;
                if (est > estDailyProfit) { estDailyProfit = est; best = d; }
            }
            return best;
        }

        bool AILaunch(Player ai, Traits t, Discovery d)
        {
            var mp = State.GetMarketProduct(d.ProductId);
            double budget = Math.Max(0, C.Cash - CashReserve(t)) * (0.3 + t.Risk * 0.25);
            // With only a handful of products, a small test order is worth the squeeze.
            if (C.Products.FindAll(x => x.Active).Count < 4) budget = Math.Max(budget, C.Cash * 0.35);
            int space = StorageCapacity() - StockOnHand() - IncomingUnits();
            // Best-value supplier we can actually afford a first order from (small test orders are fine).
            var suppliers = State.Suppliers.FindAll(s => s.Serves(mp.Category) && CanUseSupplier(s, ai.Id) && State.Day >= s.BlockedUntil);
            suppliers.Sort((a, b) =>
                (UnitPrice(a, mp, Math.Max(500, a.Moq), ai.Id) * (1.25 - a.Quality * 0.4) + a.LeadTime * 0.02)
                .CompareTo(UnitPrice(b, mp, Math.Max(500, b.Moq), ai.Id) * (1.25 - b.Quality * 0.4) + b.LeadTime * 0.02));
            Supplier best = null;
            foreach (var s in suppliers)
                if (s.Moq <= space && UnitPrice(s, mp, s.Moq, ai.Id) * s.Moq <= budget) { best = s; break; }
            if (best == null) return false;
            double want = d.DemandMid * 0.5 * (best.LeadTime + 25) * (0.7 + t.Risk * 0.6);
            int qty = (int)(Math.Ceiling(Math.Max(best.Moq, want) / 50.0) * 50);
            if (qty > space) qty = space / 50 * 50;
            double unit = UnitPrice(best, mp, qty, ai.Id);
            if (unit * qty > budget) qty = (int)(budget / unit / 10) * 10;
            if (qty < best.Moq) return false;
            double price = Math.Round(mp.RefPrice * Rng.Range(0.95, 1.05), 2);
            return AICmd(ai, CommandType.LaunchProduct, mp.Id, best.Id, qty, 0, price).Ok;
        }

        bool AIStartCampaign(Player ai, Traits t, CompanyProduct cp)
        {
            var mp = State.GetMarketProduct(cp.ProductId);
            double budget = Math.Round(mp.RefPrice * (1.2 + t.Risk * 1.8));
            int days = Rng.Range(21, 35);
            var channel = t.Risk > 0.6 || ai.HasSkill(Skills.ViralMarketing) ? AdChannel.Social : (C.LifetimeUnits > 5000 && Rng.Chance(0.3) ? AdChannel.Email : AdChannel.Search);
            return AICmd(ai, CommandType.StartCampaign, cp.Id, null, (int)channel, days, budget).Ok;
        }

        bool AINegotiate(Player ai, Traits t, Supplier s)
        {
            if (!AICmd(ai, CommandType.StartNegotiation, s.Id).Ok) return false;
            var n = State.Negotiations.Find(x => x.PlayerId == ai.Id && x.Status == NegotiationStatus.Active);
            if (n == null) return false;
            double ask = n.HintKnown ? (n.HintLow + n.HintHigh) / 2 - 0.005 : 0.08 + t.Risk * 0.06;
            int guard = 0;
            while (n.Status == NegotiationStatus.Active && guard++ < 6)
            {
                double lastCounter = n.LastCounter;
                AICmd(ai, CommandType.NegotiationOffer, n.Id, null, 0, 0, ask);
                if (n.Status != NegotiationStatus.Active) break;
                if (n.LastCounter > 0 && n.LastCounter != lastCounter) ask = n.LastCounter;
                else ask = Math.Max(0.02, ask - 0.04);
            }
            return true;
        }

        int AIChooseEventChoice(Player ai, Traits t, GameEvent e)
        {
            int best = -1;
            double bestScore = double.MinValue;
            double reserve = MonthlyObligations() * 0.25;
            for (int i = 0; i < e.Choices.Count; i++)
            {
                var ch = e.Choices[i];
                if (!string.IsNullOrEmpty(ch.RequiresSkill) && !ai.HasSkill(ch.RequiresSkill) && (string.IsNullOrEmpty(ch.RequiresSkillAlt) || !ai.HasSkill(ch.RequiresSkillAlt))) continue;
                if (ch.Cost > 0 && C.Cash - ch.Cost < reserve) continue;
                if (ch.Cost > C.Cash * 0.25 && e.Severity < 3) continue;
                if (ch.BusyDays > 0 && ActiveTaskCount(ai.Id) >= MaxTasks(ai)) continue;
                if (ch.Label.StartsWith("Raise the price") && e.ProductId != null)
                {
                    var cp = State.GetCompanyProduct(e.ProductId);
                    var mp = cp != null ? State.GetMarketProduct(cp.ProductId) : null;
                    if (mp != null && cp.Price >= mp.RefPrice * 1.25) continue;
                }
                // Only sign contracts we can actually deliver on time.
                if (e.TemplateId == "contract_offer" && i < 2 && !AIContractFeasible(e, t)) continue;
                double score = t.Loyalty * ch.AiCompany * 10 + t.Greed * ch.AiSelf * 10 - (1 - t.Risk) * ch.AiRisk * 25 + Rng.Range(-4, 4);
                // Money matters more when the bank account is thin.
                double tight = C.Cash < MonthlyObligations() * 2 ? 3 : C.Cash < MonthlyObligations() * 4 ? 1.8 : 1;
                if (ch.Cost > 0) score -= ch.Cost / Math.Max(10000, C.Cash) * 35 * (1 - t.Risk * 0.5) * tight;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        bool AIContractFeasible(GameEvent e, Traits t)
        {
            var cp = State.GetCompanyProduct(e.ProductId);
            if (cp == null || !cp.Active) return false;
            int.TryParse(e.EmployeeId, out int due);
            int owed = OutstandingContractUnits(cp.Id) + e.Count;
            // Stock we can spare after two weeks of normal sales, plus anything already on the way.
            double spare = cp.Stock + IncomingUnits(cp.Id) - cp.Sales7 * 14;
            if (spare >= owed) return true;
            // Otherwise we need a fresh order that arrives in time and that we can pay for.
            var s = PreferredSupplier(cp);
            if (s == null || State.Day < s.BlockedUntil) return false;
            var mp = State.GetMarketProduct(cp.ProductId);
            double need = owed - Math.Max(0, spare);
            double cost = UnitPrice(s, mp, (int)Math.Max(s.Moq, need), -1) * Math.Max(s.Moq, need);
            int space = StorageCapacity() - StockOnHand() - IncomingUnits();
            return s.LeadTime + 3 <= due && cost < (C.Cash - MonthlyObligations() * 0.5) * (0.5 + t.Risk * 0.4) && space >= need;
        }

        // ------------------------------------------------------------------ skills & personal money

        void AISpendSkillPoints(Player ai, Traits t)
        {
            int guard = 0;
            while (ai.SkillPoints > 0 && guard++ < 5)
            {
                string pick = null;
                foreach (var tree in t.Trees)
                {
                    foreach (var s in Skills.All)
                        if (s.Tree == tree && CanUnlock(ai, s.Id, out _)) { pick = s.Id; break; }
                    if (pick != null) break;
                }
                if (pick == null)
                    foreach (var s in Skills.All) if (CanUnlock(ai, s.Id, out _)) { pick = s.Id; break; }
                if (pick == null) return;
                AICmd(ai, CommandType.UnlockSkill, pick);
            }
        }

        void AIPersonalFinance(Player ai, Traits t)
        {
            var pr = Priv(ai.Id);
            if (pr.Cash < 25000 || !Rng.Chance(0.05)) return;
            string asset = t.Risk > 0.8 ? (Rng.Chance(0.5) ? "crypto" : "tech") : t.Risk > 0.45 ? "tech" : (Rng.Chance(0.5) ? "index" : "realestate");
            if (ai.HasSkill(Skills.Investor) && Rng.Chance(0.4)) asset = t.Risk > 0.5 ? "angel" : "privatecredit";
            double amount = Math.Round(pr.Cash * Rng.Range(0.3, 0.6) / 500) * 500;
            AICmd(ai, CommandType.PersonalInvest, asset, null, 0, 0, amount);
        }

        // ------------------------------------------------------------------ private offers

        void AIRespondOffers(Player ai, Traits t)
        {
            foreach (var o in State.Offers.ToArray())
            {
                if (o.TargetId != ai.Id || o.Status != OfferStatus.Pending || o.CreatedDay >= State.Day) continue;
                double accept = 0, report = 0;
                switch (o.Kind)
                {
                    case OfferKind.Kickback: accept = t.Greed * (1 - t.Honesty) * 1.2; report = t.Honesty > 0.8 ? 0.6 : 0.05; break;
                    case OfferKind.DataSale: accept = t.Greed * (1 - t.Honesty) * (1 - t.Loyalty) * 1.6; report = t.Honesty > 0.8 ? 0.4 : 0; break;
                    case OfferKind.FakeInvoice: accept = t.Greed * (1 - t.Honesty) * 0.9; report = t.Honesty > 0.8 ? 0.5 : 0; break;
                    case OfferKind.InfluencerSkim: accept = t.Greed * (1 - t.Honesty); report = t.Honesty > 0.8 ? 0.4 : 0; break;
                    case OfferKind.PoachJob: accept = t.Greed * (1 - t.Loyalty) * (1 - t.Honesty) * 1.3; report = t.Honesty > 0.8 ? 0.5 : 0; break;
                    case OfferKind.InsiderTip: accept = t.Greed * (1 - t.Honesty) * 0.8 + t.Risk * 0.15; break;
                    case OfferKind.SpeakingGig: accept = 0.25 + t.Greed * 0.3 + (ai.Personality == Personality.Lazy ? 0.35 : 0) - t.Diligence * 0.2; break;
                    case OfferKind.SideConsulting: accept = t.Greed * 0.6 * (1 - t.Honesty * 0.5); break;
                    case OfferKind.Blackmail:
                    {
                        var pr = Priv(ai.Id);
                        accept = o.PersonalAmount < pr.Cash * 0.5 ? (ai.Personality == Personality.Manipulative ? 0.45 : 0.75) : 0.05;
                        break;
                    }
                    default: accept = 0.3; break;
                }
                double r = Rng.Next();
                if (o.Reportable && r < report) AICmd(ai, CommandType.RespondOffer, o.Id, null, 2);
                else if (r < report + accept) AICmd(ai, CommandType.RespondOffer, o.Id, null, 0);
                else AICmd(ai, CommandType.RespondOffer, o.Id, null, 1);
            }
        }

        void AIRespondShareOffers(Player ai, Traits t)
        {
            foreach (var o in State.ShareOffers.ToArray())
            {
                if (o.ToId != ai.Id || o.Status != OfferStatus.Pending || o.CreatedDay >= State.Day) continue;
                double fair = FairSharePrice(o.Shares);
                bool accept;
                if (o.FromBuys) accept = o.Price >= fair * (1.1 + t.Ambition * 0.5 - t.Greed * 0.25) || (Priv(ai.Id).Cash < 2000 && o.Price >= fair * 0.9);
                else accept = o.Price <= fair * (0.9 + t.Ambition * 0.25) && Priv(ai.Id).Cash >= o.Price + 2000;
                var from = P(o.FromId);
                if (accept)
                {
                    AICmd(ai, CommandType.RespondShareOffer, o.Id, null, 0, 0, 0, 0, true);
                    if (from != null && !from.IsAI) Say(ai.Id, o.FromBuys ? "Fine. Pleasure doing business." : "Deal. I'll take those shares.", from.Id);
                }
                else
                {
                    AICmd(ai, CommandType.RespondShareOffer, o.Id, null, 0, 0, 0, 0, false);
                    if (from != null && !from.IsAI)
                        Say(ai.Id, o.FromBuys ? $"Not for {Fmt.Money(o.Price)}. Those shares are worth more than that." : "No thanks, that price is too rich for me.", from.Id);
                }
            }
        }

        void AIShareInitiative(Player ai, Traits t, AIMemory mem)
        {
            if (State.Day - mem.LastShareOfferDay < 60 || State.Day < 90 || !Rng.Chance(0.04)) return;
            var pr = Priv(ai.Id);
            if (t.Ambition >= 0.8 && pr.Cash > 20000)
            {
                Player target = null;
                foreach (var p in State.Players)
                    if (p.Id != ai.Id && p.Shares > C.TotalShares * 0.03 && (target == null || p.Performance < target.Performance)) target = p;
                if (target == null) return;
                double pct = Rng.Range(0.01, 0.03);
                double price = FairSharePrice((long)(pct * C.TotalShares)) * Rng.Range(0.95, 1.15);
                if (price > pr.Cash * 0.9) { pct *= pr.Cash * 0.9 / price; price = pr.Cash * 0.9; }
                if (pct < 0.003) return;
                mem.LastShareOfferDay = State.Day;
                AICmd(ai, CommandType.CreateShareOffer, null, null, target.Id, 0, pct, price, true, "I'd like a bigger stake. Fair price.");
            }
            else if ((t.Greed >= 0.6 || ai.Personality == Personality.Lazy) && pr.Cash < 5000 && ai.Shares > C.TotalShares * 0.05)
            {
                var others = State.Players.FindAll(p => p.Id != ai.Id);
                if (others.Count == 0) return;
                var target = Rng.Pick(others);
                double pct = Rng.Range(0.01, 0.02);
                double price = FairSharePrice((long)(pct * C.TotalShares)) * Rng.Range(0.88, 1.0);
                mem.LastShareOfferDay = State.Day;
                AICmd(ai, CommandType.CreateShareOffer, null, null, target.Id, 0, pct, price, false, "I need some cash. You get a discount.");
            }
        }

        // ------------------------------------------------------------------ evidence

        void AIHandleEvidence(Player ai, Traits t, AIMemory mem)
        {
            foreach (var e in State.Evidence.ToArray())
            {
                if (e.Exposed || !e.DiscoveredBy.Contains(ai.Id) || mem.HandledEvidence.Contains(e.Id)) continue;
                if (State.Day - e.Day < 1) continue;
                mem.HandledEvidence.Add(e.Id);
                var actor = P(e.ActorId);
                if (actor == null) continue;
                double trust = GetTrust(ai, actor.Id);
                double expose = t.Honesty * 0.8 + (trust < 30 ? 0.2 : 0) - t.Greed * 0.3;
                double blackmail = (1 - t.Honesty) * t.Greed * 1.2;
                double r = Rng.Next();
                if (r < expose) Expose(e, ai);
                else if (r < expose + blackmail)
                {
                    double amount = Math.Max(3000, Math.Round(e.Amount * Rng.Range(0.8, 1.5) / 500) * 500);
                    SendBlackmail(ai, e, amount);
                }
                else if (!e.Silenced.Contains(ai.Id)) e.Silenced.Add(ai.Id);
            }
            // Follow through on ignored threats.
            foreach (var o in State.Offers)
            {
                if (o.Kind != OfferKind.Blackmail || o.FromId != ai.Id) continue;
                if (o.Status != OfferStatus.Rejected && o.Status != OfferStatus.Expired) continue;
                if (o.Result == "followed") continue;
                o.Result = "followed";
                var e = State.Evidence.Find(x => x.Id == o.EvidenceId);
                if (e != null && !e.Exposed && Rng.Chance(0.6)) Expose(e, ai);
            }
        }

        // ------------------------------------------------------------------ board behaviour

        double AIVoteDelay(Player ai, Proposal prop) => 2 + (StableHash(prop.Id) + ai.Id * 37) % 40 / 10.0;

        void AIBoardProposals()
        {
            foreach (var ai in State.Players)
            {
                if (!ai.IsAI) continue;
                var t = Traits.For(ai.Personality);
                int made = 0;
                double avgPerf = AvgPerformance();
                bool hasRaise = State.Board.Agenda.Exists(x => x.Type == ProposalType.SalaryChange && x.TargetId == ai.Id && x.Status == ProposalStatus.Pending);
                double salaryCap = State.Settings.StartingSalary * 3 + Math.Max(0, ai.Performance) * 2;
                if (!hasRaise && ai.Salary < salaryCap && (ai.Performance > avgPerf * 1.1 || t.Greed > 0.8) && Rng.Chance(0.35 + t.Greed * 0.3 + t.Ambition * 0.15))
                {
                    double amount = Math.Min(salaryCap, Math.Round(ai.Salary * Rng.Range(1.15, 1.45) / 100) * 100);
                    if (amount > ai.Salary && AICmd(ai, CommandType.CreateProposal, null, null, (int)ProposalType.SalaryChange, ai.Id, amount).Ok) made++;
                }
                double obligations = MonthlyObligations();
                if (made < 2 && t.Greed >= 0.6 && C.Cash > obligations * 6 + 50000 && !State.Board.Agenda.Exists(x => x.Type == ProposalType.Dividend) && Rng.Chance(0.4 + t.Greed * 0.3))
                {
                    double amount = Math.Round((C.Cash - obligations * 6) * Rng.Range(0.2, 0.4) / 1000) * 1000;
                    if (amount >= 1000 && AICmd(ai, CommandType.CreateProposal, null, null, (int)ProposalType.Dividend, -1, amount).Ok) made++;
                }
                if (made < 2 && t.Ambition >= 0.8 && Rng.Chance(0.25) && ai.SpendingLimit < 150000)
                {
                    if (AICmd(ai, CommandType.CreateProposal, null, null, (int)ProposalType.ExecutiveLimit, ai.Id, ai.SpendingLimit + 25000).Ok) made++;
                }
                if (made < 2 && t.Loyalty > 0.9 && !C.Policies.SupplierAudits && State.Evidence.Exists(e => e.Exposed) && Rng.Chance(0.6))
                {
                    if (AICmd(ai, CommandType.CreateProposal, "audits", null, (int)ProposalType.Policy, -1, 1).Ok) made++;
                }
                if (made < 2 && t.Risk > 0.85 && C.Loans.Count == 0 && C.Cash < obligations * 3 && State.Day > 90 && Rng.Chance(0.5))
                {
                    if (AICmd(ai, CommandType.CreateProposal, "growth", null, (int)ProposalType.Loan, -1, 0).Ok) made++;
                }
                // Strategy: once the company is big enough to have one, everyone has an opinion.
                if (made < 2 && C.Stage >= 1 && string.IsNullOrEmpty(C.Positioning) && !State.Board.Agenda.Exists(x => x.Type == ProposalType.Positioning && x.Status == ProposalStatus.Pending) && Rng.Chance(0.4 + t.Ambition * 0.3))
                {
                    if (AICmd(ai, CommandType.CreateProposal, AIPreferredPositioning(ai), null, (int)ProposalType.Positioning, -1, 0).Ok) made++;
                }
                // Acquisitions: the cheapest rival we can swallow without draining the bank.
                if (made < 2 && C.Stage >= 3 && (t.Ambition >= 0.5 || t.Risk >= 0.6) && !State.Board.Agenda.Exists(x => x.Type == ProposalType.AcquireRival && x.Status == ProposalStatus.Pending))
                {
                    Rival pick = null;
                    foreach (var r in State.Rivals)
                    {
                        if (!CanAcquire(r, out _)) continue;
                        double price = AcquisitionPrice(r);
                        if (C.Cash < price + obligations * 4) continue;
                        if (pick == null || price < AcquisitionPrice(pick)) pick = r;
                    }
                    if (pick != null && Rng.Chance(0.5 + t.Ambition * 0.3) && AICmd(ai, CommandType.CreateProposal, pick.Id, null, (int)ProposalType.AcquireRival, -1, 0).Ok) made++;
                }
                // The IPO: the payday everyone has been working toward.
                if (made < 2 && C.Stage >= Stages.Max && !C.IpoPending && !C.WentPublic && !State.Board.Agenda.Exists(x => x.Type == ProposalType.GoPublic && x.Status == ProposalStatus.Pending) && Rng.Chance(0.35 + t.Greed * 0.4))
                {
                    if (AICmd(ai, CommandType.CreateProposal, null, null, (int)ProposalType.GoPublic, -1, 0).Ok) made++;
                }
            }
        }

        /// <summary>The strategy that fits what the company actually sells, nudged by personality.</summary>
        string AIPreferredPositioning(Player ai)
        {
            double ratings = 0, n = 0, trending = 0;
            foreach (var cp in C.Products)
            {
                if (!cp.Active) continue;
                n++;
                ratings += cp.Rating;
                var mp = State.GetMarketProduct(cp.ProductId);
                if (mp != null && (mp.Phase == TrendPhase.Emerging || mp.Phase == TrendPhase.Rising)) trending++;
            }
            double avgRating = n > 0 ? ratings / n : 3.5;
            double premium = (avgRating - 3.8) * 50, value = 8, trend = n > 0 ? (trending / n - 0.3) * 60 : 0;
            switch (ai.Personality)
            {
                case Personality.Greedy: case Personality.Manipulative: case Personality.Ambitious: premium += 10; break;
                case Personality.RiskTaker: case Personality.Workaholic: trend += 10; break;
                default: value += 6; break;
            }
            return premium >= value && premium >= trend ? "premium" : trend >= value ? "trend" : "value";
        }

        void AIOnProposalCreated(Proposal prop)
        {
            var proposer = P(prop.ProposerId);
            if (proposer == null) return;
            if (proposer.IsAI)
            {
                var t = Traits.For(proposer.Personality);
                // Manipulators work the humans for votes.
                if ((proposer.Personality == Personality.Manipulative || proposer.Personality == Personality.Ambitious || proposer.Personality == Personality.Greedy) && State.Settings.Intrigue)
                {
                    foreach (var h in State.Players)
                    {
                        if (h.IsAI || h.Shares <= 0 || !Rng.Chance(0.5)) continue;
                        var pr = Priv(proposer.Id);
                        bool buy = pr.Cash > 8000 && (proposer.Personality != Personality.Ambitious) && Rng.Chance(0.5);
                        double pay = buy ? Math.Round(Math.Min(pr.Cash * 0.3, Rng.Range(3000, 12000)) / 500) * 500 : 0;
                        var o = new PrivateOffer
                        {
                            Id = NewId("o"), Kind = buy ? OfferKind.VoteBuy : OfferKind.VoteTrade, TargetId = h.Id, FromId = proposer.Id, FromName = proposer.Name,
                            ProposalId = prop.Id, PersonalAmount = pay, CreatedDay = State.Day, ExpiresDay = State.Day + 5, Reportable = false, Status = OfferStatus.Pending,
                            Title = buy ? $"{proposer.Name} will pay for your vote" : $"{proposer.Name} proposes a deal",
                            Text = buy
                                ? $"{proposer.Name} messages you privately: \"Vote YES on '{prop.Text}' and I'll send you {Fmt.Money(pay)}.\""
                                : $"{proposer.Name} messages you privately: \"Back me on '{prop.Text}' and I'll vote for your next proposal. Deal?\""
                        };
                        State.Offers.Add(o);
                        Notify(h.Id, NoticeKind.Private, o.Title);
                        break;
                    }
                }
                return;
            }
            // A human proposed something: AI partners may react in chat.
            if (!Rng.Chance(0.35)) return;
            foreach (var ai in State.Players)
            {
                if (!ai.IsAI) continue;
                var t = Traits.For(ai.Personality);
                string line = null;
                if (prop.Type == ProposalType.SalaryChange && prop.TargetId == proposer.Id && prop.Amount > proposer.Salary)
                {
                    if (t.Greed > 0.7) line = $"A raise to {Fmt.Money(prop.Amount)}? We're trying to grow here, {proposer.Name}.";
                    else if (proposer.Performance > AvgPerformance() * 1.2 && t.Loyalty > 0.7) line = $"Honestly, {proposer.Name} has earned it.";
                    else if (ai.Personality == Personality.Manipulative) line = $"I could support that raise, {proposer.Name}. If you support mine.";
                }
                else if (prop.Type == ProposalType.Dividend)
                    line = t.Greed > 0.6 ? "Dividends! Finally someone talking sense." : t.Loyalty > 0.8 ? "Paying out cash now means slower growth. I'm not convinced." : null;
                else if (prop.Type == ProposalType.Loan)
                    line = t.Risk < 0.3 ? "More debt? I don't like it." : t.Risk > 0.8 ? "Leverage is how you scale. I'm in." : null;
                if (line != null) { Say(ai.Id, line); return; }
            }
        }

        public VoteChoice AIDecideVote(Player ai, Proposal prop)
        {
            if (prop.ProposerId == ai.Id) return VoteChoice.Yes;
            var t = Traits.For(ai.Personality);
            var proposer = P(prop.ProposerId);
            var target = P(prop.TargetId);
            double trust = proposer != null ? GetTrust(ai, proposer.Id) : 50;
            double score = (trust - 50) * 0.3;
            double obligations = Math.Max(1, MonthlyObligations());
            switch (prop.Type)
            {
                case ProposalType.SalaryChange:
                {
                    if (target == null) break;
                    if (target.Id == ai.Id) return prop.Amount >= ai.Salary ? VoteChoice.Yes : VoteChoice.No;
                    double delta = prop.Amount - target.Salary;
                    double perfRatio = Math.Max(1, target.Performance) / AvgPerformance();
                    double salaryRatio = prop.Amount / AvgSalary();
                    score += (perfRatio - salaryRatio) * 40 * t.Loyalty;
                    score += delta < 0 ? 12 : -delta / 500 * t.Greed * 1.5;
                    if (C.Cash < delta * 12) score -= 20;
                    break;
                }
                case ProposalType.Bonus:
                {
                    if (target == null) break;
                    if (target.Id == ai.Id) return VoteChoice.Yes;
                    double ratio = prop.Amount / Math.Max(1000, Math.Max(prop.ContextProfit, target.QuarterProfit));
                    score += (0.15 - ratio) * 60 * t.Loyalty - t.Greed * prop.Amount / 5000 * 10;
                    break;
                }
                case ProposalType.Dividend:
                {
                    double runway = (C.Cash - prop.Amount) / obligations;
                    score += t.Greed * 40 - t.Loyalty * Math.Max(0, 4 - runway) * 12 - t.Ambition * 5;
                    if (runway < 2) score -= 40;
                    if (prop.Amount * OwnershipPct(ai) > 5000) score += 10;
                    break;
                }
                case ProposalType.Loan:
                    score += (t.Risk - 0.5) * 60 + (C.Cash < obligations * 2 ? 20 : 0) - (C.Debt > C.Valuation * 0.3 ? 30 : 0);
                    break;
                case ProposalType.Warehouse:
                {
                    double util = (StockOnHand() + IncomingUnits()) / Math.Max(1.0, StorageCapacity());
                    score += (util - 0.65) * 120 + (t.Risk - 0.5) * 20;
                    if (prop.Amount > C.Cash * 0.6) score -= 30;
                    break;
                }
                case ProposalType.Investment:
                    score += 5 + AIUpgradeFit(prop.OptionId, t) * 30 - (prop.Amount > C.Cash * 0.5 ? 40 : 0) + (t.Risk - 0.5) * 10;
                    break;
                case ProposalType.Positioning:
                    score += (prop.OptionId == AIPreferredPositioning(ai) ? 22 : -6) + (string.IsNullOrEmpty(C.Positioning) ? 8 : -8);
                    if (prop.Amount > C.Cash * 0.3) score -= 25;
                    break;
                case ProposalType.AcquireRival:
                {
                    double runway = (C.Cash - prop.Amount) / obligations;
                    score += (t.Ambition - 0.4) * 40 + (t.Risk - 0.5) * 25 + 10;
                    if (runway < 3) score -= 45;
                    else if (runway < 6) score -= 15;
                    var r = State.Rivals.Find(x => x.Id == prop.OptionId);
                    if (r != null && r.Strength < 0.7) score += 10; // a bargain
                    break;
                }
                case ProposalType.GoPublic:
                {
                    int years = State.Settings.GameYears;
                    double remaining = years > 0 ? Clamp(1 - State.Day / (double)(years * GameCalendar.DaysPerYear), 0, 1) : 0.5;
                    score += 30 + t.Greed * 20 - t.Ambition * 25 * remaining + (t.Risk < 0.4 ? 10 : 0);
                    break;
                }
                case ProposalType.RaiseCapital:
                    if (target != null && target.Id == ai.Id) return VoteChoice.Yes;
                    score += (C.Cash < obligations * 2 ? 30 : -10) - t.Ambition * 25 + (t.Risk - 0.5) * 20;
                    break;
                case ProposalType.Policy:
                    switch (prop.OptionId)
                    {
                        case "audits": score += prop.Amount != 0 ? (t.Honesty - 0.5) * 60 : (0.5 - t.Honesty) * 60; break;
                        case "openbooks": score += prop.Amount != 0 ? (t.Honesty - 0.5) * 50 : (0.5 - t.Honesty) * 50; break;
                        case "ballot": score += ai.Personality == Personality.Manipulative ? 20 : 0; break;
                        case "returns": score += prop.Amount == 2 ? (t.Loyalty - 0.6) * 30 : prop.Amount == 0 ? (0.5 - t.Loyalty) * 30 : 5; break;
                        case "limit": score += t.Ambition * 20 - 10 + (t.Risk - 0.5) * 20; break;
                    }
                    break;
                case ProposalType.ExecutiveLimit:
                    if (target != null && target.Id == ai.Id) return VoteChoice.Yes;
                    if (target != null)
                    {
                        score += (GetTrust(ai, target.Id) - 50) * 0.5 + (target.Performance > AvgPerformance() ? 10 : -10) - t.Ambition * 15;
                        if (prop.Amount < target.SpendingLimit) score += 10;
                    }
                    break;
                case ProposalType.Discipline:
                    if (target != null)
                    {
                        score += t.Honesty * 50 - 20 - (GetTrust(ai, target.Id) - 50) * 0.5;
                        if (Mem(ai).Promises.Exists(pm => pm.OtherId == target.Id && !pm.Done)) score -= 25;
                    }
                    break;
                case ProposalType.TransferProduct:
                {
                    if (target != null && target.Id == ai.Id) return VoteChoice.Yes;
                    var cp = State.GetCompanyProduct(prop.OptionId);
                    if (cp != null && cp.ManagerId == ai.Id) return VoteChoice.No;
                    if (target != null) score += (GetTrust(ai, target.Id) - 50) * 0.4 + (target.Performance > AvgPerformance() ? 8 : -8);
                    break;
                }
                case ProposalType.SellCompany:
                {
                    double premium = prop.Amount / Math.Max(1, C.Valuation);
                    int years = State.Settings.GameYears;
                    double remaining = years > 0 ? Clamp(1 - State.Day / (double)(years * GameCalendar.DaysPerYear), 0, 1) : 0.5;
                    score += (premium - 1.6) * 80 + t.Greed * 25 - t.Ambition * 40 + (t.Risk < 0.3 ? 15 : 0) - remaining * 60;
                    // A company on the road to an IPO is worth more than a buyout today.
                    if (C.Stage >= 3 && !C.WentPublic) score -= 15 + 10 * (C.Stage - 3);
                    break;
                }
            }
            if (proposer != null && proposer.HasSkill(Skills.BoardInfluence)) score += 8;
            // Promises
            var mem = Mem(ai);
            foreach (var pm in mem.Promises)
            {
                if (pm.Done || proposer == null || pm.OtherId != proposer.Id || pm.Kind != "support") continue;
                score += 60 * t.Honesty + 5;
                pm.Done = true;
                break;
            }
            score += Rng.Range(-6, 6);
            if (score > 0) return VoteChoice.Yes;
            return score > -8 ? VoteChoice.Abstain : VoteChoice.No;
        }

        VoteChoice AIDecideApproval(Player ai, ApprovalRequest a)
        {
            var t = Traits.For(ai.Personality);
            double trust = GetTrust(ai, a.RequesterId);
            double obligations = MonthlyObligations();
            double score = 20 - a.Amount / Math.Max(10000, C.Cash) * 40 * (1 - t.Risk) + (trust - 50) * 0.5;
            if (C.Cash - a.Amount < obligations) score -= 40;
            if (a.Command.Type == CommandType.OrderInventory)
            {
                var cp = State.GetCompanyProduct(a.Command.A);
                var s = State.GetSupplier(a.Command.B);
                if (cp != null && s != null)
                {
                    var mp = State.GetMarketProduct(cp.ProductId);
                    double cheapest = CheapestUnitPrice(mp, a.Command.I, -1, out var cs);
                    double premium = UnitPrice(s, mp, a.Command.I, a.RequesterId) / Math.Max(0.01, cheapest) - 1;
                    if (premium >= 0.08 && (ai.HasSkill(Skills.Accountant) || t.Honesty >= 0.8))
                    {
                        score -= 25;
                        if (Rng.Chance(0.6)) Say(ai.Id, $"{State.PlayerName(a.RequesterId)}, why {s.Name}? They're {Fmt.Pct(premium)} more expensive than {cs?.Name ?? "the alternatives"}.");
                    }
                    else
                    {
                        // Restocking a proven seller at a fair price keeps the lights on, whoever asks.
                        double rate = Math.Max(cp.Sales7, cp.Demand7 * 0.8);
                        double days = rate > 0.3 ? cp.Stock / rate : 999;
                        if (rate >= 1 && days < 25 && a.Command.I <= rate * 120) score += 25 + t.Loyalty * 15 - Math.Min(0, trust - 50) * 0.4;
                    }
                }
            }
            if (a.Command.Type == CommandType.TakeLoan) score += (t.Risk - 0.5) * 50;
            if (a.Command.Type == CommandType.ResolveEvent)
            {
                var ev = State.Events.Find(x => x.Id == a.Command.A);
                if (ev != null && (ev.TemplateId == "low_stock" || ev.Severity >= 3)) score += 25 + t.Loyalty * 15 - Math.Min(0, trust - 50) * 0.4;
            }
            if (a.Command.Type == CommandType.BuyUpgrade) score += AIUpgradeFit(a.Command.A, t) * 25 - 5;
            if (a.Command.Type == CommandType.UpgradeProduct) score += 10 + t.Ambition * 10;
            return score > 0 ? VoteChoice.Yes : VoteChoice.No;
        }

        void AIOnVoteCast(Proposal prop, Player voter, VoteChoice choice)
        {
            var proposer = P(prop.ProposerId);
            if (proposer != null && proposer.IsAI && voter.Id != proposer.Id)
                AdjustTrust(proposer, voter.Id, choice == VoteChoice.Yes ? 4 : choice == VoteChoice.No ? -6 : 0);

            // Deals made in private
            foreach (var o in State.Offers)
            {
                if (o.ProposalId != prop.Id || o.TargetId != voter.Id || o.Status != OfferStatus.Accepted) continue;
                var briber = P(o.FromId);
                if (briber == null) continue;
                if (choice == VoteChoice.Yes)
                {
                    if (o.Kind == OfferKind.VoteBuy)
                    {
                        var bp = Priv(briber.Id);
                        double pay = Math.Min(o.PersonalAmount, Math.Max(0, bp.Cash));
                        bp.Cash -= pay;
                        PaySecret(voter, pay);
                        Notify(voter.Id, NoticeKind.Private, $"{briber.Name} sent you {Fmt.Money(pay)}.");
                        CreateEvidence(voter.Id, OfferKind.VoteBuy, $"{voter.Name} took {Fmt.Money(pay)} from {briber.Name} to vote for \"{prop.Text}\".", pay, 0.45);
                    }
                    o.Status = OfferStatus.Fulfilled;
                    AdjustTrust(briber, voter.Id, 10);
                }
                else
                {
                    o.Status = OfferStatus.Broken;
                    AdjustTrust(briber, voter.Id, -30);
                    if (briber.IsAI) Say(briber.Id, $"We had a deal, {voter.Name}. I won't forget this.", voter.Id);
                }
            }
        }

        void AIOnProposalResolved(Proposal prop, bool passed)
        {
            var proposer = P(prop.ProposerId);
            if (proposer == null || !proposer.IsAI || HumanCount == 0 || !Rng.Chance(0.5)) return;
            var t = Traits.For(proposer.Personality);
            if (!passed && prop.Type == ProposalType.SalaryChange && prop.TargetId == proposer.Id)
            {
                var report = State.Board.LastReport?.Players.Find(x => x.PlayerId == proposer.Id);
                Say(proposer.Id, report != null && report.ProfitGenerated > 0
                    ? $"Seriously? I generated {Fmt.Money(report.ProfitGenerated)} last quarter."
                    : "Noted. I'll remember who voted against me.");
            }
            else if (passed && prop.Type == ProposalType.Dividend && t.Greed > 0.6) Say(proposer.Id, "Pleasure doing business, partners.");
        }

        void AIOnOfferAccepted(PrivateOffer o)
        {
            var ai = P(o.FromId);
            if (ai == null || !ai.IsAI) return;
            var mem = Mem(ai);
            if (o.Kind == OfferKind.VoteTrade)
                mem.Promises.Add(new Promise { OtherId = o.TargetId, Kind = "support", CreatedDay = State.Day });
            mem.Promises.Add(new Promise { OtherId = o.TargetId, Kind = "owed", ProposalId = o.ProposalId, Payment = o.PersonalAmount, CreatedDay = State.Day });
            if (mem.Promises.Count > 20) mem.Promises.RemoveAt(0);
            AdjustTrust(ai, o.TargetId, 5);
        }

        void AIOnOfferRejected(PrivateOffer o)
        {
            var ai = P(o.FromId);
            if (ai != null && ai.IsAI) AdjustTrust(ai, o.TargetId, -3);
        }

        // ------------------------------------------------------------------ chatter

        void AIReactToFlaggedOrder(Player buyer, Supplier chosen, Supplier cheapest, double premium, CompanyProduct cp)
        {
            if (HumanCount == 0) return;
            foreach (var ai in State.Players)
            {
                if (!ai.IsAI || ai.Id == buyer.Id) continue;
                var t = Traits.For(ai.Personality);
                if (!(ai.HasSkill(Skills.Accountant) || t.Honesty >= 0.8) || !Rng.Chance(0.5)) continue;
                Say(ai.Id, $"{buyer.Name}, why did you choose {chosen.Name} for {cp.Name}? They're {Fmt.Pct(premium)} more expensive than {cheapest.Name}.");
                AdjustTrust(ai, buyer.Id, -3);
                return;
            }
        }

        void AIReplyToChat(Player ai, Player from, string text)
        {
            if (from.IsAI) return;
            string msg = (text ?? "").ToLowerInvariant();
            var t = Traits.For(ai.Personality);
            string reply;
            if (msg.Contains("raise") || msg.Contains("salary"))
                reply = t.Greed > 0.7 ? "Salaries come out of profits. My profits." : t.Loyalty > 0.8 ? "Show me the numbers and I'll back a fair raise." : "Put it on the board agenda and we'll see.";
            else if (msg.Contains("vote") || msg.Contains("support"))
                reply = ai.Personality == Personality.Manipulative ? "Depends. What's in it for me?" : t.Honesty > 0.8 ? "I vote for what's best for the company. Always." : "Convince me.";
            else if (msg.Contains("share") || msg.Contains("stake"))
                reply = t.Ambition > 0.8 ? "I'm always buying. Selling? Not cheap." : "Make me an offer in the Partners tab.";
            else if (msg.Contains("bribe") || msg.Contains("kickback") || msg.Contains("suspicious"))
                reply = t.Honesty > 0.8 ? "If you have evidence, expose it. I will too." : "I have no idea what you're talking about.";
            else
            {
                string[] lines;
                switch (ai.Personality)
                {
                    case Personality.Greedy: lines = new[] { "Let's talk when there's money on the table.", "Dividends fix everything." }; break;
                    case Personality.Loyal: lines = new[] { "Whatever helps the company.", "I'm on it. What needs doing?" }; break;
                    case Personality.RiskTaker: lines = new[] { "Go big or go home.", "We should be moving faster." }; break;
                    case Personality.Conservative: lines = new[] { "Let's not do anything reckless.", "Cash is king. Let's keep some." }; break;
                    case Personality.Workaholic: lines = new[] { "Busy. What do you need?", "Already handling three things. Go on." }; break;
                    case Personality.Lazy: lines = new[] { "Can this wait until after lunch?", "Sure, sure. Someone should do that." }; break;
                    case Personality.Manipulative: lines = new[] { "We should talk. Privately.", "You and me could run this place." }; break;
                    case Personality.Ambitious: lines = new[] { "I plan to own a bigger slice of this company.", "Big things coming. Stick with me." }; break;
                    default: lines = new[] { "Okay." }; break;
                }
                reply = Rng.Pick(lines);
            }
            Say(ai.Id, reply, from.Id);
        }

        void AIChatter(Player ai, Traits t, AIMemory mem)
        {
            if (HumanCount == 0 || State.Day - mem.LastChatDay < 25 || !Rng.Chance(0.06)) return;
            mem.LastChatDay = State.Day;
            string line = null;
            int toMeeting = State.Board.NextMeetingDay - State.Day;
            if (toMeeting > 0 && toMeeting <= 10 && t.Greed > 0.6) line = $"Board meeting in {toMeeting} days. Someone's putting dividends on the agenda, right?";
            else if (C.Cash < MonthlyObligations() && t.Risk < 0.5) line = "We're burning through cash. We need to be careful.";
            else if (C.Backlog > C.CapacityPerDay * 0.5) line = "The warehouse can't keep up. We need more hands.";
            else if (C.Valuation > C.PeakValuation * 0.98 && C.Valuation > 1000000 && Rng.Chance(0.5)) line = $"Valuation just hit {Fmt.Money(C.Valuation)}. Not bad, partners.";
            else if (t.Ambition > 0.8) line = "I'd buy more shares if anyone's selling.";
            else if (ai.Personality == Personality.Lazy) line = "Everyone's working so hard. Inspiring, really.";
            if (line != null) Say(ai.Id, line);
        }
    }
}
