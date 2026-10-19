using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// Personal interest vs company interest: private offers, secret deals, the evidence they
    /// leave behind, and what partners do when they find it.
    /// </summary>
    public partial class GameSimulation
    {
        static readonly string[] Conferences = { "E-Commerce Summit", "Retail Innovators Forum", "D2C Growth Week", "Founders Unplugged" };
        static readonly string[] Brands = { "a rival skincare brand", "a venture-backed startup", "a family-owned retailer", "a DTC furniture brand" };

        // ------------------------------------------------------------------ generation

        void OffersDaily()
        {
            // Expiry and broken obligations
            foreach (var o in State.Offers)
            {
                if (o.Status == OfferStatus.Pending && State.Day > o.ExpiresDay)
                {
                    o.Status = OfferStatus.Expired;
                    if (o.Kind == OfferKind.Blackmail && o.FromId >= 0)
                        Notify(o.FromId, NoticeKind.Private, $"{State.PlayerName(o.TargetId)} ignored your demand.");
                }
                else if (o.Status == OfferStatus.Accepted && o.Kind == OfferKind.Kickback && State.Day > o.FulfillBy)
                {
                    o.Status = OfferStatus.Broken;
                    var s = State.GetSupplier(o.SupplierId);
                    if (s != null) s.Relationship = Math.Max(0, s.Relationship - 10);
                    Notify(o.TargetId, NoticeKind.Private, $"Your side deal with {o.FromName} lapsed: you never placed the order.");
                }
            }
            if (State.Offers.Count > 150) State.Offers.RemoveAll(o => o.Status != OfferStatus.Pending && o.Status != OfferStatus.Accepted && State.Day - o.CreatedDay > 120);

            if (!State.Settings.Intrigue || State.Day < 20) return;
            double stage = Math.Min(1, C.Valuation / 1000000.0);
            foreach (var p in State.Players)
            {
                int pending = 0;
                foreach (var o in State.Offers) if (o.TargetId == p.Id && o.Status == OfferStatus.Pending) pending++;
                if (pending >= 2) continue;
                double chance = 0.022 * (p.HasSkill(Skills.Networker) ? 2 : 1) * (0.6 + 0.4 * stage) * DifficultyMult;
                if (Rng.Chance(chance)) CreatePrivateOffer(p);
            }
        }

        void CreatePrivateOffer(Player p)
        {
            var pr = Priv(p.Id);
            var kinds = new List<KeyValuePair<OfferKind, double>>
            {
                new KeyValuePair<OfferKind, double>(OfferKind.Kickback, 3.0),
                new KeyValuePair<OfferKind, double>(OfferKind.DataSale, C.Products.Count >= 2 ? 1.4 : 0),
                new KeyValuePair<OfferKind, double>(OfferKind.FakeInvoice, C.Cash > 60000 ? 1.1 : 0),
                new KeyValuePair<OfferKind, double>(OfferKind.PoachJob, C.Valuation > 1000000 ? 0.6 : 0),
                new KeyValuePair<OfferKind, double>(OfferKind.InsiderTip, pr.Cash >= 5000 ? (p.HasSkill(Skills.Investor) ? 1.2 : 0.6) : 0),
                new KeyValuePair<OfferKind, double>(OfferKind.SpeakingGig, 1.0),
                new KeyValuePair<OfferKind, double>(OfferKind.SideConsulting, 0.8),
            };
            for (int attempt = 0; attempt < 4; attempt++)
            {
                var pick = Rng.PickWeighted(kinds, k => k.Value);
                if (pick.Value <= 0) return;
                kinds.Remove(pick);
                var offer = BuildOffer(pick.Key, p);
                if (offer == null) continue;
                State.Offers.Add(offer);
                Notify(p.Id, NoticeKind.Private, $"Private offer: {offer.Title}");
                return;
            }
        }

        PrivateOffer NewOffer(OfferKind kind, Player target, string from, string title, string text, double personal, int days = 5)
        {
            double mult = target.HasSkill(Skills.Networker) ? 1.4 : 1;
            return new PrivateOffer
            {
                Id = NewId("o"), Kind = kind, TargetId = target.Id, FromName = from, Title = title, Text = text,
                PersonalAmount = Math.Round(personal * mult / 500) * 500, CreatedDay = State.Day, ExpiresDay = State.Day + days,
                Status = OfferStatus.Pending
            };
        }

        PrivateOffer BuildOffer(OfferKind kind, Player p)
        {
            switch (kind)
            {
                case OfferKind.Kickback:
                {
                    var products = C.Products.FindAll(x => x.Active && x.Sales7 >= 2);
                    if (products.Count == 0) return null;
                    var cp = Rng.Pick(products);
                    var mp = State.GetMarketProduct(cp.ProductId);
                    int qty = (int)(Math.Ceiling(Math.Max(200, cp.Sales7 * 45) / 50) * 50);
                    double cheapest = CheapestUnitPrice(mp, qty, p.Id, out var cs);
                    var options = new List<Supplier>();
                    foreach (var s in State.Suppliers)
                    {
                        if (s == cs || !s.Serves(mp.Category) || !CanUseSupplier(s, p.Id) || s.Name.StartsWith("QuickShip")) continue;
                        double prem = UnitPrice(s, mp, Math.Max(qty, s.Moq), p.Id) / cheapest - 1;
                        if (prem >= 0.08 && prem <= 0.45) options.Add(s);
                    }
                    if (options.Count == 0) return null;
                    var sup = Rng.PickWeighted(options, s => 0.2 + s.Shadiness);
                    qty = Math.Max(qty, sup.Moq);
                    double unit = UnitPrice(sup, mp, qty, p.Id);
                    double premium = unit / cheapest - 1;
                    double extraCost = (unit - cheapest) * qty;
                    double personal = Math.Max(2000, extraCost * Rng.Range(0.35, 0.6) + qty * 0.5);
                    var o = NewOffer(kind, p, sup.Name, $"{sup.Name} wants your next {cp.Name} order",
                        $"{sup.Name} contacts you privately: \"Place your next {cp.Name} order with us, at least {Fmt.Int(qty)} units, and we'll wire you {{AMOUNT}} personally. Nobody needs to know.\" Their price is {Fmt.Pct(premium)} above {cs?.Name ?? "the cheapest supplier"}.",
                        personal);
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.SupplierId = sup.Id; o.ProductId = mp.Id; o.Quantity = qty; o.PremiumPct = premium; o.CompanyCost = extraCost;
                    return o;
                }
                case OfferKind.DataSale:
                {
                    string rival = Rng.Pick(Catalog.Competitors);
                    double amount = Clamp(C.Valuation * 0.004, 5000, 60000);
                    var o = NewOffer(kind, p, rival, $"{rival} wants our sales data",
                        $"A contact at {rival} offers {{AMOUNT}} for a copy of our product sales data and supplier prices. They'd use it to compete with our best sellers.", amount);
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.CompanyCost = amount * 3;
                    return o;
                }
                case OfferKind.FakeInvoice:
                {
                    string firm = Rng.Pick(Catalog.Consultants);
                    double invoice = Math.Round(Math.Min(C.Cash * 0.1, Rng.Range(15000, 40000)) / 1000) * 1000;
                    var o = NewOffer(kind, p, firm, $"{firm} proposes a 'strategy project'",
                        $"{firm} will invoice the company {Fmt.Money(invoice)} for a strategy report nobody will read, and send {{AMOUNT}} back to you. You just approve the invoice.", invoice * 0.4);
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.CompanyCost = invoice;
                    return o;
                }
                case OfferKind.PoachJob:
                {
                    string rival = Rng.Pick(Catalog.Competitors);
                    double amount = Clamp(C.Valuation * 0.02, 50000, 150000);
                    var o = NewOffer(kind, p, rival, $"{rival} offers you {{AMOUNT}}",
                        $"{rival} will pay you {{AMOUNT}} as an 'advisory fee' for our supplier list and negotiated prices. Our discounts would evaporate once they undercut us.", amount, 6);
                    o.Title = o.Title.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.CompanyCost = amount * 2;
                    return o;
                }
                case OfferKind.InsiderTip:
                {
                    var pr = Priv(p.Id);
                    double stake = Math.Round(Math.Min(pr.Cash * 0.5, Rng.Range(5000, 30000)) / 500) * 500;
                    if (stake < 2000) return null;
                    var o = NewOffer(kind, p, "An old friend", "An insider stock tip",
                        $"A friend at a listed company hints that a takeover will be announced next week. Invest {Fmt.Money(stake)} of your own money? Illegal, but lucrative if nobody finds out.", 0, 4);
                    o.PersonalAmount = stake;
                    o.Reportable = false;
                    return o;
                }
                case OfferKind.SpeakingGig:
                {
                    string conf = Rng.Pick(Conferences);
                    double fee = Rng.Range(5000, 14000) * (1 + p.Reputation / 100);
                    var o = NewOffer(kind, p, conf, $"Speak at the {conf}",
                        $"The {conf} invites you as a speaker for a {{AMOUNT}} fee. You'd be away for 3 days and unable to work on the company.", fee);
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.Reportable = false;
                    return o;
                }
                case OfferKind.SideConsulting:
                {
                    string brand = Rng.Pick(Brands);
                    double fee = Rng.Range(6000, 12000);
                    var o = NewOffer(kind, p, Fmt.Title(brand), "A quiet side gig",
                        $"{Fmt.Title(brand)} wants a few hours of your advice for {{AMOUNT}}. A potential conflict of interest, but who would know?", fee);
                    o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
                    o.Reportable = false;
                    return o;
                }
            }
            return null;
        }

        void MaybeInfluencerSkim(GameEvent e)
        {
            if (!State.Settings.Intrigue || !Rng.Chance(0.35) || State.Players.Count == 0) return;
            var target = Rng.Pick(State.Players);
            string who = e.Title.Split(' ')[0];
            double inflated = Math.Round(e.Amount * 1.25 / 500) * 500;
            var o = NewOffer(OfferKind.InfluencerSkim, target, who, $"{who} has a private proposal",
                $"{who} messages you: \"Book the promotion at {Fmt.Money(inflated)} instead of {Fmt.Money(e.Amount)} and {{AMOUNT}} comes back to you.\" Accepting books the deal right away.", e.Amount * 0.15, 4);
            o.Text = o.Text.Replace("{AMOUNT}", Fmt.Money(o.PersonalAmount));
            o.EventId = e.Id; o.CompanyCost = inflated; o.ProductId = e.ProductId;
            State.Offers.Add(o);
            Notify(target.Id, NoticeKind.Private, $"Private offer: {o.Title}");
        }

        // ------------------------------------------------------------------ responses

        CmdResult CmdRespondOffer(Command c, Player p)
        {
            var o = State.Offers.Find(x => x.Id == c.A);
            if (o == null || o.TargetId != p.Id) return CmdResult.Fail("Unknown offer.");
            if (o.Status != OfferStatus.Pending) return CmdResult.Fail("That offer is no longer open.");
            if (State.Day > o.ExpiresDay) { o.Status = OfferStatus.Expired; return CmdResult.Fail("That offer expired."); }
            var pr = Priv(p.Id);
            int response = c.I;
            if (response == 2 && !o.Reportable) return CmdResult.Fail("There is nobody to report this to.");

            if (response == 1)
            {
                o.Status = OfferStatus.Rejected;
                pr.OffersRejected++;
                if (o.Kind == OfferKind.Blackmail && o.FromId >= 0) Notify(o.FromId, NoticeKind.Private, $"{p.Name} refused to pay you.");
                if ((o.Kind == OfferKind.VoteTrade || o.Kind == OfferKind.VoteBuy || o.Kind == OfferKind.ShareSale) && o.FromId >= 0) AIOnOfferRejected(o);
                return CmdResult.Success("Declined.");
            }
            if (response == 2)
            {
                o.Status = OfferStatus.Reported;
                pr.OffersReported++;
                ReportOffer(o, p);
                return CmdResult.Success();
            }

            // Accept
            switch (o.Kind)
            {
                case OfferKind.Kickback:
                    o.Status = OfferStatus.Accepted;
                    o.FulfillBy = State.Day + 15;
                    pr.OffersAccepted++;
                    return CmdResult.Success($"Deal. Order at least {Fmt.Int(o.Quantity)} units from {o.FromName} by {Fmt.ShortDate(o.FulfillBy)} to get paid.");

                case OfferKind.DataSale:
                {
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    PaySecret(p, o.PersonalAmount);
                    CompanyProduct top = null;
                    foreach (var cp in C.Products) if (cp.Active && (top == null || cp.Sales7 > top.Sales7)) top = cp;
                    if (top != null)
                    {
                        var mp = State.GetMarketProduct(top.ProductId);
                        mp.Competition = Math.Min(0.95, mp.Competition + 0.15);
                        top.PenaltyMult = Math.Min(top.PenaltyMult, 0.85);
                        top.PenaltyUntil = Math.Max(top.PenaltyUntil, State.Day + 60);
                    }
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} sold company sales data to {o.FromName} for about {Fmt.Money(o.PersonalAmount)}.", o.PersonalAmount, 0.75);
                    return CmdResult.Success($"{Fmt.Money(o.PersonalAmount)} landed in your personal account.");
                }
                case OfferKind.FakeInvoice:
                {
                    if (C.Cash < o.CompanyCost) return CmdResult.Fail("The company can't cover that invoice right now.");
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    Spend(o.CompanyCost, TxType.Consulting, p.Id, $"Strategy consulting: {o.FromName}", $"Unusual consulting invoice approved by {p.Name}", 2);
                    PaySecret(p, o.PersonalAmount);
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} approved a {Fmt.Money(o.CompanyCost)} invoice from {o.FromName} and received about {Fmt.Money(o.PersonalAmount)} back.", o.PersonalAmount, 0.65);
                    return CmdResult.Success($"The invoice is paid. {Fmt.Money(o.PersonalAmount)} is yours.");
                }
                case OfferKind.InfluencerSkim:
                {
                    var e = State.GetEvent(o.EventId);
                    var cp = e != null ? State.GetCompanyProduct(e.ProductId) : null;
                    if (e == null || e.Resolved || cp == null) { o.Status = OfferStatus.Expired; return CmdResult.Fail("The promotion is no longer available."); }
                    if (C.Cash < o.CompanyCost) return CmdResult.Fail("The company can't afford that price.");
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    Spend(o.CompanyCost, TxType.Marketing, p.Id, $"Influencer promotion: {cp.Name}", $"Paid {Fmt.Pct(o.CompanyCost / Math.Max(1, e.Amount) - 1)} more than the influencer's quote", 1);
                    cp.BoostMult = Math.Max(cp.BoostMult, e.Amount2);
                    cp.BoostUntil = Math.Max(cp.BoostUntil, State.Day + 7);
                    FinishEvent(e, p, 0, $"Promotion booked for {Fmt.Money(o.CompanyCost)}.", 30);
                    PaySecret(p, o.PersonalAmount);
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} booked {o.FromName} at an inflated {Fmt.Money(o.CompanyCost)} and took about {Fmt.Money(o.PersonalAmount)} back.", o.PersonalAmount, 0.6);
                    return CmdResult.Success();
                }
                case OfferKind.PoachJob:
                {
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    PaySecret(p, o.PersonalAmount);
                    foreach (var s in State.Suppliers) { s.Discount = 0; s.DiscountUntil = 0; }
                    foreach (var cp in C.Products)
                    {
                        if (!cp.Active) continue;
                        var mp = State.GetMarketProduct(cp.ProductId);
                        mp.Competition = Math.Min(0.95, mp.Competition + 0.1);
                    }
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} sold our supplier list and prices to {o.FromName} for about {Fmt.Money(o.PersonalAmount)}.", o.PersonalAmount, 0.9);
                    Log(LogKind.Market, -1, "Suppliers suddenly cancelled our negotiated discounts. A competitor seems to know our prices.");
                    return CmdResult.Success($"{Fmt.Money(o.PersonalAmount)} received. Our supplier discounts are gone.");
                }
                case OfferKind.InsiderTip:
                {
                    if (pr.Cash < o.PersonalAmount) return CmdResult.Fail("You don't have that much personal cash.");
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    bool win = Rng.Chance(0.7);
                    double delta = win ? o.PersonalAmount * 0.6 : -o.PersonalAmount * 0.5;
                    pr.Cash += delta;
                    if (win) pr.SecretIncome += delta;
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} traded on insider information.", Math.Abs(delta), 0.3);
                    return CmdResult.Success(win ? $"The tip was right: +{Fmt.Money(delta)}." : $"The deal fell through: you lost {Fmt.Money(-delta)}.");
                }
                case OfferKind.SpeakingGig:
                {
                    o.Status = OfferStatus.Accepted;
                    pr.OffersAccepted++;
                    p.AwayUntilDay = State.Day + 3;
                    p.Status = "Speaking at " + o.FromName;
                    StartTask(p.Id, TaskKind.PersonalGig, "Speaking at " + o.FromName, 3, null, (int)o.PersonalAmount);
                    Log(LogKind.Personal, p.Id, $"{p.Name} is away for 3 days on personal business.");
                    return CmdResult.Success("See you in 3 days. The fee is paid when you're back.");
                }
                case OfferKind.SideConsulting:
                    o.Status = OfferStatus.Paid;
                    pr.OffersAccepted++;
                    PaySecret(p, o.PersonalAmount);
                    CreateEvidence(p.Id, o.Kind, $"{p.Name} took a paid side job advising {o.FromName}.", o.PersonalAmount, 0.2);
                    return CmdResult.Success($"{Fmt.Money(o.PersonalAmount)} for a few hours of advice.");

                case OfferKind.VoteBuy:
                case OfferKind.VoteTrade:
                    o.Status = OfferStatus.Accepted;
                    pr.OffersAccepted++;
                    AIOnOfferAccepted(o);
                    return CmdResult.Success(o.Kind == OfferKind.VoteBuy ? "Vote yes on that proposal to get paid." : "You have a deal. Nothing enforces it, of course.");

                case OfferKind.ShareSale:
                {
                    var seller = P(o.FromId);
                    if (seller == null || seller.Shares < o.Shares) { o.Status = OfferStatus.Expired; return CmdResult.Fail("They no longer have those shares."); }
                    if (pr.Cash < o.PersonalAmount) return CmdResult.Fail("You don't have enough personal cash.");
                    o.Status = OfferStatus.Accepted;
                    TransferShares(seller, p, o.Shares, o.PersonalAmount);
                    return CmdResult.Success();
                }
                case OfferKind.Blackmail:
                {
                    if (pr.Cash < o.PersonalAmount) return CmdResult.Fail("You don't have enough personal cash to pay.");
                    o.Status = OfferStatus.Paid;
                    pr.Cash -= o.PersonalAmount;
                    PayPlayer(o.FromId, o.PersonalAmount);
                    var bp = Priv(o.FromId);
                    if (bp != null) bp.SecretIncome += o.PersonalAmount;
                    var ev = State.Evidence.Find(x => x.Id == o.EvidenceId);
                    if (ev != null && !ev.Silenced.Contains(o.FromId)) ev.Silenced.Add(o.FromId);
                    Notify(o.FromId, NoticeKind.Private, $"{p.Name} paid you {Fmt.Money(o.PersonalAmount)} for your silence.");
                    return CmdResult.Success("Paid. Hopefully that's the end of it.");
                }
            }
            return CmdResult.Fail("Unsupported offer.");
        }

        void PaySecret(Player p, double amount)
        {
            var pr = Priv(p.Id);
            pr.Cash += amount;
            pr.SecretIncome += amount;
        }

        void ReportOffer(PrivateOffer o, Player p)
        {
            switch (o.Kind)
            {
                case OfferKind.Kickback:
                {
                    var s = State.GetSupplier(o.SupplierId);
                    if (s != null)
                    {
                        s.Blacklisted = true;
                        foreach (var cp in C.Products) if (cp.SupplierId == s.Id) cp.SupplierId = PreferredSupplier(cp)?.Id;
                    }
                    Log(LogKind.Scandal, p.Id, $"{p.Name} reported {o.FromName} for offering a bribe. They are blacklisted.");
                    break;
                }
                case OfferKind.InfluencerSkim:
                    Log(LogKind.Scandal, p.Id, $"{p.Name} reported {o.FromName} for proposing a kickback on a promotion.");
                    break;
                default:
                    Log(LogKind.Scandal, p.Id, $"{p.Name} reported an attempt by {o.FromName} to buy their loyalty.");
                    break;
            }
            Award(p.Id, 40, "Reported a bribe attempt");
            p.Reports++;
            p.Reputation = Math.Min(100, p.Reputation + 5);
            foreach (var ai in State.Players) if (ai.IsAI && ai.Id != p.Id) AdjustTrust(ai, p.Id, 8);
        }

        void SettleKickback(Player p, Supplier s, CompanyProduct cp, int qty, double total)
        {
            foreach (var o in State.Offers)
            {
                if (o.Kind != OfferKind.Kickback || o.Status != OfferStatus.Accepted || o.TargetId != p.Id) continue;
                if (o.SupplierId != s.Id || o.ProductId != cp.ProductId || qty < o.Quantity) continue;
                o.Status = OfferStatus.Fulfilled;
                PaySecret(p, o.PersonalAmount);
                Notify(p.Id, NoticeKind.Private, $"{s.Name} wired {Fmt.Money(o.PersonalAmount)} to your personal account.");
                CreateEvidence(p.Id, OfferKind.Kickback,
                    $"{p.Name} received about {Fmt.Money(o.PersonalAmount)} from {s.Name} for a {Fmt.Int(qty)}-unit {cp.Name} order ({Fmt.Pct(o.PremiumPct)} above the cheapest supplier).",
                    o.PersonalAmount, 0.6);
                return;
            }
        }

        // ------------------------------------------------------------------ evidence

        public Evidence CreateEvidence(int actorId, OfferKind kind, string summary, double amount, double severity)
        {
            var e = new Evidence
            {
                Id = NewId("x"), ActorId = actorId, Kind = kind, Summary = summary, Amount = amount, Day = State.Day, Severity = severity
            };
            State.Evidence.Add(e);
            return e;
        }

        public void DiscoverEvidence(Evidence e, int playerId)
        {
            if (e.DiscoveredBy.Contains(playerId) || e.ActorId == playerId) return;
            e.DiscoveredBy.Add(playerId);
            Notify(playerId, NoticeKind.Private, $"You found something: {e.Summary}");
        }

        void IntrigueDaily()
        {
            bool audits = C.Policies.SupplierAudits;
            foreach (var e in State.Evidence)
            {
                if (e.Exposed) continue;
                int age = State.Day - e.Day;
                double ageFactor = age < 60 ? 1 : 0.4;
                double deception = Has(e.ActorId, Skills.Deception) ? 0.5 : 1;
                foreach (var p in State.Players)
                {
                    if (p.Id == e.ActorId || e.DiscoveredBy.Contains(p.Id)) continue;
                    double mult = 1 + (p.HasSkill(Skills.Investigator) ? 2 : 0) + (p.HasSkill(Skills.Accountant) ? 1 : 0);
                    if (State.Tasks.Exists(t => t.PlayerId == p.Id && t.Kind == TaskKind.Investigate)) mult *= 3;
                    double chance = 0.004 * e.Severity * mult * (audits ? 1.8 : 1) * deception * ageFactor * (C.Has("dat3") ? 2 : 1);
                    if (Rng.Chance(chance)) DiscoverEvidence(e, p.Id);
                }
            }
        }

        CmdResult CmdExposeEvidence(Command c, Player p)
        {
            var e = State.Evidence.Find(x => x.Id == c.A);
            if (e == null || !e.DiscoveredBy.Contains(p.Id)) return CmdResult.Fail("You have no such evidence.");
            if (e.Exposed) return CmdResult.Fail("Already public.");
            var actor = P(e.ActorId);
            if (actor == null) return CmdResult.Fail("Unknown partner.");
            if (actor.HasSkill(Skills.SpinDoctor) && !e.Denied && Rng.Chance(0.5))
            {
                e.Denied = true;
                e.DiscoveredBy.Remove(p.Id);
                p.Reputation = Math.Max(0, p.Reputation - 5);
                Log(LogKind.Scandal, p.Id, $"{p.Name} accused {actor.Name} of misconduct, but {actor.Name} convincingly denied everything.");
                NotifyAll(NoticeKind.Warning, $"{p.Name} accused {actor.Name}, who denied it convincingly.");
                return CmdResult.Success();
            }
            Expose(e, p);
            return CmdResult.Success();
        }

        public void Expose(Evidence e, Player exposer)
        {
            var actor = P(e.ActorId);
            e.Exposed = true;
            e.ExposedBy = exposer.Id;
            e.ExposedDay = State.Day;
            double dmg = actor.HasSkill(Skills.SpinDoctor) ? 0.5 : 1;
            actor.Reputation = Math.Max(0, actor.Reputation - 25 * dmg);
            actor.ScandalCount++;
            Penalize(actor.Id, (int)(250 * dmg), "Exposed: " + e.Summary);
            C.Reputation = Math.Max(0, C.Reputation - 4);
            Award(exposer.Id, 60, $"Exposed misconduct by {actor.Name}");
            exposer.Exposures++;
            exposer.Reputation = Math.Min(100, exposer.Reputation + 5);
            Log(LogKind.Scandal, exposer.Id, $"SCANDAL: {exposer.Name} exposed {actor.Name}. {e.Summary}");
            NotifyAll(NoticeKind.Danger, $"Scandal: {exposer.Name} exposed {actor.Name}.");
            foreach (var ai in State.Players)
            {
                if (!ai.IsAI) continue;
                if (ai.Id != actor.Id) AdjustTrust(ai, actor.Id, -30);
                if (ai.Id != exposer.Id) AdjustTrust(ai, exposer.Id, 5);
            }
            if (e.Kind == OfferKind.Kickback)
            {
                foreach (var s in State.Suppliers)
                    if (e.Summary.Contains(s.Name)) { s.Blacklisted = true; break; }
            }
            double fine = Math.Max(5000, Math.Round(e.Amount * 2 / 500) * 500);
            // Several scandals before the next meeting become one disciplinary vote with a bigger fine.
            var existing = State.Board.Agenda.Find(x => x.Type == ProposalType.Discipline && x.TargetId == actor.Id && x.Status == ProposalStatus.Pending);
            if (existing != null)
            {
                existing.Amount += fine;
                existing.Text = $"Discipline {actor.Name}: {Fmt.Money(existing.Amount)} fine to the company, salary cut 30%, spending authority revoked";
                return;
            }
            var prop = new Proposal
            {
                Id = NewId("pr"), Type = ProposalType.Discipline, ProposerId = exposer.Id, TargetId = actor.Id, Amount = fine,
                Text = $"Discipline {actor.Name}: {Fmt.Money(fine)} fine to the company, salary cut 30%, spending authority revoked",
                CreatedDay = State.Day, Status = ProposalStatus.Pending
            };
            State.Board.Agenda.Add(prop);
        }

        CmdResult CmdBlackmailEvidence(Command c, Player p)
        {
            var e = State.Evidence.Find(x => x.Id == c.A);
            if (e == null || !e.DiscoveredBy.Contains(p.Id)) return CmdResult.Fail("You have no such evidence.");
            if (e.Exposed) return CmdResult.Fail("It's already public.");
            if (State.Offers.Exists(o => o.Kind == OfferKind.Blackmail && o.EvidenceId == e.Id && o.FromId == p.Id && o.Status == OfferStatus.Pending))
                return CmdResult.Fail("You already made a demand.");
            double amount = Math.Round(Clamp(c.X, 1000, 10000000));
            SendBlackmail(p, e, amount);
            return CmdResult.Success("Your message has been delivered.");
        }

        public void SendBlackmail(Player from, Evidence e, double amount)
        {
            var target = P(e.ActorId);
            var o = new PrivateOffer
            {
                Id = NewId("o"), Kind = OfferKind.Blackmail, TargetId = target.Id, FromId = from.Id, FromName = from.Name,
                Title = $"{from.Name} knows your secret", Text = $"{from.Name} writes privately: \"I know about this: {e.Summary} Pay me {Fmt.Money(amount)} and it stays between us.\"",
                PersonalAmount = amount, EvidenceId = e.Id, CreatedDay = State.Day, ExpiresDay = State.Day + 4, Reportable = false,
                Status = OfferStatus.Pending
            };
            State.Offers.Add(o);
            Notify(target.Id, NoticeKind.Danger, $"Private: {from.Name} knows about one of your deals.");
        }
    }
}
