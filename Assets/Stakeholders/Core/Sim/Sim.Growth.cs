using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// Long-term play: growth stages, the company upgrade tree, rival companies, promotions,
    /// product R&D, auto-restock, B2B contracts, personal ambitions, delegation and the IPO.
    /// </summary>
    public partial class GameSimulation
    {
        public void Celebrate(string text) => NotifyAll(NoticeKind.Celebration, text);

        void GrowthDaily()
        {
            BuildsDaily();
            StagesDaily();
            PromotionsDaily();
            ProductUpgradesDaily();
            UpdateCategoryShares();
            if (State.Day % GameCalendar.DaysPerMonth == 0) RivalsMonthly();
            RankDaily();
            IpoDaily();
            AmbitionsDaily();
        }

        // ------------------------------------------------------------------ upgrades

        public bool UpgradeUnlocked(Catalog.UpgradeDef u, Player p, out string reason)
        {
            reason = null;
            if (C.Stage < u.Stage) { reason = $"Needs the {Stages.Get(u.Stage).Name} stage."; return false; }
            if (u.Requires != null && !C.Has(u.Requires)) { reason = $"Needs {Catalog.GetUpgrade(u.Requires)?.Name} first."; return false; }
            if (u.InvestorOnly && (p == null || !p.HasSkill(Skills.Investor))) { reason = "Only a partner with the Investor skill can arrange this."; return false; }
            if (C.Valuation < u.MinValuation) { reason = $"Needs a {Fmt.Money(u.MinValuation)} valuation."; return false; }
            return true;
        }

        public bool IsBuilding(string upgradeId) => C.Building.Exists(b => b.Id == upgradeId);

        CmdResult CmdBuyUpgrade(Command c, Player p)
        {
            var u = Catalog.GetUpgrade(c.A);
            if (u == null) return CmdResult.Fail("Unknown upgrade.");
            if (C.Has(u.Id)) return CmdResult.Fail("We already have that.");
            if (IsBuilding(u.Id)) return CmdResult.Fail("That upgrade is already being built.");
            if (!UpgradeUnlocked(u, p, out string why)) return CmdResult.Fail(why);
            if (C.Cash < u.Cost) return CmdResult.Fail($"{u.Name} costs {Fmt.Money(u.Cost)}.");
            if (NeedsApproval(p, c, u.Cost, $"Company upgrade: {u.Name}", out var ar)) return ar;
            StartUpgradeBuild(u, p.Id);
            return CmdResult.Success($"Construction started. {u.Name} will be ready in {Fmt.Days(u.BuildDays)}.");
        }

        public void StartUpgradeBuild(Catalog.UpgradeDef u, int actor)
        {
            Spend(u.Cost, TxType.Investment, actor, $"Upgrade: {u.Name}");
            C.Building.Add(new UpgradeBuild { Id = u.Id, StartDay = State.Day, DoneDay = State.Day + Math.Max(1, u.BuildDays), OrderedBy = actor });
            var p = P(actor);
            if (p != null) p.UpgradesBought++;
            Log(LogKind.Company, actor, $"{State.PlayerName(actor)} started building {u.Name} ({Fmt.Money(u.Cost)}, {Fmt.Days(u.BuildDays)}).");
        }

        void BuildsDaily()
        {
            for (int i = C.Building.Count - 1; i >= 0; i--)
            {
                var b = C.Building[i];
                if (State.Day < b.DoneDay) continue;
                C.Building.RemoveAt(i);
                var u = Catalog.GetUpgrade(b.Id);
                if (u == null || C.Has(u.Id)) continue;
                C.Upgrades.Add(u.Id);
                if (u.Id == "brd1") C.BrandBonus = Math.Min(20, C.BrandBonus + 3);
                if (u.Id == "ppl1") C.Satisfaction = Math.Min(100, C.Satisfaction + 3);
                Log(LogKind.Company, b.OrderedBy, $"Upgrade complete: {u.Name}.");
                NotifyAll(NoticeKind.Success, $"Upgrade complete: {u.Name}. {u.Description}");
                if (b.OrderedBy >= 0) Award(b.OrderedBy, 15 + 15 * Math.Max(1, u.Tier), $"Built {u.Name}");
            }
        }

        // ------------------------------------------------------------------ stages

        void StagesDaily()
        {
            // A stage is earned by holding the valuation for a week, not by a one-day spike.
            double sustained = C.Valuation;
            var hv = State.History.Valuation;
            for (int i = Math.Max(0, hv.Count - 6); i < hv.Count; i++) sustained = Math.Min(sustained, hv[i]);
            int guard = 0;
            while (C.Stage < Stages.Max && sustained >= Stages.Get(C.Stage + 1).Valuation && guard++ < 6)
            {
                C.Stage++;
                var st = Stages.Get(C.Stage);
                Log(LogKind.Company, -1, $"MILESTONE: {C.Name} is now {Stages.InSentence(C.Stage)}! Unlocked: {st.Unlocks}.");
                Celebrate($"{C.Name} is now {Stages.InSentence(C.Stage)}!|Valuation passed {Fmt.Money(st.Valuation)}. Unlocked: {st.Unlocks}.");
                foreach (var p in State.Players) Award(p.Id, 40 * C.Stage, $"Company reached {st.Name}");
                foreach (var ai in State.Players)
                    if (ai.IsAI && HumanCount > 0 && Rng.Chance(0.5)) { Say(ai.Id, C.Stage >= 3 ? "Look at us now. This is just the beginning." : "We're really doing this. Nice work, partners."); break; }
            }
        }

        // ------------------------------------------------------------------ rivals

        public void InitRivals()
        {
            double vMult = State.Settings.Difficulty == 0 ? 0.8 : State.Settings.Difficulty == 2 ? 1.3 : 1.0;
            double sAdd = State.Settings.Difficulty == 0 ? -0.15 : State.Settings.Difficulty == 2 ? 0.2 : 0;
            foreach (var def in RivalCatalog.All)
            {
                var r = new Rival
                {
                    Id = GameFactory.Slug(def.Name), Name = def.Name, Style = def.Style, Valuation = Math.Round(def.Valuation * vMult * Rng.Range(0.85, 1.15) / 10000) * 10000,
                    Strength = Clamp(def.Strength + sAdd + Rng.Range(-0.1, 0.1), 0.3, 2.5)
                };
                r.Focus.AddRange(def.Focus);
                r.LastMonthValuation = r.Valuation;
                State.Rivals.Add(r);
            }
        }

        public double RivalPresence(string category)
        {
            double s = 0;
            foreach (var r in State.Rivals) if (!r.Acquired && r.Focus.Contains(category)) s += r.Strength;
            return s;
        }

        bool DiscounterIn(string category)
        {
            foreach (var r in State.Rivals) if (!r.Acquired && r.Style == "Discounter" && r.Focus.Contains(category)) return true;
            return false;
        }

        void UpdateCategoryShares()
        {
            foreach (var cat in Catalog.Categories)
            {
                double ours = 0, market = 0;
                foreach (var cp in C.Products) if (cp.Active && cp.Category == cat) ours += cp.Sales7;
                foreach (var mp in State.Market.Products) if (mp.Category == cat) market += mp.BaseDemand * mp.Trend;
                double others = market * 0.5 * (0.4 + RivalPresence(cat));
                C.CategoryShare[cat] = ours <= 0 ? 0 : ours / (ours + Math.Max(1, others));
            }
        }

        double OurShareIn(List<string> categories)
        {
            if (categories.Count == 0) return 0;
            double s = 0;
            foreach (var c in categories) s += C.CategoryShare.TryGetValue(c, out double v) ? v : 0;
            return s / categories.Count;
        }

        void RivalsMonthly()
        {
            foreach (var r in State.Rivals)
            {
                if (r.Acquired) continue;
                double ours = OurShareIn(r.Focus);
                double growth = 0.012 + 0.018 * r.Strength - 0.04 * ours + Rng.Normal(0, 0.02);
                if (r.Style == "Trend Chaser") growth += Rng.Normal(0, 0.03);
                r.LastMonthValuation = r.Valuation;
                r.Valuation = Math.Max(200000, Math.Round(r.Valuation * (1 + growth) / 1000) * 1000);
                r.Strength = Clamp(r.Strength + 0.04 * (0.35 - ours) + Rng.Normal(0, 0.03), 0.3, 2.5);
                if (Rng.Chance(0.45)) RivalMove(r);
            }
        }

        void RivalMove(Rival r)
        {
            var moves = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("pricewar", r.Style == "Discounter" ? 3 : r.Style == "Premium" ? 0.3 : 1),
                new KeyValuePair<string, double>("blitz", r.Style == "Premium" ? 2 : r.Style == "Generalist" ? 1.5 : 1),
                new KeyValuePair<string, double>("expand", r.Style == "Trend Chaser" ? 2 : r.Style == "Generalist" ? 1.5 : 0.8),
                new KeyValuePair<string, double>("copycat", r.Style == "Trend Chaser" ? 2 : r.Style == "Discounter" ? 1 : 0.4),
                new KeyValuePair<string, double>("poach", 0.5),
                new KeyValuePair<string, double>("stumble", 0.6),
            };
            string move = Rng.PickWeighted(moves, m => m.Value).Key;
            var ourInFocus = C.Products.FindAll(cp => cp.Active && r.Focus.Contains(cp.Category));
            switch (move)
            {
                case "pricewar":
                {
                    if (ourInFocus.Count == 0) goto case "expand";
                    string cat = Rng.Pick(ourInFocus).Category;
                    double cut = Math.Round(Rng.Range(0.12, 0.2), 2);
                    foreach (var mp in State.Market.Products)
                        if (mp.Category == cat) { mp.CompetitorCut = Math.Max(mp.CompetitorCut, cut); mp.CompetitorCutUntil = State.Day + 30; }
                    SetRivalMove(r, $"started a price war in {cat} (-{Fmt.Pct(cut)})");
                    var e = NewEvent("price_war", EventCategory.Competition, 2, $"{r.Name} started a price war in {cat}",
                        $"{r.Name} cut prices on every {cat} product by about {Fmt.Pct(cut)} for a month. Our {cat} sales will suffer unless we respond.", 5);
                    e.SupplierId = r.Id;
                    e.Amount = cut;
                    e.Text += $" (We sell {ourInFocus.FindAll(x => x.Category == cat).Count} {cat} products.)";
                    e.ProductId = null;
                    e.EmployeeId = cat; // category carried in a spare field
                    e.Choices.Add(Choice($"Cut our {cat} prices 10%", "Protect volume, sacrifice margin.", 0, 3));
                    e.Choices.Add(Choice("Hold prices, run defensive ads", "A month of extra visibility across the category.", 5000, 3));
                    e.Choices.Add(Choice("Ignore them", "Our customers are loyal. Right?", 0, -1, 0, 0.4));
                    AddEvent(e);
                    break;
                }
                case "blitz":
                    foreach (var cp in ourInFocus) { cp.PenaltyMult = Math.Min(cp.PenaltyMult, 0.9); cp.PenaltyUntil = Math.Max(cp.PenaltyUntil, State.Day + 20); }
                    SetRivalMove(r, $"launched a marketing blitz in {string.Join(" & ", r.Focus)}");
                    if (ourInFocus.Count > 0) NotifyAll(NoticeKind.Warning, $"{r.Name} launched a marketing blitz. Our {ourInFocus[0].Category} sales will dip for a few weeks.");
                    break;
                case "expand":
                {
                    // Expand into the category where we are strongest: they come for our customers.
                    string best = null;
                    double bestShare = -1;
                    foreach (var kv in C.CategoryShare)
                        if (!r.Focus.Contains(kv.Key) && kv.Value > bestShare) { bestShare = kv.Value; best = kv.Key; }
                    if (best == null) best = Rng.Pick(Catalog.Categories);
                    if (r.Focus.Contains(best)) break;
                    r.Focus.Add(best);
                    if (r.Focus.Count > 4) r.Focus.RemoveAt(0);
                    SetRivalMove(r, $"expanded into {best}");
                    if (bestShare > 0.1) NotifyAll(NoticeKind.Warning, $"{r.Name} is expanding into {best}, where we are strong.");
                    break;
                }
                case "copycat":
                {
                    CompanyProduct top = null;
                    foreach (var cp in C.Products) if (cp.Active && (top == null || cp.Sales7 > top.Sales7)) top = cp;
                    if (top == null || top.Sales7 < 5) goto case "blitz";
                    var mp = State.GetMarketProduct(top.ProductId);
                    mp.Competition = Math.Min(0.95, mp.Competition + 0.12);
                    SetRivalMove(r, $"copied our {top.Name}");
                    NotifyAll(NoticeKind.Warning, $"{r.Name} launched a copy of our {top.Name}. Competition just got tougher.");
                    break;
                }
                case "poach":
                {
                    var e = MakePoaching();
                    if (e != null) { e.Title = $"{r.Name} is poaching our people"; AddEvent(e); SetRivalMove(r, "is poaching our staff"); }
                    break;
                }
                case "stumble":
                {
                    r.Strength = Math.Max(0.3, r.Strength - 0.25);
                    r.Valuation = Math.Round(r.Valuation * 0.9 / 1000) * 1000;
                    string cat = Rng.Pick(r.Focus);
                    SetRivalMove(r, "is stumbling after a logistics meltdown");
                    foreach (var cp in C.Products)
                        if (cp.Active && cp.Category == cat) { cp.BoostMult = Math.Max(cp.BoostMult, 1.15); cp.BoostUntil = Math.Max(cp.BoostUntil, State.Day + 20); }
                    var e = NewEvent("rival_stumble", EventCategory.Opportunity, 1, $"{r.Name} is stumbling",
                        $"{r.Name} botched their holiday logistics. Their {cat} customers are looking for alternatives.", 5);
                    e.SupplierId = r.Id;
                    e.EmployeeId = cat;
                    e.Choices.Add(Choice($"Launch a {cat} campaign to win them over", "Big visibility boost for our products in that category for a month.", 4000, 5));
                    e.Choices.Add(Choice("Noted", "", 0, 1));
                    AddEvent(e);
                    break;
                }
            }
        }

        void SetRivalMove(Rival r, string text)
        {
            r.LastMove = text;
            r.LastMoveDay = State.Day;
            Log(LogKind.Market, -1, $"{r.Name} {text}.");
        }

        /// <summary>1-based rank of our company by valuation among active rivals.</summary>
        public int CompanyRank()
        {
            int rank = 1;
            foreach (var r in State.Rivals) if (!r.Acquired && r.Valuation > C.Valuation) rank++;
            return rank;
        }

        public int CompanyCount()
        {
            int n = 1;
            foreach (var r in State.Rivals) if (!r.Acquired) n++;
            return n;
        }

        void RankDaily()
        {
            if (State.Day % 7 != 0) return;
            if (C.ReachedTop || CompanyRank() != 1) return;
            C.ReachedTop = true;
            Log(LogKind.Company, -1, $"MILESTONE: {C.Name} is now the #1 e-commerce company in the industry!");
            Celebrate($"#1 in the industry!|{C.Name} is now worth more than every rival.");
            foreach (var p in State.Players) Award(p.Id, 100, "Company became #1 in the industry");
        }

        public double AcquisitionPrice(Rival r) => Math.Round(r.Valuation * (r.Strength < 0.7 ? 0.8 : 1.15) / 10000) * 10000;

        public bool CanAcquire(Rival r, out string why)
        {
            why = null;
            if (r == null || r.Acquired) { why = "Not available."; return false; }
            if (C.Stage < 3) { why = $"Acquisitions unlock at the {Stages.Get(3).Name} stage."; return false; }
            if (C.Valuation < r.Valuation * 1.5) { why = $"We need to be worth at least 1.5x {r.Name} ({Fmt.Money(r.Valuation * 1.5)})."; return false; }
            return true;
        }

        string AcquireRival(Rival r, int actor)
        {
            double price = AcquisitionPrice(r);
            if (C.Cash < price) return $"!We can't afford {Fmt.Money(price)}";
            Spend(price, TxType.Investment, actor, $"Acquired {r.Name}");
            r.Acquired = true;
            r.AcquiredDay = State.Day;
            r.AcquiredBy = actor;
            r.AcquiredFor = price;
            foreach (var cat in r.Focus) if (!C.AcquiredCategories.Contains(cat)) C.AcquiredCategories.Add(cat);
            C.Reputation = Math.Min(100, C.Reputation + 5);
            Log(LogKind.Company, actor, $"MILESTONE: {C.Name} acquired {r.Name} for {Fmt.Money(price)}.");
            Celebrate($"We acquired {r.Name}!|Their customers in {string.Join(", ", r.Focus)} are ours now: +15% demand in those categories, permanently.");
            if (actor >= 0) Award(actor, 200, $"Acquired {r.Name}");
            return $"{r.Name} is ours";
        }

        // ------------------------------------------------------------------ promotions

        public static int PromoDays(string promo) => promo == "flash" ? 3 : 7;
        public static int PromoCooldown(string promo) => promo == "flash" ? 25 : 30;

        CmdResult CmdStartPromotion(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            string promo = c.I == 1 ? "freeship" : "flash";
            if (cp.PromoActive(State.Day)) return CmdResult.Fail("A promotion is already running on this product.");
            if (State.Day < cp.PromoCooldownUntil) return CmdResult.Fail($"Customers need a break: next promotion possible on {Fmt.ShortDate(cp.PromoCooldownUntil)}.");
            if (cp.Stock < 20) return CmdResult.Fail("Not enough stock to run a promotion.");
            cp.Promo = promo;
            cp.PromoUntil = State.Day + PromoDays(promo);
            cp.PromoCooldownUntil = cp.PromoUntil + PromoCooldown(promo);
            cp.PromoUnits = 0;
            cp.PromoRevenue = 0;
            Award(p.Id, 10, promo == "flash" ? $"Flash sale on {cp.Name}" : $"Free shipping week on {cp.Name}");
            Log(LogKind.Company, p.Id, promo == "flash"
                ? $"{p.Name} started a 3-day flash sale on {cp.Name} (20% off)."
                : $"{p.Name} started a free-shipping week on {cp.Name}.");
            return CmdResult.Success();
        }

        void PromotionsDaily()
        {
            foreach (var cp in C.Products)
            {
                if (cp.Promo == null || State.Day <= cp.PromoUntil) continue;
                string what = cp.Promo == "flash" ? "Flash sale" : "Free shipping week";
                Log(LogKind.Company, -1, $"{what} on {cp.Name} ended: {Fmt.Int(cp.PromoUnits)} units sold for {Fmt.Money(cp.PromoRevenue)}.");
                NotifyAll(NoticeKind.Info, $"{what} on {cp.Name} ended: {Fmt.Int(cp.PromoUnits)} units, {Fmt.Money(cp.PromoRevenue)}.");
                cp.Promo = null;
                cp.AfterglowUntil = State.Day + 10;
            }
        }

        // ------------------------------------------------------------------ product R&D

        public double ProductUpgradeCost(CompanyProduct cp)
        {
            var mp = State.GetMarketProduct(cp.ProductId);
            double refPrice = mp != null ? mp.RefPrice : cp.Price;
            return Math.Round((refPrice * (cp.Version == 1 ? 500 : 1200) + 5000) / 500) * 500;
        }

        public int ProductUpgradeDays(CompanyProduct cp) => cp.Version == 1 ? 10 : 18;

        public bool CanUpgradeProduct(CompanyProduct cp, out string why)
        {
            why = null;
            if (cp == null || !cp.Active) { why = "Unknown product."; return false; }
            if (cp.Version >= 3) { why = "Already the Pro version."; return false; }
            if (cp.UpgradeDoneDay > 0) { why = "R&D is already working on it."; return false; }
            if (cp.Version == 1 && C.Stage < 1) { why = $"Product upgrades unlock at the {Stages.Get(1).Name} stage."; return false; }
            if (cp.Version == 2 && (C.Stage < 2 || !C.Has("src2"))) { why = $"Pro versions need the {Stages.Get(2).Name} stage and the Private Label Program."; return false; }
            return true;
        }

        CmdResult CmdUpgradeProduct(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (!CanUpgradeProduct(cp, out string why)) return CmdResult.Fail(why);
            double cost = ProductUpgradeCost(cp);
            if (C.Cash < cost) return CmdResult.Fail($"R&D costs {Fmt.Money(cost)}.");
            if (NeedsApproval(p, c, cost, $"Develop {cp.BaseName ?? cp.Name} {(cp.Version == 1 ? "Plus" : "Pro")}", out var ar)) return ar;
            Spend(cost, TxType.Research, p.Id, $"R&D: {cp.BaseName ?? cp.Name} {(cp.Version == 1 ? "Plus" : "Pro")}");
            int days = ProductUpgradeDays(cp);
            cp.UpgradeDoneDay = State.Day + days;
            cp.UpgradedBy = p.Id;
            Log(LogKind.Company, p.Id, $"{p.Name} started R&D on {cp.BaseName ?? cp.Name} {(cp.Version == 1 ? "Plus" : "Pro")} ({Fmt.Money(cost)}, {Fmt.Days(days)}).");
            return CmdResult.Success($"R&D started. The new version launches in {Fmt.Days(days)}.");
        }

        void ProductUpgradesDaily()
        {
            foreach (var cp in C.Products)
            {
                if (cp.UpgradeDoneDay <= 0 || State.Day < cp.UpgradeDoneDay) continue;
                cp.UpgradeDoneDay = 0;
                if (string.IsNullOrEmpty(cp.BaseName)) cp.BaseName = cp.Name;
                cp.Version = Math.Min(3, cp.Version + 1);
                cp.Name = cp.BaseName + (cp.Version == 2 ? " Plus" : " Pro");
                cp.Rating = Math.Min(5, cp.Rating + 0.2);
                Log(LogKind.Company, cp.UpgradedBy, $"{cp.Name} is live: better quality, more demand, customers accept a higher price.");
                NotifyAll(NoticeKind.Success, $"{cp.Name} is live! +15% demand and customers accept about 10% higher prices.");
                if (cp.UpgradedBy >= 0) Award(cp.UpgradedBy, 60 * (cp.Version - 1), $"Launched {cp.Name}");
            }
        }

        // ------------------------------------------------------------------ auto-restock

        CmdResult CmdSetAutoRestock(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            if (!C.Has("dat2")) return CmdResult.Fail("Auto-restock needs the Demand Forecasting upgrade.");
            cp.AutoRestock = c.Flag;
            cp.AutoRestockDays = Clamp(c.I <= 0 ? 30 : c.I, 10, 120);
            Log(LogKind.Company, p.Id, $"{p.Name} turned auto-restock {(c.Flag ? $"on ({cp.AutoRestockDays} days of stock)" : "off")} for {cp.Name}.");
            return CmdResult.Success();
        }

        void AutoRestockDaily()
        {
            if (!C.Has("dat2")) return;
            foreach (var cp in C.Products)
            {
                if (!cp.Active || !cp.AutoRestock || IncomingUnits(cp.Id) > 0 || cp.Sales7 < 0.5) continue;
                var s = PreferredSupplier(cp);
                if (s == null || State.Day < s.BlockedUntil) continue;
                double days = cp.Stock / Math.Max(0.1, cp.Sales7);
                if (days > s.LeadTime + 5) continue;
                var mp = State.GetMarketProduct(cp.ProductId);
                int qty = (int)(Math.Ceiling(Math.Max(s.Moq, cp.Sales7 * (s.LeadTime + cp.AutoRestockDays) - cp.Stock) / 50.0) * 50);
                int space = StorageCapacity() - StockOnHand() - IncomingUnits();
                if (space < s.Moq) continue;
                if (qty > space) qty = space / 50 * 50;
                var buyer = P(cp.ManagerId) ?? State.Players[0];
                double unit = UnitPrice(s, mp, qty, buyer.Id);
                double budget = C.Cash - MonthlyObligations() * 0.3;
                if (unit * qty > budget) qty = (int)(Math.Max(0, budget) / unit / 50) * 50;
                if (qty < s.Moq) continue;
                PlaceOrder(buyer, cp, mp, s, qty, false);
                Log(LogKind.Company, -1, $"Auto-restock ordered {Fmt.Int(qty)} x {cp.Name} from {s.Name}.");
            }
        }

        // ------------------------------------------------------------------ contracts

        static readonly string[] ContractClients =
        {
            "A hotel chain", "A corporate gifting agency", "A national gym chain", "A department store", "A subscription box company",
            "A university bookstore", "An airline's duty-free catalog", "A big-box retailer"
        };

        GameEvent MakeContractOffer()
        {
            var cp = RandomActiveProduct(x => x.Sales7 >= 3);
            if (cp == null) return null;
            int open = 0;
            foreach (var k in C.Contracts) if (k.Status == 0) open++;
            if (open >= 3 || HasOpenEvent("contract_offer")) return null;
            bool wholesale = C.Has("chn3");
            int qty = (int)(Math.Round(Math.Max(150, cp.Sales7 * Rng.Range(8, 20) * (wholesale ? 1.6 : 1) * (1 + C.Stage * 0.2)) / 50) * 50);
            double unit = Math.Round(cp.Price * Rng.Range(0.8, 0.95), 2);
            int due = Rng.Range(14, 32);
            double penalty = Math.Round(qty * unit * 0.2 / 100) * 100;
            string client = Rng.Pick(ContractClients);
            var e = NewEvent("contract_offer", EventCategory.Opportunity, 1, $"Contract offer: {Fmt.Int(qty)} x {cp.Name}",
                $"{client} wants {Fmt.Int(qty)} units of {cp.Name} at {Fmt.Price(unit)} each ({Fmt.Money(qty * unit)}), delivered within {due} days. No marketplace fees. Missing the deadline costs {Fmt.Money(penalty)}.", 4);
            e.ProductId = cp.Id; e.Count = qty; e.Amount = unit; e.Amount2 = penalty; e.SupplierId = client; e.EmployeeId = due.ToString();
            e.Choices.Add(Choice("Sign the contract", $"Deliver {Fmt.Int(qty)} units by the deadline.", 0, 5));
            e.Choices.Add(Choice("Negotiate 8% more", "They might accept. They might walk.", 0, 4, 0, 0.5));
            e.Choices.Add(Choice("Decline", "", 0, 0));
            return e;
        }

        void SignContract(GameEvent e, Player p, double unitPrice)
        {
            var cp = State.GetCompanyProduct(e.ProductId);
            int.TryParse(e.EmployeeId, out int due);
            var k = new Contract
            {
                Id = NewId("k"), Client = e.SupplierId, CompanyProductId = e.ProductId, ProductName = cp?.Name, Quantity = e.Count, UnitPrice = unitPrice,
                CreatedDay = State.Day, DueDay = State.Day + Math.Max(7, due), AcceptedBy = p.Id, Penalty = e.Amount2
            };
            C.Contracts.Add(k);
            if (C.Contracts.Count > 30) C.Contracts.RemoveAll(x => x.Status != 0 && State.Day - x.CompletedDay > 60);
        }

        void ContractsDaily()
        {
            foreach (var k in C.Contracts)
            {
                if (k.Status != 0) continue;
                var cp = State.GetCompanyProduct(k.CompanyProductId);
                if (cp != null && cp.Active && cp.Stock >= k.Quantity)
                {
                    double revenue = k.Quantity * k.UnitPrice;
                    double cogs = k.Quantity * cp.AvgCost;
                    double fees = revenue * 0.03;
                    cp.Stock -= k.Quantity;
                    C.Cash += revenue - fees;
                    C.Today.Revenue += revenue;
                    C.Today.Cogs += cogs;
                    C.Today.Fees += fees;
                    C.Today.Units += k.Quantity;
                    C.LifetimeUnits += k.Quantity;
                    cp.Revenue += revenue;
                    cp.Profit += revenue - cogs - fees;
                    cp.ProfitQuarter += revenue - cogs - fees;
                    cp.SoldTotal += k.Quantity;
                    CreditProfit(cp, revenue - cogs - fees);
                    AddLedger(TxType.Revenue, revenue - fees, k.AcceptedBy, $"Contract delivered: {Fmt.Int(k.Quantity)} x {cp.Name} for {k.Client}", null, 0);
                    k.Status = 1;
                    k.CompletedDay = State.Day;
                    C.Reputation = Math.Min(100, C.Reputation + 1);
                    var acc = P(k.AcceptedBy);
                    if (acc != null)
                    {
                        acc.ContractsDelivered++;
                        Award(acc.Id, (int)Clamp(40 + k.Quantity / 40.0, 40, 120), $"Delivered a contract for {k.Client}");
                    }
                    Log(LogKind.Company, k.AcceptedBy, $"Delivered {Fmt.Int(k.Quantity)} x {cp.Name} to {k.Client} ({Fmt.Money(revenue)}).");
                    NotifyAll(NoticeKind.Success, $"Contract delivered to {k.Client}: {Fmt.Money(revenue)}.");
                }
                else if (State.Day > k.DueDay)
                {
                    k.Status = 2;
                    k.CompletedDay = State.Day;
                    Spend(k.Penalty, TxType.Fine, k.AcceptedBy, $"Contract penalty: {k.Client}");
                    C.Reputation = Math.Max(0, C.Reputation - 3);
                    Penalize(k.AcceptedBy, 40, $"Missed the contract deadline for {k.Client}");
                    Log(LogKind.Company, k.AcceptedBy, $"We missed the {k.Client} contract deadline. Penalty {Fmt.Money(k.Penalty)}.");
                    NotifyAll(NoticeKind.Danger, $"Missed the {k.Client} contract: {Fmt.Money(k.Penalty)} penalty.");
                }
            }
        }

        // ------------------------------------------------------------------ ambitions

        public int YearsScale => Math.Max(1, State.Settings.GameYears > 0 ? State.Settings.GameYears : 3);

        public void AssignAmbitionChoices()
        {
            foreach (var p in State.Players)
            {
                var pr = Priv(p.Id);
                var pool = new List<AmbitionDef>();
                foreach (var a in Ambitions.All) if (!a.NeedsIntrigue || State.Settings.Intrigue) pool.Add(a);
                Rng.Shuffle(pool);
                pr.AmbitionChoices.Clear();
                for (int i = 0; i < 3 && i < pool.Count; i++) pr.AmbitionChoices.Add(pool[i].Id);
                if (p.IsAI) pr.Ambition = AIPreferredAmbition(p, pr.AmbitionChoices);
            }
        }

        static string AIPreferredAmbition(Player ai, List<string> choices)
        {
            string[] prefs;
            switch (ai.Personality)
            {
                case Personality.Greedy: prefs = new[] { "shark", "fatcat", "mogul" }; break;
                case Personality.Loyal: prefs = new[] { "boyscout", "workhorse", "empire" }; break;
                case Personality.RiskTaker: prefs = new[] { "empire", "topdog", "visionary" }; break;
                case Personality.Conservative: prefs = new[] { "mogul", "tycoon", "dealmaker" }; break;
                case Personality.Workaholic: prefs = new[] { "workhorse", "visionary", "dealmaker" }; break;
                case Personality.Lazy: prefs = new[] { "fatcat", "mogul" }; break;
                case Personality.Manipulative: prefs = new[] { "shark", "kingmaker", "tycoon" }; break;
                default: prefs = new[] { "tycoon", "kingmaker", "topdog" }; break;
            }
            foreach (var p in prefs) if (choices.Contains(p)) return p;
            return choices.Count > 0 ? choices[0] : "empire";
        }

        CmdResult CmdChooseAmbition(Command c, Player p)
        {
            var pr = Priv(p.Id);
            if (!string.IsNullOrEmpty(pr.Ambition)) return CmdResult.Fail("You already chose your ambition.");
            if (!pr.AmbitionChoices.Contains(c.A)) return CmdResult.Fail("That ambition wasn't offered to you.");
            pr.Ambition = c.A;
            return CmdResult.Success($"Your secret ambition: {Ambitions.Get(c.A)?.Name}. Nobody else knows.");
        }

        void AmbitionsDaily()
        {
            if (State.Day != 20) return;
            foreach (var p in State.Players)
            {
                var pr = Priv(p.Id);
                if (string.IsNullOrEmpty(pr.Ambition) && pr.AmbitionChoices.Count > 0)
                {
                    pr.Ambition = pr.AmbitionChoices[0];
                    Notify(p.Id, NoticeKind.Private, $"You didn't pick an ambition, so fate chose one: {Ambitions.Get(pr.Ambition)?.Name}.");
                }
            }
        }

        public string AmbitionText(string id)
        {
            var a = Ambitions.Get(id);
            if (a == null) return "";
            int y = YearsScale;
            return a.Description
                .Replace("{N}", AmbitionCountTarget(id).ToString())
                .Replace("{M}", Fmt.Money(AmbitionMoneyTarget(id)))
                .Replace("{S}", Stages.Get(Math.Min(4, 1 + y)).Name);
        }

        int AmbitionCountTarget(string id)
        {
            int y = YearsScale;
            switch (id)
            {
                case "workhorse": return 12 * y;
                case "visionary": return 3 * y;
                case "kingmaker": return 3 * y;
                case "dealmaker": return 4 * y;
                case "talent": return 5 * y;
            }
            return 0;
        }

        double AmbitionMoneyTarget(string id) => id == "shark" ? 100000 * YearsScale : id == "mogul" ? 250000 * YearsScale : 0;

        /// <summary>Progress toward a player's ambition: a short status text, a 0..1 fraction and whether it is currently met.</summary>
        public (string status, double fraction, bool done) AmbitionProgress(int playerId)
        {
            var p = P(playerId);
            var pr = Priv(playerId);
            if (p == null || pr == null || string.IsNullOrEmpty(pr.Ambition)) return ("", 0, false);
            switch (pr.Ambition)
            {
                case "tycoon": { double own = OwnershipPct(p); return ($"You own {Fmt.Pct(own, 1)} of 35%", own / 0.35, own >= 0.35); }
                case "rainmaker":
                {
                    int rank = 1;
                    foreach (var o in State.Players) if (o.Id != p.Id && o.ProfitGenerated > p.ProfitGenerated) rank++;
                    return ($"#{rank} in profit generated ({Fmt.Money(p.ProfitGenerated)})", rank == 1 ? 1 : 1.0 / rank, rank == 1 && p.ProfitGenerated > 0);
                }
                case "workhorse": return Count(p.EventsResolved, AmbitionCountTarget("workhorse"), "situations handled");
                case "shark": { double t = AmbitionMoneyTarget("shark"); return ($"{Fmt.Money(pr.SecretIncome)} of {Fmt.Money(t)} off the books", pr.SecretIncome / t, pr.SecretIncome >= t); }
                case "boyscout":
                {
                    bool clean = pr.OffersAccepted == 0;
                    int good = p.Reports + p.Exposures;
                    return (clean ? $"Clean so far · {good} of 2 reported or exposed" : "You took a deal. This one is lost.", clean ? Math.Min(1, good / 2.0) : 0, clean && good >= 2);
                }
                case "visionary": return Count(p.ProductsLaunched, AmbitionCountTarget("visionary"), "products launched");
                case "empire":
                {
                    int target = Math.Min(4, 1 + YearsScale);
                    return ($"Stage: {Stages.Get(C.Stage).Name} (goal: {Stages.Get(target).Name})", C.Stage / (double)target, C.Stage >= target);
                }
                case "fatcat":
                {
                    int rank = 1;
                    foreach (var o in State.Players) if (o.Id != p.Id && o.Salary > p.Salary) rank++;
                    return ($"#{rank} salary ({Fmt.Money(p.Salary)}/mo)", rank == 1 ? 1 : 1.0 / rank, rank == 1);
                }
                case "kingmaker": return Count(p.ProposalsPassed, AmbitionCountTarget("kingmaker"), "proposals passed");
                case "mogul":
                {
                    double w = pr.Cash + HoldingsValue(p.Id), t = AmbitionMoneyTarget("mogul");
                    return ($"{Fmt.Money(w)} of {Fmt.Money(t)} in personal wealth", w / t, w >= t);
                }
                case "dealmaker": return Count(p.NegotiationsWon + p.ContractsDelivered, AmbitionCountTarget("dealmaker"), "deals won");
                case "talent": return Count(p.Hires, AmbitionCountTarget("talent"), "hires");
                case "topdog": { int rank = CompanyRank(); return ($"Company rank #{rank} of {CompanyCount()}", 1.0 / rank, rank == 1); }
            }
            return ("", 0, false);
        }

        static (string, double, bool) Count(int have, int need, string label) => ($"{have} of {need} {label}", need > 0 ? have / (double)need : 0, have >= need);

        public double AmbitionBonus(double finalValuation) => Math.Max(100000, finalValuation * 0.05);

        // ------------------------------------------------------------------ delegation

        public bool AIHasRole(Player ai, string role) => !ai.IsAI || ai.Roles.Contains(role);

        public void AssignDefaultRoles()
        {
            var ais = State.Players.FindAll(p => p.IsAI);
            if (ais.Count == 0) return;
            var taken = new HashSet<string>();
            // Each AI starts with one area (its favourite still free); the rest is left to the humans.
            foreach (var ai in ais)
            {
                foreach (var r in Roles.Preferred(ai.Personality))
                    if (!taken.Contains(r)) { ai.Roles.Add(r); taken.Add(r); break; }
                if (ai.Roles.Count == 0) ai.Roles.Add(Roles.Preferred(ai.Personality)[0]);
            }
            // With no humans at all, AIs must cover everything.
            if (HumanCount == 0)
                foreach (var r in Roles.All)
                    if (!taken.Contains(r)) { ais[taken.Count % ais.Count].Roles.Add(r); taken.Add(r); }
        }

        /// <summary>Take purchasing and upgrade duties away from a disciplined AI. With no humans to pick them up, another AI does.</summary>
        void StripMoneyRoles(Player ai)
        {
            foreach (var role in new[] { Roles.Buying, Roles.Upgrades })
            {
                if (!ai.Roles.Remove(role)) continue;
                Player heir = null;
                if (HumanCount == 0)
                    foreach (var o in State.Players)
                        if (o.IsAI && o.Id != ai.Id && o.SpendingLimit > 0 && (heir == null || o.Roles.Count < heir.Roles.Count)) heir = o;
                if (heir != null && !heir.Roles.Contains(role)) heir.Roles.Add(role);
                Log(LogKind.Team, ai.Id, heir != null
                    ? $"{ai.Name} lost {Roles.Name(role)} duties after the disciplinary vote. {heir.Name} takes over."
                    : $"{ai.Name} lost {Roles.Name(role)} duties after the disciplinary vote. The human partners handle it now.");
                foreach (var h in State.Players)
                    if (!h.IsAI) Notify(h.Id, NoticeKind.Warning, $"{ai.Name} no longer handles {Roles.Name(role)}. You can reassign it on the Partners page.");
            }
        }

        CmdResult CmdSetRoles(Command c, Player p)
        {
            var ai = P(c.I);
            if (ai == null || !ai.IsAI) return CmdResult.Fail("Only AI partners can be given responsibilities.");
            if (p.IsAI) return CmdResult.Fail("Only human partners delegate work.");
            var roles = new List<string>();
            foreach (var r in (c.Text ?? "").Split(','))
            {
                string t = r.Trim();
                if (Array.IndexOf(Roles.All, t) >= 0 && !roles.Contains(t)) roles.Add(t);
            }
            var added = roles.FindAll(r => !ai.Roles.Contains(r));
            ai.Roles = roles;
            string list = roles.Count == 0 ? "nothing" : string.Join(", ", roles.ConvertAll(Roles.Name));
            Log(LogKind.Team, p.Id, $"{p.Name} put {ai.Name} in charge of: {list}.");
            if (added.Count > 0)
            {
                var t = Traits.For(ai.Personality);
                double trust = GetTrust(ai, p.Id);
                string reply = ai.Personality == Personality.Lazy ? "More work? Fine. I'll get to it." :
                               ai.Personality == Personality.Greedy && added.Contains(Roles.Buying) ? "Purchasing? Excellent. I know all the right suppliers." :
                               trust < 35 ? "Sure. I'll do it my way." :
                               t.Diligence > 0.8 ? "On it. You won't regret this." : "Okay, I'll take care of it.";
                Say(ai.Id, reply, p.Id);
            }
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ starter product & claiming

        CmdResult CmdClaimProduct(Command c, Player p)
        {
            var cp = State.GetCompanyProduct(c.A);
            if (cp == null || !cp.Active) return CmdResult.Fail("Unknown product.");
            if (cp.ManagerId >= 0) return CmdResult.Fail($"{State.PlayerName(cp.ManagerId)} already manages it.");
            cp.ManagerId = p.Id;
            Award(p.Id, 10, $"Took charge of {cp.Name}");
            Log(LogKind.Company, p.Id, $"{p.Name} took charge of {cp.Name}. Its profits now count toward their performance.");
            return CmdResult.Success();
        }

        // ------------------------------------------------------------------ IPO

        void IpoDaily()
        {
            if (!C.IpoPending || State.Day < C.IpoDay || State.Phase != GamePhase.Running) return;
            C.IpoPending = false;
            C.WentPublic = true;
            Log(LogKind.Company, -1, $"{C.Name} rang the opening bell. The IPO is complete!");
            Celebrate($"{C.Name} is now public!|The IPO values the company at a 50% premium. Every partner's shares are now liquid.");
            EndGame($"{C.Name} went public! The IPO valued the company at {Fmt.Money(C.Valuation * 1.5)}, and every partner cashed in.");
        }
    }
}
