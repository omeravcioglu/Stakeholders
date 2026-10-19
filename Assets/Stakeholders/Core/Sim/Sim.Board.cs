using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>Board meetings, proposals, weighted voting, spending approvals and share trades.</summary>
    public partial class GameSimulation
    {
        double _meetingRevisionTimer;

        // ------------------------------------------------------------------ meetings

        public void StartBoardMeeting(bool extraordinary = false)
        {
            var b = State.Board;
            State.Phase = GamePhase.BoardMeeting;
            b.MeetingNumber++;
            b.MeetingElapsed = 0;
            b.MeetingTimeLeft = State.Settings.MeetingSeconds;
            b.Extraordinary = extraordinary;
            foreach (var p in State.Players) p.Ready = false;

            if (!extraordinary)
            {
                int d = State.Day;
                var report = new QuarterReport
                {
                    Year = (d - 1) / GameCalendar.DaysPerYear + 1,
                    Quarter = ((d - 1) / GameCalendar.DaysPerQuarter) % 4 + 1,
                    Revenue = C.Quarter.Revenue,
                    Profit = C.Quarter.NetProfit,
                    CashEnd = C.Cash,
                    ValuationStart = b.LastValuation > 0 ? b.LastValuation : State.Settings.StartingCash,
                    ValuationEnd = C.Valuation
                };
                // Performance equity: contribution slowly turns into ownership.
                double pool = State.Settings.PerformanceEquityPct / 100.0 * C.TotalShares;
                double totalXp = 0;
                foreach (var p in State.Players) totalXp += Math.Max(0, p.QuarterXp);
                foreach (var p in State.Players)
                {
                    long grant = pool > 0 && totalXp > 0 ? (long)(pool * Math.Max(0, p.QuarterXp) / totalXp) : 0;
                    if (grant > 0)
                    {
                        p.Shares += grant;
                        C.TotalShares += grant;
                    }
                    report.Players.Add(new PlayerQuarter
                    {
                        PlayerId = p.Id, ProfitGenerated = p.QuarterProfit, Xp = p.QuarterXp, Events = p.QuarterEvents, Performance = p.Performance,
                        SharesGranted = grant
                    });
                }
                b.LastReport = report;
                C.LastQuarter = C.Quarter.Copy();
                C.Quarter = new Financials();
                foreach (var cp in C.Products) cp.ProfitQuarter = 0;
                foreach (var p in State.Players) { p.QuarterProfit = 0; p.QuarterXp = 0; p.QuarterEvents = 0; }
                b.NextMeetingDay += GameCalendar.DaysPerQuarter;
                b.LastValuation = C.Valuation;
                Log(LogKind.Board, -1, $"Board meeting #{b.MeetingNumber}: Q{report.Quarter} Y{report.Year} results. Revenue {Fmt.Money(report.Revenue)}, profit {Fmt.Money(report.Profit)}.");
            }
            else Log(LogKind.Board, -1, "An emergency board meeting has been called.");

            foreach (var prop in b.Agenda) prop.Age = 0;
            AIBoardProposals();
            NotifyAll(NoticeKind.Warning, extraordinary ? "Emergency board meeting!" : $"Board meeting #{b.MeetingNumber} is in session. The clock is paused.");
            State.Revision++;
        }

        void TickMeeting(double dt)
        {
            var b = State.Board;
            b.MeetingElapsed += dt;
            foreach (var prop in b.Agenda) if (prop.Status == ProposalStatus.Pending) prop.Age += dt;
            TickApprovalsRealtime(dt);

            // AI partners vote after a short, personality-dependent delay.
            foreach (var ai in State.Players)
            {
                if (!ai.IsAI) continue;
                foreach (var prop in b.Agenda.ToArray())
                {
                    if (prop.Status != ProposalStatus.Pending || HasVoted(prop, ai.Id) || !CanVote(prop, ai)) continue;
                    if (prop.Age >= AIVoteDelay(ai, prop)) CastVoteInternal(prop, ai, AIDecideVote(ai, prop));
                }
            }
            foreach (var prop in b.Agenda.ToArray())
                if (prop.Status == ProposalStatus.Pending && EveryoneVoted(prop)) ResolveProposal(prop);

            bool humansReady = true;
            int humans = 0;
            foreach (var p in State.Players)
            {
                if (p.IsAI || !p.Connected) continue;
                humans++;
                if (!p.Ready) humansReady = false;
            }
            // With no connected humans (all-AI test games, or everyone disconnected) the meeting runs briefly on its own.
            if (humans == 0) humansReady = b.MeetingElapsed > 3;
            if (humansReady) { EndMeeting(); return; }
            if (b.MeetingTimeLeft > 0)
            {
                b.MeetingTimeLeft -= dt;
                if (b.MeetingTimeLeft <= 0) { EndMeeting(); return; }
            }
            _meetingRevisionTimer += dt;
            if (_meetingRevisionTimer >= 0.5) { _meetingRevisionTimer = 0; State.Revision++; }
        }

        public void EndMeeting()
        {
            var b = State.Board;
            foreach (var prop in b.Agenda.ToArray())
            {
                if (prop.Status != ProposalStatus.Pending) continue;
                foreach (var ai in State.Players)
                    if (ai.IsAI && !HasVoted(prop, ai.Id) && CanVote(prop, ai)) CastVoteInternal(prop, ai, AIDecideVote(ai, prop));
                if (State.Phase == GamePhase.GameOver) break;
                ResolveProposal(prop);
            }
            b.Agenda.RemoveAll(x => x.Status != ProposalStatus.Pending);
            if (State.Phase == GamePhase.BoardMeeting) State.Phase = GamePhase.Running;
            foreach (var p in State.Players) p.Ready = false;
            b.Extraordinary = false;
            Log(LogKind.Board, -1, "The board meeting is adjourned.");
            State.Revision++;
        }

        public void CallEmergencyMeeting(Player p, ProposalType type, double amount, string text)
        {
            var prop = new Proposal
            {
                Id = NewId("pr"), Type = type, ProposerId = p.Id, Amount = amount, Text = text, CreatedDay = State.Day,
                Status = ProposalStatus.Pending
            };
            State.Board.Agenda.Add(prop);
            if (State.Phase == GamePhase.Running) StartBoardMeeting(true);
        }

        // ------------------------------------------------------------------ proposals

        public static string ProposalTitle(ProposalType t)
        {
            switch (t)
            {
                case ProposalType.SalaryChange: return "Salary change";
                case ProposalType.Bonus: return "Bonus";
                case ProposalType.Dividend: return "Dividend";
                case ProposalType.Loan: return "Take a loan";
                case ProposalType.Warehouse: return "New warehouse";
                case ProposalType.Investment: return "Company investment";
                case ProposalType.RaiseCapital: return "Issue new shares";
                case ProposalType.Policy: return "Company policy";
                case ProposalType.ExecutiveLimit: return "Spending authority";
                case ProposalType.Discipline: return "Discipline a partner";
                case ProposalType.TransferProduct: return "Transfer product management";
                case ProposalType.SellCompany: return "Sell the company";
                case ProposalType.Positioning: return "Company strategy";
                case ProposalType.AcquireRival: return "Acquire a rival";
                case ProposalType.GoPublic: return "Take the company public (IPO)";
            }
            return t.ToString();
        }

        CmdResult CmdCreateProposal(Command c, Player p)
        {
            var type = (ProposalType)c.I;
            if (type == ProposalType.SellCompany) return CmdResult.Fail("Only a real acquisition offer can put the company up for sale.");
            int pending = 0;
            foreach (var x in State.Board.Agenda) if (x.ProposerId == p.Id && x.Status == ProposalStatus.Pending) pending++;
            if (pending >= 3) return CmdResult.Fail("You already have 3 proposals on the agenda.");
            var target = c.J >= 0 ? P(c.J) : null;
            var prop = new Proposal
            {
                Id = NewId("pr"), Type = type, ProposerId = p.Id, TargetId = c.J, Amount = Math.Round(c.X), OptionId = c.A,
                CreatedDay = State.Day, Status = ProposalStatus.Pending
            };
            switch (type)
            {
                case ProposalType.SalaryChange:
                    if (target == null) return CmdResult.Fail("Choose a partner.");
                    if (prop.Amount < 0 || prop.Amount > 50000) return CmdResult.Fail("Salary must be between $0 and $50K per month.");
                    prop.CurrentValue = target.Salary;
                    prop.ContextProfit = State.Board.LastReport?.Players.Find(x => x.PlayerId == target.Id)?.ProfitGenerated ?? target.QuarterProfit;
                    prop.ContextPerformance = target.Performance;
                    prop.Text = $"Change {target.Name}'s salary: {Fmt.Money(target.Salary)} -> {Fmt.Money(prop.Amount)}/month";
                    break;
                case ProposalType.Bonus:
                    if (target == null) return CmdResult.Fail("Choose a partner.");
                    if (prop.Amount < 500) return CmdResult.Fail("Bonus must be at least $500.");
                    prop.ContextProfit = target.QuarterProfit;
                    prop.ContextPerformance = target.Performance;
                    prop.Text = $"Pay {target.Name} a {Fmt.Money(prop.Amount)} bonus";
                    break;
                case ProposalType.Dividend:
                    if (prop.Amount < 1000) return CmdResult.Fail("Dividend must be at least $1,000.");
                    prop.Text = $"Pay {Fmt.Money(prop.Amount)} in dividends to shareholders";
                    break;
                case ProposalType.Loan:
                {
                    bool ok = false;
                    foreach (var l in Catalog.Loans) if (l.Id == c.A) ok = true;
                    if (!ok) return CmdResult.Fail("Choose a loan.");
                    double amount = LoanOfferAmount(c.A, out string why);
                    if (amount <= 0) return CmdResult.Fail(why);
                    prop.Amount = amount;
                    foreach (var l in Catalog.Loans) if (l.Id == c.A) prop.Text = $"Take a {Fmt.Money(amount)} {l.Lender} ({Fmt.Pct(l.Rate)}, {l.Months} months)";
                    break;
                }
                case ProposalType.Warehouse:
                {
                    var def = Catalog.GetWarehouse(c.A);
                    if (def == null || def.Id == "garage") return CmdResult.Fail("Choose a warehouse type.");
                    prop.Amount = def.SetupCost;
                    prop.Text = $"Open a {def.Name} ({Fmt.Money(def.SetupCost)} + {Fmt.Money(def.MonthlyCost)}/month)";
                    break;
                }
                case ProposalType.Investment:
                {
                    var u = Catalog.GetUpgrade(c.A);
                    if (u == null || C.Has(u.Id) || IsBuilding(u.Id)) return CmdResult.Fail("Choose an upgrade we don't have yet.");
                    if (!UpgradeUnlocked(u, p, out string why)) return CmdResult.Fail(why);
                    prop.Amount = u.Cost;
                    prop.Text = $"Build {u.Name} for {Fmt.Money(u.Cost)}";
                    break;
                }
                case ProposalType.Positioning:
                {
                    var pos = Positionings.Get(c.A);
                    if (pos == null) return CmdResult.Fail("Choose a strategy.");
                    if (C.Stage < 1) return CmdResult.Fail($"A company strategy unlocks at the {Stages.Get(1).Name} stage.");
                    if (C.Positioning == pos.Id) return CmdResult.Fail("That is already our strategy.");
                    prop.Amount = string.IsNullOrEmpty(C.Positioning) ? 0 : 50000;
                    prop.Text = $"Adopt the {pos.Name} strategy" + (prop.Amount > 0 ? $" (rebranding costs {Fmt.Money(prop.Amount)})" : "");
                    break;
                }
                case ProposalType.AcquireRival:
                {
                    var r = State.Rivals.Find(x => x.Id == c.A);
                    if (!CanAcquire(r, out string why)) return CmdResult.Fail(why);
                    prop.Amount = AcquisitionPrice(r);
                    prop.Text = $"Acquire {r.Name} for {Fmt.Money(prop.Amount)}";
                    break;
                }
                case ProposalType.GoPublic:
                    if (C.Stage < Stages.Max) return CmdResult.Fail($"The company must reach the {Stages.Get(Stages.Max).Name} stage first.");
                    if (C.IpoPending || C.WentPublic) return CmdResult.Fail("The IPO is already under way.");
                    prop.Text = "Take the company public: IPO in 30 days at a 50% valuation premium (ends the game)";
                    break;
                case ProposalType.RaiseCapital:
                    if (prop.Amount < 10000 || prop.Amount > C.Valuation * 0.5) return CmdResult.Fail($"Raise between $10K and {Fmt.Money(C.Valuation * 0.5)}.");
                    prop.Text = target != null
                        ? $"Sell new shares worth {Fmt.Money(prop.Amount)} to {target.Name} at the current valuation"
                        : $"Raise {Fmt.Money(prop.Amount)} from outside investors (15% discount, dilutes everyone)";
                    break;
                case ProposalType.Policy:
                    prop.Text = PolicyProposalText(c.A, (int)c.X);
                    if (prop.Text == null) return CmdResult.Fail("Unknown policy.");
                    break;
                case ProposalType.ExecutiveLimit:
                    if (target == null) return CmdResult.Fail("Choose a partner.");
                    if (prop.Amount < 0 || prop.Amount > 2000000) return CmdResult.Fail("Invalid limit.");
                    prop.CurrentValue = target.SpendingLimit;
                    prop.Text = $"Set {target.Name}'s spending authority: {Fmt.Money(target.SpendingLimit)} -> {Fmt.Money(prop.Amount)}";
                    break;
                case ProposalType.Discipline:
                    if (target == null || target.Id == p.Id) return CmdResult.Fail("Choose another partner.");
                    if (State.Board.Agenda.Exists(x => x.Type == ProposalType.Discipline && x.TargetId == target.Id && x.Status == ProposalStatus.Pending))
                        return CmdResult.Fail($"A disciplinary vote on {target.Name} is already on the agenda.");
                    if (prop.Amount < 0) prop.Amount = 0;
                    prop.Text = $"Discipline {target.Name}: {Fmt.Money(prop.Amount)} fine, salary cut 30%, spending authority revoked";
                    break;
                case ProposalType.TransferProduct:
                {
                    var cp = State.GetCompanyProduct(c.A);
                    if (cp == null || !cp.Active || target == null) return CmdResult.Fail("Choose a product and a partner.");
                    if (cp.ManagerId == target.Id) return CmdResult.Fail("They already manage it.");
                    prop.Text = $"Make {target.Name} the manager of {cp.Name} (currently {State.PlayerName(cp.ManagerId)})";
                    break;
                }
                default:
                    return CmdResult.Fail("Unknown proposal.");
            }
            if (!string.IsNullOrWhiteSpace(c.Text)) prop.Text += $" - \"{(c.Text.Length > 140 ? c.Text.Substring(0, 140) : c.Text)}\"";
            State.Board.Agenda.Add(prop);
            CastVoteInternal(prop, p, VoteChoice.Yes);
            Log(LogKind.Board, p.Id, $"{p.Name} proposed: {prop.Text}.");
            foreach (var other in State.Players) if (other.Id != p.Id) Notify(other.Id, NoticeKind.Info, $"New proposal from {p.Name}: {prop.Text}");
            AIOnProposalCreated(prop);
            return CmdResult.Success(State.Phase == GamePhase.BoardMeeting ? "Proposal is on the table." : "Proposal added to the agenda for the next board meeting.");
        }

        string PolicyProposalText(string policy, int value)
        {
            switch (policy)
            {
                case "returns": return "Return policy: " + (value == 0 ? "strict (fewer refunds, lower satisfaction)" : value == 2 ? "generous (happier customers, more refunds)" : "standard");
                case "audits": return value != 0 ? "Start supplier audits (catch kickbacks more often)" : "Stop supplier audits";
                case "openbooks": return value != 0 ? "Open books: every partner sees ledger anomaly flags" : "Close the books: only accountants see anomaly flags";
                case "ballot": return value != 0 ? "Secret ballot: votes stay hidden until the result" : "Open ballot: everyone sees how everyone votes";
                case "limit": return $"Default spending authority for all partners: {Fmt.Money(Clamp(value, 5000, 1000000))}";
            }
            return null;
        }

        bool HasVoted(Proposal prop, int playerId)
        {
            foreach (var v in prop.Votes) if (v.PlayerId == playerId && v.Choice != VoteChoice.None) return true;
            return false;
        }

        public bool CanVote(Proposal prop, Player p)
        {
            if (prop.Type == ProposalType.Discipline && prop.TargetId == p.Id) return false;
            return p.Shares > 0;
        }

        bool EveryoneVoted(Proposal prop)
        {
            foreach (var p in State.Players)
                if (CanVote(prop, p) && !HasVoted(prop, p.Id)) return false;
            return true;
        }

        CmdResult CmdCastVote(Command c, Player p)
        {
            var prop = State.Board.Agenda.Find(x => x.Id == c.A);
            if (prop == null || prop.Status != ProposalStatus.Pending) return CmdResult.Fail("That proposal is closed.");
            if (!CanVote(prop, p)) return CmdResult.Fail(prop.Type == ProposalType.Discipline ? "You can't vote on your own discipline." : "Only shareholders can vote.");
            var choice = (VoteChoice)Clamp(c.I, 1, 3);
            CastVoteInternal(prop, p, choice);
            if (State.Phase == GamePhase.BoardMeeting && EveryoneVoted(prop)) ResolveProposal(prop);
            return CmdResult.Success();
        }

        void CastVoteInternal(Proposal prop, Player p, VoteChoice choice)
        {
            prop.Votes.RemoveAll(v => v.PlayerId == p.Id);
            prop.Votes.Add(new VoteRecord { PlayerId = p.Id, Choice = choice });
            AIOnVoteCast(prop, p, choice);
        }

        CmdResult CmdWithdrawProposal(Command c, Player p)
        {
            var prop = State.Board.Agenda.Find(x => x.Id == c.A);
            if (prop == null || prop.Status != ProposalStatus.Pending) return CmdResult.Fail("That proposal is closed.");
            if (prop.ProposerId != p.Id) return CmdResult.Fail("Only the proposer can withdraw it.");
            prop.Status = ProposalStatus.Withdrawn;
            prop.Result = "Withdrawn";
            prop.ResolvedDay = State.Day;
            State.Board.Agenda.Remove(prop);
            AddToHistory(prop);
            Log(LogKind.Board, p.Id, $"{p.Name} withdrew: {prop.Text}.");
            return CmdResult.Success();
        }

        void AddToHistory(Proposal prop)
        {
            State.Board.History.Add(prop);
            if (State.Board.History.Count > 40) State.Board.History.RemoveAt(0);
        }

        /// <summary>Selling the whole company needs a supermajority; everything else a simple majority.</summary>
        public static double RequiredMajority(Proposal prop) =>
            prop.Type == ProposalType.SellCompany ? 0.749 : prop.Type == ProposalType.GoPublic ? 0.66 : 0.5;

        /// <summary>Weighted tally. Returns (yes, no) as fractions of the eligible voting weight.</summary>
        public void Tally(Proposal prop, out double yes, out double no)
        {
            yes = 0; no = 0;
            double eligible = 0;
            foreach (var p in State.Players) if (CanVote(prop, p)) eligible += p.Shares;
            if (eligible <= 0) return;
            foreach (var v in prop.Votes)
            {
                var p = P(v.PlayerId);
                if (p == null || !CanVote(prop, p)) continue;
                double w = p.Shares / eligible;
                if (v.Choice == VoteChoice.Yes)
                {
                    if (p.Id == prop.ProposerId && p.HasSkill(Skills.BoardInfluence)) w += 0.1;
                    yes += w;
                }
                else if (v.Choice == VoteChoice.No) no += w;
            }
        }

        void ResolveProposal(Proposal prop)
        {
            if (prop.Status != ProposalStatus.Pending) return;
            Tally(prop, out double yes, out double no);
            bool passed = yes > RequiredMajority(prop);
            // Record effective weights for display
            double eligible = 0;
            foreach (var p in State.Players) if (CanVote(prop, p)) eligible += p.Shares;
            foreach (var v in prop.Votes)
            {
                var p = P(v.PlayerId);
                v.Weight = p != null && eligible > 0 ? p.Shares / eligible : 0;
            }
            string effect = passed ? ApplyProposal(prop) : null;
            if (passed && effect != null && effect.StartsWith("!"))
            {
                passed = false;
                effect = effect.Substring(1);
            }
            prop.Status = passed ? ProposalStatus.Passed : ProposalStatus.Failed;
            prop.ResolvedDay = State.Day;
            prop.Result = (passed ? "Passed" : "Rejected") + $" ({Fmt.Pct(yes)} yes, {Fmt.Pct(no)} no)" + (string.IsNullOrEmpty(effect) ? "" : ". " + effect);
            State.Board.Agenda.Remove(prop);
            AddToHistory(prop);
            var proposer = P(prop.ProposerId);
            if (passed && proposer != null)
            {
                proposer.ProposalsPassed++;
                Award(proposer.Id, 15, "Proposal passed");
            }
            Log(LogKind.Board, prop.ProposerId, $"{(passed ? "PASSED" : "REJECTED")}: {prop.Text} ({Fmt.Pct(yes)} yes / {Fmt.Pct(no)} no).");
            AIOnProposalResolved(prop, passed);
        }

        /// <summary>Apply a passed proposal. A result starting with "!" means it could not be carried out.</summary>
        string ApplyProposal(Proposal prop)
        {
            var target = P(prop.TargetId);
            switch (prop.Type)
            {
                case ProposalType.SalaryChange:
                    if (target == null) return "!Partner not found";
                    target.Salary = prop.Amount;
                    return null;
                case ProposalType.Bonus:
                    if (target == null) return "!Partner not found";
                    if (C.Cash < prop.Amount) return "!Not enough cash";
                    Spend(prop.Amount, TxType.Bonus, target.Id, $"Bonus: {target.Name}");
                    PayPlayer(target.Id, prop.Amount);
                    Priv(target.Id).BonusesReceived += prop.Amount;
                    return null;
                case ProposalType.Dividend:
                {
                    double amount = Math.Min(prop.Amount, Math.Max(0, C.Cash));
                    if (amount < 1000) return "!Not enough cash";
                    Spend(amount, TxType.Dividend, -1, $"Dividend payout ({Fmt.Money(amount)})");
                    foreach (var p in State.Players)
                    {
                        double share = amount * OwnershipPct(p);
                        PayPlayer(p.Id, share);
                        Priv(p.Id).DividendsReceived += share;
                        Notify(p.Id, NoticeKind.Success, $"You received {Fmt.Money(share)} in dividends.");
                    }
                    return $"{Fmt.Money(amount)} paid out";
                }
                case ProposalType.Loan:
                {
                    foreach (var l in Catalog.Loans)
                    {
                        if (l.Id != prop.OptionId) continue;
                        if (C.Loans.Exists(x => x.Lender == l.Lender)) return "!We already have that loan";
                        double amount = LoanOfferAmount(l.Id, out string why);
                        if (amount <= 0) return "!" + why;
                        TakeLoan(l, amount, prop.ProposerId);
                        return $"{Fmt.Money(amount)} received";
                    }
                    return "!Unknown loan";
                }
                case ProposalType.Warehouse:
                {
                    var def = Catalog.GetWarehouse(prop.OptionId);
                    if (def == null) return "!Unknown warehouse";
                    if (C.Cash < def.SetupCost) return "!Not enough cash";
                    OpenWarehouse(def, prop.ProposerId);
                    return null;
                }
                case ProposalType.Investment:
                {
                    var u = Catalog.GetUpgrade(prop.OptionId);
                    if (u == null || C.Has(u.Id) || IsBuilding(u.Id)) return "!Not available";
                    if (C.Cash < u.Cost) return "!Not enough cash";
                    StartUpgradeBuild(u, prop.ProposerId);
                    return $"ready in {Fmt.Days(u.BuildDays)}";
                }
                case ProposalType.Positioning:
                {
                    var pos = Positionings.Get(prop.OptionId);
                    if (pos == null) return "!Unknown strategy";
                    if (prop.Amount > 0)
                    {
                        if (C.Cash < prop.Amount) return "!Not enough cash to rebrand";
                        Spend(prop.Amount, TxType.Marketing, prop.ProposerId, $"Rebranding: {pos.Name}");
                    }
                    C.Positioning = pos.Id;
                    C.PositioningDay = State.Day;
                    Celebrate($"New strategy: {pos.Name}|{pos.Description}");
                    return null;
                }
                case ProposalType.AcquireRival:
                {
                    var r = State.Rivals.Find(x => x.Id == prop.OptionId);
                    if (!CanAcquire(r, out string why)) return "!" + why;
                    return AcquireRival(r, prop.ProposerId);
                }
                case ProposalType.GoPublic:
                    if (C.IpoPending || C.WentPublic) return "!Already under way";
                    C.IpoPending = true;
                    C.IpoDay = State.Day + 30;
                    Celebrate($"The IPO is on!|Bankers are preparing the listing. {C.Name} goes public on {Fmt.Date(C.IpoDay)}. Make the numbers look good.");
                    return $"IPO on {Fmt.ShortDate(C.IpoDay)}";
                case ProposalType.RaiseCapital:
                {
                    double pricePerShare = C.Valuation / C.TotalShares;
                    if (target != null)
                    {
                        var pr = Priv(target.Id);
                        if (pr.Cash < prop.Amount) return $"!{target.Name} doesn't have {Fmt.Money(prop.Amount)}";
                        long newShares = (long)(prop.Amount / pricePerShare);
                        pr.Cash -= prop.Amount;
                        target.Shares += newShares;
                        C.TotalShares += newShares;
                        C.Cash += prop.Amount;
                        AddLedger(TxType.Capital, prop.Amount, target.Id, $"New shares sold to {target.Name}", null, 0);
                        return $"{target.Name} now owns {Fmt.Pct(OwnershipPct(target), 1)}";
                    }
                    else
                    {
                        long newShares = (long)(prop.Amount / (pricePerShare * 0.85));
                        C.InvestorShares += newShares;
                        C.TotalShares += newShares;
                        C.Cash += prop.Amount;
                        AddLedger(TxType.Capital, prop.Amount, -1, "New shares sold to outside investors", null, 0);
                        return $"Outside investors now own {Fmt.Pct((double)C.InvestorShares / C.TotalShares, 1)}";
                    }
                }
                case ProposalType.Policy:
                    return ApplyPolicy(prop.OptionId, (int)prop.Amount);
                case ProposalType.ExecutiveLimit:
                    if (target == null) return "!Partner not found";
                    target.SpendingLimit = prop.Amount;
                    return null;
                case ProposalType.Discipline:
                {
                    if (target == null) return "!Partner not found";
                    var pr = Priv(target.Id);
                    double fine = Math.Min(prop.Amount, Math.Max(0, pr.Cash));
                    pr.Cash -= fine;
                    if (fine > 0) { C.Cash += fine; AddLedger(TxType.Fine, fine, target.Id, $"Fine paid by {target.Name}", null, 0); }
                    target.Salary = Math.Round(target.Salary * 0.7 / 50) * 50;
                    target.SpendingLimit = 0;
                    // Nobody leaves purchasing or the upgrade budget with a partner the board just disciplined.
                    if (target.IsAI) StripMoneyRoles(target);
                    return $"{target.Name} paid {Fmt.Money(fine)}";
                }
                case ProposalType.TransferProduct:
                {
                    var cp = State.GetCompanyProduct(prop.OptionId);
                    if (cp == null || target == null) return "!Not possible";
                    cp.ManagerId = target.Id;
                    return null;
                }
                case ProposalType.SellCompany:
                {
                    double price = prop.Amount;
                    C.Sold = true;
                    C.SalePrice = price;
                    foreach (var p in State.Players)
                    {
                        double payout = price * OwnershipPct(p);
                        PayPlayer(p.Id, payout);
                        Notify(p.Id, NoticeKind.Success, $"The company was sold. Your payout: {Fmt.Money(payout)}.");
                    }
                    Log(LogKind.Company, -1, $"{C.Name} was sold for {Fmt.Money(price)}.");
                    EndGame($"The partners sold {C.Name} for {Fmt.Money(price)}. Everyone was paid by ownership.");
                    return null;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ approvals

        void ApprovalsDaily()
        {
            // AI partners always get to vote at least once a day, even at high game speed.
            foreach (var a in State.Approvals.ToArray())
            {
                if (a.Status != ProposalStatus.Pending || a.CreatedDay >= State.Day) continue;
                foreach (var ai in State.Players)
                {
                    if (a.Status != ProposalStatus.Pending) break;
                    if (!ai.IsAI || ai.Id == a.RequesterId || ai.Shares <= 0 || a.Votes.Exists(v => v.PlayerId == ai.Id)) continue;
                    RecordApprovalVote(a, ai, AIDecideApproval(ai, a));
                }
            }
            foreach (var a in State.Approvals)
            {
                if (a.Status != ProposalStatus.Pending || State.Day <= a.ExpiresDay) continue;
                FailApproval(a, "No majority in time");
            }
            State.Approvals.RemoveAll(a => a.Status != ProposalStatus.Pending && State.Day - a.CreatedDay > 30);
            foreach (var o in State.ShareOffers)
                if (o.Status == OfferStatus.Pending && State.Day > o.ExpiresDay) o.Status = OfferStatus.Expired;
            State.ShareOffers.RemoveAll(o => o.Status != OfferStatus.Pending && State.Day - o.CreatedDay > 30);
            State.Negotiations.RemoveAll(n => n.Status != NegotiationStatus.Active && State.Day - n.StartedDay > 3);
        }

        void TickApprovalsRealtime(double dt)
        {
            foreach (var a in State.Approvals.ToArray())
            {
                if (a.Status != ProposalStatus.Pending) continue;
                a.Age += dt;
                foreach (var ai in State.Players)
                {
                    if (!ai.IsAI || ai.Id == a.RequesterId || ai.Shares <= 0) continue;
                    if (a.Votes.Exists(v => v.PlayerId == ai.Id)) continue;
                    if (a.Age >= 1.5 + (ai.Id * 0.7) % 2.5) RecordApprovalVote(a, ai, AIDecideApproval(ai, a));
                }
            }
        }

        CmdResult CmdVoteApproval(Command c, Player p)
        {
            var a = State.Approvals.Find(x => x.Id == c.A);
            if (a == null || a.Status != ProposalStatus.Pending) return CmdResult.Fail("That request is closed.");
            if (p.Shares <= 0) return CmdResult.Fail("Only shareholders can vote.");
            RecordApprovalVote(a, p, (VoteChoice)Clamp(c.I, 1, 3));
            return CmdResult.Success();
        }

        void RecordApprovalVote(ApprovalRequest a, Player p, VoteChoice choice)
        {
            a.Votes.RemoveAll(v => v.PlayerId == p.Id);
            a.Votes.Add(new VoteRecord { PlayerId = p.Id, Choice = choice, Weight = VotingWeight(p) });
            double yes = 0, no = 0;
            foreach (var v in a.Votes)
            {
                var voter = P(v.PlayerId);
                if (voter == null) continue;
                if (v.Choice == VoteChoice.Yes) yes += VotingWeight(voter);
                else if (v.Choice == VoteChoice.No) no += VotingWeight(voter);
            }
            if (yes > 0.5) PassApproval(a);
            else if (no >= 0.5) FailApproval(a, "Rejected by the partners");
            else
            {
                bool allVoted = true;
                foreach (var pl in State.Players) if (pl.Shares > 0 && !a.Votes.Exists(v => v.PlayerId == pl.Id)) allVoted = false;
                if (allVoted) FailApproval(a, "No majority");
            }
            State.Revision++;
        }

        void PassApproval(ApprovalRequest a)
        {
            a.Status = ProposalStatus.Passed;
            var cmd = a.Command.Clone();
            cmd.Approved = true;
            var r = Execute(cmd);
            a.Result = r.Ok ? "Approved" : "Approved, but failed: " + r.Message;
            Log(LogKind.Board, a.RequesterId, $"Partners approved {State.PlayerName(a.RequesterId)}'s request: {a.Summary}.");
        }

        void FailApproval(ApprovalRequest a, string why)
        {
            a.Status = ProposalStatus.Failed;
            a.Result = why;
            if (a.Command.Type == CommandType.ResolveEvent)
            {
                var e = State.GetEvent(a.Command.A);
                if (e != null) e.PendingApproval = false;
            }
            Log(LogKind.Board, a.RequesterId, $"Request denied ({why}): {a.Summary}.");
            Notify(a.RequesterId, NoticeKind.Warning, $"Your request was denied: {a.Summary}.");
        }

        // ------------------------------------------------------------------ shares

        CmdResult CmdCreateShareOffer(Command c, Player p)
        {
            var other = P(c.I);
            if (other == null || other.Id == p.Id) return CmdResult.Fail("Choose another partner.");
            long shares = (long)Math.Round(Clamp(c.X, 0.001, 1) * C.TotalShares);
            double price = Math.Round(c.Y);
            if (price < 0) return CmdResult.Fail("Invalid price.");
            bool fromBuys = c.Flag;
            var seller = fromBuys ? other : p;
            var buyer = fromBuys ? p : other;
            if (seller.Shares < shares) return CmdResult.Fail($"{(seller == p ? "You don't" : seller.Name + " doesn't")} own that many shares.");
            if (fromBuys && Priv(p.Id).Cash < price) return CmdResult.Fail("You don't have that much personal cash.");
            int pending = State.ShareOffers.FindAll(o => o.FromId == p.Id && o.ToId == other.Id && o.Status == OfferStatus.Pending).Count;
            if (pending >= 2) return CmdResult.Fail("You already have offers waiting with that partner.");
            var offer = new ShareOffer
            {
                Id = NewId("so"), FromId = p.Id, ToId = other.Id, FromBuys = fromBuys, Shares = shares, Price = price,
                Note = string.IsNullOrWhiteSpace(c.Text) ? null : (c.Text.Length > 160 ? c.Text.Substring(0, 160) : c.Text),
                CreatedDay = State.Day, ExpiresDay = State.Day + 5, Status = OfferStatus.Pending
            };
            State.ShareOffers.Add(offer);
            string pct = Fmt.Shares(shares, C.TotalShares);
            Notify(other.Id, NoticeKind.Private, fromBuys
                ? $"{p.Name} offers {Fmt.Money(price)} for {pct} of your shares."
                : $"{p.Name} offers to sell you {pct} of the company for {Fmt.Money(price)}.");
            return CmdResult.Success("Offer sent.");
        }

        CmdResult CmdRespondShareOffer(Command c, Player p)
        {
            var o = State.ShareOffers.Find(x => x.Id == c.A);
            if (o == null || o.ToId != p.Id || o.Status != OfferStatus.Pending) return CmdResult.Fail("That offer is closed.");
            if (!c.Flag)
            {
                o.Status = OfferStatus.Rejected;
                Notify(o.FromId, NoticeKind.Info, $"{p.Name} declined your share offer.");
                return CmdResult.Success("Declined.");
            }
            return AcceptShareOffer(o);
        }

        public CmdResult AcceptShareOffer(ShareOffer o)
        {
            var from = P(o.FromId);
            var to = P(o.ToId);
            var seller = o.FromBuys ? to : from;
            var buyer = o.FromBuys ? from : to;
            if (seller.Shares < o.Shares) { o.Status = OfferStatus.Expired; return CmdResult.Fail($"{seller.Name} no longer owns those shares."); }
            if (Priv(buyer.Id).Cash < o.Price) { o.Status = OfferStatus.Expired; return CmdResult.Fail($"{buyer.Name} can't afford it anymore."); }
            o.Status = OfferStatus.Accepted;
            TransferShares(seller, buyer, o.Shares, o.Price);
            return CmdResult.Success();
        }

        public void TransferShares(Player seller, Player buyer, long shares, double price)
        {
            seller.Shares -= shares;
            buyer.Shares += shares;
            Priv(buyer.Id).Cash -= price;
            var sp = Priv(seller.Id);
            sp.Cash += price;
            sp.ShareTradeIncome += price;
            string pct = Fmt.Shares(shares, C.TotalShares);
            Log(LogKind.Board, buyer.Id, $"{buyer.Name} bought {pct} of {C.Name} from {seller.Name} for {Fmt.Money(price)}.");
            Notify(seller.Id, NoticeKind.Success, $"You sold {pct} to {buyer.Name} for {Fmt.Money(price)}.");
            Notify(buyer.Id, NoticeKind.Success, $"You bought {pct} from {seller.Name} for {Fmt.Money(price)}.");
        }
    }
}
