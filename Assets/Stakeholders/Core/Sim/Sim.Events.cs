using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// The situation engine: a steady stream of shared problems and opportunities, scaled to the
    /// size of the company so that more partners always have something useful to do.
    /// </summary>
    public partial class GameSimulation
    {
        int _lastAcquisitionDay = -1000;
        int _lastCashCrunchDay = -1000;

        // ------------------------------------------------------------------ plumbing

        GameEvent NewEvent(string template, EventCategory cat, int severity, string title, string text, int days)
        {
            return new GameEvent
            {
                Id = NewId("ev"), TemplateId = template, Category = cat, Severity = severity, Title = title, Text = text,
                CreatedDay = State.Day, DeadlineDay = State.Day + Math.Max(1, days), PublicDay = State.Day
            };
        }

        static EventChoice Choice(string label, string detail, double cost = 0, double aiCompany = 0, double aiSelf = 0, double aiRisk = 0,
            int busy = 0, string skill = null, string skillAlt = null)
        {
            return new EventChoice
            {
                Label = label, Detail = detail, Cost = Math.Round(cost), AiCompany = aiCompany, AiSelf = aiSelf, AiRisk = aiRisk,
                BusyDays = busy, RequiresSkill = skill, RequiresSkillAlt = skillAlt
            };
        }

        void AddEvent(GameEvent e)
        {
            if (e == null) return;
            State.Events.Add(e);
            if (e.PublicDay <= State.Day)
            {
                Log(LogKind.Event, -1, $"New situation: {e.Title}");
                NotifyAll(e.Severity >= 3 ? NoticeKind.Danger : e.Severity == 2 ? NoticeKind.Warning : NoticeKind.Info, e.Title);
                // Solo: give the player time to think when something critical happens.
                if (e.Severity >= 3 && State.Settings.AutoPause && !State.Settings.Multiplayer && HumanCount == 1 && State.Phase == GamePhase.Running)
                    State.Paused = true;
            }
            else
            {
                foreach (var id in e.EarlyAccess)
                    Notify(id, NoticeKind.Private, $"Early signal (only you see this for now): {e.Title}");
            }
        }

        bool HasOpenEvent(string template, string productId = null, string otherId = null)
        {
            foreach (var e in State.Events)
            {
                if (e.Resolved || e.TemplateId != template) continue;
                if (productId != null && e.ProductId != productId) continue;
                if (otherId != null && e.CampaignId != otherId && e.ShipmentId != otherId && e.EmployeeId != otherId && e.SupplierId != otherId) continue;
                return true;
            }
            return false;
        }

        int OpenPublicEvents()
        {
            int n = 0;
            foreach (var e in State.Events) if (!e.Resolved && e.PublicDay <= State.Day) n++;
            return n;
        }

        void FinishEvent(GameEvent e, Player p, int choice, string outcome, int xp)
        {
            e.Resolved = true;
            e.ResolvedBy = p != null ? p.Id : -1;
            e.ResolvedDay = State.Day;
            e.ChosenIndex = choice;
            e.Outcome = outcome;
            e.PendingApproval = false;
            State.Events.Remove(e);
            State.ResolvedEvents.Add(e);
            if (State.ResolvedEvents.Count > 30) State.ResolvedEvents.RemoveAt(0);
            if (p != null)
            {
                p.EventsResolved++;
                p.QuarterEvents++;
                if (xp > 0) Award(p.Id, xp, e.Title);
                string label = choice >= 0 && choice < e.Choices.Count ? e.Choices[choice].Label : "decided";
                Log(LogKind.Event, p.Id, $"{p.Name} handled \"{e.Title}\": {label}. {outcome}");
            }
            else
            {
                Log(LogKind.Event, -1, $"\"{e.Title}\" was left unhandled. {outcome}");
            }
        }

        // ------------------------------------------------------------------ daily update

        void UpdateEvents()
        {
            foreach (var e in State.Events)
            {
                if (e.PublicDay == State.Day && e.EarlyAccess.Count > 0)
                {
                    Log(LogKind.Event, -1, $"New situation: {e.Title}");
                    NotifyAll(NoticeKind.Info, e.Title);
                }
            }
            for (int i = State.Events.Count - 1; i >= 0; i--)
            {
                var e = State.Events[i];
                if (e.Resolved || e.PendingApproval || State.Day <= e.DeadlineDay) continue;
                if (e.ClaimedBy >= 0) Penalize(e.ClaimedBy, 30, $"Claimed \"{e.Title}\" but let it expire");
                string outcome = ExpireEventEffects(e);
                FinishEvent(e, null, -1, outcome, 0);
            }
            CheckAlerts();
            DirectorRoll();
        }

        void DirectorRoll()
        {
            int active = 0;
            foreach (var cp in C.Products) if (cp.Active) active++;
            double size = active * 0.35 + C.Employees.Count * 0.06 + HumanCount * 0.4;
            double target = Math.Min(8, 1.5 + size) * DifficultyMult;
            int open = OpenPublicEvents();
            double chance = open < target ? 0.22 + 0.06 * (target - open) : 0.04;
            if (!Rng.Chance(chance)) return;

            var options = new List<KeyValuePair<Func<GameEvent>, double>>
            {
                new KeyValuePair<Func<GameEvent>, double>(MakeCustomerCrisis, 0.5),
                new KeyValuePair<Func<GameEvent>, double>(MakeSupplierContract, 0.7),
                new KeyValuePair<Func<GameEvent>, double>(MakeRaiseRequest, 0.9),
                new KeyValuePair<Func<GameEvent>, double>(MakeCompetitorCut, 1.0),
                new KeyValuePair<Func<GameEvent>, double>(MakeInfluencerOffer, AnyoneHas(Skills.InfluencerNetwork) ? 1.3 : 0.8),
                new KeyValuePair<Func<GameEvent>, double>(MakeBulkOrder, 0.7),
                new KeyValuePair<Func<GameEvent>, double>(MakeQualityIssue, C.Has("src1") ? 0.25 : 0.6),
                new KeyValuePair<Func<GameEvent>, double>(MakeContractOffer, C.Has("chn3") ? 1.6 : 0.8),
                new KeyValuePair<Func<GameEvent>, double>(MakePlatformPolicy, 0.35),
                new KeyValuePair<Func<GameEvent>, double>(MakePaymentHold, 0.3),
                new KeyValuePair<Func<GameEvent>, double>(MakeWarehouseIncident, 0.35),
                new KeyValuePair<Func<GameEvent>, double>(MakePoaching, 0.5),
                new KeyValuePair<Func<GameEvent>, double>(MakeMediaInterview, 0.45),
                new KeyValuePair<Func<GameEvent>, double>(MakeTaxAudit, 0.25),
                new KeyValuePair<Func<GameEvent>, double>(MakeCounterfeit, 0.5),
                new KeyValuePair<Func<GameEvent>, double>(MakeSupplierBankrupt, 0.12),
                new KeyValuePair<Func<GameEvent>, double>(MakeTeamMorale, 0.6),
                new KeyValuePair<Func<GameEvent>, double>(MakeAcquisitionOffer, 0.05),
                new KeyValuePair<Func<GameEvent>, double>(MakeTradeShow, 0.45),
                new KeyValuePair<Func<GameEvent>, double>(MakeReviewBoom, 0.35),
            };
            // Try a few weighted picks; many templates have eligibility conditions.
            for (int attempt = 0; attempt < 6 && options.Count > 0; attempt++)
            {
                var pick = Rng.PickWeighted(options, o => o.Value);
                options.Remove(pick);
                var e = pick.Key();
                if (e == null) continue;
                AddEvent(e);
                if (e.TemplateId == "influencer_offer") MaybeInfluencerSkim(e);
                return;
            }
        }

        void CheckAlerts()
        {
            // Low stock
            foreach (var cp in C.Products)
            {
                if (!cp.Active || cp.Sales7 < 1 || HasOpenEvent("low_stock", cp.Id)) continue;
                if (IncomingUnits(cp.Id) > 0) continue;
                var s = PreferredSupplier(cp);
                int lead = s != null ? s.LeadTime : 14;
                double days = cp.Stock / Math.Max(0.1, cp.Sales7);
                if (days < lead + 4) AddEvent(MakeLowStock(cp, s, days));
            }
            // Bad campaigns
            foreach (var cam in C.Campaigns)
            {
                if (!cam.Active || State.Day - cam.StartDay < 7 || cam.Spent7 <= 0) continue;
                if (cam.Roas7 < 1.5 && !HasOpenEvent("campaign_bad", null, cam.Id) && !cam.Reviewed && Rng.Chance(0.25))
                    AddEvent(MakeCampaignBad(cam));
            }
            // Shipment delays
            foreach (var sh in C.Shipments)
            {
                if (sh.Delayed || sh.Rush) continue;
                var s = State.GetSupplier(sh.SupplierId);
                if (s == null) continue;
                double daily = (1 - s.Reliability) / Math.Max(3, s.LeadTime) * 1.2;
                if (Rng.Chance(daily)) { AddEvent(MakeShipmentDelay(sh, s)); break; }
            }
            // Cash crunch
            if (C.Cash < 0 && State.Day - _lastCashCrunchDay > 30 && !HasOpenEvent("cash_crunch"))
            {
                _lastCashCrunchDay = State.Day;
                AddEvent(MakeCashCrunch());
            }
            // Month-end bills
            int dom = State.DayOfMonth;
            if (dom == 26 && !HasOpenEvent("bills_due"))
            {
                double due = MonthlyObligations();
                if (due > C.Cash * 0.8 && due > 5000) AddEvent(MakeBillsDue(due));
            }
            // Holiday prep
            if (State.Day % GameCalendar.DaysPerYear == 271) AddEvent(MakeHolidayPrep());
            // Private early cash warnings
            if (State.Day % 10 == 0 && C.Products.Count > 0)
            {
                double projected = ProjectCash(21);
                if (projected < 0)
                    foreach (var p in State.Players)
                        if (p.HasSkill(Skills.CashFlowExpert))
                            Notify(p.Id, NoticeKind.Private, $"Cash flow warning: at the current burn rate the company runs dry in about 3 weeks (projected {Fmt.Money(projected)}).");
            }
        }

        public double MonthlyObligations()
        {
            double due = 0;
            foreach (var w in C.Warehouses) due += w.MonthlyCost;
            foreach (var e in C.Employees) due += e.Salary;
            foreach (var p in State.Players) due += p.Salary;
            foreach (var l in C.Loans) due += l.MonthlyPayment;
            return due + 900 + 20 * C.Employees.Count;
        }

        public Supplier PreferredSupplier(CompanyProduct cp)
        {
            var s = State.GetSupplier(cp.SupplierId);
            if (s != null && !s.Bankrupt && !s.Blacklisted) return s;
            var mp = State.GetMarketProduct(cp.ProductId);
            CheapestUnitPrice(mp, 500, -1, out var cheapest);
            return cheapest;
        }

        CompanyProduct RandomActiveProduct(Func<CompanyProduct, bool> filter = null)
        {
            var list = C.Products.FindAll(cp => cp.Active && (filter == null || filter(cp)));
            return list.Count == 0 ? null : Rng.Pick(list);
        }

        int SuggestedReorder(CompanyProduct cp, Supplier s, int extraDays = 30)
        {
            int lead = s != null ? s.LeadTime : 14;
            double want = cp.Sales7 * (lead + extraDays) - cp.Stock - IncomingUnits(cp.Id);
            int moq = s != null ? s.Moq : 100;
            int qty = (int)(Math.Ceiling(Math.Max(moq, want) / 50.0) * 50);
            int space = StorageCapacity() - StockOnHand() - IncomingUnits();
            if (space > 0 && qty > space) qty = Math.Max(moq, space / 50 * 50);
            return qty;
        }

        // ------------------------------------------------------------------ alert templates

        GameEvent MakeLowStock(CompanyProduct cp, Supplier s, double days)
        {
            if (s == null) return null;
            var mp = State.GetMarketProduct(cp.ProductId);
            int qty = SuggestedReorder(cp, s);
            int rushQty = (int)(Math.Ceiling(Math.Max(s.Moq, cp.Sales7 * 20) / 50.0) * 50);
            double unit = UnitPrice(s, mp, qty, -1);
            double rushUnit = UnitPrice(s, mp, rushQty, -1) * 1.35;
            var e = NewEvent("low_stock", EventCategory.Inventory, days < 4 ? 2 : 1, $"Running low: {cp.Name}",
                $"Only {Fmt.Int(cp.Stock)} units of {cp.Name} left (about {Fmt.Int(days)} days). A normal order from {s.Name} takes {s.LeadTime} days.",
                (int)Clamp(days, 2, 7));
            e.ProductId = cp.Id; e.SupplierId = s.Id; e.Count = qty; e.Amount2 = rushQty;
            e.Choices.Add(Choice($"Reorder {Fmt.Int(qty)} units from {s.Name}", $"Arrives in ~{s.LeadTime} days.", unit * qty, 8));
            e.Choices.Add(Choice($"Rush {Fmt.Int(rushQty)} units by air freight", "35% more expensive, arrives in 3 days.", rushUnit * rushQty, 5, 0, 0.1));
            e.Choices.Add(Choice("Raise the price 10% to stretch stock", "Slower sales, higher margin.", 0, 2));
            e.Choices.Add(Choice("Let it sell out", "Customers will find it elsewhere.", 0, -3));
            return e;
        }

        GameEvent MakeCampaignBad(Campaign cam)
        {
            var cp = State.GetCompanyProduct(cam.CompanyProductId);
            if (cp == null) return null;
            var e = NewEvent("campaign_bad", EventCategory.Marketing, 1, $"Campaign underperforming: {cp.Name}",
                $"The {cam.Channel} campaign for {cp.Name} returned only {Fmt.Price(cam.Roas7)} in sales per $1 spent over the last week.", 5);
            e.CampaignId = cam.Id; e.ProductId = cp.Id;
            e.Choices.Add(Choice("Stop the campaign", "Cut the losses now.", 0, 5));
            e.Choices.Add(Choice("Refresh the creatives", "New ads: likely a big efficiency boost.", 1500, 3, 0, 0.3));
            e.Choices.Add(Choice("Double the budget", "Maybe the algorithm just needs more data.", 0, -3, 0, 0.8));
            e.Choices.Add(Choice("Keep it running", "Give it more time.", 0, -2));
            return e;
        }

        GameEvent MakeShipmentDelay(Shipment sh, Supplier s)
        {
            var cp = State.GetCompanyProduct(sh.CompanyProductId);
            if (cp == null) return null;
            int delay = Rng.Range(6, 14);
            int originalEta = sh.EtaDay;
            sh.EtaDay += delay;
            sh.Delayed = true;
            double value = sh.Quantity * sh.UnitCost;
            double expedite = Math.Max(1500, value * 0.08);
            var e = NewEvent("shipment_delay", EventCategory.Logistics, 1, $"Shipment delayed: {cp.Name}",
                $"{Fmt.Int(sh.Quantity)} units of {cp.Name} from {s.Name} are stuck at customs. New arrival: {Fmt.ShortDate(sh.EtaDay)} (+{delay} days).", 3);
            e.ShipmentId = sh.Id; e.ProductId = cp.Id; e.SupplierId = s.Id; e.Count = originalEta;
            e.Choices.Add(Choice("Pay for expedited clearance", "Back on the original schedule.", expedite, 4, 0, 0));
            e.Choices.Add(Choice("Wait it out", "Free, but slow.", 0, 0));
            e.Choices.Add(Choice("Cancel the shipment", "Get 80% of the money back.", 0, -1));
            return e;
        }

        GameEvent MakeCashCrunch()
        {
            var e = NewEvent("cash_crunch", EventCategory.Crisis, 3, "Cash crunch!",
                $"The company account is overdrawn by {Fmt.Money(-C.Cash)}. The bank allows up to {Fmt.Money(OverdraftLimit())} at 2% a month. Deeper than that for 30 days, or overdrawn at all for 90 days, means bankruptcy.", 7);
            double mca = LoanOfferAmount("mca", out _);
            e.Choices.Add(Choice($"Take a {Fmt.Money(mca)} merchant cash advance", "Fast money at 35% APR.", 0, 4, 0, 0.3));
            e.Choices.Add(Choice("Pause all advertising", "Stops the bleeding, slows sales.", 0, 3));
            e.Choices.Add(Choice("Liquidate the slowest product", "Sell its stock at 35% of cost.", 0, 2));
            e.Choices.Add(Choice("Do nothing for now", "Hope sales catch up.", 0, -4, 0, 0.6));
            return e;
        }

        GameEvent MakeBillsDue(double due)
        {
            var e = NewEvent("bills_due", EventCategory.Finance, 2, "Month-end bills approaching",
                $"Payroll, rent, partner salaries and loan payments of {Fmt.Money(due)} are due in 4 days. Cash on hand: {Fmt.Money(C.Cash)}.", 3);
            e.Amount = due;
            e.Choices.Add(Choice("Noted, we will manage", "", 0, 1));
            e.Choices.Add(Choice("Cut all ad budgets in half", "Frees up cash immediately.", 0, 3));
            return e;
        }

        GameEvent MakeHolidayPrep()
        {
            var e = NewEvent("holiday_prep", EventCategory.Opportunity, 1, "Holiday season is coming",
                "November and December bring huge demand for electronics, toys, fashion and home goods. Suppliers need weeks: stock up now.", 8);
            e.Choices.Add(Choice("Noted: start planning", "", 0, 2));
            return e;
        }

        public void SpawnViralEvent(CompanyProduct cp)
        {
            if (HasOpenEvent("viral_moment", cp.Id)) return;
            var s = PreferredSupplier(cp);
            if (s == null) return;
            var mp = State.GetMarketProduct(cp.ProductId);
            int qty = (int)(Math.Ceiling(Math.Max(s.Moq, cp.Sales7 * 2.5 * 14) / 50.0) * 50);
            double cost = UnitPrice(s, mp, qty, -1) * 1.35 * qty;
            var e = NewEvent("viral_moment", EventCategory.Opportunity, 2, $"{cp.Name} is going viral!",
                $"Demand for {cp.Name} just exploded. We have {Fmt.Int(cp.Stock)} units left.", 5);
            e.ProductId = cp.Id; e.SupplierId = s.Id; e.Count = qty;
            e.Choices.Add(Choice($"Rush {Fmt.Int(qty)} units by air freight", "Arrives in 3 days.", cost, 9, 0, 0.2));
            e.Choices.Add(Choice("Raise the price 15%", "Ride the wave with better margins.", 0, 5));
            e.Choices.Add(Choice("Enjoy it", "", 0, 0));
            AddEvent(e);
        }

        void SpawnDeadStockEvent(CompanyProduct cp)
        {
            double value = cp.Stock * cp.AvgCost;
            var e = NewEvent("dead_stock", EventCategory.Inventory, 1, $"Dead stock: {cp.Name}",
                $"{cp.Name} hasn't sold in 40 days. {Fmt.Int(cp.Stock)} units ({Fmt.Money(value)} at cost) are gathering dust.", 10);
            e.ProductId = cp.Id; e.Amount = value;
            e.Choices.Add(Choice("Liquidate at 35% of cost", "Frees space and some cash.", 0, 3));
            e.Choices.Add(Choice("Cut the price 30%", "Try to move it.", 0, 2));
            e.Choices.Add(Choice("Hold", "Maybe the trend comes back.", 0, -1, 0, 0.4));
            AddEvent(e);
        }

        void SpawnTrendEvent(MarketProduct mp)
        {
            var e = NewEvent("trend_spike", EventCategory.Opportunity, 1, $"Trending: {mp.Name}",
                $"Search volume for {mp.Name} ({mp.Category}) is climbing fast.", 7);
            e.ProductId = mp.Id;
            foreach (var p in State.Players) if (p.HasSkill(Skills.TrendHunter)) e.EarlyAccess.Add(p.Id);
            if (e.EarlyAccess.Count > 0)
            {
                e.PublicDay = State.Day + 4;
                e.DeadlineDay = e.PublicDay + 6;
            }
            e.Choices.Add(Choice("Research it now", "Free: adds it to your notebook with an estimate.", 0, 5, 3));
            e.Choices.Add(Choice("Ignore it", "", 0, -1));
            AddEvent(e);
        }

        // ------------------------------------------------------------------ random templates

        GameEvent MakeCustomerCrisis()
        {
            var cp = RandomActiveProduct(x => x.Sales7 >= 3);
            if (cp == null || HasOpenEvent("customer_crisis")) return null;
            double recall = Math.Max(3000, cp.Sales7 * 7 * cp.Price * 0.3);
            var e = NewEvent("customer_crisis", EventCategory.Customer, 3, $"Viral complaint about {cp.Name}",
                $"A customer's video about a defective {cp.Name} has two million views. Returns are spiking and reviews are tanking.", 4);
            e.ProductId = cp.Id; e.Amount = recall;
            e.Choices.Add(Choice("Full recall and refunds", "Expensive, but customers will respect it.", recall, 6, 0, 0));
            e.Choices.Add(Choice("Apology and discount codes", "A middle path.", 3000, 3, 0, 0.2));
            e.Choices.Add(Choice("Deny responsibility", "Free. Might blow over. Might explode.", 0, -4, 0, 0.8));
            e.Choices.Add(Choice("Personally lead the PR response", "Best outcome. Takes 2 days of your time.", 0, 8, 3, 0.1, 2, Skills.BrandBuilder, Skills.AdSpecialist));
            return e;
        }

        GameEvent MakeSupplierContract()
        {
            var used = State.Suppliers.FindAll(s => s.TotalPurchased > 8000 && !s.Bankrupt && !s.Blacklisted && (s.Discount <= 0 || State.Day > s.DiscountUntil));
            if (used.Count == 0) return null;
            var sup = Rng.Pick(used);
            if (HasOpenEvent("supplier_contract", null, sup.Id)) return null;
            double d = Math.Round(Rng.Range(0.06, 0.12), 2);
            var e = NewEvent("supplier_contract", EventCategory.Supplier, 1, $"{sup.Name} proposes a contract",
                $"{sup.Name} offers {Fmt.Pct(d)} off every order for six months if we make them our main supplier.", 6);
            e.SupplierId = sup.Id; e.Amount = d;
            e.Choices.Add(Choice("Accept the contract", $"{Fmt.Pct(d)} off for 180 days.", 0, 5));
            e.Choices.Add(Choice("Push for more", "They might go higher, or walk away.", 0, 4, 0, 0.5));
            e.Choices.Add(Choice("Decline", "", 0, -1));
            return e;
        }

        GameEvent MakeRaiseRequest()
        {
            var list = C.Employees.FindAll(x => State.Day - x.HiredDay > 60 && State.Day - x.LastRaiseDay > 120);
            if (list.Count == 0) return null;
            var emp = Rng.Pick(list);
            if (HasOpenEvent("raise_request", null, emp.Id)) return null;
            double pct = Math.Round(Rng.Range(0.12, 0.25), 2);
            double newSalary = Math.Round(emp.Salary * (1 + pct) / 50) * 50;
            var e = NewEvent("raise_request", EventCategory.HR, 1, $"{emp.Name} asks for a raise",
                $"{emp.Name} ({Catalog.RoleName(emp.Role)}, skill {emp.Skill}/5) wants a {Fmt.Pct(pct)} raise to {Fmt.Money(newSalary)}/month.", 5);
            e.EmployeeId = emp.Id; e.Amount = newSalary;
            e.Choices.Add(Choice("Approve the raise", $"+{Fmt.Money(newSalary - emp.Salary)}/month. Morale up.", 0, emp.Skill >= 3 ? 3 : 0));
            e.Choices.Add(Choice("Counter with half", "They might accept.", 0, 2, 0, 0.4));
            e.Choices.Add(Choice("Decline", "Morale drops. They might quit.", 0, emp.Skill >= 3 ? -3 : 1, 0, 0.5));
            return e;
        }

        GameEvent MakeCompetitorCut()
        {
            var cp = RandomActiveProduct(x => x.Sales7 >= 2);
            if (cp == null || HasOpenEvent("competitor_cut", cp.Id)) return null;
            var mp = State.GetMarketProduct(cp.ProductId);
            double cut = Math.Round(Rng.Range(0.15, 0.25), 2);
            mp.CompetitorCut = cut;
            mp.CompetitorCutUntil = State.Day + 45;
            double theirs = mp.CompetitorPrice * (1 - cut);
            string rival = Rng.Pick(Catalog.Competitors);
            var e = NewEvent("competitor_cut", EventCategory.Competition, 2, $"{rival} slashed prices on {cp.Name}",
                $"{rival} now sells {cp.Name} at {Fmt.Price(theirs)}. We charge {Fmt.Price(cp.Price)}.", 5);
            e.ProductId = cp.Id; e.Amount = theirs;
            e.Choices.Add(Choice($"Match their price ({Fmt.Price(theirs)})", "Keep volume, lose margin.", 0, 3));
            e.Choices.Add(Choice($"Undercut them ({Fmt.Price(theirs * 0.95)})", "Win the price war.", 0, 1, 0, 0.4));
            e.Choices.Add(Choice("Hold price, boost ads", "Spend to defend demand for 20 days.", 3000, 3));
            e.Choices.Add(Choice("Ignore it", "Our customers are loyal. Right?", 0, -1, 0, 0.3));
            return e;
        }

        GameEvent MakeInfluencerOffer()
        {
            var cp = RandomActiveProduct(x => x.Stock >= 100);
            if (cp == null || HasOpenEvent("influencer_offer")) return null;
            string who = Rng.Pick(Catalog.Influencers);
            double followers = Math.Round(Rng.Range(0.3, 4.0), 1);
            double price = Math.Round(followers * 6000 * Rng.Range(0.8, 1.2) / 500) * 500;
            double boost = Math.Min(3.5, 1.6 + followers / 2);
            var e = NewEvent("influencer_offer", EventCategory.Marketing, 1, $"{who} offers to promote {cp.Name}",
                $"{who} ({Fmt.Num(followers)}M followers) will feature {cp.Name} for {Fmt.Money(price)}. Expect roughly {Fmt.Fixed(boost, 1)}x demand for a week.", 5);
            e.ProductId = cp.Id; e.Amount = price; e.Amount2 = boost;
            e.Choices.Add(Choice($"Accept ({Fmt.Money(price)})", "A week of hype, and maybe a viral moment.", price, 4, 0, 0.3));
            e.Choices.Add(Choice($"Use your network ({Fmt.Money(price * 0.7)})", "Same deal, 30% cheaper.", price * 0.7, 6, 0, 0.3, 0, Skills.InfluencerNetwork));
            e.Choices.Add(Choice("Negotiate 15% off", "They might walk.", 0, 3, 0, 0.5));
            e.Choices.Add(Choice("Decline", "", 0, 0));
            return e;
        }

        GameEvent MakeBulkOrder()
        {
            var cp = RandomActiveProduct(x => x.Stock >= 300);
            if (cp == null || HasOpenEvent("bulk_order")) return null;
            int count = (int)(Math.Round(Math.Min(cp.Stock * 0.6, Rng.Range(200, 2000)) / 50) * 50);
            if (count < 100) return null;
            double total = count * cp.Price * 0.85;
            var e = NewEvent("bulk_order", EventCategory.Opportunity, 1, $"Corporate order: {Fmt.Int(count)} x {cp.Name}",
                $"A corporate client wants {Fmt.Int(count)} units of {cp.Name} at 15% below list ({Fmt.Money(total)} total). They need them now.", 4);
            e.ProductId = cp.Id; e.Count = count;
            e.Choices.Add(Choice("Accept at 15% off", $"{Fmt.Money(total)} in revenue, immediately.", 0, 5));
            e.Choices.Add(Choice("Counter at 8% off", "They might accept, or walk.", 0, 4, 0, 0.5));
            e.Choices.Add(Choice("Decline", "Keep the stock for regular customers.", 0, 0));
            return e;
        }

        GameEvent MakeQualityIssue()
        {
            var cp = RandomActiveProduct(x => x.Stock >= 200 && x.Quality < 0.72);
            if (cp == null || HasOpenEvent("quality_issue", cp.Id)) return null;
            int count = (int)(cp.Stock * Rng.Range(0.1, 0.25));
            var sup = State.GetSupplier(cp.SupplierId);
            var e = NewEvent("quality_issue", EventCategory.Supplier, 2, $"Defective batch: {cp.Name}",
                $"Quality control found about {Fmt.Int(count)} defective units of {cp.Name}{(sup != null ? " from " + sup.Name : "")}.", 4);
            e.ProductId = cp.Id; e.Count = count; e.SupplierId = sup?.Id;
            e.Choices.Add(Choice("Return them to the supplier", "Lose the units, get 80% refunded.", 0, 4));
            e.Choices.Add(Choice("Rework them locally", "Fix and improve quality.", count * 2.0, 3));
            e.Choices.Add(Choice("Sell them anyway", "Free now. Angry customers later.", 0, -4, 0, 0.7));
            return e;
        }

        GameEvent MakePlatformPolicy()
        {
            if (C.Products.Count == 0 || HasOpenEvent("platform_fees") || State.Day <= C.FeeModifierUntil) return null;
            C.FeeModifier = 0.02;
            C.FeeModifierUntil = State.Day + 60;
            var e = NewEvent("platform_fees", EventCategory.Operations, 1, "Marketplace fee increase",
                "The marketplace is raising seller fees by 2 percentage points for the next 60 days.", 6);
            e.Choices.Add(Choice("Accept it", "", 0, 0));
            if (!C.Has("chn1") && !IsBuilding("chn1"))
                e.Choices.Add(Choice("Build our own web store", "Permanently lower fees by 3 points.", Catalog.GetUpgrade("chn1").Cost, 4, 0, 0.2));
            e.Choices.Add(Choice("Lobby the account manager", "Takes a day of your time. Might halve the increase.", 0, 3, 0, 0.3, 1));
            return e;
        }

        GameEvent MakePaymentHold()
        {
            if (C.LastMonth.Revenue < 20000 || C.HeldCash > 0 || C.Cash < 10000) return null;
            double hold = Math.Min(C.Cash * 0.6, Math.Max(5000, C.LastMonth.Revenue * 0.2));
            C.Cash -= hold;
            C.HeldCash += hold;
            C.HeldUntilDay = State.Day + 30;
            var e = NewEvent("payment_hold", EventCategory.Finance, 2, $"Payment processor froze {Fmt.Money(hold)}",
                $"Our payment processor is holding {Fmt.Money(hold)} for a 30-day 'risk review'.", 5);
            e.Amount = hold;
            e.Choices.Add(Choice("Send them documents", "A day of paperwork: funds released in 5 days.", 0, 4, 0, 0, 1));
            e.Choices.Add(Choice("Switch processors", "Funds released now.", 5000, 3));
            e.Choices.Add(Choice("Wait it out", "30 days.", 0, 0));
            return e;
        }

        GameEvent MakeWarehouseIncident()
        {
            CompanyProduct worst = null;
            foreach (var cp in C.Products) if (cp.Active && (worst == null || cp.Stock > worst.Stock)) worst = cp;
            if (worst == null || worst.Stock < 400) return null;
            int count = (int)(worst.Stock * Rng.Range(0.05, 0.12));
            double value = count * worst.AvgCost;
            worst.Stock -= count;
            var e = NewEvent("warehouse_incident", EventCategory.Operations, 1, $"Water leak destroyed {Fmt.Int(count)} units",
                $"A leak in the warehouse destroyed {Fmt.Int(count)} units of {worst.Name} ({Fmt.Money(value)} at cost).", 5);
            e.ProductId = worst.Id; e.Count = count; e.Amount = value;
            e.Choices.Add(Choice("File an insurance claim", "A day of paperwork: recover half the value.", 0, 4, 0, 0, 1));
            e.Choices.Add(Choice("Absorb the loss", "", 0, 0));
            return e;
        }

        GameEvent MakePoaching()
        {
            var list = C.Employees.FindAll(x => x.Skill >= 3);
            if (list.Count == 0) return null;
            var emp = Rng.Pick(list);
            if (HasOpenEvent("poaching", null, emp.Id)) return null;
            string rival = Rng.Pick(Catalog.Competitors);
            var e = NewEvent("poaching", EventCategory.HR, 2, $"{rival} is poaching {emp.Name}",
                $"{rival} offered {emp.Name} ({Catalog.RoleName(emp.Role)}, skill {emp.Skill}/5) a 25% raise to join them.", 4);
            e.EmployeeId = emp.Id;
            e.Choices.Add(Choice("Counter-offer +20% salary", $"{Fmt.Money(emp.Salary * 1.2)}/month.", 0, emp.Skill >= 4 ? 5 : 2));
            e.Choices.Add(Choice("One-time retention bonus", "Usually works.", emp.Salary * 1.5, 3, 0, 0.3));
            e.Choices.Add(Choice("Let them go", "", 0, emp.Skill >= 4 ? -4 : -1));
            return e;
        }

        GameEvent MakeMediaInterview()
        {
            if (C.Reputation < 40 && C.Valuation < 400000) return null;
            if (HasOpenEvent("media_interview")) return null;
            var e = NewEvent("media_interview", EventCategory.Opportunity, 1, "A journalist wants an interview",
                $"A popular business podcast wants to feature {C.Name}. One partner would spend a day on it.", 5);
            e.Choices.Add(Choice("Do the interview", "Company reputation up, and your personal profile too.", 0, 3, 5, 0, 1));
            e.Choices.Add(Choice("Decline politely", "", 0, 0));
            return e;
        }

        GameEvent MakeTaxAudit()
        {
            if (State.Day < 180 || HasOpenEvent("tax_audit")) return null;
            double penalty = Math.Round(Clamp(C.Lifetime.Revenue * 0.02, 5000, 150000));
            var e = NewEvent("tax_audit", EventCategory.Finance, 2, "Tax audit",
                $"The tax authority is auditing our books. Potential penalty: up to {Fmt.Money(penalty)}.", 6);
            e.Amount = penalty;
            e.Choices.Add(Choice("Hire a tax advisor", "Small risk of a reduced penalty.", 8000, 4));
            e.Choices.Add(Choice("Handle it with your expertise", "No penalty. Takes 2 days.", 0, 6, 2, 0, 2, Skills.TaxSpecialist, Skills.Accountant));
            e.Choices.Add(Choice("Handle it yourself anyway", "50% chance of the full penalty.", 0, -2, 0, 0.6, 1));
            return e;
        }

        GameEvent MakeCounterfeit()
        {
            var cp = RandomActiveProduct(x => x.Sales7 >= 10);
            if (cp == null || HasOpenEvent("counterfeit", cp.Id)) return null;
            cp.PenaltyMult = 0.75;
            cp.PenaltyUntil = State.Day + 45;
            var e = NewEvent("counterfeit", EventCategory.Competition, 2, $"Counterfeits of {cp.Name}",
                $"Knock-off sellers are copying {cp.Name} at half the price. Our sales are down about 25%.", 6);
            e.ProductId = cp.Id;
            e.Choices.Add(Choice("File IP takedowns", "A day of work plus legal fees. Usually works.", 2000, 5, 0, 0.2, 1));
            e.Choices.Add(Choice("Cut our price 10%", "", 0, 1));
            e.Choices.Add(Choice("Ignore it", "", 0, -2));
            return e;
        }

        GameEvent MakeSupplierBankrupt()
        {
            var list = State.Suppliers.FindAll(s => !s.Bankrupt && !s.Exclusive && s.TotalPurchased > 0 && s.Name != "QuickShip Dropship Hub");
            if (list.Count == 0) return null;
            var sup = Rng.Pick(list);
            sup.Bankrupt = true;
            int lost = 0;
            double refund = 0;
            for (int i = C.Shipments.Count - 1; i >= 0; i--)
            {
                var sh = C.Shipments[i];
                if (sh.SupplierId != sup.Id) continue;
                refund += sh.Quantity * sh.UnitCost * 0.5;
                lost++;
                C.Shipments.RemoveAt(i);
            }
            if (refund > 0) Receive(refund, TxType.Inventory, -1, $"Partial refund from bankrupt {sup.Name}");
            foreach (var cp in C.Products)
                if (cp.SupplierId == sup.Id) cp.SupplierId = PreferredSupplier(cp)?.Id;
            var e = NewEvent("supplier_bankrupt", EventCategory.Supplier, 2, $"{sup.Name} went bankrupt",
                $"{sup.Name} has shut down. {(lost > 0 ? $"{lost} pending shipment{(lost == 1 ? " was" : "s were")} lost (50% refunded). " : "")}We'll need other suppliers.", 4);
            e.SupplierId = sup.Id;
            e.Choices.Add(Choice("Understood", "Switch affected products to other suppliers.", 0, 1));
            return e;
        }

        GameEvent MakeTeamMorale()
        {
            if (C.Employees.Count < 3 || HasOpenEvent("team_morale")) return null;
            double avg = 0;
            foreach (var x in C.Employees) avg += x.Morale;
            avg /= C.Employees.Count;
            if (avg > 52) return null;
            double offsite = 400 * C.Employees.Count;
            double bonus = 0;
            foreach (var x in C.Employees) bonus += x.Salary * 0.5;
            var e = NewEvent("team_morale", EventCategory.HR, 2, "Team morale is sinking",
                $"Average employee morale is {Fmt.Int(avg)}/100. People are updating their CVs.", 6);
            e.Choices.Add(Choice("Team offsite", "+20 morale for everyone.", offsite, 4));
            e.Choices.Add(Choice("Half-month bonuses", "+30 morale for everyone.", bonus, 3));
            e.Choices.Add(Choice("Ignore it", "", 0, -3));
            return e;
        }

        GameEvent MakeAcquisitionOffer()
        {
            if (State.Day < 300 || C.Valuation < 4000000 || State.Day - _lastAcquisitionDay < 300 || HasOpenEvent("acquisition")) return null;
            if (State.Board.Agenda.Exists(x => x.Type == ProposalType.SellCompany)) return null;
            _lastAcquisitionDay = State.Day;
            double price = Math.Round(C.Valuation * Rng.Range(1.25, 1.75) / 100000) * 100000;
            string buyer = Rng.Pick(new[] { "Amazonia Holdings", "Globex Retail Group", "Summit Brands Inc.", "Northwind Commerce" });
            var e = NewEvent("acquisition", EventCategory.Opportunity, 3, $"Acquisition offer: {Fmt.Money(price)}",
                $"{buyer} wants to buy {C.Name} outright for {Fmt.Money(price)} ({Fmt.Fixed(price / Math.Max(1, C.Valuation), 1)}x our valuation). Selling needs 75% of the votes and ends the game: everyone is paid by ownership.", 10);
            e.Amount = price;
            e.Choices.Add(Choice("Call an emergency board vote", "Partners vote on selling the company.", 0, 0, 5));
            e.Choices.Add(Choice("Reject the offer", "We're building something bigger.", 0, 2));
            return e;
        }

        GameEvent MakeTradeShow()
        {
            if (HasOpenEvent("trade_show")) return null;
            var e = NewEvent("trade_show", EventCategory.Opportunity, 1, "Trade show invitation",
                "The Global Sourcing Expo invites us to exhibit. Great for discovering products and meeting suppliers.", 6);
            e.Choices.Add(Choice("Attend the expo", "2 days of your time: new product leads, better supplier relations.", 8000, 3, 3, 0, 2));
            e.Choices.Add(Choice("Skip it", "", 0, 0));
            return e;
        }

        GameEvent MakeReviewBoom()
        {
            var cp = RandomActiveProduct(x => x.Rating >= 4.35 && x.Sales7 >= 5);
            if (cp == null || HasOpenEvent("review_boom", cp.Id)) return null;
            cp.BoostMult = Math.Max(cp.BoostMult, 1.3);
            cp.BoostUntil = Math.Max(cp.BoostUntil, State.Day + 20);
            var e = NewEvent("review_boom", EventCategory.Opportunity, 1, $"Glowing reviews for {cp.Name}",
                $"{cp.Name} is collecting five-star reviews ({Fmt.Fixed(cp.Rating, 1)} average). Demand is climbing.", 5);
            e.ProductId = cp.Id;
            e.Choices.Add(Choice("Advertise it for a week", "A 7-day search campaign at $150/day.", 1050, 4));
            e.Choices.Add(Choice("Great, noted", "", 0, 1));
            return e;
        }

        // ------------------------------------------------------------------ resolution

        /// <summary>Apply a chosen option. Assumes validation already happened.</summary>
        public void ResolveEventChoice(GameEvent e, int choice, Player p)
        {
            var ch = e.Choices[choice];
            var cp = e.ProductId != null ? State.GetCompanyProduct(e.ProductId) : null;
            string outcome = "";
            int xp = 20;
            if (ch.BusyDays > 0) StartTask(p.Id, TaskKind.PublicRelations, ch.Label, ch.BusyDays);

            switch (e.TemplateId)
            {
                case "low_stock":
                case "viral_moment":
                {
                    var s = State.GetSupplier(e.SupplierId);
                    var mp = cp != null ? State.GetMarketProduct(cp.ProductId) : null;
                    bool isReorder = e.TemplateId == "low_stock" ? choice <= 1 : choice == 0;
                    if (isReorder && cp != null && s != null && mp != null)
                    {
                        bool rush = e.TemplateId == "viral_moment" || choice == 1;
                        int qty = e.TemplateId == "low_stock" && choice == 1 ? (int)e.Amount2 : e.Count;
                        double total = UnitPrice(s, mp, qty, p.Id) * (rush ? 1.35 : 1) * qty;
                        if (C.Cash < total || !CanUseSupplier(s, p.Id)) { outcome = "Couldn't place the order (cash or supplier problem)."; xp = 0; break; }
                        PlaceOrder(p, cp, mp, s, qty, rush);
                        outcome = $"Ordered {Fmt.Int(qty)} units{(rush ? " by air" : "")}.";
                        xp = e.TemplateId == "viral_moment" ? 50 : 15;
                    }
                    else if (cp != null && ((e.TemplateId == "low_stock" && choice == 2) || (e.TemplateId == "viral_moment" && choice == 1)))
                    {
                        double mult = e.TemplateId == "low_stock" ? 1.1 : 1.15;
                        var mpRef = State.GetMarketProduct(cp.ProductId);
                        cp.Price = Math.Round(Math.Min(cp.Price * mult, mpRef.RefPrice * 1.6), 2);
                        outcome = $"Price raised to {Fmt.Price(cp.Price)}.";
                        xp = e.TemplateId == "viral_moment" ? 30 : 5;
                    }
                    else { outcome = "No action."; xp = 0; }
                    break;
                }
                case "campaign_bad":
                {
                    var cam = C.Campaigns.Find(x => x.Id == e.CampaignId);
                    if (cam == null || !cam.Active) { outcome = "The campaign already ended."; xp = 5; break; }
                    if (choice == 0) { EndCampaign(cam, $"stopped by {p.Name}"); outcome = "Campaign stopped."; xp = 25; }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.Marketing, p.Id, $"New ad creatives for {ProductName(cam.CompanyProductId)}");
                        if (Rng.Chance(0.7)) { cam.Efficiency *= 1.4; outcome = "The new creatives work much better."; xp = 25; }
                        else { outcome = "The new creatives didn't help."; xp = 10; }
                    }
                    else if (choice == 2) { cam.DailyBudget *= 2; outcome = $"Budget doubled to {Fmt.Money(cam.DailyBudget)}/day."; xp = 5; }
                    else { outcome = "Left it running."; xp = 0; }
                    break;
                }
                case "shipment_delay":
                {
                    var sh = C.Shipments.Find(x => x.Id == e.ShipmentId);
                    if (sh == null) { outcome = "The shipment already arrived."; xp = 5; break; }
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, "Expedited customs clearance");
                        sh.EtaDay = Math.Max(State.Day + 1, e.Count);
                        outcome = $"Back on schedule: arrives {Fmt.ShortDate(sh.EtaDay)}.";
                        xp = 20;
                    }
                    else if (choice == 2)
                    {
                        C.Shipments.Remove(sh);
                        Receive(sh.Quantity * sh.UnitCost * 0.8, TxType.Inventory, p.Id, "Cancelled delayed shipment (80% refund)");
                        outcome = "Shipment cancelled, 80% refunded.";
                        xp = 10;
                    }
                    else { outcome = "Waiting it out."; xp = 5; }
                    break;
                }
                case "cash_crunch":
                {
                    if (choice == 0)
                    {
                        Catalog.LoanDef def = Catalog.Loans[3];
                        if (C.Loans.Exists(l => l.Lender == def.Lender)) { outcome = "We already have a cash advance."; xp = 5; break; }
                        double amt = LoanOfferAmount("mca", out _);
                        TakeLoan(def, amt, p.Id);
                        outcome = $"Took {Fmt.Money(amt)} at 35% APR.";
                        xp = 20;
                    }
                    else if (choice == 1)
                    {
                        foreach (var cam in C.Campaigns) if (cam.Active) EndCampaign(cam, "paused (cash crunch)");
                        outcome = "All advertising stopped."; xp = 10;
                    }
                    else if (choice == 2)
                    {
                        CompanyProduct slow = null;
                        foreach (var x in C.Products)
                            if (x.Active && x.Stock > 0 && (slow == null || x.Sales7 < slow.Sales7)) slow = x;
                        if (slow == null) { outcome = "Nothing to liquidate."; xp = 0; break; }
                        CmdLiquidate(new Command { Type = CommandType.Liquidate, PlayerId = p.Id, A = slow.Id }, p);
                        outcome = $"Liquidated {slow.Name}."; xp = 10;
                    }
                    else { outcome = "Hoping for the best."; xp = 0; }
                    break;
                }
                case "bills_due":
                {
                    if (choice == 1)
                    {
                        foreach (var cam in C.Campaigns) if (cam.Active) cam.DailyBudget = Math.Max(20, Math.Round(cam.DailyBudget / 2));
                        outcome = "Ad budgets halved."; xp = 15;
                    }
                    else { outcome = "Acknowledged."; xp = 10; }
                    break;
                }
                case "holiday_prep": outcome = "The team is preparing for the holidays."; xp = 10; break;
                case "dead_stock":
                {
                    if (cp == null || !cp.Active) { outcome = "Already gone."; xp = 0; break; }
                    if (choice == 0) { CmdLiquidate(new Command { Type = CommandType.Liquidate, PlayerId = p.Id, A = cp.Id }, p); outcome = "Liquidated."; xp = 10; }
                    else if (choice == 1)
                    {
                        var mpRef = State.GetMarketProduct(cp.ProductId);
                        cp.Price = Math.Round(Math.Max(cp.Price * 0.7, mpRef.RefPrice * 0.4), 2);
                        outcome = $"Price cut to {Fmt.Price(cp.Price)}."; xp = 10;
                    }
                    else { outcome = "Holding the stock."; xp = 0; }
                    break;
                }
                case "trend_spike":
                {
                    var mp = State.GetMarketProduct(e.ProductId);
                    if (choice == 0 && mp != null)
                    {
                        var pr = Priv(p.Id);
                        if (!pr.Notebook.Exists(d => d.ProductId == mp.Id)) pr.Notebook.Add(MakeDiscovery(mp, p.Id, 1));
                        outcome = $"{p.Name} researched {mp.Name}.";
                        xp = 20;
                    }
                    else { outcome = "Ignored."; xp = 0; }
                    break;
                }
                case "customer_crisis":
                {
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, $"Recall and refunds: {ProductName(e.ProductId)}");
                        C.Reputation = Math.Min(100, C.Reputation + 3);
                        C.Satisfaction = Math.Min(100, C.Satisfaction + 6);
                        if (cp != null) cp.Rating = Math.Min(5, cp.Rating + 0.3);
                        outcome = "Customers appreciated the full recall."; xp = 80; p.CrisesHandled++;
                    }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, "Discount codes for affected customers");
                        C.Reputation = Math.Max(0, C.Reputation - 2);
                        C.Satisfaction = Math.Min(100, C.Satisfaction + 2);
                        outcome = "The apology limited the damage."; xp = 50;
                    }
                    else if (choice == 2)
                    {
                        if (Rng.Chance(0.45)) { outcome = "Somehow, it blew over."; xp = 30; }
                        else
                        {
                            C.Reputation = Math.Max(0, C.Reputation - 12);
                            C.Satisfaction = Math.Max(0, C.Satisfaction - 10);
                            if (cp != null) { cp.PenaltyMult = 0.6; cp.PenaltyUntil = State.Day + 30; }
                            outcome = "The denial backfired badly. Reputation took a big hit.";
                            xp = 0;
                            Penalize(p.Id, 30, "PR disaster");
                        }
                    }
                    else
                    {
                        C.Reputation = Math.Min(100, C.Reputation + 6);
                        C.Satisfaction = Math.Min(100, C.Satisfaction + 8);
                        outcome = $"{p.Name}'s personal response turned critics into fans."; xp = 100; p.CrisesHandled++;
                        p.Reputation = Math.Min(100, p.Reputation + 5);
                    }
                    break;
                }
                case "supplier_contract":
                {
                    var s = State.GetSupplier(e.SupplierId);
                    if (s == null) { outcome = "The supplier is gone."; xp = 0; break; }
                    if (choice == 0)
                    {
                        s.Discount = Math.Max(s.Discount, e.Amount); s.DiscountUntil = State.Day + 180;
                        outcome = $"{Fmt.Pct(e.Amount)} off {s.Name} for six months."; xp = 30;
                    }
                    else if (choice == 1)
                    {
                        double p0 = 0.5 + (p.HasSkill(Skills.Negotiator) ? 0.2 : 0) + (p.HasSkill(Skills.FastTalker) ? 0.1 : 0);
                        if (Rng.Chance(p0))
                        {
                            s.Discount = Math.Max(s.Discount, e.Amount + 0.04); s.DiscountUntil = State.Day + 180;
                            outcome = $"Pushed them to {Fmt.Pct(e.Amount + 0.04)} off!"; xp = 45; p.NegotiationsWon++;
                        }
                        else { s.Relationship = Math.Max(0, s.Relationship - 5); outcome = $"{s.Name} withdrew the offer."; xp = 5; }
                    }
                    else { s.Relationship = Math.Max(0, s.Relationship - 2); outcome = "Declined."; xp = 5; }
                    break;
                }
                case "raise_request":
                {
                    var emp = C.Employees.Find(x => x.Id == e.EmployeeId);
                    if (emp == null) { outcome = "They already left."; xp = 0; break; }
                    if (choice == 0) { emp.Salary = e.Amount; emp.Morale = Math.Min(100, emp.Morale + 25); emp.LastRaiseDay = State.Day; outcome = $"{emp.Name} is happy."; xp = 20; }
                    else if (choice == 1)
                    {
                        emp.LastRaiseDay = State.Day;
                        if (Rng.Chance(0.6)) { emp.Salary = Math.Round((emp.Salary + e.Amount) / 2 / 50) * 50; emp.Morale = Math.Min(100, emp.Morale + 8); outcome = $"{emp.Name} accepted the counter-offer."; xp = 20; }
                        else { emp.Morale = Math.Max(0, emp.Morale - 12); outcome = $"{emp.Name} is disappointed."; xp = 10; }
                    }
                    else outcome = RejectRaise(emp, 0.35, ref xp);
                    break;
                }
                case "competitor_cut":
                {
                    if (cp == null) { outcome = "No longer relevant."; xp = 0; break; }
                    if (choice == 0) { cp.Price = Math.Round(e.Amount, 2); outcome = $"Matched at {Fmt.Price(cp.Price)}."; xp = 25; }
                    else if (choice == 1) { cp.Price = Math.Round(e.Amount * 0.95, 2); outcome = $"Undercut at {Fmt.Price(cp.Price)}."; xp = 25; }
                    else if (choice == 2)
                    {
                        Spend(ch.Cost, TxType.Marketing, p.Id, $"Defensive ads for {cp.Name}");
                        cp.BoostMult = Math.Max(cp.BoostMult, 1.25); cp.BoostUntil = Math.Max(cp.BoostUntil, State.Day + 20);
                        outcome = "Defensive ads running for 20 days."; xp = 25;
                    }
                    else { outcome = "Ignored the price cut."; xp = 0; }
                    break;
                }
                case "influencer_offer":
                {
                    if (cp == null) { outcome = "The product is gone."; xp = 0; break; }
                    if (choice == 0 || choice == 1) { ApplyInfluencer(e, cp, ch.Cost, p); outcome = $"Promotion booked for {Fmt.Money(ch.Cost)}."; xp = choice == 1 ? 50 : 40; }
                    else if (choice == 2)
                    {
                        if (Rng.Chance(0.5))
                        {
                            double price = e.Amount * 0.85;
                            if (C.Cash < price) { outcome = "They agreed, but we can't afford it."; xp = 0; break; }
                            ApplyInfluencer(e, cp, price, p); outcome = $"Negotiated down to {Fmt.Money(price)}."; xp = 45;
                        }
                        else { outcome = "The influencer walked away."; xp = 5; }
                    }
                    else { outcome = "Declined."; xp = 0; }
                    break;
                }
                case "bulk_order":
                {
                    if (cp == null) { outcome = "No longer possible."; xp = 0; break; }
                    double discount = choice == 0 ? 0.15 : 0.08;
                    if (choice == 1 && !Rng.Chance(0.5)) { outcome = "The client walked away."; xp = 5; break; }
                    if (choice == 2) { outcome = "Declined."; xp = 0; break; }
                    if (cp.Stock < e.Count) { outcome = "Not enough stock left to fill it."; xp = 0; break; }
                    SellBulk(cp, e.Count, discount, p);
                    outcome = $"Sold {Fmt.Int(e.Count)} units at {Fmt.Pct(discount)} off."; xp = choice == 1 ? 50 : 40;
                    break;
                }
                case "quality_issue":
                {
                    if (cp == null) { outcome = "No longer relevant."; xp = 0; break; }
                    int count = Math.Min(e.Count, cp.Stock);
                    if (choice == 0)
                    {
                        cp.Stock -= count;
                        Receive(count * cp.AvgCost * 0.8, TxType.Inventory, p.Id, $"Refund for {Fmt.Int(count)} defective {cp.Name}");
                        var s = State.GetSupplier(e.SupplierId);
                        if (s != null) s.Relationship = Math.Max(0, s.Relationship - 5);
                        outcome = "Defective units returned."; xp = 30;
                    }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, $"Rework of {Fmt.Int(count)} {cp.Name}");
                        cp.Quality = Math.Min(1, cp.Quality + 0.05);
                        outcome = "Units reworked; quality improved."; xp = 30;
                    }
                    else
                    {
                        ApplyDefectsSold(cp);
                        outcome = "Sold as-is. Expect complaints."; xp = 0;
                    }
                    break;
                }
                case "platform_fees":
                {
                    string label = ch.Label;
                    if (label.StartsWith("Build our own"))
                    {
                        var u = Catalog.GetUpgrade("chn1");
                        if (C.Has(u.Id) || IsBuilding(u.Id)) { outcome = "We already have a web store."; xp = 5; break; }
                        StartUpgradeBuild(u, p.Id);
                        outcome = $"Our web store opens in {Fmt.Days(u.BuildDays)}."; xp = 40;
                    }
                    else if (label.StartsWith("Lobby"))
                    {
                        if (Rng.Chance(0.5)) { C.FeeModifier = 0.01; outcome = "The increase was halved."; xp = 30; }
                        else { outcome = "The account manager wouldn't budge."; xp = 10; }
                    }
                    else { outcome = "Accepted the new fees."; xp = 5; }
                    break;
                }
                case "payment_hold":
                {
                    if (choice == 0) { C.HeldUntilDay = Math.Min(C.HeldUntilDay, State.Day + 5); outcome = "Funds will be released in 5 days."; xp = 30; }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, "Switched payment processors");
                        C.Cash += C.HeldCash;
                        AddLedger(TxType.Other, C.HeldCash, p.Id, "Held funds released", null, 0);
                        C.HeldCash = 0;
                        outcome = "Funds released."; xp = 20;
                    }
                    else { outcome = "Waiting 30 days."; xp = 0; }
                    break;
                }
                case "warehouse_incident":
                {
                    if (choice == 0)
                    {
                        Receive(e.Amount * 0.5, TxType.Insurance, p.Id, "Insurance payout: water damage");
                        outcome = $"Insurance paid {Fmt.Money(e.Amount * 0.5)}."; xp = 25;
                    }
                    else { outcome = "Absorbed the loss."; xp = 0; }
                    break;
                }
                case "poaching":
                {
                    var emp = C.Employees.Find(x => x.Id == e.EmployeeId);
                    if (emp == null) { outcome = "Already gone."; xp = 0; break; }
                    if (choice == 0) { emp.Salary = Math.Round(emp.Salary * 1.2 / 50) * 50; emp.Morale = Math.Min(100, emp.Morale + 15); outcome = $"{emp.Name} stays."; xp = 20; }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.Payroll, p.Id, $"Retention bonus: {emp.Name}");
                        if (Rng.Chance(0.7)) { emp.Morale = Math.Min(100, emp.Morale + 10); outcome = $"{emp.Name} stays."; xp = 15; }
                        else { C.Employees.Remove(emp); outcome = $"{emp.Name} took the bonus and left anyway."; xp = 0; }
                    }
                    else { C.Employees.Remove(emp); outcome = $"{emp.Name} left for the competition."; xp = 0; }
                    break;
                }
                case "media_interview":
                {
                    if (choice == 0)
                    {
                        C.Reputation = Math.Min(100, C.Reputation + 3);
                        p.Reputation = Math.Min(100, p.Reputation + 8);
                        outcome = $"{p.Name} was the face of the company on the podcast."; xp = 40;
                    }
                    else { outcome = "Declined."; xp = 0; }
                    break;
                }
                case "tax_audit":
                {
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, "Tax advisor");
                        if (Rng.Chance(0.25)) { Spend(e.Amount * 0.3, TxType.Fine, -1, "Tax penalty (reduced)"); outcome = $"Reduced penalty of {Fmt.Money(e.Amount * 0.3)}."; }
                        else outcome = "The advisor cleared everything.";
                        xp = 30;
                    }
                    else if (choice == 1) { outcome = "Handled expertly. No penalty."; xp = 60; }
                    else
                    {
                        if (Rng.Chance(0.5)) { Spend(e.Amount, TxType.Fine, -1, "Tax penalty"); outcome = $"Penalty: {Fmt.Money(e.Amount)}."; xp = 5; }
                        else { outcome = "Got lucky: no penalty."; xp = 30; }
                    }
                    break;
                }
                case "counterfeit":
                {
                    if (cp == null) { outcome = "No longer relevant."; xp = 0; break; }
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, $"IP takedowns for {cp.Name}");
                        if (Rng.Chance(0.7)) { cp.PenaltyUntil = State.Day; outcome = "The knock-offs were taken down."; xp = 40; }
                        else { outcome = "The takedowns failed."; xp = 10; }
                    }
                    else if (choice == 1) { cp.Price = Math.Round(cp.Price * 0.9, 2); outcome = $"Price cut to {Fmt.Price(cp.Price)}."; xp = 15; }
                    else { outcome = "Ignored."; xp = 0; }
                    break;
                }
                case "supplier_bankrupt": outcome = "Noted."; xp = 5; break;
                case "team_morale":
                {
                    if (choice == 0) { Spend(ch.Cost, TxType.EventCost, p.Id, "Team offsite"); foreach (var x in C.Employees) x.Morale = Math.Min(100, x.Morale + 20); outcome = "The offsite was a hit."; xp = 30; }
                    else if (choice == 1) { Spend(ch.Cost, TxType.Payroll, p.Id, "Team bonuses"); foreach (var x in C.Employees) x.Morale = Math.Min(100, x.Morale + 30); outcome = "Bonuses paid."; xp = 30; }
                    else { foreach (var x in C.Employees) x.Morale = Math.Max(0, x.Morale - 5); outcome = "Ignored. Morale fell further."; xp = 0; }
                    break;
                }
                case "acquisition":
                {
                    if (choice == 0)
                    {
                        CallEmergencyMeeting(p, ProposalType.SellCompany, e.Amount, $"Sell {C.Name} for {Fmt.Money(e.Amount)}");
                        outcome = "Emergency board meeting called."; xp = 20;
                    }
                    else { outcome = "Offer rejected."; xp = 10; }
                    break;
                }
                case "trade_show":
                {
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.EventCost, p.Id, "Global Sourcing Expo");
                        var found = RunResearch(p.Id, "Any", 1);
                        C.Reputation = Math.Min(100, C.Reputation + 2);
                        foreach (var s in State.Suppliers) s.Relationship = Math.Min(100, s.Relationship + 3);
                        outcome = $"Came back with {found.Count} product leads."; xp = 40;
                    }
                    else { outcome = "Skipped."; xp = 0; }
                    break;
                }
                case "review_boom":
                {
                    if (choice == 0 && cp != null)
                    {
                        if (C.Campaigns.Exists(x => x.Active && x.CompanyProductId == cp.Id && x.Channel == AdChannel.Search)) { outcome = "A search campaign is already running."; xp = 5; break; }
                        C.Campaigns.Add(new Campaign { Id = NewId("m"), CompanyProductId = cp.Id, Channel = AdChannel.Search, DailyBudget = 150, StartDay = State.Day + 1, EndDay = State.Day + 7, CreatedBy = p.Id });
                        outcome = "A week of search ads is running."; xp = 20;
                    }
                    else { outcome = "Noted."; xp = 5; }
                    break;
                }
                case "contract_offer":
                {
                    if (cp == null || !cp.Active) { outcome = "The product is gone."; xp = 0; break; }
                    if (choice == 0) { SignContract(e, p, e.Amount); outcome = $"Signed: deliver {Fmt.Int(e.Count)} units within {e.EmployeeId} days."; xp = 20; }
                    else if (choice == 1)
                    {
                        if (Rng.Chance(0.55)) { SignContract(e, p, Math.Round(e.Amount * 1.08, 2)); outcome = "They agreed to 8% more. Signed."; xp = 35; }
                        else { outcome = "They found another supplier."; xp = 5; }
                    }
                    else { outcome = "Declined."; xp = 0; }
                    break;
                }
                case "price_war":
                {
                    string cat = e.EmployeeId;
                    var affected = C.Products.FindAll(x => x.Active && x.Category == cat);
                    if (choice == 0)
                    {
                        foreach (var x in affected) x.Price = Math.Round(x.Price * 0.9, 2);
                        outcome = $"Cut prices on {affected.Count} {cat} products."; xp = 30;
                    }
                    else if (choice == 1)
                    {
                        Spend(ch.Cost, TxType.Marketing, p.Id, $"Defensive ads in {cat}");
                        foreach (var x in affected) { x.BoostMult = Math.Max(x.BoostMult, 1.2); x.BoostUntil = Math.Max(x.BoostUntil, State.Day + 30); }
                        outcome = $"Defensive ads running across {cat} for a month."; xp = 30;
                    }
                    else { outcome = "Held our ground."; xp = 0; }
                    break;
                }
                case "rival_stumble":
                {
                    string cat = e.EmployeeId;
                    if (choice == 0)
                    {
                        Spend(ch.Cost, TxType.Marketing, p.Id, $"Conquest campaign in {cat}");
                        foreach (var x in C.Products)
                            if (x.Active && x.Category == cat) { x.BoostMult = Math.Max(x.BoostMult, 1.35); x.BoostUntil = Math.Max(x.BoostUntil, State.Day + 30); }
                        var r = State.Rivals.Find(z => z.Id == e.SupplierId);
                        if (r != null) r.Strength = Math.Max(0.3, r.Strength - 0.1);
                        outcome = $"Our {cat} campaign is stealing their customers."; xp = 45;
                    }
                    else { outcome = "Noted."; xp = 5; }
                    break;
                }
                default: outcome = "Done."; break;
            }
            FinishEvent(e, p, choice, outcome, xp);
        }

        string RejectRaise(Employee emp, double quitChance, ref int xp)
        {
            emp.Morale = Math.Max(0, emp.Morale - 25);
            emp.LastRaiseDay = State.Day;
            if (Rng.Chance(quitChance))
            {
                C.Employees.Remove(emp);
                xp = 0;
                return $"{emp.Name} quit.";
            }
            xp = 10;
            return $"{emp.Name} is unhappy but staying.";
        }

        void ApplyInfluencer(GameEvent e, CompanyProduct cp, double cost, Player p)
        {
            Spend(cost, TxType.Marketing, p.Id, $"Influencer promotion: {cp.Name}");
            cp.BoostMult = Math.Max(cp.BoostMult, e.Amount2);
            cp.BoostUntil = Math.Max(cp.BoostUntil, State.Day + 7);
            var mp = State.GetMarketProduct(cp.ProductId);
            if (mp != null && !mp.Viral && Rng.Chance(0.12 + mp.ViralPotential * 0.2)) TriggerViral(mp, cp, p.Id);
        }

        void SellBulk(CompanyProduct cp, int count, double discount, Player p)
        {
            double revenue = count * cp.Price * (1 - discount);
            double cogs = count * cp.AvgCost;
            double fees = revenue * 0.03;
            cp.Stock -= count;
            C.Cash += revenue - fees;
            C.Today.Revenue += revenue;
            C.Today.Cogs += cogs;
            C.Today.Fees += fees;
            C.Today.Units += count;
            cp.Revenue += revenue;
            cp.Profit += revenue - cogs - fees;
            cp.ProfitQuarter += revenue - cogs - fees;
            cp.SoldTotal += count;
            C.LifetimeUnits += count;
            AddLedger(TxType.Revenue, revenue - fees, p.Id, $"Corporate order: {Fmt.Int(count)} x {cp.Name}", null, 0);
            CreditProfit(cp, revenue - cogs - fees);
        }

        void ApplyDefectsSold(CompanyProduct cp)
        {
            C.Satisfaction = Math.Max(0, C.Satisfaction - 6);
            cp.Rating = Math.Max(1, cp.Rating - 0.3);
            cp.Quality = Math.Max(0.1, cp.Quality - 0.08);
        }

        string ExpireEventEffects(GameEvent e)
        {
            var cp = e.ProductId != null ? State.GetCompanyProduct(e.ProductId) : null;
            switch (e.TemplateId)
            {
                case "customer_crisis":
                    C.Reputation = Math.Max(0, C.Reputation - 10);
                    C.Satisfaction = Math.Max(0, C.Satisfaction - 8);
                    if (cp != null) { cp.PenaltyMult = 0.7; cp.PenaltyUntil = State.Day + 30; }
                    return "Nobody responded to the complaints. Reputation dropped sharply.";
                case "raise_request":
                {
                    var emp = C.Employees.Find(x => x.Id == e.EmployeeId);
                    if (emp == null) return "";
                    int xp = 0;
                    return RejectRaise(emp, 0.25, ref xp);
                }
                case "poaching":
                {
                    var emp = C.Employees.Find(x => x.Id == e.EmployeeId);
                    if (emp != null) { C.Employees.Remove(emp); return $"{emp.Name} left for the competition."; }
                    return "";
                }
                case "quality_issue":
                    if (cp != null) ApplyDefectsSold(cp);
                    return "The defective units were shipped to customers.";
                case "tax_audit":
                    Spend(e.Amount, TxType.Fine, -1, "Tax penalty (audit ignored)");
                    return $"The audit went unanswered: {Fmt.Money(e.Amount)} penalty.";
                case "team_morale":
                    foreach (var x in C.Employees) x.Morale = Math.Max(0, x.Morale - 5);
                    return "Morale fell further.";
                case "cash_crunch":
                    return "No emergency action was taken.";
                case "acquisition":
                    return "The acquisition offer lapsed.";
            }
            return "";
        }
    }
}
