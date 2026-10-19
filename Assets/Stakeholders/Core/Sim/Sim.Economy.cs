using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    public partial class GameSimulation
    {
        public const double ShippingPerUnit = 2.5;
        readonly Dictionary<string, double> _adSpendToday = new Dictionary<string, double>();
        double _qualitySoldToday;
        double _unitsSoldToday;

        // ------------------------------------------------------------------ pricing

        public static double BulkDiscount(int qty, bool bulkSpecialist)
        {
            if (bulkSpecialist)
            {
                if (qty >= 10000) return 0.18;
                if (qty >= 5000) return 0.14;
            }
            if (qty >= 2500) return 0.09;
            if (qty >= 1000) return 0.06;
            if (qty >= 500) return 0.03;
            return 0;
        }

        /// <summary>Price per unit the company pays this supplier for this product, as ordered by this player.</summary>
        public double UnitPrice(Supplier s, MarketProduct mp, int qty, int buyerId)
        {
            double list = mp.BaseCost * s.CostFactor;
            double price = list;
            price *= 1 - BulkDiscount(qty, Has(buyerId, Skills.BulkSpecialist));
            if (s.Discount > 0 && State.Day <= s.DiscountUntil) price *= 1 - s.Discount;
            price *= 1 - BuyerBonus();
            if (C.Has("supplierstake")) price *= 0.94;
            if (C.Has("src3")) price *= 0.92;
            if (C.Has("src4")) price *= 0.88;
            if (C.Positioning == "value") price *= 0.95;
            // Discounts stack, but suppliers still need to make money: never below 62% of their list price.
            return Math.Round(Math.Max(price, list * 0.62), 2);
        }

        /// <summary>Delivery cost per unit sold (more for international shipping and free-shipping promos).</summary>
        public double ShippingCost(CompanyProduct cp)
        {
            double s = ShippingPerUnit;
            if (C.Has("chn4")) s += 1.0;
            if (cp != null && cp.PromoActive(State.Day) && cp.Promo == "freeship") s += 2.0;
            return s;
        }

        /// <summary>What customers actually pay today (flash sales discount the list price).</summary>
        public double SellingPrice(CompanyProduct cp) => cp.PromoActive(State.Day) && cp.Promo == "flash" ? Math.Round(cp.Price * 0.8, 2) : cp.Price;

        /// <summary>Discount from purchasing buyers on staff (max 10%).</summary>
        public double BuyerBonus() => Math.Min(0.10, EmployeePower(EmployeeRole.Buyer) * 0.04);

        public bool CanUseSupplier(Supplier s, int playerId)
        {
            if (s.Bankrupt || s.Blacklisted) return false;
            if (s.Exclusive && !Has(playerId, Skills.SupplierNetwork)) return false;
            return true;
        }

        /// <summary>Cheapest unit price any non-exclusive, usable supplier offers for this product and quantity.</summary>
        public double CheapestUnitPrice(MarketProduct mp, int qty, int buyerId, out Supplier cheapest)
        {
            cheapest = null;
            double best = double.MaxValue;
            foreach (var s in State.Suppliers)
            {
                if (!s.Serves(mp.Category) || s.Bankrupt || s.Blacklisted) continue;
                if (s.Exclusive && !Has(buyerId, Skills.SupplierNetwork)) continue;
                double p = UnitPrice(s, mp, Math.Max(qty, s.Moq), buyerId);
                if (p < best) { best = p; cheapest = s; }
            }
            return cheapest == null ? mp.BaseCost : best;
        }

        public double FeeRate()
        {
            double rate = 0.12;
            if (State.Day <= C.FeeModifierUntil) rate += C.FeeModifier;
            if (C.Has("chn1")) rate -= 0.03;
            if (AnyoneHas(Skills.TaxSpecialist)) rate *= 0.85;
            return Math.Max(0.03, rate);
        }

        public double RefundRate(double quality)
        {
            double r = 0.09 - quality * 0.07;
            if (C.Policies.ReturnPolicy == 2) r += 0.025;
            if (C.Policies.ReturnPolicy == 0) r -= 0.015;
            if (C.Has("log3")) r *= 0.9;
            return Clamp(r, 0.01, 0.2);
        }

        public double EffectiveQuality(CompanyProduct cp)
        {
            double q = cp.Quality + cp.QualityBonus + 0.06 * (cp.Version - 1);
            if (C.Has("src2")) q += 0.08;
            return Clamp(q, 0, 1);
        }

        public static double Seasonality(string category, int monthIndex)
        {
            switch (category)
            {
                case "Electronics": return monthIndex == 10 ? 1.35 : monthIndex == 11 ? 1.6 : monthIndex == 0 ? 0.85 : 1;
                case "Toys & Games": return monthIndex == 10 ? 1.5 : monthIndex == 11 ? 1.9 : monthIndex == 0 ? 0.8 : 1;
                case "Fashion": return monthIndex == 10 ? 1.25 : monthIndex == 11 ? 1.35 : 1;
                case "Fitness": return monthIndex == 0 ? 1.45 : monthIndex == 1 ? 1.2 : monthIndex == 11 ? 0.85 : 1;
                case "Outdoor": return (monthIndex >= 4 && monthIndex <= 7) ? 1.35 : (monthIndex >= 10 || monthIndex <= 1) ? 0.75 : 1;
                case "Beauty": return monthIndex == 11 ? 1.2 : monthIndex == 1 ? 1.1 : 1;
                case "Home & Kitchen": return monthIndex == 10 ? 1.2 : monthIndex == 11 ? 1.3 : 1;
            }
            return 1;
        }

        // ------------------------------------------------------------------ capacity

        /// <summary>Sum of (skill-adjusted, morale-adjusted) headcount for a role.</summary>
        public double EmployeePower(EmployeeRole role)
        {
            double sum = 0;
            foreach (var e in C.Employees)
            {
                if (e.Role != role) continue;
                sum += EmployeeOutput(e);
            }
            return sum;
        }

        public double EmployeeOutput(Employee e)
        {
            double skill = 0.6 + 0.2 * e.Skill;
            double morale = 0.7 + 0.45 * e.Morale / 100.0;
            double leader = Has(e.ManagerId, Skills.Leader) ? 1.2 : 1.0;
            double training = C.Has("ppl2") ? 1.15 : 1.0;
            return skill * morale * leader * training;
        }

        public double FulfillmentCapacity()
        {
            double cap = 40 + 10 * State.Players.Count;
            cap += 90 * EmployeePower(EmployeeRole.Warehouse);
            double bonus = 0;
            foreach (var w in C.Warehouses) bonus = Math.Max(bonus, w.FulfillmentBonus);
            if (C.Has("log2")) bonus += 0.2;
            if (C.Has("log4")) bonus += 0.6;
            return cap * (1 + bonus);
        }

        public double SupportCapacity()
        {
            double cap = 30 + 150 * EmployeePower(EmployeeRole.Support);
            if (C.Has("ppl1")) cap *= 1.3;
            return cap;
        }

        public int StorageCapacity()
        {
            int cap = 0;
            foreach (var w in C.Warehouses) cap += w.Capacity;
            if (C.Has("log1")) cap = (int)(cap * 1.25);
            return cap;
        }

        public int StockOnHand()
        {
            int n = 0;
            foreach (var cp in C.Products) n += cp.Stock;
            return n;
        }

        public int IncomingUnits(string companyProductId = null)
        {
            int n = 0;
            foreach (var s in C.Shipments)
                if (companyProductId == null || s.CompanyProductId == companyProductId) n += s.Quantity;
            return n;
        }

        // ------------------------------------------------------------------ daily flow

        void ArriveShipments()
        {
            for (int i = C.Shipments.Count - 1; i >= 0; i--)
            {
                var s = C.Shipments[i];
                if (s.EtaDay > State.Day) continue;
                C.Shipments.RemoveAt(i);
                var cp = State.GetCompanyProduct(s.CompanyProductId);
                if (cp == null) continue;
                double totalCost = cp.AvgCost * cp.Stock + s.UnitCost * s.Quantity;
                double totalQuality = cp.Quality * cp.Stock + s.Quality * s.Quantity;
                cp.Stock += s.Quantity;
                cp.AvgCost = cp.Stock > 0 ? totalCost / cp.Stock : s.UnitCost;
                cp.Quality = cp.Stock > 0 ? totalQuality / cp.Stock : s.Quality;
                cp.Active = true;
                cp.DeadStockFlagged = false;
                var sup = State.GetSupplier(s.SupplierId);
                Log(LogKind.Company, s.OrderedBy, $"{Fmt.Int(s.Quantity)} units of {cp.Name} arrived from {sup?.Name ?? "supplier"}.");

                // Bad purchase: a proven product receiving far more than it can sell.
                // Judged on demand, not sales: a product that just sold out has depressed sales numbers.
                double rate = Math.Max(cp.Sales7, cp.Demand7 * 0.8);
                if (cp.SalesHistory.Count >= 21 && rate > 0.5)
                {
                    double daysOfStock = cp.Stock / rate;
                    if (daysOfStock > 200 && s.Quantity > rate * 120)
                        Penalize(s.OrderedBy, 50, $"Bad inventory purchase: {cp.Name} now has ~{Fmt.Int(daysOfStock)} days of stock.");
                }
            }
        }

        void RunCampaigns()
        {
            _adSpendToday.Clear();
            double totalSpend = 0;
            int active = 0;
            foreach (var cam in C.Campaigns)
            {
                if (!cam.Active) continue;
                var cp = State.GetCompanyProduct(cam.CompanyProductId);
                if (State.Day > cam.EndDay || cp == null || !cp.Active)
                {
                    EndCampaign(cam, cp == null || !cp.Active ? "product discontinued" : "finished");
                    continue;
                }
                cam.Spent7 *= 6.0 / 7.0;
                cam.Revenue7 *= 6.0 / 7.0;
                if (C.Cash <= 0 || cp.Stock <= 0) continue; // no money or nothing to sell: pause spend
                double spend = cam.DailyBudget;
                C.Cash -= spend;
                C.Today.Marketing += spend;
                cam.Spent += spend;
                cam.Spent7 += spend;
                totalSpend += spend;
                active++;
                _adSpendToday.TryGetValue(cp.Id, out double prev);
                _adSpendToday[cp.Id] = prev + spend;

                var mp = State.GetMarketProduct(cp.ProductId);
                double scale = mp.RefPrice * 4;
                // Brand building
                if (Has(cam.CreatedBy, Skills.BrandBuilder))
                    C.BrandBonus = Math.Min(15, C.BrandBonus + 0.012 * Math.Log(1 + spend / 100));
                // Viral chance (social mostly)
                double viralBase = cam.Channel == AdChannel.Social ? 0.004 : cam.Channel == AdChannel.Search ? 0.0008 : 0.0015;
                double pViral = mp.ViralPotential * viralBase * Math.Log(1 + spend / scale) * (Has(cam.CreatedBy, Skills.ViralMarketing) ? 2.5 : 1) * ViralMultiplier();
                if (!mp.Viral && Rng.Chance(pViral)) TriggerViral(mp, cp, cam.CreatedBy);
            }
            if (totalSpend > 0)
                AddLedger(TxType.Marketing, -totalSpend, -1, $"Advertising ({active} campaign{(active == 1 ? "" : "s")})", null, 0);
        }

        void EndCampaign(Campaign cam, string why)
        {
            cam.Active = false;
            var creator = P(cam.CreatedBy);
            string name = ProductName(cam.CompanyProductId);
            Log(LogKind.Company, cam.CreatedBy, $"{cam.Channel} campaign for {name} {why}. Spent {Fmt.Money(cam.Spent)}, ROAS {Fmt.Fixed(cam.Roas, 2)}x.");
            if (creator == null || cam.Reviewed) return;
            cam.Reviewed = true;
            // Attributed revenue at ~45% contribution margin approximates the profit impact.
            double profitImpact = cam.AttributedRevenue * 0.45 - cam.Spent;
            if (cam.Spent >= 2000 && cam.Roas >= 3) Award(creator.Id, 50, $"Great campaign for {name} ({Fmt.Fixed(cam.Roas, 1)}x ROAS)");
            else if (profitImpact < -20000) Penalize(creator.Id, 150, $"Major loss: the {name} campaign lost about {Fmt.Money(-profitImpact)}");
            else if (profitImpact < -5000) Penalize(creator.Id, 50, $"Money-losing campaign for {name}");
        }

        void TriggerViral(MarketProduct mp, CompanyProduct cp, int creditTo)
        {
            mp.Viral = true;
            mp.ViralUntil = State.Day + Rng.Range(7, 14);
            Log(LogKind.Market, creditTo, $"{mp.Name} is going VIRAL!");
            NotifyAll(NoticeKind.Success, $"{mp.Name} just went viral! Demand is exploding.");
            if (creditTo >= 0) Award(creditTo, 100, $"Made {mp.Name} go viral");
            if (cp != null) SpawnViralEvent(cp);
        }

        void SimulateSales()
        {
            double revenueTotal = 0, cogsTotal = 0, feesTotal = 0, refundsTotal = 0;
            int unitsTotal = 0;
            _qualitySoldToday = 0;
            _unitsSoldToday = 0;
            double feeRate = FeeRate();
            double staffMult = 1 + Math.Min(0.4, EmployeePower(EmployeeRole.Marketing) * 0.08);

            foreach (var cp in C.Products)
            {
                cp.SoldToday = 0;
                cp.LostToday = 0;
                if (!cp.Active) continue;
                var mp = State.GetMarketProduct(cp.ProductId);
                if (mp == null) continue;

                // Advertising buys additional customers at a cost per acquisition (diminishing returns).
                double adUnits = 0;
                if (_adSpendToday.ContainsKey(cp.Id))
                {
                    foreach (var cam in C.Campaigns)
                    {
                        if (!cam.Active || cam.CompanyProductId != cp.Id) continue;
                        adUnits += CampaignUnits(cam, cp, mp, staffMult);
                    }
                }

                double organic = DemandFor(cp, mp);
                double expected = organic + adUnits;
                double noisy = expected * (1 + Rng.Normal(0, 0.12));
                if (noisy < 0) noisy = 0;
                int units = (int)Math.Floor(noisy);
                if (Rng.Chance(noisy - units)) units++;
                int sold = Math.Min(units, cp.Stock);
                cp.Stock -= sold;
                cp.SoldToday = sold;
                cp.LostToday = units - sold;

                double q = EffectiveQuality(cp);
                double revenue = sold * SellingPrice(cp);
                double cogs = sold * cp.AvgCost;
                double fees = revenue * feeRate + sold * ShippingCost(cp);
                double refunds = revenue * RefundRate(q);
                double adSpend = 0;
                _adSpendToday.TryGetValue(cp.Id, out adSpend);
                double profit = revenue - cogs - fees - refunds - adSpend;

                // Attribute ad-driven revenue to campaigns
                if (adUnits > 0 && sold > 0 && expected > 0)
                {
                    double adRevenue = revenue * adUnits / expected;
                    foreach (var cam in C.Campaigns)
                    {
                        if (!cam.Active || cam.CompanyProductId != cp.Id) continue;
                        double share = CampaignUnits(cam, cp, mp, staffMult) / adUnits;
                        cam.AttributedRevenue += adRevenue * share;
                        cam.Revenue7 += adRevenue * share;
                    }
                }

                if (cp.PromoActive(State.Day)) { cp.PromoUnits += sold; cp.PromoRevenue += revenue; }
                cp.Revenue += revenue;
                cp.Profit += profit;
                cp.ProfitQuarter += profit;
                cp.SoldTotal += sold;
                cp.Sales7 = cp.Sales7 * 6.0 / 7.0 + sold / 7.0;
                cp.Demand7 = cp.Demand7 * 6.0 / 7.0 + units / 7.0;
                cp.SalesHistory.Add(sold);
                if (cp.SalesHistory.Count > 60) cp.SalesHistory.RemoveAt(0);
                cp.DaysNoSales = sold == 0 ? cp.DaysNoSales + 1 : 0;
                if (cp.Stock == 0 && units > sold) cp.StockoutDays++;

                double ratingTarget = 1.5 + q * 3.2 + (C.Satisfaction - 70) / 60.0 + (C.Has("brd1") ? 0.15 : 0) + (C.Positioning == "premium" ? 0.1 : 0) + 0.1 * (cp.Version - 1);
                cp.Rating = Clamp(cp.Rating + (ratingTarget - cp.Rating) * 0.02, 1, 5);

                CreditProfit(cp, profit);

                revenueTotal += revenue;
                cogsTotal += cogs;
                feesTotal += fees;
                refundsTotal += refunds;
                unitsTotal += sold;
                _qualitySoldToday += q * sold;
                _unitsSoldToday += sold;
            }

            C.Cash += revenueTotal - feesTotal - refundsTotal;
            C.Today.Revenue += revenueTotal;
            C.Today.Cogs += cogsTotal;
            C.Today.Fees += feesTotal;
            C.Today.Refunds += refundsTotal;
            C.Today.Units += unitsTotal;
            C.LifetimeUnits += unitsTotal;
            C.OrdersToday = unitsTotal;

            // Fulfillment: orders beyond capacity wait in a backlog; very late orders get cancelled.
            double capacity = FulfillmentCapacity();
            C.CapacityPerDay = capacity;
            C.Backlog += unitsTotal;
            double shipped = Math.Min(C.Backlog, capacity);
            C.Backlog -= shipped;
            double cancelRefund = 0;
            if (C.Backlog > capacity * 2)
            {
                double cancel = (C.Backlog - capacity * 2) * 0.3;
                double avgPrice = unitsTotal > 0 ? revenueTotal / unitsTotal : 25;
                cancelRefund = cancel * avgPrice;
                C.Backlog -= cancel;
                C.Cash -= cancelRefund;
                C.Today.Refunds += cancelRefund;
            }

            if (unitsTotal > 0 || cancelRefund > 0)
            {
                double net = revenueTotal - feesTotal - refundsTotal - cancelRefund;
                string text = $"Sales: {Fmt.Int(unitsTotal)} units, revenue {Fmt.Money(revenueTotal)} (fees & refunds {Fmt.Money(feesTotal + refundsTotal + cancelRefund)})";
                AddLedger(TxType.Revenue, net, -1, text, null, 0);
            }
        }

        /// <summary>
        /// Customers a campaign brings in per day. Each channel has a base cost per acquisition as a
        /// share of the price; hot, well-priced, well-reviewed products convert more cheaply.
        /// Returns halve once daily spend reaches the saturation point.
        /// </summary>
        double CampaignUnits(Campaign cam, CompanyProduct cp, MarketProduct mp, double staffMult)
        {
            double spend = cam.DailyBudget;
            double cpaRatio;
            switch (cam.Channel)
            {
                case AdChannel.Search: cpaRatio = 0.34; break;
                case AdChannel.Social: cpaRatio = 0.42; break;
                case AdChannel.Email:
                    cpaRatio = 0.22;
                    spend = Math.Min(spend, (20 + C.LifetimeUnits * 0.04) * (C.Has("brd3") ? 2 : 1)); // you can only email customers you have
                    break;
                default: cpaRatio = 0.3; break;
            }
            double price = SellingPrice(cp);
            double eff = cam.Efficiency * staffMult * (Has(cam.CreatedBy, Skills.AdSpecialist) ? 1.25 : 1.0) * (C.Has("brd2") ? 1.2 : 1.0);
            double compPrice = mp.CompetitorPrice * (1 - (State.Day <= mp.CompetitorCutUntil ? mp.CompetitorCut : 0));
            double priceFactor = Clamp(Math.Pow(Math.Max(0.1, price / (0.5 * mp.RefPrice + 0.5 * compPrice)), -mp.Elasticity), 0.05, 3);
            double appeal = Math.Sqrt(Clamp(mp.Trend * priceFactor * Clamp(1 + (cp.Rating - 4.0) * 0.25, 0.4, 1.4), 0.1, 6));
            double cpa = Math.Max(0.5, price * cpaRatio / (eff * appeal));
            double saturation = price * (15 + 25 * C.Reputation / 100.0);
            return spend / cpa / (1 + spend / saturation);
        }

        public double ViralMultiplier() => (C.Has("brd4") ? 2.0 : 1.0) * (C.Has("chn2") ? 1.5 : 1.0);

        /// <summary>Demand multiplier from upgrades, strategy, promotions, product version and market position.</summary>
        public double StrategyDemandMult(CompanyProduct cp, MarketProduct mp)
        {
            double m = 1;
            if (C.Has("log3")) m *= 1.05;
            if (C.Has("brd3")) m *= 1.08;
            if (C.Has("chn2")) m *= 1.08;
            if (C.Has("chn4")) m *= 1.35;
            if (C.Has("dat4")) m *= 1.06;
            switch (C.Positioning)
            {
                case "premium": m *= cp.Rating >= 4.0 ? 1.1 : cp.Rating >= 3.5 ? 0.95 : 0.85; break;
                case "value": m *= 1.12; break;
                case "trend": m *= (mp.Phase == TrendPhase.Emerging || mp.Phase == TrendPhase.Rising) ? 1.25 : 0.9; break;
            }
            m *= 1 + 0.15 * (cp.Version - 1);
            if (cp.PromoActive(State.Day)) m *= cp.Promo == "flash" ? 1.6 : 1.3;
            else if (State.Day <= cp.AfterglowUntil) m *= 1.08;
            if (C.CategoryShare.TryGetValue(mp.Category, out double share))
            {
                if (share >= 0.4) m *= 1.1;                          // the go-to brand in the category
                if (share > 0.55) m *= 1 - (share - 0.55) * 0.8;     // but a saturated market has few new customers left
            }
            // Our own products in the same category compete for the same shoppers.
            int siblings = 0;
            foreach (var other in C.Products) if (other.Active && other.Id != cp.Id && other.Category == cp.Category) siblings++;
            m *= Math.Max(0.75, 1 - 0.05 * siblings);
            if (C.AcquiredCategories.Contains(mp.Category)) m *= 1.15;
            return m;
        }

        public double DemandFor(CompanyProduct cp, MarketProduct mp)
        {
            double baseD = mp.BaseDemand * mp.Trend * Seasonality(mp.Category, State.MonthIndex) * State.Market.Economy;
            double compPrice = mp.CompetitorPrice * (1 - (State.Day <= mp.CompetitorCutUntil ? mp.CompetitorCut : 0));
            double refP = (0.5 * mp.RefPrice + 0.5 * compPrice) * (1 + 0.1 * (cp.Version - 1));
            double price = SellingPrice(cp);
            double elasticity = mp.Elasticity;
            if (C.Positioning == "premium" && price > refP) elasticity *= 0.75;
            if (C.Positioning == "value" && price < refP) elasticity *= 1.15;
            double priceFactor = Clamp(Math.Pow(Math.Max(0.1, price / refP), -elasticity), 0.02, 5);
            double repFactor = 0.7 + C.Reputation / 100.0 * 0.6;
            double ratingFactor = Clamp(1 + (cp.Rating - 4.0) * 0.25, 0.4, 1.4);
            double compFactor = 1.15 - 0.4 * mp.Competition;
            double viral = mp.Viral ? 2.0 : 1;
            double boost = State.Day <= cp.BoostUntil ? cp.BoostMult : 1;
            double penalty = State.Day <= cp.PenaltyUntil ? cp.PenaltyMult : 1;
            int age = State.Day - cp.LaunchedDay;
            double ramp = Math.Min(1, 0.35 + 0.65 * age / 14.0);
            double companyMod = State.Day <= C.DemandModifierUntil ? C.DemandModifier : 1;
            double difficulty = State.Settings.Difficulty == 0 ? 1.15 : State.Settings.Difficulty == 2 ? 0.85 : 1.0;
            return 0.9 * difficulty * baseD * priceFactor * repFactor * ratingFactor * compFactor * viral * boost * penalty * ramp * companyMod * StrategyDemandMult(cp, mp);
        }

        void CreditProfit(CompanyProduct cp, double profit)
        {
            var mgr = P(cp.ManagerId);
            if (mgr == null) return;
            mgr.ProfitGenerated += profit;
            mgr.QuarterProfit += profit;
            cp.ProfitCredited += profit;
            int guard = 0;
            while (cp.ProfitCredited >= cp.NextMilestone && guard++ < 10)
            {
                Award(mgr.Id, 150, $"{cp.Name} generated another {Fmt.Money(50000)} in profit");
                cp.NextMilestone += 50000;
            }
            if (!cp.ProfitableAwarded && cp.ProfitCredited >= 10000)
            {
                cp.ProfitableAwarded = true;
                if (cp.DiscovererId >= 0) Award(cp.DiscovererId, 100, $"Found a profitable product: {cp.Name}");
                if (cp.ManagerId != cp.DiscovererId) Award(cp.ManagerId, 25, $"{cp.Name} turned profitable");
            }
            if (!cp.LossPenalized && cp.ProfitCredited <= -30000)
            {
                cp.LossPenalized = true;
                Penalize(cp.ManagerId, 150, $"Major financial loss: {cp.Name} is down {Fmt.Money(-cp.ProfitCredited)}");
            }
        }

        void UpdateOperations()
        {
            double capacity = C.CapacityPerDay > 0 ? C.CapacityPerDay : FulfillmentCapacity();
            double support = SupportCapacity();
            C.SupportPerDay = support;
            C.StorageCapacity = StorageCapacity();
            C.StorageUsed = StockOnHand();
            double orders = C.OrdersToday;

            double target;
            if (_unitsSoldToday > 0)
            {
                double avgQ = _qualitySoldToday / _unitsSoldToday;
                target = 55 + (avgQ - 0.55) * 60;
            }
            else target = 65;
            if (C.Policies.ReturnPolicy == 0) target -= 5;
            if (C.Policies.ReturnPolicy == 2) target += 6;
            if (C.Has("ppl1")) target += 3;
            if (C.Has("log3")) target += 6;
            double backlogDays = C.Backlog / Math.Max(1, capacity);
            if (backlogDays > 0.5) target -= Math.Min(30, (backlogDays - 0.5) * 12);
            if (orders > 0)
            {
                double coverage = support / orders;
                if (coverage < 1) target -= (1 - coverage) * 35;
            }
            int stockouts = 0;
            foreach (var cp in C.Products) if (cp.Active && cp.Stock == 0 && cp.LostToday > 0) stockouts++;
            target -= Math.Min(8, stockouts * 1.5);
            C.Satisfaction = Clamp(C.Satisfaction + (target - C.Satisfaction) * 0.06, 0, 100);

            double repTarget = 5 + C.Satisfaction * 0.5 + Math.Min(15, 3 * Math.Log10(1 + C.LifetimeUnits)) + Math.Min(12, C.BrandBonus)
                               + (C.Has("brd4") ? 10 : 0) + (C.Positioning == "premium" ? 5 : C.Positioning == "value" ? -3 : 0) + (C.Stage >= 4 ? 5 : 0);
            C.Reputation = Clamp(C.Reputation + (repTarget - C.Reputation) * 0.012, 0, 100);
            C.BrandBonus *= 0.9995;

            // Employee morale
            for (int i = C.Employees.Count - 1; i >= 0; i--)
            {
                var e = C.Employees[i];
                double mt = 65;
                if (Has(e.ManagerId, Skills.Leader)) mt += 10;
                if (C.Has("ppl3")) mt += 15;
                if (C.Cash < 0) mt -= 15;
                if (e.Salary > Catalog.RoleBaseSalary(e.Role) * (0.8 + 0.2 * e.Skill) * 1.15) mt += 6;
                if (backlogDays > 2) mt -= 8;
                e.Morale = Clamp(e.Morale + (mt - e.Morale) * 0.02, 0, 100);
                if (e.Morale < 25 && Rng.Chance(0.01))
                {
                    C.Employees.RemoveAt(i);
                    Log(LogKind.Team, -1, $"{e.Name} ({Catalog.RoleName(e.Role)}) quit. Morale was too low.");
                    NotifyAll(NoticeKind.Warning, $"{e.Name} quit the company (low morale).");
                }
            }
        }

        void DailyUpkeep()
        {
            if (C.HeldCash > 0 && State.Day >= C.HeldUntilDay)
            {
                C.Cash += C.HeldCash;
                AddLedger(TxType.Other, C.HeldCash, -1, "Payment processor released held funds", null, 0);
                C.HeldCash = 0;
            }
            int cap = StorageCapacity();
            int stock = StockOnHand();
            if (stock > cap)
            {
                double fee = (stock - cap) * 0.25;
                Spend(fee, TxType.Rent, -1, $"Overflow storage fees ({Fmt.Int(stock - cap)} units over capacity)", null, 0, false);
            }
            // Dead stock alert
            foreach (var cp in C.Products)
            {
                if (!cp.Active || cp.Stock <= 0 || cp.DeadStockFlagged) continue;
                if (cp.DaysNoSales >= 40)
                {
                    cp.DeadStockFlagged = true;
                    SpawnDeadStockEvent(cp);
                }
            }
        }

        void MonthEnd()
        {
            double rent = 0;
            foreach (var w in C.Warehouses) rent += w.MonthlyCost;
            if (rent > 0) Spend(rent, TxType.Rent, -1, "Warehouse rent");

            double payroll = 0;
            foreach (var e in C.Employees) payroll += e.Salary;
            if (payroll > 0) Spend(payroll, TxType.Payroll, -1, $"Payroll ({C.Employees.Count} employees)");

            foreach (var p in State.Players)
            {
                if (p.Salary <= 0) continue;
                Spend(p.Salary, TxType.PartnerSalary, p.Id, $"Salary: {p.Name}");
                PayPlayer(p.Id, p.Salary);
                var pr = Priv(p.Id);
                if (pr != null) pr.SalaryReceived += p.Salary;
            }

            // Big companies carry big-company costs: legal, compliance, HR and management layers.
            double overhead = 900 + 20 * C.Employees.Count + 150 * C.Products.FindAll(x => x.Active).Count + Math.Max(0, C.Month.Revenue + C.Today.Revenue - 100000) * 0.04;
            if (AnyoneHas(Skills.TaxSpecialist)) overhead *= 0.85;
            overhead *= 1 - Math.Min(0.2, EmployeePower(EmployeeRole.Accountant) * 0.05);
            Spend(overhead, TxType.Other, -1, "Software, insurance, legal & admin");

            // Corporate income tax on the month's profit.
            double taxable = C.Month.PreTaxProfit + C.Today.PreTaxProfit;
            if (taxable > 0)
            {
                double rate = 0.2 * (AnyoneHas(Skills.TaxSpecialist) ? 0.8 : 1) * (1 - Math.Min(0.15, EmployeePower(EmployeeRole.Accountant) * 0.04));
                Spend(Math.Round(taxable * rate), TxType.Tax, -1, $"Corporate income tax ({Fmt.Pct(rate)} of {Fmt.Money(taxable)} profit)");
            }

            for (int i = C.Loans.Count - 1; i >= 0; i--)
            {
                var l = C.Loans[i];
                double interest = l.Balance * l.AnnualRate / 12.0;
                double principal = Math.Min(l.Balance, Math.Max(0, l.MonthlyPayment - interest));
                if (l.MonthsLeft <= 1) principal = l.Balance;
                C.Cash -= interest + principal;
                C.Today.Interest += interest;
                l.Balance -= principal;
                l.MonthsLeft--;
                AddLedger(TxType.LoanPayment, -(interest + principal), -1, $"Loan payment: {l.Lender} (interest {Fmt.Money(interest)})", null, 0);
                if (l.Balance < 1 || l.MonthsLeft <= 0)
                {
                    C.Loans.RemoveAt(i);
                    Log(LogKind.Finance, -1, $"Paid off the {l.Lender}.");
                }
            }

            if (C.Cash < 0)
            {
                double interest = -C.Cash * 0.02;
                C.Cash -= interest;
                C.Today.Interest += interest;
                AddLedger(TxType.LoanPayment, -interest, -1, "Overdraft interest (2%/month)", null, 0);
            }

            if (C.Upgrades.Contains("treasury") && C.Cash > 250000)
            {
                double interest = (C.Cash - 250000) * 0.04 / 12.0;
                Receive(interest, TxType.Other, -1, "Treasury reserve interest");
            }
            // The month's books roll over in CloseDayBooks, after today's numbers (bills, tax) are in.
        }

        void UpdateHoldings()
        {
            foreach (var pr in State.Private)
            {
                foreach (var h in pr.Holdings)
                {
                    var a = Catalog.GetAsset(h.AssetId);
                    if (a == null) continue;
                    double r = a.AnnualReturn / GameCalendar.DaysPerYear + Rng.Normal(0, a.DailyVol);
                    if (a.Id == "crypto" && Rng.Chance(0.002)) r = -0.35;
                    h.Value = Math.Max(0, h.Value * (1 + r));
                }
            }
        }

        // ------------------------------------------------------------------ valuation & history

        public double InventoryValue()
        {
            double v = 0;
            foreach (var cp in C.Products) v += cp.Stock * cp.AvgCost;
            foreach (var s in C.Shipments) v += s.Quantity * s.UnitCost;
            return v;
        }

        static double SumLast(List<float> list, int n, int skip = 0)
        {
            double s = 0;
            int end = list.Count - skip;
            for (int i = Math.Max(0, end - n); i < end; i++) s += list[i];
            return s;
        }

        void UpdateValuation()
        {
            if (C.Bankrupt || C.Sold) { C.Valuation = 0; return; }
            var h = State.History;
            int n = Math.Min(90, h.Profit.Count);
            double annualProfit = 0, annualRevenue = 0;
            if (n >= 15)
            {
                annualProfit = SumLast(h.Profit, n) * GameCalendar.DaysPerYear / n;
                annualRevenue = SumLast(h.Revenue, n) * GameCalendar.DaysPerYear / n;
            }
            double growth = 0;
            if (h.Revenue.Count >= 90)
            {
                double recent = SumLast(h.Revenue, 45);
                double prev = SumLast(h.Revenue, 45, 45);
                if (prev > 1000) growth = Clamp(recent / prev - 1, -0.5, 1.2);
            }
            double multiple = Clamp(3 + C.Reputation / 30 + growth * 2.5, 2, 10);
            double warehouses = 0;
            foreach (var w in C.Warehouses) warehouses += w.SetupCost;
            // Upgrades and acquired rivals are assets on the balance sheet.
            double capital = 0;
            foreach (var id in C.Upgrades) capital += (Catalog.GetUpgrade(id)?.Cost ?? 0) * 0.4;
            foreach (var b in C.Building) capital += (Catalog.GetUpgrade(b.Id)?.Cost ?? 0) * 0.4;
            foreach (var r in State.Rivals) if (r.Acquired) capital += r.AcquiredFor * 0.6;
            double netAssets = C.Cash + C.HeldCash + InventoryValue() * 0.7 + warehouses * 0.3 + capital - C.Debt;
            // Investors trust a track record: the first two months lean on the balance sheet.
            double confidence = Math.Min(1, h.Profit.Count / 60.0);
            double earningsValue = Math.Max(0, annualProfit) * multiple * confidence;
            double revenueValue = annualRevenue * (0.3 + C.Reputation / 250) * confidence;
            C.Valuation = Math.Max(1000, netAssets + Math.Max(earningsValue, revenueValue));
            if (C.Valuation > C.PeakValuation) C.PeakValuation = C.Valuation;
        }

        void RecordHistory()
        {
            var h = State.History;
            h.Cash.Add((float)C.Cash);
            h.Revenue.Add((float)C.Today.Revenue);
            h.Profit.Add((float)C.Today.NetProfit);
            h.Valuation.Add((float)C.Valuation);
            h.Satisfaction.Add((float)C.Satisfaction);
            h.Reputation.Add((float)C.Reputation);
            if (h.Cash.Count > History.MaxSamples)
            {
                int extra = h.Cash.Count - History.MaxSamples;
                h.Cash.RemoveRange(0, extra);
                h.Revenue.RemoveRange(0, extra);
                h.Profit.RemoveRange(0, extra);
                h.Valuation.RemoveRange(0, extra);
                h.Satisfaction.RemoveRange(0, extra);
                h.Reputation.RemoveRange(0, extra);
                h.StartDay += extra;
            }
        }

        /// <summary>Projected cash over the next N days (for Cash Flow Expert / Accountant).</summary>
        public double ProjectCash(int days)
        {
            var h = State.History;
            int n = Math.Min(30, h.Revenue.Count);
            double dailyGross = 0;
            if (n > 0)
            {
                // gross operating cash approximated from the last 30 days of revenue at current margins
                double rev = SumLast(h.Revenue, n) / n;
                double ratio = C.Month.Revenue > 0 ? Clamp(C.Month.GrossProfit / C.Month.Revenue, -0.5, 0.9) : 0.35;
                dailyGross = rev * ratio;
            }
            double monthly = 0;
            foreach (var w in C.Warehouses) monthly += w.MonthlyCost;
            foreach (var e in C.Employees) monthly += e.Salary;
            foreach (var p in State.Players) monthly += p.Salary;
            foreach (var l in C.Loans) monthly += l.MonthlyPayment;
            monthly += 900 + 20 * C.Employees.Count;
            double ads = 0;
            foreach (var cam in C.Campaigns) if (cam.Active) ads += cam.DailyBudget;
            double inbound = 0;
            return C.Cash + inbound + days * (dailyGross - ads) - monthly * days / 30.0;
        }

        // ------------------------------------------------------------------ end of game

        void CheckGameEnd()
        {
            if (State.Phase == GamePhase.GameOver) return;
            if (C.NegativeCashDays >= 30 || C.OverdraftDays >= 90)
            {
                C.Bankrupt = true;
                C.Valuation = 0;
                Log(LogKind.Finance, -1, $"{C.Name} is bankrupt.");
                EndGame($"{C.Name} ran out of money and went bankrupt. Company shares are worthless.");
                return;
            }
            int years = State.Settings.GameYears;
            if (years > 0 && State.Day >= years * GameCalendar.DaysPerYear)
                EndGame($"After {years} year{(years == 1 ? "" : "s")}, the partnership is over. Time to count the money.");
        }

        public void EndGame(string reason)
        {
            State.Phase = GamePhase.GameOver;
            UpdateValuation();
            double finalValuation = (C.Bankrupt || C.Sold) ? 0 : C.Valuation * (C.WentPublic ? 1.5 : 1);
            var sum = new GameOverSummary
            {
                Reason = reason, FinalValuation = finalValuation, Stage = C.Stage, CompanyRank = CompanyRank(), CompanyCount = CompanyCount(), Ipo = C.WentPublic
            };
            foreach (var p in State.Players)
            {
                var pr = Priv(p.Id);
                double inv = 0;
                foreach (var h in pr.Holdings) inv += h.Value;
                double equity = sum.FinalValuation * OwnershipPct(p);
                var (_, _, done) = AmbitionProgress(p.Id);
                double bonus = done ? AmbitionBonus(Math.Max(finalValuation, C.Sold ? C.SalePrice : 0)) : 0;
                sum.Standings.Add(new FinalStanding
                {
                    PlayerId = p.Id, Name = p.Name, PersonalCash = pr.Cash, Investments = inv, EquityValue = equity,
                    NetWorth = pr.Cash + inv + equity + bonus, OwnershipPct = OwnershipPct(p), Performance = p.Performance, Level = p.Level,
                    SecretIncome = pr.SecretIncome, Ambition = pr.Ambition, AmbitionDone = done, AmbitionBonus = bonus
                });
                if (!string.IsNullOrEmpty(pr.Ambition))
                    sum.Secrets.Add($"{p.Name}'s secret ambition was {Ambitions.Get(pr.Ambition)?.Name}: {AmbitionText(pr.Ambition)} {(done ? "COMPLETED (+" + Fmt.Money(bonus) + ")" : "Failed.")}");
            }
            sum.Standings.Sort((a, b) => b.NetWorth.CompareTo(a.NetWorth));
            for (int i = 0; i < sum.Standings.Count; i++) sum.Standings[i].Rank = i + 1;

            // The Reckoning: every secret comes out.
            int ambitionLines = sum.Secrets.Count;
            foreach (var o in State.Offers)
            {
                if (o.Status != OfferStatus.Accepted && o.Status != OfferStatus.Fulfilled && o.Status != OfferStatus.Paid) continue;
                if (o.Kind == OfferKind.SpeakingGig || o.Kind == OfferKind.ShareSale) continue;
                string who = State.PlayerName(o.TargetId);
                string amount = o.PersonalAmount != 0 ? $" ({Fmt.Money(Math.Abs(o.PersonalAmount))})" : "";
                sum.Secrets.Add($"{who} accepted: {o.Title}{amount}.");
            }
            foreach (var e in State.Evidence)
            {
                if (e.Exposed) continue;
                if (e.DiscoveredBy.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in e.DiscoveredBy) names.Add(State.PlayerName(id));
                    sum.Secrets.Add($"{string.Join(", ", names)} knew about {State.PlayerName(e.ActorId)}'s deal but never exposed it.");
                }
            }
            if (sum.Secrets.Count == ambitionLines) sum.Secrets.Add("Nobody took a single bribe. Remarkable.");

            if (sum.Standings.Count > 0)
            {
                sum.Awards.Add($"Company: {Stages.Get(sum.Stage).Name}, #{sum.CompanyRank} of {sum.CompanyCount} in the industry" + (sum.Ipo ? ", taken public" : ""));
                sum.Awards.Add($"Richest partner: {sum.Standings[0].Name} ({Fmt.Money(sum.Standings[0].NetWorth)})");
                FinalStanding perf = null, owner = null, secret = null;
                foreach (var s in sum.Standings)
                {
                    if (perf == null || s.Performance > perf.Performance) perf = s;
                    if (owner == null || s.OwnershipPct > owner.OwnershipPct) owner = s;
                    if (secret == null || s.SecretIncome > secret.SecretIncome) secret = s;
                }
                sum.Awards.Add($"Top performer: {perf.Name} ({Fmt.Int(perf.Performance)} pts)");
                sum.Awards.Add($"Largest shareholder: {owner.Name} ({Fmt.Pct(owner.OwnershipPct, 1)})");
                if (secret != null && secret.SecretIncome > 0) sum.Awards.Add($"Deepest pockets under the table: {secret.Name} ({Fmt.Money(secret.SecretIncome)} in secret income)");
                foreach (var s in sum.Standings)
                    if (Priv(s.PlayerId).OffersAccepted == 0 && P(s.PlayerId).ScandalCount == 0)
                        sum.Awards.Add($"Clean hands: {s.Name}");
            }
            State.Summary = sum;
            Log(LogKind.Company, -1, "Game over. " + reason);
            State.Revision++;
        }
    }
}
