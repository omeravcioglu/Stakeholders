using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Stakeholders.Core;

namespace Stakeholders.Harness
{
    static class Program
    {
        static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "balance";
            try
            {
                switch (mode)
                {
                    case "balance": return Balance(args.Length > 1 ? int.Parse(args[1]) : 3, args.Length > 2 ? int.Parse(args[2]) : 4);
                    case "passive": return Passive(args.Length > 1 ? int.Parse(args[1]) : 3);
                    case "fuzz": return Fuzz(args.Length > 1 ? int.Parse(args[1]) : 5);
                    case "serialize": return SerializeTest();
                    case "trace": return Trace(int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]));
                    case "all":
                        int r = Balance(3, 4);
                        r |= Passive(2);
                        r |= Fuzz(4);
                        r |= SerializeTest();
                        return r;
                }
                Console.WriteLine("modes: balance [seeds] [ai], passive [seeds], fuzz [seeds], serialize, all");
                return 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FATAL: " + ex);
                return 2;
            }
        }

        // -------------------------------------------------------------- helpers

        static GameSettings AllAi(int count, int seed)
        {
            var st = new GameSettings { Seed = seed, GameYears = 3 };
            var pers = new[] { Personality.Greedy, Personality.Loyal, Personality.RiskTaker, Personality.Workaholic, Personality.Manipulative, Personality.Conservative, Personality.Ambitious, Personality.Lazy };
            for (int i = 0; i < count; i++)
                st.Seats.Add(new SeatConfig { Name = Catalog.AINames[i], IsAI = true, Personality = pers[(i + seed) % pers.Length] });
            return st;
        }

        /// <summary>Run until game over, finishing board meetings (humans auto-adjourn after a few seconds).</summary>
        static void RunGame(GameSimulation sim, Action<GameSimulation> onQuarter = null, Action<GameSimulation> eachDay = null, int maxDays = 5000)
        {
            var s = sim.State;
            int lastMeeting = 0;
            int guard = 0;
            while (s.Phase != GamePhase.GameOver && guard++ < maxDays * 20)
            {
                if (s.Phase == GamePhase.BoardMeeting)
                {
                    if (s.Board.MeetingNumber != lastMeeting)
                    {
                        lastMeeting = s.Board.MeetingNumber;
                        onQuarter?.Invoke(sim);
                    }
                    sim.Advance(1.0);
                    if (s.Board.MeetingElapsed > 7)
                        foreach (var p in s.Players)
                            if (!p.IsAI && !p.Ready) sim.Execute(new Command { Type = CommandType.SetReady, PlayerId = p.Id, Flag = true });
                    continue;
                }
                sim.StepDay();
                eachDay?.Invoke(sim);
                if (s.Day > maxDays) break;
            }
        }

        static void PrintQuarter(GameSimulation sim)
        {
            var s = sim.State;
            var c = s.Company;
            var r = s.Board.LastReport;
            int active = c.Products.Count(p => p.Active);
            Console.WriteLine($"  Day {s.Day,4} Q{r?.Quarter}Y{r?.Year}: cash {Fmt.Money(c.Cash),9} rev(q) {Fmt.Money(r?.Revenue ?? 0),9} profit(q) {Fmt.Money(r?.Profit ?? 0),9} val {Fmt.Money(c.Valuation),9} " +
                              $"prod {active,2} emp {c.Employees.Count,2} wh {c.Warehouses.Count} debt {Fmt.Money(c.Debt),7} sat {c.Satisfaction,4:0} rep {c.Reputation,4:0} ev {s.Events.Count} agenda {s.Board.Agenda.Count}");
            var rivals = string.Join(" ", s.Rivals.Where(x => !x.Acquired).OrderByDescending(x => x.Valuation).Select(x => $"{x.Name}:{Fmt.Money(x.Valuation)}"));
            int vers = c.Products.Count(p => p.Active && p.Version > 1);
            Console.WriteLine($"         stage {c.Stage} ({Stages.Get(c.Stage).Name}) rank #{sim.CompanyRank()}/{sim.CompanyCount()} upgrades {c.Upgrades.Count} [{string.Join(",", c.Upgrades)}] building {c.Building.Count} strategy {(string.IsNullOrEmpty(c.Positioning) ? "-" : c.Positioning)} " +
                              $"v2+ {vers} contracts {c.Contracts.Count(k => k.Status == 1)}ok/{c.Contracts.Count(k => k.Status == 2)}miss acquired {c.AcquiredCategories.Count} | {rivals}");
            if (Verbose)
            {
                var q = c.LastQuarter;
                double inv = 0;
                foreach (var tx in c.Ledger) if (tx.Type == TxType.Inventory && tx.Day > s.Day - 90) inv += -tx.Amount;
                Console.WriteLine($"         cogs {Fmt.Money(q.Cogs)} fees {Fmt.Money(q.Fees)} refunds {Fmt.Money(q.Refunds)} mkt {Fmt.Money(q.Marketing)} payroll {Fmt.Money(q.Payroll)} partners {Fmt.Money(q.PartnerSalaries)} rent {Fmt.Money(q.Rent)} int {Fmt.Money(q.Interest)} other {Fmt.Money(q.Other)} bonus {Fmt.Money(q.Bonuses)} div {Fmt.Money(q.Dividends)} | inventory bought ~{Fmt.Money(inv)} stock value {Fmt.Money(sim.InventoryValue())}");
                var byType = c.Ledger.Where(tx => tx.Day > s.Day - 90 && tx.Amount < 0 && tx.Type != TxType.Revenue)
                    .GroupBy(tx => tx.Type).Select(g => $"{g.Key}:{Fmt.Money(-g.Sum(x => x.Amount))}");
                Console.WriteLine($"         outflows [{string.Join(" ", byType)}]");
                var roles = c.Employees.GroupBy(e => e.Role).Select(g => $"{g.Key}:{g.Count()}");
                Console.WriteLine($"         staff [{string.Join(" ", roles)}] cap {c.CapacityPerDay:0}/d support {c.SupportPerDay:0}/d orders {c.OrdersToday:0}/d backlog {c.Backlog:0} storage {c.StorageUsed:0}/{c.StorageCapacity:0}");
                foreach (var cp in c.Products.Where(x => x.Active))
                    Console.WriteLine($"         - {cp.Name,-26} price {cp.Price,6:0.00} cost {cp.AvgCost,6:0.00} stock {cp.Stock,6} sales7 {cp.Sales7,6:0.0} demand7 {cp.Demand7,6:0.0} profit {Fmt.Money(cp.Profit),8} rating {cp.Rating:0.0} q {cp.Quality:0.00}");
                foreach (var cam in c.Campaigns.Where(x => x.Active))
                    Console.WriteLine($"         * campaign {cam.Channel} {Fmt.Money(cam.DailyBudget)}/d roas7 {cam.Roas7:0.00} total roas {cam.Roas:0.00}");
            }
        }

        static bool Verbose = Environment.GetEnvironmentVariable("SIM_VERBOSE") == "1";

        static void PrintSummary(GameSimulation sim)
        {
            var s = sim.State;
            Console.WriteLine($"  RESULT: {s.Summary?.Reason}");
            if (s.Summary != null)
                foreach (var st in s.Summary.Standings)
                {
                    var p = s.GetPlayer(st.PlayerId);
                    Console.WriteLine($"    #{st.Rank} {st.Name,-14} {p.Personality,-12} net {Fmt.Money(st.NetWorth),9} (cash {Fmt.Money(st.PersonalCash),8}, inv {Fmt.Money(st.Investments),8}, equity {Fmt.Money(st.EquityValue),9}, own {Fmt.Pct(st.OwnershipPct, 1)}) perf {st.Performance,5} lvl {st.Level,2} salary {Fmt.Money(p.Salary)} secret {Fmt.Money(st.SecretIncome)} skills {p.Skills.Count} ambition {st.Ambition}{(st.AmbitionDone ? " DONE" : "")} roles [{string.Join(",", p.Roles)}]");
                }
            if (s.Summary != null)
                Console.WriteLine($"    company: stage {s.Summary.Stage}, rank #{s.Summary.CompanyRank} of {s.Summary.CompanyCount}, IPO {s.Summary.Ipo}, final valuation {Fmt.Money(s.Summary.FinalValuation)}");
            int offers = s.Offers.Count(o => o.Status == OfferStatus.Accepted || o.Status == OfferStatus.Paid || o.Status == OfferStatus.Fulfilled);
            Console.WriteLine($"    offers accepted {offers}, evidence {s.Evidence.Count} (exposed {s.Evidence.Count(e => e.Exposed)}), proposals decided {s.Board.History.Count}, resolved events {s.ResolvedEvents.Count}, log {s.Log.Count}");
        }

        // -------------------------------------------------------------- modes

        static int Balance(int seeds, int ai)
        {
            Console.WriteLine($"=== BALANCE: {seeds} seeds, {ai} AI partners, 3 years ===");
            var sw = Stopwatch.StartNew();
            var valuations = new List<double>();
            int bankrupt = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                Console.WriteLine($"-- seed {seed}");
                var state = GameFactory.Create(AllAi(ai, seed));
                var sim = new GameSimulation(state);
                RunGame(sim, PrintQuarter);
                PrintSummary(sim);
                valuations.Add(state.Summary?.FinalValuation ?? 0);
                if (state.Company.Bankrupt) bankrupt++;
            }
            Console.WriteLine($"  final valuations: {string.Join(", ", valuations.Select(Fmt.Money))}; bankrupt {bankrupt}/{seeds}; {sw.ElapsedMilliseconds} ms");
            return 0;
        }

        /// <summary>Print the company log for the first N days of one all-AI game.</summary>
        static int Trace(int seed, int ai, int days)
        {
            var state = GameFactory.Create(AllAi(ai, seed));
            var sim = new GameSimulation(state);
            LogEntry last = null;
            RunGame(sim, null, s =>
            {
                var log = s.State.Log;
                int from = last == null ? 0 : log.IndexOf(last) + 1;
                for (int i = from; i < log.Count; i++)
                    Console.WriteLine($"  d{log[i].Day,4} [{log[i].Kind}] {(log[i].ActorId >= 0 ? s.State.PlayerName(log[i].ActorId) + ": " : "")}{log[i].Text}");
                if (log.Count > 0) last = log[log.Count - 1];
                if (s.State.Day % 10 == 0)
                    Console.WriteLine($"  ---- day {s.State.Day}: cash {Fmt.Money(s.State.Company.Cash)}, stock {s.State.Company.Products.Sum(p => p.Stock)}, sales7 {s.State.Company.Products.Sum(p => p.Sales7):0}");
            }, days);
            return 0;
        }

        static int Passive(int seeds)
        {
            Console.WriteLine($"=== PASSIVE HUMAN + 2 AI: {seeds} seeds ===");
            for (int seed = 11; seed < 11 + seeds; seed++)
            {
                var st = GameFactory.SoloSettings("Idle Human", 2, seed);
                st.GameYears = 2;
                var state = GameFactory.Create(st);
                var sim = new GameSimulation(state);
                RunGame(sim, PrintQuarter);
                PrintSummary(sim);
            }
            return 0;
        }

        /// <summary>Random commands from every seat, including garbage input, while the game runs.</summary>
        static int Fuzz(int seeds)
        {
            Console.WriteLine($"=== FUZZ: {seeds} seeds ===");
            var types = (CommandType[])Enum.GetValues(typeof(CommandType));
            int failures = 0;
            for (int seed = 100; seed < 100 + seeds; seed++)
            {
                var st = GameFactory.SoloSettings("Fuzzer", 3, seed);
                st.Seats.Add(new SeatConfig { Name = "Fuzzer 2", IsAI = false });
                st.GameYears = 2;
                var state = GameFactory.Create(st);
                var sim = new GameSimulation(state);
                var rnd = new Random(seed);
                int commands = 0, ok = 0;
                RunGame(sim, null, s =>
                {
                    for (int k = 0; k < 6; k++)
                    {
                        var cmd = RandomCommand(s.State, rnd, types);
                        if (cmd.Type == CommandType.AdvanceDays) continue;
                        try
                        {
                            var r = s.Execute(cmd);
                            commands++;
                            if (r.Ok) ok++;
                            if (r.Message != null && r.Message.StartsWith("Something went wrong"))
                            {
                                failures++;
                                Console.WriteLine($"  EXCEPTION via {cmd.Type}: {r.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            failures++;
                            Console.WriteLine($"  THROW {cmd.Type}: {ex.Message}");
                        }
                    }
                    // Views must build for every seat without throwing.
                    foreach (var p in s.State.Players) ViewBuilder.Build(s.State, p.Id);
                    Invariants(s.State);
                });
                Console.WriteLine($"  seed {seed}: day {state.Day}, {commands} commands ({ok} ok), phase {state.Phase}, valuation {Fmt.Money(state.Company.Valuation)}");
            }
            Console.WriteLine(failures == 0 ? "  FUZZ OK" : $"  FUZZ FAILURES: {failures}");
            return failures == 0 ? 0 : 3;
        }

        static void Invariants(GameState s)
        {
            long shares = s.TotalPlayerShares + s.Company.InvestorShares;
            if (shares != s.Company.TotalShares) throw new Exception($"Share mismatch: {shares} vs {s.Company.TotalShares}");
            foreach (var p in s.Players) if (p.Shares < 0) throw new Exception("Negative shares for " + p.Name);
            foreach (var cp in s.Company.Products) if (cp.Stock < 0) throw new Exception("Negative stock " + cp.Name);
            if (double.IsNaN(s.Company.Cash) || double.IsInfinity(s.Company.Cash)) throw new Exception("Cash is NaN");
            if (double.IsNaN(s.Company.Valuation)) throw new Exception("Valuation is NaN");
            foreach (var pr in s.Private) if (double.IsNaN(pr.Cash)) throw new Exception("Personal cash NaN");
        }

        static string RandomId<T>(Random r, List<T> list, Func<T, string> id) => list.Count == 0 ? "nope" : id(list[r.Next(list.Count)]);

        static Command RandomCommand(GameState s, Random r, CommandType[] types)
        {
            var humans = s.Players.Where(p => !p.IsAI).ToList();
            var p0 = r.Next(10) == 0 ? s.Players[r.Next(s.Players.Count)] : humans[r.Next(humans.Count)];
            var c = new Command { Type = types[r.Next(types.Length)], PlayerId = p0.Id };
            var pr = s.GetPrivate(p0.Id);
            switch (c.Type)
            {
                case CommandType.StartResearch: c.A = r.Next(3) == 0 ? "Any" : Catalog.Categories[r.Next(Catalog.Categories.Length)]; c.I = r.Next(2); break;
                case CommandType.ShareDiscovery: c.A = RandomId(r, pr.Notebook, d => d.ProductId); break;
                case CommandType.LaunchProduct:
                    c.A = RandomId(r, pr.Notebook, d => d.ProductId);
                    c.B = RandomId(r, s.Suppliers, x => x.Id);
                    c.I = r.Next(0, 3000); c.X = r.Next(0, 200);
                    break;
                case CommandType.SetPrice: c.A = RandomId(r, s.Company.Products, x => x.Id); c.X = r.Next(1, 300); break;
                case CommandType.OrderInventory:
                    c.A = RandomId(r, s.Company.Products, x => x.Id);
                    c.B = RandomId(r, s.Suppliers, x => x.Id);
                    c.I = r.Next(0, 4000); c.Flag = r.Next(5) == 0;
                    break;
                case CommandType.Liquidate: if (r.Next(8) == 0) c.A = RandomId(r, s.Company.Products, x => x.Id); break;
                case CommandType.StartNegotiation: c.A = RandomId(r, s.Suppliers, x => x.Id); break;
                case CommandType.NegotiationOffer: c.A = RandomId(r, s.Negotiations, x => x.Id); c.X = r.NextDouble() * 0.3; break;
                case CommandType.NegotiationWalkAway: c.A = RandomId(r, s.Negotiations, x => x.Id); break;
                case CommandType.StartCampaign: c.A = RandomId(r, s.Company.Products, x => x.Id); c.I = r.Next(4); c.X = r.Next(0, 800); c.J = r.Next(0, 100); break;
                case CommandType.StopCampaign: c.A = RandomId(r, s.Company.Campaigns, x => x.Id); break;
                case CommandType.Recruit: c.A = ((EmployeeRole)r.Next(6)).ToString(); break;
                case CommandType.Hire: c.A = RandomId(r, s.Candidates, x => x.Id); break;
                case CommandType.Fire: if (r.Next(6) == 0) c.A = RandomId(r, s.Company.Employees, x => x.Id); break;
                case CommandType.BuyWarehouse: c.A = Catalog.Warehouses[r.Next(Catalog.Warehouses.Length)].Id; break;
                case CommandType.TakeLoan: c.A = Catalog.Loans[r.Next(Catalog.Loans.Length)].Id; break;
                case CommandType.RepayLoan: c.A = RandomId(r, s.Company.Loans, x => x.Id); break;
                case CommandType.CompanyInvest: c.A = Catalog.Upgrades[r.Next(Catalog.Upgrades.Length)].Id; break;
                case CommandType.SetPolicy: c.A = new[] { "returns", "audits", "openbooks", "ballot", "limit", "junk" }[r.Next(6)]; c.I = r.Next(0, 60000); break;
                case CommandType.ClaimEvent: c.A = RandomId(r, s.Events, x => x.Id); break;
                case CommandType.ResolveEvent: c.A = RandomId(r, s.Events, x => x.Id); c.I = r.Next(-1, 5); break;
                case CommandType.RespondOffer: c.A = RandomId(r, s.Offers, x => x.Id); c.I = r.Next(3); break;
                case CommandType.ExposeEvidence:
                case CommandType.BlackmailEvidence:
                case CommandType.IgnoreEvidence: c.A = RandomId(r, s.Evidence, x => x.Id); c.X = r.Next(0, 50000); break;
                case CommandType.CreateProposal:
                    c.I = r.Next(0, 17); c.J = r.Next(-1, s.Players.Count + 1); c.X = r.Next(-1000, 60000);
                    c.A = new[] { "credit", "growth", "small", "regional", "log1", "brd2", "audits", "limit", "premium", "value", "trend", RandomId(r, s.Rivals, x => x.Id), null }[r.Next(13)];
                    break;
                case CommandType.BuyUpgrade: c.A = Catalog.Upgrades[r.Next(Catalog.Upgrades.Length)].Id; break;
                case CommandType.StartPromotion: c.A = RandomId(r, s.Company.Products, x => x.Id); c.I = r.Next(0, 3); break;
                case CommandType.UpgradeProduct: c.A = RandomId(r, s.Company.Products, x => x.Id); break;
                case CommandType.SetAutoRestock: c.A = RandomId(r, s.Company.Products, x => x.Id); c.Flag = r.Next(2) == 0; c.I = r.Next(-5, 200); break;
                case CommandType.ClaimProduct: c.A = RandomId(r, s.Company.Products, x => x.Id); break;
                case CommandType.SetRoles: c.I = r.Next(-1, s.Players.Count + 1); c.Text = string.Join(",", Roles.All.Where(_ => r.Next(2) == 0)) + (r.Next(4) == 0 ? ",junk" : ""); break;
                case CommandType.ChooseAmbition: c.A = pr.AmbitionChoices.Count > 0 && r.Next(3) > 0 ? pr.AmbitionChoices[r.Next(pr.AmbitionChoices.Count)] : "junk"; break;
                case CommandType.CastVote: c.A = RandomId(r, s.Board.Agenda, x => x.Id); c.I = r.Next(0, 5); break;
                case CommandType.WithdrawProposal: c.A = RandomId(r, s.Board.Agenda, x => x.Id); break;
                case CommandType.VoteApproval: c.A = RandomId(r, s.Approvals, x => x.Id); c.I = r.Next(1, 4); break;
                case CommandType.CreateShareOffer: c.I = r.Next(-1, s.Players.Count + 1); c.X = r.NextDouble() * 0.2; c.Y = r.Next(0, 300000); c.Flag = r.Next(2) == 0; break;
                case CommandType.RespondShareOffer: c.A = RandomId(r, s.ShareOffers, x => x.Id); c.Flag = r.Next(2) == 0; break;
                case CommandType.CancelShareOffer: c.A = RandomId(r, s.ShareOffers, x => x.Id); break;
                case CommandType.UnlockSkill: c.A = Skills.All[r.Next(Skills.All.Count)].Id; break;
                case CommandType.PersonalInvest: c.A = Catalog.PersonalAssets[r.Next(Catalog.PersonalAssets.Length)].Id; c.X = r.Next(0, 20000); break;
                case CommandType.PersonalSell: c.A = Catalog.PersonalAssets[r.Next(Catalog.PersonalAssets.Length)].Id; c.X = r.Next(0, 20000); break;
                case CommandType.Chat: c.Text = "hello"; c.I = r.Next(-1, s.Players.Count); break;
                case CommandType.SetSpeed: c.I = r.Next(0, 10); break;
                case CommandType.SetReady: c.Flag = true; break;
                case CommandType.TogglePause: c.Type = CommandType.SetReady; c.Flag = false; break;
            }
            return c;
        }

        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        /// <summary>A save/load round trip must continue the exact same game.</summary>
        static int SerializeTest()
        {
            Console.WriteLine("=== SERIALIZE ===");
            var st = GameFactory.SoloSettings("Saver", 3, 7);
            var a = GameFactory.Create(st);
            var simA = new GameSimulation(a);
            for (int i = 0; i < 120; i++)
            {
                if (a.Phase == GamePhase.BoardMeeting)
                {
                    foreach (var p in a.Players) if (!p.IsAI) simA.Execute(new Command { Type = CommandType.SetReady, PlayerId = p.Id, Flag = true });
                    simA.Advance(1);
                    continue;
                }
                simA.StepDay();
            }
            string json = JsonConvert.SerializeObject(a, Json);
            var b = JsonConvert.DeserializeObject<GameState>(json, Json);
            string json2 = JsonConvert.SerializeObject(b, Json);
            Console.WriteLine($"  state json {json.Length / 1024} KB, re-serialized equal: {json == json2}");
            var simB = new GameSimulation(b);
            // Note: AI/meeting timers keep per-simulation scratch state; step plain days only.
            for (int i = 0; i < 60; i++)
            {
                if (a.Phase != GamePhase.Running || b.Phase != GamePhase.Running) break;
                simA.StepDay();
                simB.StepDay();
            }
            string ja = JsonConvert.SerializeObject(a, Json), jb = JsonConvert.SerializeObject(b, Json);
            bool same = ja == jb;
            Console.WriteLine($"  continued 60 days deterministically: {same} (day {a.Day} vs {b.Day}, cash {Fmt.Money(a.Company.Cash)} vs {Fmt.Money(b.Company.Cash)})");
            foreach (var p in a.Players)
            {
                var view = ViewBuilder.Build(a, p.Id);
                string vj = JsonConvert.SerializeObject(view, Json);
                var back = JsonConvert.DeserializeObject<GameState>(vj, Json);
                bool leak = back.Private.Any(x => x.PlayerId != p.Id) || back.Offers.Any(o => o.TargetId != p.Id && o.FromId != p.Id) || back.RngState != 0;
                Console.WriteLine($"  view for {p.Name}: {vj.Length / 1024} KB, leak: {leak}");
                if (leak) return 4;
            }
            return same && json == json2 ? 0 : 5;
        }
    }
}
